namespace pos_backend.Models;

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public int? ProductId { get; set; }
    public Product? Product { get; set; }

    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal DiscountAmount { get; set; } = 0;
    public decimal TotalPrice { get; set; }
    public string? SelectedModifiersJson { get; set; } // E.g., selected modifiers/addons
    public string? Notes { get; set; }
}

