using CyberGame.Domain.Enums;

namespace CyberGame.Application.DTOs;

public record BranchDto(
    Guid Id,
    string Code,
    string Name,
    string? Address,
    string TimeZoneId,
    string? Phone,
    bool IsActive
);

public record UpdateBranchRequest(
    string Name,
    string? Address,
    string TimeZoneId,
    string? Phone
);

public record MachineGroupDto(
    Guid Id,
    Guid BranchId,
    string Code,
    string Name,
    int SortOrder,
    bool IsActive,
    int TotalMachines
);

public record CreateMachineGroupRequest(string Code, string Name, int SortOrder = 0);
public record UpdateMachineGroupRequest(string Name, int SortOrder, bool IsActive);

public record MachineDto(
    Guid Id,
    Guid BranchId,
    Guid MachineGroupId,
    string GroupName,
    string Code,
    string DisplayName,
    string? IpAddress,
    string? MacAddress,
    MachineStatus Status,
    string? Notes,
    bool IsActive,
    Guid? ActiveSessionId,
    string? CurrentCustomerName,
    DateTime? SessionStartedAtUtc,
    decimal CurrentUsageAmount
);

public record CreateMachineRequest(
    Guid MachineGroupId,
    string Code,
    string DisplayName,
    string? IpAddress,
    string? MacAddress,
    string? Notes
);

public record UpdateMachineRequest(
    Guid MachineGroupId,
    string DisplayName,
    string? IpAddress,
    string? MacAddress,
    string? Notes,
    bool IsActive
);

public record UpdateMachineStatusRequest(MachineStatus Status, string? Note);

public record PricingPlanDto(
    Guid Id,
    Guid BranchId,
    Guid? MachineGroupId,
    string? MachineGroupName,
    string Name,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    bool IsDefault,
    bool IsActive,
    IReadOnlyList<PricingRuleDto> Rules
);

public record PricingRuleDto(
    Guid Id,
    byte DayOfWeekMask,
    TimeSpan StartTime,
    TimeSpan EndTime,
    decimal HourlyRate,
    int Priority
);

public record CreatePricingPlanRequest(
    Guid? MachineGroupId,
    string Name,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    bool IsDefault,
    List<CreatePricingRuleRequest> Rules
);

public record CreatePricingRuleRequest(
    byte DayOfWeekMask,
    TimeSpan StartTime,
    TimeSpan EndTime,
    decimal HourlyRate,
    int Priority
);
