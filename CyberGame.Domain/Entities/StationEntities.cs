using CyberGame.Domain.Enums;

namespace CyberGame.Domain.Entities;

public class MachineGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<Machine> Machines { get; set; } = new List<Machine>();
    public ICollection<PricingPlan> PricingPlans { get; set; } = new List<PricingPlan>();
}

public class Machine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public Guid MachineGroupId { get; set; }
    public MachineGroup MachineGroup { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? MacAddress { get; set; }
    public MachineStatus Status { get; set; } = MachineStatus.Available;
    public DateTime? LastHeartbeatAtUtc { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<PlaySession> PlaySessions { get; set; } = new List<PlaySession>();
    public ICollection<MaintenanceTicket> MaintenanceTickets { get; set; } = new List<MaintenanceTicket>();
}

public class PricingPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public Guid? MachineGroupId { get; set; }
    public MachineGroup? MachineGroup { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<PricingRule> PricingRules { get; set; } = new List<PricingRule>();
}

public class PricingRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PricingPlanId { get; set; }
    public PricingPlan PricingPlan { get; set; } = null!;
    public byte DayOfWeekMask { get; set; } = 127; // bits for Mon-Sun
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public decimal HourlyRate { get; set; }
    public int Priority { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
