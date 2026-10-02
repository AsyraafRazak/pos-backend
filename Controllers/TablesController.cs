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
public class TablesController : ControllerBase
{
    private readonly PosDbContext _context;
    private readonly IHubContext<PosHub, IPosClient> _hubContext;
    private readonly ILogger<TablesController> _logger;

    public TablesController(
        PosDbContext context,
        IHubContext<PosHub, IPosClient> hubContext,
        ILogger<TablesController> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _logger = logger;
    }

    // GET: api/v1/tables
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TableDto>>> GetTables([FromQuery] string? zone)
    {
        var query = _context.Tables
            .Include(t => t.CurrentOrder)
                .ThenInclude(o => o!.Items)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(zone))
        {
            query = query.Where(t => t.Zone.ToLower() == zone.ToLower());
        }

        var tables = await query
            .OrderBy(t => t.Zone)
            .ThenBy(t => t.TableNumber)
            .Select(t => new TableDto(
                t.Id,
                t.TableNumber,
                t.Zone,
                t.Capacity,
                t.Status,
                t.CurrentOrderId,
                t.CurrentOrder != null ? t.CurrentOrder.OrderNumber : null,
                t.CurrentOrder != null ? t.CurrentOrder.GrandTotal : null,
                t.CurrentOrder != null ? t.CurrentOrder.CreatedAt : null,
                t.CurrentOrder != null ? t.CurrentOrder.Items.Count : null
            ))
            .ToListAsync();

