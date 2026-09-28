namespace pos_backend.Models;

public enum PaymentMethod
{
    Cash = 1,
    Card = 2,
    EWallet = 3,
    Split = 4,
    Other = 5
}

public enum PaymentStatus
{
    Pending = 1,
    Completed = 2,
    Refunded = 3,
    Failed = 4
}

public class Payment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    public decimal AmountTendered { get; set; }
    public decimal ChangeGiven { get; set; } = 0;
    public decimal TotalPaid { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Completed;
    public string? TransactionReference { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

