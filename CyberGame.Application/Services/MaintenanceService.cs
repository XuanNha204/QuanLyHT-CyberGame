using CyberGame.Application.Common;
using CyberGame.Application.DTOs;
using CyberGame.Domain.Entities;
using CyberGame.Domain.Enums;
using CyberGame.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CyberGame.Application.Services;

public interface IMaintenanceService
{
    Task<IReadOnlyList<MaintenanceTicketDto>> GetTicketsAsync(MaintenanceStatus? status = null, Guid? machineId = null, CancellationToken ct = default);
    Task<MaintenanceTicketDto> GetTicketByIdAsync(Guid id, CancellationToken ct = default);
    Task<MaintenanceTicketDto> CreateTicketAsync(CreateMaintenanceTicketRequest request, CancellationToken ct = default);
    Task<MaintenanceTicketDto> AddLogAsync(Guid ticketId, AddMaintenanceLogRequest request, CancellationToken ct = default);
}

public class MaintenanceService : IMaintenanceService
{
    private readonly ICyberGameDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public MaintenanceService(ICyberGameDbContext db, ICurrentUserService currentUser)
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

    public async Task<IReadOnlyList<MaintenanceTicketDto>> GetTicketsAsync(MaintenanceStatus? status = null, Guid? machineId = null, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var query = _db.MaintenanceTickets
            .AsNoTracking()
            .Include(t => t.Machine)
            .Include(t => t.AssignedToUser)
            .Include(t => t.Logs).ThenInclude(l => l.User)
            .Where(t => t.BranchId == branchId)
            .AsQueryable();

        if (status.HasValue) query = query.Where(t => t.Status == status.Value);
        if (machineId.HasValue) query = query.Where(t => t.MachineId == machineId.Value);

        var tickets = await query.OrderByDescending(t => t.OpenedAtUtc).ToListAsync(ct);
        return tickets.Select(MapToDto).ToList();
    }

    public async Task<MaintenanceTicketDto> GetTicketByIdAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var ticket = await _db.MaintenanceTickets
            .AsNoTracking()
            .Include(t => t.Machine)
            .Include(t => t.AssignedToUser)
            .Include(t => t.Logs).ThenInclude(l => l.User)
            .FirstOrDefaultAsync(t => t.Id == id && t.BranchId == branchId, ct);

        if (ticket == null) throw new NotFoundException("Không tìm thấy phiếu bảo trì.", "ticket_not_found");
        return MapToDto(ticket);
    }

    public async Task<MaintenanceTicketDto> CreateTicketAsync(CreateMaintenanceTicketRequest request, CancellationToken ct = default)
    {
        var currentUserId = _currentUser.UserId ?? throw new DomainException("Chưa đăng nhập.", "unauthenticated");
        var branchId = await GetBranchIdAsync(ct);

        var machine = await _db.Machines
            .Include(m => m.PlaySessions.Where(s => s.Status == PlaySessionStatus.Open || s.Status == PlaySessionStatus.Paused))
            .FirstOrDefaultAsync(m => m.Id == request.MachineId && m.BranchId == branchId, ct);

        if (machine == null) throw new NotFoundException("Không tìm thấy máy trạm trong chi nhánh này.", "machine_not_found");
        if (machine.PlaySessions.Any())
        {
            throw new BusinessRuleException("Máy đang có phiên chơi hoạt động. Vui lòng kết thúc phiên trước khi tạo phiếu bảo trì.", "machine_in_use");
        }

        var now = DateTime.UtcNow;
        var ticket = new MaintenanceTicket
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            MachineId = machine.Id,
            TicketNumber = $"MNT-{now:yyyyMMddHHmmss}",
            Priority = request.Priority,
            Status = MaintenanceStatus.Open,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            AssignedToUserId = currentUserId,
            OpenedAtUtc = now,
            Cost = 0m,
            CreatedByUserId = currentUserId
        };

        // Chuyển máy sang trạng thái Maintenance
        machine.Status = MachineStatus.Maintenance;
        machine.UpdatedAtUtc = now;

        var initialLog = new MaintenanceLog
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            StatusFrom = null,
            StatusTo = "Open",
            Note = "Tạo phiếu bảo trì",
            CostDelta = 0m,
            OccurredAtUtc = now,
            UserId = currentUserId
        };

        ticket.Logs.Add(initialLog);
        _db.MaintenanceTickets.Add(ticket);
        await _db.SaveChangesAsync(ct);

        return await GetTicketByIdAsync(ticket.Id, ct);
    }

    public async Task<MaintenanceTicketDto> AddLogAsync(Guid ticketId, AddMaintenanceLogRequest request, CancellationToken ct = default)
    {
        var currentUserId = _currentUser.UserId ?? throw new DomainException("Chưa đăng nhập.", "unauthenticated");
        var branchId = await GetBranchIdAsync(ct);

        var ticket = await _db.MaintenanceTickets
            .Include(t => t.Machine)
            .Include(t => t.Logs)
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.BranchId == branchId, ct);

        if (ticket == null) throw new NotFoundException("Không tìm thấy phiếu bảo trì.", "ticket_not_found");

        var now = DateTime.UtcNow;
        string? oldStatus = ticket.Status.ToString();
        string? newStatus = request.StatusTo;

        if (!string.IsNullOrWhiteSpace(newStatus) && Enum.TryParse<MaintenanceStatus>(newStatus, true, out var parsedStatus))
        {
            ticket.Status = parsedStatus;
            if (parsedStatus == MaintenanceStatus.Resolved)
            {
                ticket.ResolvedAtUtc = now;
            }
            else if (parsedStatus == MaintenanceStatus.Closed)
            {
                ticket.ClosedAtUtc = now;
                // Khi đóng phiếu, đưa máy trở về Available
                ticket.Machine.Status = MachineStatus.Available;
                ticket.Machine.UpdatedAtUtc = now;
            }
            else if (parsedStatus == MaintenanceStatus.InProgress)
            {
                ticket.Machine.Status = MachineStatus.Maintenance;
                ticket.Machine.UpdatedAtUtc = now;
            }
        }

        if (request.CostDelta != 0)
        {
            ticket.Cost += request.CostDelta;
        }

        var log = new MaintenanceLog
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            StatusFrom = oldStatus,
            StatusTo = ticket.Status.ToString(),
            Note = request.Note.Trim(),
            CostDelta = request.CostDelta,
            OccurredAtUtc = now,
            UserId = currentUserId
        };

        ticket.Logs.Add(log);
        ticket.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(ct);
        return await GetTicketByIdAsync(ticket.Id, ct);
    }

    private static MaintenanceTicketDto MapToDto(MaintenanceTicket t)
    {
        return new MaintenanceTicketDto(
            t.Id,
            t.BranchId,
            t.MachineId,
            t.Machine?.Code ?? string.Empty,
            t.TicketNumber,
            t.Priority,
            t.Status,
            t.Title,
            t.Description,
            t.AssignedToUser?.FullName,
            t.OpenedAtUtc,
            t.ResolvedAtUtc,
            t.ClosedAtUtc,
            t.Resolution,
            t.Cost,
            t.Logs.OrderBy(l => l.OccurredAtUtc).Select(l => new MaintenanceLogDto(
                l.Id,
                l.StatusFrom,
                l.StatusTo,
                l.Note,
                l.CostDelta,
                l.OccurredAtUtc,
                l.User?.FullName ?? "N/A"
            )).ToList()
        );
    }
}
