using CyberGame.Domain.Enums;

namespace CyberGame.Domain.Entities;

public class CustomerGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public decimal DiscountPercent { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Customer> Customers { get; set; } = new List<Customer>();
}

public class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HomeBranchId { get; set; }
    public Branch HomeBranch { get; set; } = null!;
    public Guid? CustomerGroupId { get; set; }
    public CustomerGroup? CustomerGroup { get; set; }
    public string Code { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public CustomerStatus Status { get; set; } = CustomerStatus.Active;
    public string? MemberPinHash { get; set; }
    public int MemberFailedLoginCount { get; set; }
    public DateTime? MemberLockoutEndUtc { get; set; }
    public DateTime? MemberLastLoginAtUtc { get; set; }
    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Wallet? Wallet { get; set; }
    public ICollection<PlaySession> PlaySessions { get; set; } = new List<PlaySession>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<MemberTopUpRequest> MemberTopUpRequests { get; set; } = new List<MemberTopUpRequest>();
}

public class MemberTopUpRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public decimal Amount { get; set; }
    public MemberTopUpRequestStatus Status { get; set; } = MemberTopUpRequestStatus.Pending;
    public string? Note { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAtUtc { get; set; }
    public Guid? ProcessedByUserId { get; set; }
    public AppUser? ProcessedByUser { get; set; }
    public Guid? WalletTransactionId { get; set; }
    public WalletTransaction? WalletTransaction { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class Wallet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public decimal Balance { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<WalletTransaction> Transactions { get; set; } = new List<WalletTransaction>();
}

public class WalletTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WalletId { get; set; }
    public Wallet Wallet { get; set; } = null!;
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public WalletTransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? ShiftId { get; set; }
    public Shift? Shift { get; set; }
    public Guid CreatedByUserId { get; set; }
    public AppUser CreatedByUser { get; set; } = null!;
}
