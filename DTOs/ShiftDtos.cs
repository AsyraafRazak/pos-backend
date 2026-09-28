using pos_backend.Models;

namespace pos_backend.DTOs;

public record OpenShiftRequest(
    string CashierId,
    string CashierName,
    decimal StartingFloat
);

public record CloseShiftRequest(
    decimal ActualCash,
    string? ClosingNotes
);

public record ShiftResponseDto(
    int Id,
    string CashierId,
    string CashierName,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    decimal StartingFloat,
    decimal CashSales,
    decimal NonCashSales,
    decimal ExpectedCash,
    decimal? ActualCash,
    decimal? Discrepancy,
    string? ClosingNotes,
    ShiftStatus Status,
    int TotalOrdersCount
);

