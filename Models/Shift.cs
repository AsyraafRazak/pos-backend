namespace pos_backend.Models;

public enum ShiftStatus
{
    Open = 1,
    Closed = 2
}

public class Shift
{
    public int Id { get; set; }
    public string CashierId { get; set; } = string.Empty;
    public string CashierName { get; set; } = string.Empty;
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
    public decimal StartingFloat { get; set; }
    public decimal CashSales { get; set; } = 0;
    public decimal NonCashSales { get; set; } = 0;
    public decimal ExpectedCash { get; set; } = 0;
    public decimal? ActualCash { get; set; }
    public decimal? Discrepancy { get; set; }
    public string? ClosingNotes { get; set; }
    public ShiftStatus Status { get; set; } = ShiftStatus.Open;

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}

