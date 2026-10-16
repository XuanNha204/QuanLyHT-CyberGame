using CyberGame.Domain.Enums;

namespace CyberGame.Domain.Entities;

public class Shift
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public string ShiftNumber { get; set; } = string.Empty;
    public ShiftStatus Status { get; set; } = ShiftStatus.Open;
    public DateTime OpenedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAtUtc { get; set; }
    public decimal OpeningCash { get; set; }
    public decimal? ExpectedCash { get; set; }
    public decimal? ActualCash { get; set; }
    public decimal? Difference { get; set; }
    public string? ClosingNote { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<CashMovement> CashMovements { get; set; } = new List<CashMovement>();
    public ICollection<PlaySession> PlaySessions { get; set; } = new List<PlaySession>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}

public class CashMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShiftId { get; set; }
    public Shift Shift { get; set; } = null!;
    public CashMovementType Type { get; set; }
    public decimal Amount { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public Guid CreatedByUserId { get; set; }
    public AppUser CreatedByUser { get; set; } = null!;
}

public class ExpenseCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}

public class Expense
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public Guid? ShiftId { get; set; }
    public Shift? Shift { get; set; }
    public Guid CategoryId { get; set; }
    public ExpenseCategory Category { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateTime SpentAtUtc { get; set; } = DateTime.UtcNow;
    public string Description { get; set; } = string.Empty;
    public string? ReceiptNumber { get; set; }
    public ExpenseStatus Status { get; set; } = ExpenseStatus.Approved;
    public Guid? ApprovedByUserId { get; set; }
    public AppUser? ApprovedByUser { get; set; }
    public Guid CreatedByUserId { get; set; }
    public AppUser CreatedByUser { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class MaintenanceTicket
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public Guid MachineId { get; set; }
    public Machine Machine { get; set; } = null!;
    public string TicketNumber { get; set; } = string.Empty;
    public MaintenancePriority Priority { get; set; } = MaintenancePriority.Medium;
    public MaintenanceStatus Status { get; set; } = MaintenanceStatus.Open;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? AssignedToUserId { get; set; }
    public AppUser? AssignedToUser { get; set; }
    public DateTime OpenedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? Resolution { get; set; }
    public decimal Cost { get; set; }
    public Guid CreatedByUserId { get; set; }
    public AppUser CreatedByUser { get; set; } = null!;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<MaintenanceLog> Logs { get; set; } = new List<MaintenanceLog>();
}

public class MaintenanceLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TicketId { get; set; }
    public MaintenanceTicket Ticket { get; set; } = null!;
    public string? StatusFrom { get; set; }
    public string? StatusTo { get; set; }
    public string Note { get; set; } = string.Empty;
    public decimal CostDelta { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
}

public class BranchSetting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public string Key { get; set; } = string.Empty;
    public string ValueJson { get; set; } = "{}";
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid UpdatedByUserId { get; set; }
    public AppUser UpdatedByUser { get; set; } = null!;
}

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public Guid? UserId { get; set; }
    public AppUser? User { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public string? IpAddress { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}

public class IdempotencyRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Scope { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public int ResponseStatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
