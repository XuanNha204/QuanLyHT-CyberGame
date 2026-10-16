using CyberGame.Application.Common;
using CyberGame.Application.DTOs;
using CyberGame.Domain.Entities;
using CyberGame.Domain.Enums;
using CyberGame.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CyberGame.Application.Services;

public interface IBranchMachineService
{
    Task<BranchDto> GetCurrentBranchAsync(CancellationToken ct = default);
    Task<BranchDto> UpdateCurrentBranchAsync(UpdateBranchRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<MachineGroupDto>> GetMachineGroupsAsync(CancellationToken ct = default);
    Task<MachineGroupDto> CreateMachineGroupAsync(CreateMachineGroupRequest request, CancellationToken ct = default);
    Task<MachineGroupDto> UpdateMachineGroupAsync(Guid id, UpdateMachineGroupRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<MachineDto>> GetMachinesAsync(Guid? groupId = null, MachineStatus? status = null, CancellationToken ct = default);
    Task<MachineDto> GetMachineByIdAsync(Guid id, CancellationToken ct = default);
    Task<MachineDto> CreateMachineAsync(CreateMachineRequest request, CancellationToken ct = default);
    Task<MachineDto> UpdateMachineAsync(Guid id, UpdateMachineRequest request, CancellationToken ct = default);
    Task<MachineDto> UpdateMachineStatusAsync(Guid id, UpdateMachineStatusRequest request, CancellationToken ct = default);
}

public class BranchMachineService : IBranchMachineService
{
    private readonly ICyberGameDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public BranchMachineService(ICyberGameDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    private async Task<Guid> GetBranchIdAsync(CancellationToken ct)
    {
        if (_currentUser.BranchId.HasValue)
        {
            return _currentUser.BranchId.Value;
        }

        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.IsActive, ct);
        if (branch != null)
        {
            return branch.Id;
        }

        throw new NotFoundException("Không tìm thấy chi nhánh mặc định.", "branch_not_found");
    }

    public async Task<BranchDto> GetCurrentBranchAsync(CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.Id == branchId, ct);
        if (branch == null)
        {
            throw new NotFoundException("Không tìm thấy chi nhánh.", "branch_not_found");
        }

        return new BranchDto(branch.Id, branch.Code, branch.Name, branch.Address, branch.TimeZoneId, branch.Phone, branch.IsActive);
    }

    public async Task<BranchDto> UpdateCurrentBranchAsync(UpdateBranchRequest request, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.Id == branchId, ct);
        if (branch == null)
        {
            throw new NotFoundException("Không tìm thấy chi nhánh.", "branch_not_found");
        }

        branch.Name = request.Name.Trim();
        branch.Address = request.Address?.Trim();
        branch.TimeZoneId = request.TimeZoneId.Trim();
        branch.Phone = request.Phone?.Trim();
        branch.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return new BranchDto(branch.Id, branch.Code, branch.Name, branch.Address, branch.TimeZoneId, branch.Phone, branch.IsActive);
    }

    public async Task<IReadOnlyList<MachineGroupDto>> GetMachineGroupsAsync(CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var groups = await _db.MachineGroups
            .AsNoTracking()
            .Where(g => g.BranchId == branchId)
            .OrderBy(g => g.SortOrder)
            .Select(g => new MachineGroupDto(
                g.Id,
                g.BranchId,
                g.Code,
                g.Name,
                g.SortOrder,
                g.IsActive,
                g.Machines.Count(m => m.IsActive)
            ))
            .ToListAsync(ct);

        return groups;
    }

    public async Task<MachineGroupDto> CreateMachineGroupAsync(CreateMachineGroupRequest request, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var code = request.Code.Trim().ToUpperInvariant();
        if (await _db.MachineGroups.AnyAsync(g => g.BranchId == branchId && g.Code == code, ct))
        {
            throw new ConflictException("Mã nhóm máy đã tồn tại trong chi nhánh.", "machine_group_exists");
        }

        var group = new MachineGroup
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            Code = code,
            Name = request.Name.Trim(),
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.MachineGroups.Add(group);
        await _db.SaveChangesAsync(ct);

        return new MachineGroupDto(group.Id, group.BranchId, group.Code, group.Name, group.SortOrder, group.IsActive, 0);
    }

