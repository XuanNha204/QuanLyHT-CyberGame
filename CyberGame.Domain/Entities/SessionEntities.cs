using CyberGame.Domain.Enums;

namespace CyberGame.Domain.Entities;

public class PlaySession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public Guid MachineId { get; set; }
    public Machine Machine { get; set; } = null!;
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid ShiftId { get; set; }
    public Shift Shift { get; set; } = null!;
    public string SessionNumber { get; set; } = string.Empty;
    public BillingMode BillingMode { get; set; }
    public PlaySessionStatus Status { get; set; } = PlaySessionStatus.Open;
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PausedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public int TotalPausedSeconds { get; set; }
    public decimal UsageAmount { get; set; }
    public decimal ServiceAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public Guid? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<SessionChargeSegment> ChargeSegments { get; set; } = new List<SessionChargeSegment>();
    public ICollection<SessionEvent> Events { get; set; } = new List<SessionEvent>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}

public class SessionChargeSegment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlaySessionId { get; set; }
    public PlaySession PlaySession { get; set; } = null!;
    public Guid? PricingRuleId { get; set; }
    public PricingRule? PricingRule { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }
    public decimal RatePerHour { get; set; }
    public int BillableSeconds { get; set; }
    public decimal Amount { get; set; }
}

public class SessionEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlaySessionId { get; set; }
    public PlaySession PlaySession { get; set; } = null!;
    public string EventType { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public string? DataJson { get; set; }
    public string? Note { get; set; }
}
