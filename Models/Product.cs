namespace pos_backend.Models;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Sku { get; set; }
    public string? Barcode { get; set; }
    public decimal Price { get; set; }
    public decimal CostPrice { get; set; }
    public int StockQuantity { get; set; } = 0;
    public bool TrackStock { get; set; } = true;
    public string? ImageUrl { get; set; }
    public string? ModifiersJson { get; set; } // JSON list of modifier groups e.g. [{ name: "Size", options: [...] }]
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public int CategoryId { get; set; }
    public Category? Category { get; set; }
}

