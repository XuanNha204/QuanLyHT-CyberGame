using CyberGame.Domain.Enums;

namespace CyberGame.Application.DTOs;

public record ShiftDto(
    Guid Id,
    Guid BranchId,
    Guid UserId,
    string UserName,
    string ShiftNumber,
    ShiftStatus Status,
    DateTime OpenedAtUtc,
    DateTime? ClosedAtUtc,
    decimal OpeningCash,
    decimal? ExpectedCash,
    decimal? ActualCash,
    decimal? Difference,
    string? ClosingNote
);

public record OpenShiftRequest(decimal OpeningCash);

public record CloseShiftRequest(
    decimal ActualCash,
    string? ClosingNote = null
);

public record CreateCashMovementRequest(
    CashMovementType Type,
    decimal Amount,
    string Description
);

public record CashMovementDto(
    Guid Id,
    CashMovementType Type,
    decimal Amount,
    string Description,
    DateTime OccurredAtUtc,
    string CreatedByUsername
);

public record ShiftSummaryDto(
    ShiftDto Shift,
    decimal TotalCashSales,
    decimal TotalCashTopUps,
    decimal TotalCashIn,
    decimal TotalCashRefunds,
    decimal TotalCashExpenses,
    decimal TotalCashOut,
    decimal TotalOtherPayments,
    decimal ExpectedCash,
    int TotalOrders,
    int TotalSessions
);

public record ExpenseCategoryDto(
    Guid Id,
    Guid BranchId,
    string Name,
    bool IsActive
);

public record CreateExpenseCategoryRequest(string Name);

public record ExpenseDto(
    Guid Id,
    Guid BranchId,
    Guid? ShiftId,
    Guid CategoryId,
    string CategoryName,
    decimal Amount,
    DateTime SpentAtUtc,
    string Description,
    string? ReceiptNumber,
    ExpenseStatus Status,
    string CreatedByUsername
);

public record CreateExpenseRequest(
    Guid CategoryId,
    decimal Amount,
    string Description,
    string? ReceiptNumber
);

public record MaintenanceTicketDto(
    Guid Id,
    Guid BranchId,
    Guid MachineId,
    string MachineCode,
    string TicketNumber,
    MaintenancePriority Priority,
    MaintenanceStatus Status,
    string Title,
    string Description,
    string? AssignedToUserName,
    DateTime OpenedAtUtc,
    DateTime? ResolvedAtUtc,
    DateTime? ClosedAtUtc,
    string? Resolution,
    decimal Cost,
    IReadOnlyList<MaintenanceLogDto> Logs
);

public record MaintenanceLogDto(
    Guid Id,
    string? StatusFrom,
    string? StatusTo,
    string Note,
    decimal CostDelta,
    DateTime OccurredAtUtc,
    string UserName
);

public record CreateMaintenanceTicketRequest(
    Guid MachineId,
    MaintenancePriority Priority,
    string Title,
    string Description
);

public record AddMaintenanceLogRequest(
    string? StatusTo,
    string Note,
    decimal CostDelta = 0
);
