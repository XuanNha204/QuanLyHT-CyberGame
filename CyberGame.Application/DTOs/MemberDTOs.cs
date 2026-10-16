using CyberGame.Domain.Enums;

namespace CyberGame.Application.DTOs;

// Member Auth
public record MemberLoginRequest(
    string MemberCode,
    string Pin,
    string MachineCode,
    Guid? MachineId = null
);

public record MemberMachineSelectionDto(
    Guid Id,
    string Code,
    string DisplayName,
    string GroupName,
    Guid BranchId,
    string BranchName,
    MachineStatus Status
);

public record MemberProfileDto(
    Guid Id,
    string Code,
    string FullName,
    string? Phone,
    string? Email,
    decimal WalletBalance
);

public record MemberMachineDto(
    Guid Id,
    string Code,
    string DisplayName,
    Guid BranchId,
    string BranchName
);

public record MemberLoginResponse(
    string Token,
    MemberProfileDto Member,
    MemberMachineDto Machine
);

public record MemberMeResponse(
    MemberProfileDto Member,
    MemberMachineDto Machine
);

// Member Session
public record MemberSessionDto(
    Guid SessionId,
    string SessionNumber,
    PlaySessionStatus Status,
    BillingMode BillingMode,
    string MachineCode,
    string MachineDisplayName,
    DateTime StartedAtUtc,
    DateTime? PausedAtUtc,
    int TotalPausedSeconds,
    decimal HourlyRate,
    decimal UsageAmount,
    decimal ServiceAmount,
    decimal TotalAmount,
    decimal WalletBalance,
    decimal AvailableBalance
);

public record MemberSessionQuoteDto(
    Guid SessionId,
    decimal HourlyRate,
    int TotalBillableSeconds,
    decimal UsageAmount,
    decimal ServiceAmount,
    decimal TotalAmount,
    decimal WalletBalance,
    decimal AvailableBalance,
    PlaySessionStatus Status,
    DateTime ServerTimeUtc
);

public record MemberSessionSummaryDto(
    Guid SessionId,
    string SessionNumber,
    string MachineCode,
    DateTime StartedAtUtc,
    DateTime EndedAtUtc,
    int TotalSeconds,
    decimal UsageAmount,
    decimal ServiceAmount,
    decimal TotalAmount,
    decimal WalletBalanceBefore,
    decimal WalletBalanceAfter,
    string? InvoiceNumber
);

// Member Catalog & Order
public record MemberProductDto(
    Guid Id,
    string Name,
    string Sku,
    string? CategoryName,
    decimal SalePrice,
    string Unit,
    string? ImageUrl,
    bool TrackInventory,
    decimal? QuantityOnHand
);

public record MemberOrderItemRequest(
    Guid ProductId,
    decimal Quantity,
    string? Note
);

public record CreateMemberOrderRequest(
    List<MemberOrderItemRequest> Items,
    PaymentMethod PaymentMethod = PaymentMethod.Cash
);

public record MemberOrderItemDto(
    Guid ProductId,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    string? Note
);

public record MemberOrderDto(
    Guid Id,
    string OrderNumber,
    OrderStatus Status,
    decimal TotalAmount,
    DateTime CreatedAtUtc,
    List<MemberOrderItemDto> Items,
    PaymentMethod PaymentMethod = PaymentMethod.Cash,
    PaymentStatus PaymentStatus = PaymentStatus.Pending,
    DateTime? PaidAtUtc = null
);

// Member & Staff Top-up Request
public record CreateMemberTopUpRequest(
    decimal Amount,
    string? Note
);

public record MemberTopUpRequestDto(
    Guid Id,
    decimal Amount,
    MemberTopUpRequestStatus Status,
    string? Note,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc
);

public record StaffMemberTopUpRequestDto(
    Guid Id,
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    decimal Amount,
    MemberTopUpRequestStatus Status,
    string? Note,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc,
    string? ProcessedByStaffName
);

public record SetMemberPinRequest(
    string Pin
);

public record ChangeMemberPinRequest(
    string CurrentPin,
    string NewPin
);