    public async Task<MachineGroupDto> UpdateMachineGroupAsync(Guid id, UpdateMachineGroupRequest request, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var group = await _db.MachineGroups.Include(g => g.Machines).FirstOrDefaultAsync(g => g.Id == id && g.BranchId == branchId, ct);
        if (group == null)
        {
            throw new NotFoundException("Không tìm thấy nhóm máy.", "machine_group_not_found");
        }

        group.Name = request.Name.Trim();
        group.SortOrder = request.SortOrder;
        group.IsActive = request.IsActive;
        group.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new MachineGroupDto(group.Id, group.BranchId, group.Code, group.Name, group.SortOrder, group.IsActive, group.Machines.Count(m => m.IsActive));
    }

    public async Task<IReadOnlyList<MachineDto>> GetMachinesAsync(Guid? groupId = null, MachineStatus? status = null, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var query = _db.Machines
            .AsNoTracking()
            .Include(m => m.MachineGroup)
            .Include(m => m.PlaySessions.Where(s => s.Status == PlaySessionStatus.Open || s.Status == PlaySessionStatus.Paused))
                .ThenInclude(s => s.Customer)
            .Where(m => m.BranchId == branchId && m.IsActive);

        if (groupId.HasValue)
        {
            query = query.Where(m => m.MachineGroupId == groupId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(m => m.Status == status.Value);
        }

        var machines = await query.OrderBy(m => m.Code).ToListAsync(ct);

        return machines.Select(m =>
        {
            var activeSession = m.PlaySessions.FirstOrDefault();
            return new MachineDto(
                m.Id,
                m.BranchId,
                m.MachineGroupId,
                m.MachineGroup.Name,
                m.Code,
                m.DisplayName,
                m.IpAddress,
                m.MacAddress,
                m.Status,
                m.Notes,
                m.IsActive,
                activeSession?.Id,
                activeSession?.Customer?.FullName ?? (activeSession != null ? "Khách vãng lai" : null),
                activeSession?.StartedAtUtc,
                activeSession?.TotalAmount ?? 0m
            );
        }).ToList();
    }

    public async Task<MachineDto> GetMachineByIdAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var machine = await _db.Machines
            .AsNoTracking()
            .Include(m => m.MachineGroup)
            .Include(m => m.PlaySessions.Where(s => s.Status == PlaySessionStatus.Open || s.Status == PlaySessionStatus.Paused))
                .ThenInclude(s => s.Customer)
            .FirstOrDefaultAsync(m => m.Id == id && m.BranchId == branchId, ct);

        if (machine == null)
        {
            throw new NotFoundException("Không tìm thấy máy trạm.", "machine_not_found");
        }

        var activeSession = machine.PlaySessions.FirstOrDefault();
        return new MachineDto(
            machine.Id,
            machine.BranchId,
            machine.MachineGroupId,
            machine.MachineGroup.Name,
            machine.Code,
            machine.DisplayName,
            machine.IpAddress,
            machine.MacAddress,
            machine.Status,
            machine.Notes,
            machine.IsActive,
            activeSession?.Id,
            activeSession?.Customer?.FullName ?? (activeSession != null ? "Khách vãng lai" : null),
            activeSession?.StartedAtUtc,
            activeSession?.TotalAmount ?? 0m
        );
    }

