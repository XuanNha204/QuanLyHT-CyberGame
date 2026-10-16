using CyberGame.Domain.Enums;

namespace CyberGame.Application.DTOs;

public record CustomerGroupDto(
    Guid Id,
    Guid BranchId,
    string Name,
    decimal DiscountPercent,
    bool IsActive
);

public record CustomerDto(
    Guid Id,
    Guid HomeBranchId,
    Guid? CustomerGroupId,
    string? GroupName,
    string Code,
    string FullName,
    string? Phone,
    string? Email,
    DateOnly? DateOfBirth,
    CustomerStatus Status,
    decimal WalletBalance,
    DateTime JoinedAtUtc,
    string? Notes
);

public record CreateCustomerRequest(
    Guid? CustomerGroupId,
    string Code,
    string FullName,
    string? Phone,
    string? Email,
    DateOnly? DateOfBirth,
    string InitialPin,
    decimal InitialDeposit = 0,
    string? Notes = null
);

public record UpdateCustomerRequest(
    Guid? CustomerGroupId,
    string FullName,
    string? Phone,
    string? Email,
    DateOnly? DateOfBirth,
    CustomerStatus Status,
    string? Notes
);

public record TopUpWalletRequest(
    decimal Amount,
    PaymentMethod Method,
    string? Note = null
);

public record AdjustWalletRequest(
    decimal Amount,
    string Reason
);

public record WalletTransactionDto(
    Guid Id,
    Guid WalletId,
    WalletTransactionType Type,
    decimal Amount,
    decimal BalanceBefore,
    decimal BalanceAfter,
    string Description,
    DateTime OccurredAtUtc,
    string CreatedByUsername
);
