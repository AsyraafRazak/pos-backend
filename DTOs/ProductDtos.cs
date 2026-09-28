namespace pos_backend.DTOs;

public record ProductDto(
    int Id,
    string Name,
    string? Description,
    string? Sku,
    string? Barcode,
    decimal Price,
    decimal CostPrice,
    int StockQuantity,
    bool TrackStock,
    string? ImageUrl,
    string? ModifiersJson,
    bool IsActive,
    int CategoryId,
    string? CategoryName
);

public record CreateProductRequest(
    string Name,
    string? Description,
    string? Sku,
    string? Barcode,
    decimal Price,
    decimal CostPrice,
    int StockQuantity,
    bool TrackStock,
    string? ImageUrl,
    string? ModifiersJson,
    bool IsActive,
    int CategoryId
);

public record UpdateProductRequest(
    string Name,
    string? Description,
    string? Sku,
    string? Barcode,
    decimal Price,
    decimal CostPrice,
    int StockQuantity,
    bool TrackStock,
    string? ImageUrl,
    string? ModifiersJson,
    bool IsActive,
    int CategoryId
);

public record AdjustStockRequest(
    int QuantityAdjustment,
    string? Reason
);