    public async Task<MachineDto> CreateMachineAsync(CreateMachineRequest request, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var code = request.Code.Trim().ToUpperInvariant();

        if (await _db.Machines.AnyAsync(m => m.BranchId == branchId && m.Code == code, ct))
        {
            throw new ConflictException("Mã máy đã tồn tại trong chi nhánh.", "machine_code_exists");
        }

        var group = await _db.MachineGroups.FirstOrDefaultAsync(g => g.Id == request.MachineGroupId && g.BranchId == branchId, ct);
        if (group == null)
        {
            throw new NotFoundException("Không tìm thấy nhóm máy trong chi nhánh này.", "machine_group_not_found");
        }

        var machine = new Machine
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            MachineGroupId = request.MachineGroupId,
            Code = code,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? code : request.DisplayName.Trim(),
            IpAddress = request.IpAddress?.Trim(),
            MacAddress = request.MacAddress?.Trim(),
            Status = MachineStatus.Available,
            Notes = request.Notes?.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Machines.Add(machine);
        await _db.SaveChangesAsync(ct);

        return new MachineDto(
            machine.Id,
            machine.BranchId,
            machine.MachineGroupId,
            group.Name,
            machine.Code,
            machine.DisplayName,
            machine.IpAddress,
            machine.MacAddress,
            machine.Status,
            machine.Notes,
            machine.IsActive,
            null,
            null,
            null,
            0m
        );
    }

    public async Task<MachineDto> UpdateMachineAsync(Guid id, UpdateMachineRequest request, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var machine = await _db.Machines
            .Include(m => m.MachineGroup)
            .FirstOrDefaultAsync(m => m.Id == id && m.BranchId == branchId, ct);

        if (machine == null)
        {
            throw new NotFoundException("Không tìm thấy máy trạm.", "machine_not_found");
        }

        if (machine.MachineGroupId != request.MachineGroupId)
        {
            var newGroup = await _db.MachineGroups.FirstOrDefaultAsync(g => g.Id == request.MachineGroupId && g.BranchId == branchId, ct);
            if (newGroup == null)
            {
                throw new NotFoundException("Không tìm thấy nhóm máy mới trong chi nhánh này.", "machine_group_not_found");
            }
            machine.MachineGroupId = request.MachineGroupId;
            machine.MachineGroup = newGroup;
        }

        machine.DisplayName = request.DisplayName.Trim();
        machine.IpAddress = request.IpAddress?.Trim();
        machine.MacAddress = request.MacAddress?.Trim();
        machine.Notes = request.Notes?.Trim();
        machine.IsActive = request.IsActive;
        machine.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return await GetMachineByIdAsync(id, ct);
    }

    public async Task<MachineDto> UpdateMachineStatusAsync(Guid id, UpdateMachineStatusRequest request, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var machine = await _db.Machines
            .Include(m => m.MachineGroup)
            .Include(m => m.PlaySessions.Where(s => s.Status == PlaySessionStatus.Open || s.Status == PlaySessionStatus.Paused))
            .FirstOrDefaultAsync(m => m.Id == id && m.BranchId == branchId, ct);

        if (machine == null)
        {
            throw new NotFoundException("Không tìm thấy máy trạm.", "machine_not_found");
        }

        // Quy tắc chuyển trạng thái hợp lệ theo DATABASE_DESIGN.md
        // Available -> InUse -> Available; Available <-> Maintenance; Available <-> Offline
        // Không chuyển InUse -> Maintenance trước khi kết phiên.
        if (machine.Status == MachineStatus.InUse && request.Status == MachineStatus.Maintenance)
        {
            throw new BusinessRuleException("Không thể chuyển máy đang có phiên chơi sang bảo trì. Hãy kết thúc phiên trước.", "machine_in_use");
        }

        if (machine.Status == MachineStatus.InUse && request.Status == MachineStatus.Offline)
        {
            throw new BusinessRuleException("Không thể chuyển máy đang có phiên chơi sang ngoại tuyến.", "machine_in_use");
        }

        if (request.Status == MachineStatus.InUse && machine.Status != MachineStatus.InUse)
        {
            // Chỉ mở phiên mới được chuyển sang InUse
            if (!machine.PlaySessions.Any())
            {
                throw new BusinessRuleException("Chỉ có thể chuyển sang Đang sử dụng khi mở phiên chơi.", "cannot_set_in_use");
            }
        }

        machine.Status = request.Status;
        machine.UpdatedAtUtc = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            machine.Notes = request.Note.Trim();
        }

        await _db.SaveChangesAsync(ct);
        return await GetMachineByIdAsync(id, ct);
    }
}
