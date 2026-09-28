using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pos_backend.Data;
using pos_backend.DTOs;
using pos_backend.Models;

namespace pos_backend.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ShiftsController : ControllerBase
{
    private readonly PosDbContext _context;

    public ShiftsController(PosDbContext context)
    {
        _context = context;
    }

    [HttpGet("current")]
    public async Task<ActionResult<ShiftResponseDto>> GetCurrentShift([FromQuery] string? cashierId)
    {
        var query = _context.Shifts
            .Include(s => s.Orders)
            .Where(s => s.Status == ShiftStatus.Open);

        if (!string.IsNullOrWhiteSpace(cashierId))
        {
            query = query.Where(s => s.CashierId == cashierId);
        }

        var currentShift = await query
            .OrderByDescending(s => s.OpenedAt)
            .FirstOrDefaultAsync();

        if (currentShift == null)
        {
            return NotFound(new { message = "No active open shift found." });
        }

        return Ok(MapToResponseDto(currentShift));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ShiftResponseDto>> GetShift(int id)
    {
        var shift = await _context.Shifts
            .Include(s => s.Orders)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (shift == null)
        {
            return NotFound(new { message = $"Shift with ID {id} not found." });
        }

        return Ok(MapToResponseDto(shift));
    }

    [HttpPost("open")]
    public async Task<ActionResult<ShiftResponseDto>> OpenShift([FromBody] OpenShiftRequest request)
    {
        var existingOpenShift = await _context.Shifts
            .FirstOrDefaultAsync(s => s.CashierId == request.CashierId && s.Status == ShiftStatus.Open);

        if (existingOpenShift != null)
        {
            return BadRequest(new { message = $"Cashier {request.CashierId} already has an active open shift (ID: {existingOpenShift.Id}). Close it before opening a new one." });
        }

        var shift = new Shift
        {
            CashierId = request.CashierId,
            CashierName = request.CashierName,
            StartingFloat = request.StartingFloat,
            CashSales = 0,
            NonCashSales = 0,
            ExpectedCash = request.StartingFloat,
            Status = ShiftStatus.Open,
            OpenedAt = DateTime.UtcNow
        };

        _context.Shifts.Add(shift);
        await _context.SaveChangesAsync();

        var outboxEntry = new SyncOutbox
        {
            EventType = "Shift.Opened",
            AggregateType = "Shift",
            AggregateId = shift.Id.ToString(),
            PayloadJson = JsonSerializer.Serialize(MapToResponseDto(shift)),
            CreatedAt = DateTime.UtcNow,
            IsSynced = false
        };
        _context.SyncOutbox.Add(outboxEntry);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetShift), new { id = shift.Id }, MapToResponseDto(shift));
    }

    [HttpPost("{id:int}/close")]
    public async Task<ActionResult<ShiftResponseDto>> CloseShift(int id, [FromBody] CloseShiftRequest request)
    {
        var shift = await _context.Shifts
            .Include(s => s.Orders)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (shift == null)
        {
            return NotFound(new { message = $"Shift with ID {id} not found." });
        }

        if (shift.Status == ShiftStatus.Closed)
        {
            return BadRequest(new { message = "Shift is already closed." });
        }

        shift.ActualCash = request.ActualCash;
        shift.Discrepancy = request.ActualCash - shift.ExpectedCash;
        shift.ClosingNotes = request.ClosingNotes;
        shift.Status = ShiftStatus.Closed;
        shift.ClosedAt = DateTime.UtcNow;

        var outboxEntry = new SyncOutbox
        {
            EventType = "Shift.Closed",
            AggregateType = "Shift",
            AggregateId = shift.Id.ToString(),
            PayloadJson = JsonSerializer.Serialize(MapToResponseDto(shift)),
            CreatedAt = DateTime.UtcNow,
            IsSynced = false
        };
        _context.SyncOutbox.Add(outboxEntry);

        await _context.SaveChangesAsync();

        return Ok(MapToResponseDto(shift));
    }

    private static ShiftResponseDto MapToResponseDto(Shift s)
    {
        return new ShiftResponseDto(
            s.Id,
            s.CashierId,
            s.CashierName,
            s.OpenedAt,
            s.ClosedAt,
            s.StartingFloat,
            s.CashSales,
            s.NonCashSales,
            s.ExpectedCash,
            s.ActualCash,
            s.Discrepancy,
            s.ClosingNotes,
            s.Status,
            s.Orders.Count
        );
    }
}

