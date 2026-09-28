namespace pos_backend.Models;

public class SyncOutbox
{
    public long Id { get; set; }
    public Guid EventId { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = string.Empty; // e.g. "Order.Created", "Shift.Closed"
    public string AggregateType { get; set; } = string.Empty; // e.g. "Order", "Shift"
    public string AggregateId { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsSynced { get; set; } = false;
    public DateTime? SyncedAt { get; set; }
    public int RetryCount { get; set; } = 0;
    public string? LastError { get; set; }
}

