using pos_backend.Models;

namespace pos_backend.DTOs;

public record CreateOrderItemRequest(
    int? ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal DiscountAmount,
    decimal TotalPrice,
    string? SelectedModifiersJson,
    string? Notes
);

public record CreatePaymentRequest(
    PaymentMethod Method,
    decimal AmountTendered,
    decimal ChangeGiven,
    decimal TotalPaid,
    string? TransactionReference
);

public record CreateOrderRequest(
    string? OrderNumber,
    string? TableNumber,
    OrderType Type,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal TaxTotal,
    decimal GrandTotal,
    string? CustomerName,
    string? Notes,
    string CashierId,
    string CashierName,
    int? ShiftId,
    List<CreateOrderItemRequest> Items,
    List<CreatePaymentRequest> Payments
);

public record OrderItemDto(
    int Id,
    int? ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal DiscountAmount,
    decimal TotalPrice,
    string? SelectedModifiersJson,
    string? Notes
);

public record PaymentDto(
    int Id,
    PaymentMethod Method,
    decimal AmountTendered,
    decimal ChangeGiven,
    decimal TotalPaid,
    PaymentStatus Status,
    string? TransactionReference,
    DateTime CreatedAt
);

public record OrderResponseDto(
    int Id,
    string OrderNumber,
    string? TableNumber,
    OrderType Type,
    OrderStatus Status,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal TaxTotal,
    decimal GrandTotal,
    string? CustomerName,
    string? Notes,
    string CashierId,
    string CashierName,
    int? ShiftId,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    List<OrderItemDto> Items,
    List<PaymentDto> Payments
);

