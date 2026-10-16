using CyberGame.Domain.Enums;

namespace CyberGame.Application.DTOs;

public record PlaySessionDto(
    Guid Id,
    Guid BranchId,
    Guid MachineId,
    string MachineCode,
    string MachineDisplayName,
    Guid? CustomerId,
    string? CustomerName,
    string? CustomerCode,
    Guid ShiftId,
    string SessionNumber,
    BillingMode BillingMode,
    PlaySessionStatus Status,
    DateTime StartedAtUtc,
    DateTime? PausedAtUtc,
    DateTime? EndedAtUtc,
    int TotalPausedSeconds,
    decimal UsageAmount,
    decimal ServiceAmount,
    decimal DiscountAmount,
    decimal TotalAmount,
    Guid? InvoiceId,
    decimal HourlyRate,
    int BillableMinutes
);

public record StartSessionRequest(
    Guid MachineId,
    Guid? CustomerId,
    BillingMode BillingMode
);

public record TransferSessionRequest(
    Guid NewMachineId
);

public record CompleteSessionRequest(
    bool AutoCreateInvoice = true
);

public record CancelSessionRequest(
    string Reason
);

public record SessionQuoteDto(
    Guid SessionId,
    decimal RatePerHour,
    int BillableSeconds,
    decimal UsageAmount,
    decimal ServiceAmount,
    decimal TotalAmount
);

public record SessionEventDto(
    Guid Id,
    string EventType,
    DateTime OccurredAtUtc,
    string UserName,
    string? Note
);