        return Ok(tables);
    }

    // GET: api/v1/tables/5
    [HttpGet("{id}")]
    public async Task<ActionResult<TableDto>> GetTable(int id)
    {
        var table = await _context.Tables
            .Include(t => t.CurrentOrder)
                .ThenInclude(o => o!.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);

        if (table == null)
        {
            return NotFound(new { message = $"Table with ID {id} not found." });
        }

        var dto = new TableDto(
            table.Id,
            table.TableNumber,
            table.Zone,
            table.Capacity,
            table.Status,
            table.CurrentOrderId,
            table.CurrentOrder?.OrderNumber,
            table.CurrentOrder?.GrandTotal,
            table.CurrentOrder?.CreatedAt,
            table.CurrentOrder?.Items.Count
        );

        return Ok(dto);
    }

    // POST: api/v1/tables
    [HttpPost]
    public async Task<ActionResult<TableDto>> CreateTable([FromBody] CreateTableRequest request)
    {
        if (await _context.Tables.AnyAsync(t => t.TableNumber.ToLower() == request.TableNumber.ToLower()))
        {
            return Conflict(new { message = $"Table number '{request.TableNumber}' already exists." });
        }

        var table = new RestaurantTable
        {
            TableNumber = request.TableNumber.Trim(),
            Zone = string.IsNullOrWhiteSpace(request.Zone) ? "Main Hall" : request.Zone.Trim(),
            Capacity = request.Capacity <= 0 ? 4 : request.Capacity,
            Status = TableStatus.Available,
            CreatedAt = DateTime.UtcNow
        };

        _context.Tables.Add(table);
        await _context.SaveChangesAsync();

        var dto = new TableDto(
            table.Id,
            table.TableNumber,
            table.Zone,
            table.Capacity,
            table.Status,
            null, null, null, null, null
        );

        await _hubContext.Clients.All.TableStatusChanged(dto);

        return CreatedAtAction(nameof(GetTable), new { id = table.Id }, dto);
    }

    // PUT: api/v1/tables/5
    [HttpPut("{id}")]
    public async Task<ActionResult<TableDto>> UpdateTable(int id, [FromBody] UpdateTableRequest request)
    {
        var table = await _context.Tables.FindAsync(id);
        if (table == null)
        {
            return NotFound(new { message = $"Table with ID {id} not found." });
        }

        if (await _context.Tables.AnyAsync(t => t.Id != id && t.TableNumber.ToLower() == request.TableNumber.ToLower()))
        {
            return Conflict(new { message = $"Table number '{request.TableNumber}' is already in use." });
        }

        table.TableNumber = request.TableNumber.Trim();
        table.Zone = request.Zone.Trim();
        table.Capacity = request.Capacity;
        table.Status = request.Status;
        table.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var dto = new TableDto(
            table.Id,
            table.TableNumber,
            table.Zone,
            table.Capacity,
            table.Status,
            table.CurrentOrderId,
            null, null, null, null
        );

        await _hubContext.Clients.All.TableStatusChanged(dto);

        return Ok(dto);
    }

    // PATCH: api/v1/tables/5/status
    [HttpPatch("{id}/status")]
    public async Task<ActionResult<TableDto>> UpdateTableStatus(int id, [FromBody] UpdateTableStatusRequest request)
    {
        var table = await _context.Tables.FindAsync(id);
        if (table == null)
        {
            return NotFound(new { message = $"Table with ID {id} not found." });
        }

        table.Status = request.Status;
        if (request.Status == TableStatus.Available)
        {
            table.CurrentOrderId = null;
        }
        table.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var dto = new TableDto(
            table.Id,
            table.TableNumber,
            table.Zone,
            table.Capacity,
            table.Status,
            table.CurrentOrderId,
            null, null, null, null
        );

        await _hubContext.Clients.All.TableStatusChanged(dto);

        return Ok(dto);
    }

    // POST: api/v1/tables/5/transfer
    [HttpPost("{id}/transfer")]
    public async Task<IActionResult> TransferTable(int id, [FromBody] TransferTableRequest request)
    {
        var sourceTable = await _context.Tables.FindAsync(id);
        if (sourceTable == null)
        {
            return NotFound(new { message = $"Source table ID {id} not found." });
        }

        if (!sourceTable.CurrentOrderId.HasValue)
        {
            return BadRequest(new { message = "Source table does not have an active order." });
        }

        var targetTable = await _context.Tables.FindAsync(request.TargetTableId);
        if (targetTable == null)
        {
            return NotFound(new { message = $"Target table ID {request.TargetTableId} not found." });
        }

        if (targetTable.Status == TableStatus.Occupied && targetTable.CurrentOrderId.HasValue && targetTable.Id != sourceTable.Id)
        {
            return BadRequest(new { message = $"Target table {targetTable.TableNumber} is already occupied." });
        }

        var order = await _context.Orders.FindAsync(sourceTable.CurrentOrderId.Value);
        if (order != null)
        {
            order.TableId = targetTable.Id;
            order.TableNumber = targetTable.TableNumber;
        }

        var activeOrderId = sourceTable.CurrentOrderId;
        sourceTable.CurrentOrderId = null;
        sourceTable.Status = TableStatus.Available;
        sourceTable.UpdatedAt = DateTime.UtcNow;

        targetTable.CurrentOrderId = activeOrderId;
        targetTable.Status = TableStatus.Occupied;
        targetTable.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var sourceDto = new TableDto(sourceTable.Id, sourceTable.TableNumber, sourceTable.Zone, sourceTable.Capacity, sourceTable.Status, null, null, null, null, null);
        var targetDto = new TableDto(targetTable.Id, targetTable.TableNumber, targetTable.Zone, targetTable.Capacity, targetTable.Status, targetTable.CurrentOrderId, order?.OrderNumber, order?.GrandTotal, order?.CreatedAt, null);

        await _hubContext.Clients.All.TableStatusChanged(sourceDto);
        await _hubContext.Clients.All.TableStatusChanged(targetDto);

        return Ok(new { message = $"Transferred order from {sourceTable.TableNumber} to {targetTable.TableNumber}." });
    }

    // DELETE: api/v1/tables/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTable(int id)
    {
        var table = await _context.Tables.FindAsync(id);
        if (table == null)
        {
            return NotFound(new { message = $"Table with ID {id} not found." });
        }

        if (table.Status == TableStatus.Occupied)
        {
            return BadRequest(new { message = "Cannot delete an occupied table." });
        }

        _context.Tables.Remove(table);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
