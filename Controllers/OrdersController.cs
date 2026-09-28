using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pos_backend.Data;
using pos_backend.DTOs;
using pos_backend.Models;

namespace pos_backend.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly PosDbContext _context;

    public OrdersController(PosDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderResponseDto>>> GetOrders(
        [FromQuery] DateTime? date,
        [FromQuery] int? shiftId,
        [FromQuery] OrderStatus? status,
        [FromQuery] int limit = 50)
    {
        var query = _context.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .AsQueryable();

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

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
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

            // 2. Build Order entity
            var order = new Order
            {
                OrderNumber = orderNumber,
                TableNumber = request.TableNumber,
                Type = request.Type,
                Status = OrderStatus.Completed,
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
                CompletedAt = DateTime.UtcNow
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
            if (request.ShiftId.HasValue)
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

            // 5. Append to SyncOutbox (Resilient Outbox Pattern)
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

            return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, responseDto);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Error processing order", error = ex.Message });
        }
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] OrderStatus newStatus)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null)
        {
            return NotFound(new { message = $"Order with ID {id} not found." });
        }

        order.Status = newStatus;
        if (newStatus == OrderStatus.Completed && order.CompletedAt == null)
        {
            order.CompletedAt = DateTime.UtcNow;
        }

        var outboxEntry = new SyncOutbox
        {
            EventType = "Order.StatusUpdated",
            AggregateType = "Order",
            AggregateId = order.OrderNumber,
            PayloadJson = JsonSerializer.Serialize(new { order.Id, order.OrderNumber, Status = newStatus }),
            CreatedAt = DateTime.UtcNow,
            IsSynced = false
        };
        _context.SyncOutbox.Add(outboxEntry);

        await _context.SaveChangesAsync();

        return NoContent();
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
            order.TableNumber,
            order.Type,
            order.Status,
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

