using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using pos_backend.Data;
using pos_backend.DTOs;
using pos_backend.Hubs;
using pos_backend.Models;

namespace pos_backend.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly PosDbContext _context;
    private readonly IHubContext<PosHub, IPosClient> _hubContext;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        PosDbContext context,
        IHubContext<PosHub, IPosClient> hubContext,
        ILogger<OrdersController> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderResponseDto>>> GetOrders(
        [FromQuery] DateTime? date,
        [FromQuery] int? shiftId,
        [FromQuery] OrderStatus? status,
        [FromQuery] PaymentStatus? paymentStatus,
        [FromQuery] bool? activeKds,
        [FromQuery] int limit = 50)
    {
        var query = _context.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .AsNoTracking()
            .AsQueryable();

        if (activeKds == true)
        {
            query = query.Where(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.Preparing || o.Status == OrderStatus.Ready);
        }
        else if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        if (paymentStatus.HasValue)
        {
            query = query.Where(o => o.PaymentStatus == paymentStatus.Value);
        }

        if (date.HasValue)
        {
            var startOfDay = date.Value.Date;
            var endOfDay = startOfDay.AddDays(1);
            query = query.Where(o => o.CreatedAt >= startOfDay && o.CreatedAt < endOfDay);
        }

        if (shiftId.HasValue)
        {
            query = query.Where(o => o.ShiftId == shiftId.Value);
        }

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Take(limit)
            .Select(o => MapToResponseDto(o))
            .ToListAsync();

        return Ok(orders);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderResponseDto>> GetOrder(int id)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return NotFound(new { message = $"Order with ID {id} not found." });
        }

        return Ok(MapToResponseDto(order));
    }

    [HttpGet("by-number/{orderNumber}")]
    public async Task<ActionResult<OrderResponseDto>> GetOrderByNumber(string orderNumber)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);

        if (order == null)
        {
            return NotFound(new { message = $"Order '{orderNumber}' not found." });
        }

        return Ok(MapToResponseDto(order));
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponseDto>> CreateOrder([FromBody] CreateOrderRequest request)
    {
        if (request.Items == null || !request.Items.Any())
        {
            return BadRequest(new { message = "Order must contain at least one item." });
        }

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // 1. Generate Order Number if not provided
            var orderNumber = string.IsNullOrWhiteSpace(request.OrderNumber)
                ? await GenerateOrderNumberAsync()
                : request.OrderNumber;

            // Determine initial payment status and order status
            var hasPayments = request.Payments != null && request.Payments.Any();
            var paymentStatus = request.PaymentStatus ?? (hasPayments ? PaymentStatus.Completed : PaymentStatus.Pending);
            
            var orderStatus = request.Status ?? (paymentStatus == PaymentStatus.Completed ? OrderStatus.Preparing : OrderStatus.Preparing);

            // 2. Build Order entity
            var order = new Order
            {
                OrderNumber = orderNumber,
                TableId = request.TableId,
                TableNumber = request.TableNumber,
                Type = request.Type,
                Status = orderStatus,
                PaymentStatus = paymentStatus,
                Subtotal = request.Subtotal,
                DiscountTotal = request.DiscountTotal,
                TaxTotal = request.TaxTotal,
                GrandTotal = request.GrandTotal,
                CustomerName = request.CustomerName,
                Notes = request.Notes,
                CashierId = request.CashierId,
                CashierName = request.CashierName,
                ShiftId = request.ShiftId,
                CreatedAt = DateTime.UtcNow,
                SentToKitchenAt = DateTime.UtcNow,
                CompletedAt = (orderStatus == OrderStatus.Completed && paymentStatus == PaymentStatus.Completed) ? DateTime.UtcNow : null
            };

            // 3. Add Order Items & Deduct Stock
            foreach (var itemReq in request.Items)
            {
                var orderItem = new OrderItem
                {
                    ProductId = itemReq.ProductId,
                    ProductName = itemReq.ProductName,
                    UnitPrice = itemReq.UnitPrice,
                    Quantity = itemReq.Quantity,
                    DiscountAmount = itemReq.DiscountAmount,
                    TotalPrice = itemReq.TotalPrice,
                    SelectedModifiersJson = itemReq.SelectedModifiersJson,
                    Notes = itemReq.Notes
                };

                order.Items.Add(orderItem);

                // Deduct inventory if product exists & tracks stock
                if (itemReq.ProductId.HasValue)
                {
                    var product = await _context.Products.FindAsync(itemReq.ProductId.Value);
                    if (product != null && product.TrackStock)
                    {
                        product.StockQuantity = Math.Max(0, product.StockQuantity - itemReq.Quantity);
                        product.UpdatedAt = DateTime.UtcNow;
                    }
                }
            }

            // 4. Add Payments & Update Shift totals if applicable
            decimal cashPaid = 0;
            decimal nonCashPaid = 0;

            if (request.Payments != null)
            {
                foreach (var payReq in request.Payments)
                {
                    var payment = new Payment
                    {
                        Method = payReq.Method,
                        AmountTendered = payReq.AmountTendered,
                        ChangeGiven = payReq.ChangeGiven,
                        TotalPaid = payReq.TotalPaid,
                        Status = PaymentStatus.Completed,
                        TransactionReference = payReq.TransactionReference,
                        CreatedAt = DateTime.UtcNow
                    };

                    order.Payments.Add(payment);

                    if (payReq.Method == PaymentMethod.Cash)
                    {
                        cashPaid += (payReq.TotalPaid - payReq.ChangeGiven);
                    }
                    else
                    {
                        nonCashPaid += payReq.TotalPaid;
                    }
                }
            }

            // Update shift summary if open shift exists
            if (request.ShiftId.HasValue && hasPayments)
            {
                var shift = await _context.Shifts.FindAsync(request.ShiftId.Value);
                if (shift != null && shift.Status == ShiftStatus.Open)
                {
                    shift.CashSales += cashPaid;
                    shift.NonCashSales += nonCashPaid;
                    shift.ExpectedCash = shift.StartingFloat + shift.CashSales;
                }
            }

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // 5. Update Table Status if associated with Table
            RestaurantTable? table = null;
            if (request.TableId.HasValue)
            {
                table = await _context.Tables.FindAsync(request.TableId.Value);
                if (table != null)
                {
                    order.TableNumber = table.TableNumber;
                    table.CurrentOrderId = order.Id;
                    table.Status = TableStatus.Occupied;
                    table.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }

            // 6. Append to SyncOutbox (Resilient Outbox Pattern)
            var responseDto = MapToResponseDto(order);
            var outboxEntry = new SyncOutbox
            {
                EventType = "Order.Created",
                AggregateType = "Order",
                AggregateId = order.OrderNumber,
                PayloadJson = JsonSerializer.Serialize(responseDto),
                CreatedAt = DateTime.UtcNow,
                IsSynced = false
            };
            _context.SyncOutbox.Add(outboxEntry);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            // 7. Broadcast realtime SignalR events to POS and KDS
            await _hubContext.Clients.All.OrderCreated(responseDto);
            await _hubContext.Clients.All.OrderFiredToKitchen(responseDto);

            if (table != null)
            {
                var tableDto = new TableDto(
                    table.Id,
                    table.TableNumber,
                    table.Zone,
                    table.Capacity,
                    table.Status,
                    table.CurrentOrderId,
                    order.OrderNumber,
                    order.GrandTotal,
                    order.CreatedAt,
                    order.Items.Count
                );
                await _hubContext.Clients.All.TableStatusChanged(tableDto);
            }

            return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, responseDto);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error processing order creation");
            return StatusCode(500, new { message = "Error processing order", error = ex.Message });
        }
    }

    [HttpPost("{id:int}/pay")]
    public async Task<ActionResult<OrderResponseDto>> PayOrder(int id, [FromBody] PayOrderRequest request)
    {
        if (request.Payments == null || !request.Payments.Any())
        {
            return BadRequest(new { message = "Payment list cannot be empty." });
        }

        var order = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return NotFound(new { message = $"Order with ID {id} not found." });
        }

        if (order.PaymentStatus == PaymentStatus.Completed)
        {
            return BadRequest(new { message = "This order is already fully paid." });
        }

        decimal cashPaid = 0;
        decimal nonCashPaid = 0;

        foreach (var payReq in request.Payments)
        {
            var payment = new Payment
            {
                OrderId = order.Id,
                Method = payReq.Method,
                AmountTendered = payReq.AmountTendered,
                ChangeGiven = payReq.ChangeGiven,
                TotalPaid = payReq.TotalPaid,
                Status = PaymentStatus.Completed,
                TransactionReference = payReq.TransactionReference,
                CreatedAt = DateTime.UtcNow
            };

            order.Payments.Add(payment);

            if (payReq.Method == PaymentMethod.Cash)
            {
                cashPaid += (payReq.TotalPaid - payReq.ChangeGiven);
            }
            else
            {
                nonCashPaid += payReq.TotalPaid;
            }
        }

        order.PaymentStatus = PaymentStatus.Completed;
        if (order.Status == OrderStatus.Ready || order.Status == OrderStatus.Completed)
        {
            order.Status = OrderStatus.Completed;
            order.CompletedAt = DateTime.UtcNow;
        }

        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            order.Notes = string.IsNullOrWhiteSpace(order.Notes) ? request.Notes : $"{order.Notes} | {request.Notes}";
        }

        // Update shift
        if (order.ShiftId.HasValue)
        {
            var shift = await _context.Shifts.FindAsync(order.ShiftId.Value);
            if (shift != null && shift.Status == ShiftStatus.Open)
            {
                shift.CashSales += cashPaid;
                shift.NonCashSales += nonCashPaid;
                shift.ExpectedCash = shift.StartingFloat + shift.CashSales;
            }
        }

        // Release Table if assigned
        RestaurantTable? table = null;
        if (order.TableId.HasValue)
        {
            table = await _context.Tables.FindAsync(order.TableId.Value);
            if (table != null && table.CurrentOrderId == order.Id)
            {
                table.CurrentOrderId = null;
                table.Status = TableStatus.Available;
                table.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();

        var responseDto = MapToResponseDto(order);

        // Realtime SignalR broadcasts
        await _hubContext.Clients.All.OrderPaid(responseDto);
        await _hubContext.Clients.All.OrderStatusChanged(responseDto);

        if (table != null)
        {
            var tableDto = new TableDto(table.Id, table.TableNumber, table.Zone, table.Capacity, table.Status, null, null, null, null, null);
            await _hubContext.Clients.All.TableStatusChanged(tableDto);
        }

        return Ok(responseDto);
    }

    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<OrderResponseDto>> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusRequest request)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return NotFound(new { message = $"Order with ID {id} not found." });
        }

        order.Status = request.Status;
        if (request.Status == OrderStatus.Ready && order.ReadyAt == null)
        {
            order.ReadyAt = DateTime.UtcNow;
        }
        else if (request.Status == OrderStatus.Completed && order.CompletedAt == null)
        {
            order.CompletedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        var responseDto = MapToResponseDto(order);
        await _hubContext.Clients.All.OrderStatusChanged(responseDto);

        return Ok(responseDto);
    }

    private async Task<string> GenerateOrderNumberAsync()
    {
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var todayCount = await _context.Orders
            .CountAsync(o => o.OrderNumber.StartsWith($"ORD-{today}"));

        return $"ORD-{today}-{(todayCount + 1):D4}";
    }

    private static OrderResponseDto MapToResponseDto(Order order)
    {
        return new OrderResponseDto(
            order.Id,
            order.OrderNumber,
            order.TableId,
            order.TableNumber,
            order.Type,
            order.Status,
            order.PaymentStatus,
            order.Subtotal,
            order.DiscountTotal,
            order.TaxTotal,
            order.GrandTotal,
            order.CustomerName,
            order.Notes,
            order.CashierId,
            order.CashierName,
            order.ShiftId,
            order.CreatedAt,
            order.SentToKitchenAt,
            order.ReadyAt,
            order.CompletedAt,
            order.Items.Select(i => new OrderItemDto(
                i.Id,
                i.ProductId,
                i.ProductName,
                i.UnitPrice,
                i.Quantity,
                i.DiscountAmount,
                i.TotalPrice,
                i.SelectedModifiersJson,
                i.Notes
            )).ToList(),
            order.Payments.Select(p => new PaymentDto(
                p.Id,
                p.Method,
                p.AmountTendered,
                p.ChangeGiven,
                p.TotalPaid,
                p.Status,
                p.TransactionReference,
                p.CreatedAt
            )).ToList()
        );
    }
}
