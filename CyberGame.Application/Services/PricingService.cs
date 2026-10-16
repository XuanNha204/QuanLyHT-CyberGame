using CyberGame.Application.Common;
using CyberGame.Application.DTOs;
using CyberGame.Domain.Entities;
using CyberGame.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CyberGame.Application.Services;

public interface IPricingService
{
    Task<IReadOnlyList<PricingPlanDto>> GetPricingPlansAsync(CancellationToken ct = default);
    Task<PricingPlanDto> GetPricingPlanByIdAsync(Guid id, CancellationToken ct = default);
    Task<PricingPlanDto> CreatePricingPlanAsync(CreatePricingPlanRequest request, CancellationToken ct = default);
    Task<PricingPlanDto> UpdatePricingPlanAsync(Guid id, CreatePricingPlanRequest request, CancellationToken ct = default);
    Task<decimal> GetEffectiveRatePerHourAsync(Guid branchId, Guid machineGroupId, DateTime atUtc, CancellationToken ct = default);
}

public class PricingService : IPricingService
{
    private readonly ICyberGameDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public PricingService(ICyberGameDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    private async Task<Guid> GetBranchIdAsync(CancellationToken ct)
    {
        if (_currentUser.BranchId.HasValue) return _currentUser.BranchId.Value;
        var b = await _db.Branches.FirstOrDefaultAsync(x => x.IsActive, ct);
        return b?.Id ?? throw new NotFoundException("Không tìm thấy chi nhánh.", "branch_not_found");
    }

    public async Task<IReadOnlyList<PricingPlanDto>> GetPricingPlansAsync(CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var plans = await _db.PricingPlans
            .AsNoTracking()
            .Include(p => p.MachineGroup)
            .Include(p => p.PricingRules)
            .Where(p => p.BranchId == branchId && p.IsActive)
            .OrderByDescending(p => p.IsDefault)
            .ThenBy(p => p.Name)
            .ToListAsync(ct);

        return plans.Select(MapToDto).ToList();
    }

    public async Task<PricingPlanDto> GetPricingPlanByIdAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var plan = await _db.PricingPlans
            .AsNoTracking()
            .Include(p => p.MachineGroup)
            .Include(p => p.PricingRules)
            .FirstOrDefaultAsync(p => p.Id == id && p.BranchId == branchId, ct);

        if (plan == null) throw new NotFoundException("Không tìm thấy bảng giá.", "pricing_plan_not_found");
        return MapToDto(plan);
    }

    public async Task<PricingPlanDto> CreatePricingPlanAsync(CreatePricingPlanRequest request, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);

        if (request.EffectiveToUtc.HasValue && request.EffectiveToUtc <= request.EffectiveFromUtc)
        {
            throw new BusinessRuleException("Thời điểm kết thúc bảng giá phải lớn hơn thời điểm bắt đầu.", "invalid_effective_range");
        }

        var group = await _db.MachineGroups.FirstOrDefaultAsync(g => g.Id == request.MachineGroupId && g.BranchId == branchId, ct);
        if (group == null)
        {
            throw new NotFoundException("Không tìm thấy nhóm máy trong chi nhánh này.", "machine_group_not_found");
        }

