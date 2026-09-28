using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pos_backend.Data;
using pos_backend.Models;

namespace pos_backend.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class SyncController : ControllerBase
{
    private readonly PosDbContext _context;

    public SyncController(PosDbContext context)
    {
        _context = context;
    }

    [HttpGet("pending")]
    public async Task<ActionResult<IEnumerable<SyncOutbox>>> GetPendingSyncRecords([FromQuery] int limit = 100)
    {
        var records = await _context.SyncOutbox
            .Where(s => !s.IsSynced)
            .OrderBy(s => s.CreatedAt)
            .Take(limit)
            .ToListAsync();

        return Ok(records);
    }

    [HttpPost("ack")]
    public async Task<IActionResult> AcknowledgeSync([FromBody] List<long> outboxIds)
    {
        if (outboxIds == null || !outboxIds.Any())
        {
            return BadRequest(new { message = "No IDs provided." });
        }

        var records = await _context.SyncOutbox
            .Where(s => outboxIds.Contains(s.Id))
            .ToListAsync();

        foreach (var rec in records)
        {
            rec.IsSynced = true;
            rec.SyncedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return Ok(new { message = $"Successfully marked {records.Count} records as synced." });
    }
}

