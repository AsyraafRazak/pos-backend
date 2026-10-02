namespace pos_backend.Models;

public enum TableStatus
{
    Available = 1,
    Occupied = 2,
    Reserved = 3,
    BillRequested = 4
}

public class RestaurantTable
{
    public int Id { get; set; }
    public string TableNumber { get; set; } = string.Empty; // e.g., "T-01"
    public string Zone { get; set; } = "Main Hall";         // e.g., "Indoor", "Patio", "VIP"
    public int Capacity { get; set; } = 4;
    public TableStatus Status { get; set; } = TableStatus.Available;
    
    public int? CurrentOrderId { get; set; }
    public Order? CurrentOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