        var plan = new PricingPlan
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            MachineGroupId = request.MachineGroupId,
            Name = request.Name.Trim(),
            EffectiveFromUtc = request.EffectiveFromUtc,
            EffectiveToUtc = request.EffectiveToUtc,
            IsDefault = request.IsDefault,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        if (request.Rules != null)
        {
            foreach (var r in request.Rules)
            {
                plan.PricingRules.Add(new PricingRule
                {
                    Id = Guid.NewGuid(),
                    PricingPlanId = plan.Id,
                    DayOfWeekMask = r.DayOfWeekMask,
                    StartTime = r.StartTime,
                    EndTime = r.EndTime,
                    HourlyRate = r.HourlyRate,
                    Priority = r.Priority,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }

        _db.PricingPlans.Add(plan);
        await _db.SaveChangesAsync(ct);

        return await GetPricingPlanByIdAsync(plan.Id, ct);
    }

    public async Task<PricingPlanDto> UpdatePricingPlanAsync(Guid id, CreatePricingPlanRequest request, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var plan = await _db.PricingPlans
            .Include(p => p.PricingRules)
            .FirstOrDefaultAsync(p => p.Id == id && p.BranchId == branchId, ct);

        if (plan == null) throw new NotFoundException("Không tìm thấy bảng giá.", "pricing_plan_not_found");

        var group = await _db.MachineGroups.FirstOrDefaultAsync(g => g.Id == request.MachineGroupId && g.BranchId == branchId, ct);
        if (group == null)
        {
            throw new NotFoundException("Không tìm thấy nhóm máy trong chi nhánh này.", "machine_group_not_found");
        }

        plan.Name = request.Name.Trim();
        plan.MachineGroupId = request.MachineGroupId;
        plan.EffectiveFromUtc = request.EffectiveFromUtc;
        plan.EffectiveToUtc = request.EffectiveToUtc;
        plan.IsDefault = request.IsDefault;
        plan.UpdatedAtUtc = DateTime.UtcNow;

        plan.PricingRules.Clear();
        if (request.Rules != null)
        {
            foreach (var r in request.Rules)
            {
                plan.PricingRules.Add(new PricingRule
                {
                    Id = Guid.NewGuid(),
                    PricingPlanId = plan.Id,
                    DayOfWeekMask = r.DayOfWeekMask,
                    StartTime = r.StartTime,
                    EndTime = r.EndTime,
                    HourlyRate = r.HourlyRate,
                    Priority = r.Priority,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        return await GetPricingPlanByIdAsync(plan.Id, ct);
    }

    public async Task<decimal> GetEffectiveRatePerHourAsync(Guid branchId, Guid machineGroupId, DateTime atUtc, CancellationToken ct = default)
    {
        // 1. Tìm bảng giá gắn với nhóm máy này trước, nếu không có lấy bảng giá mặc định của branch
        var activePlans = await _db.PricingPlans
            .AsNoTracking()
            .Include(p => p.PricingRules)
            .Where(p => p.BranchId == branchId && p.IsActive &&
                        p.EffectiveFromUtc <= atUtc &&
                        (!p.EffectiveToUtc.HasValue || p.EffectiveToUtc.Value >= atUtc))
            .OrderByDescending(p => p.MachineGroupId == machineGroupId) // Ưu tiên plan theo group
            .ThenByDescending(p => p.IsDefault)
            .ToListAsync(ct);

        var timeOfDay = atUtc.TimeOfDay;
        int dayIndex = (int)atUtc.DayOfWeek; // 0=Sun, 1=Mon, ..., 6=Sat
        byte dayBit = (byte)(1 << dayIndex);

        foreach (var plan in activePlans)
        {
            var matchingRule = plan.PricingRules
                .Where(r => (r.DayOfWeekMask & dayBit) != 0)
                .Where(r => r.StartTime <= timeOfDay && r.EndTime >= timeOfDay)
                .OrderByDescending(r => r.Priority)
                .FirstOrDefault();

            if (matchingRule != null)
            {
                return matchingRule.HourlyRate;
            }

            // Nếu không có rule theo khung giờ chi tiết, lấy rule bất kỳ có rate hợp lệ
            var fallback = plan.PricingRules.OrderByDescending(r => r.Priority).FirstOrDefault();
            if (fallback != null)
            {
                return fallback.HourlyRate;
            }
        }

        // Giá mặc định an toàn nếu chưa có bảng giá: 10.000đ/giờ
        return 10000m;
    }

    private static PricingPlanDto MapToDto(PricingPlan p)
    {
        return new PricingPlanDto(
            p.Id,
            p.BranchId,
            p.MachineGroupId,
            p.MachineGroup?.Name,
            p.Name,
            p.EffectiveFromUtc,
            p.EffectiveToUtc,
            p.IsDefault,
            p.IsActive,
            p.PricingRules.OrderBy(r => r.StartTime).Select(r => new PricingRuleDto(
                r.Id,
                r.DayOfWeekMask,
                r.StartTime,
                r.EndTime,
                r.HourlyRate,
                r.Priority
            )).ToList()
        );
    }
}
