namespace pos_backend.Models;

public enum OrderStatus
{
    Pending = 1,
    Preparing = 2,
    Ready = 3,
    Completed = 4,
    Cancelled = 5,
    Parked = 6
}

public enum OrderType
{
    DineIn = 1,
    Takeaway = 2,
    Delivery = 3
}

public class Order
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty; // e.g., "ORD-20260928-0001"
    public string? TableNumber { get; set; }
    public int? TableId { get; set; }
    public RestaurantTable? Table { get; set; }
    
    public OrderType Type { get; set; } = OrderType.DineIn;
    public OrderStatus Status { get; set; } = OrderStatus.Completed;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Completed;

    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; } = 0;
    public decimal TaxTotal { get; set; } = 0;
    public decimal GrandTotal { get; set; }

    public string? CustomerName { get; set; }
    public string? Notes { get; set; }
    public string CashierId { get; set; } = "cashier-1";
    public string CashierName { get; set; } = "Cashier";
    public int? ShiftId { get; set; }
    public Shift? Shift { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentToKitchenAt { get; set; }
    public DateTime? ReadyAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

