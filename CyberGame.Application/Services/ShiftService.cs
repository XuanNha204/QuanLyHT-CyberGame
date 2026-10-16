using CyberGame.Application.Common;
using CyberGame.Application.DTOs;
using CyberGame.Domain.Entities;
using CyberGame.Domain.Enums;
using CyberGame.Domain.Exceptions;
using CyberGame.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace CyberGame.Application.Services;

public interface IShiftService
{
    Task<ShiftDto?> GetCurrentShiftAsync(CancellationToken ct = default);
    Task<ShiftDto> OpenShiftAsync(OpenShiftRequest request, CancellationToken ct = default);
    Task<ShiftDto> CloseShiftAsync(Guid id, CloseShiftRequest request, CancellationToken ct = default);
    Task<ShiftSummaryDto> GetShiftSummaryAsync(Guid id, CancellationToken ct = default);
    Task<CashMovementDto> AddCashMovementAsync(Guid shiftId, CreateCashMovementRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<CashMovementDto>> GetCashMovementsAsync(Guid shiftId, CancellationToken ct = default);
}

public class ShiftService : IShiftService
{
    private readonly ICyberGameDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ShiftService(ICyberGameDbContext db, ICurrentUserService currentUser)
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

    public async Task<ShiftDto?> GetCurrentShiftAsync(CancellationToken ct = default)
    {
        var currentUserId = _currentUser.UserId;
        if (!currentUserId.HasValue) return null;
        var branchId = await GetBranchIdAsync(ct);

        var shift = await _db.Shifts
            .AsNoTracking()
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.UserId == currentUserId.Value && s.BranchId == branchId && s.Status == ShiftStatus.Open, ct);

        return shift != null ? MapToDto(shift) : null;
    }

    public async Task<ShiftDto> OpenShiftAsync(OpenShiftRequest request, CancellationToken ct = default)
    {
        if (request.OpeningCash < 0)
        {
            throw new BusinessRuleException("Số tiền đầu ca không thể âm.", "invalid_opening_cash");
        }

        var currentUserId = _currentUser.UserId ?? throw new DomainException("Chưa đăng nhập.", "unauthenticated");
        var branchId = await GetBranchIdAsync(ct);

        // Mỗi thu ngân chỉ có tối đa một ca Open tại một chi nhánh
        var existingOpen = await _db.Shifts.FirstOrDefaultAsync(s => s.UserId == currentUserId && s.BranchId == branchId && s.Status == ShiftStatus.Open, ct);
        if (existingOpen != null)
        {
            throw new ConflictException($"Bạn đang có ca làm việc mở ({existingOpen.ShiftNumber}). Vui lòng kết thúc ca hiện tại trước khi mở ca mới.", "shift_already_open");
        }

        var now = DateTime.UtcNow;
        var shift = new Shift
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            UserId = currentUserId,
            ShiftNumber = $"SHF-{now:yyyyMMddHHmmss}",
            Status = ShiftStatus.Open,
            OpenedAtUtc = now,
            OpeningCash = request.OpeningCash,
            ExpectedCash = request.OpeningCash
        };

        // Ghi cash movement đầu ca
        var openingMovement = new CashMovement
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            Type = CashMovementType.Opening,
            Amount = request.OpeningCash,
            ReferenceType = "ShiftOpening",
            ReferenceId = shift.Id,
            Description = "Tiền mặt đầu ca",
            OccurredAtUtc = now,
            CreatedByUserId = currentUserId
        };

        shift.CashMovements.Add(openingMovement);
        _db.Shifts.Add(shift);
        await _db.SaveChangesAsync(ct);

        return await GetShiftDtoByIdAsync(shift.Id, ct);
    }

    public async Task<ShiftDto> CloseShiftAsync(Guid id, CloseShiftRequest request, CancellationToken ct = default)
    {
        if (request.ActualCash < 0)
        {
            throw new BusinessRuleException("Tiền thực đếm không thể âm.", "invalid_actual_cash");
        }

        var branchId = await GetBranchIdAsync(ct);
        var shift = await _db.Shifts
            .Include(s => s.User)
            .Include(s => s.CashMovements)
            .Include(s => s.Expenses)
            .FirstOrDefaultAsync(s => s.Id == id && s.BranchId == branchId, ct);

        if (shift == null) throw new NotFoundException("Không tìm thấy ca làm việc.", "shift_not_found");
        if (shift.Status == ShiftStatus.Closed)
        {
            throw new BusinessRuleException("Ca làm việc này đã được đóng.", "shift_already_closed");
        }

        var now = DateTime.UtcNow;

        // Tính tiền kỳ vọng:
        // Expected = OpeningCash + Thu tiền mặt bán hàng + Thu tiền mặt nạp ví + Thu tiền mặt khác - Chi tiền mặt hoàn tiền - Chi tiền mặt chi phí - Chi tiền mặt khác
        decimal cashSales = shift.CashMovements.Where(c => c.Type == CashMovementType.Sale).Sum(c => c.Amount);
        decimal cashTopUps = shift.CashMovements.Where(c => c.Type == CashMovementType.TopUp).Sum(c => c.Amount);
        decimal cashIn = shift.CashMovements.Where(c => c.Type == CashMovementType.CashIn).Sum(c => c.Amount);
        decimal cashRefunds = shift.CashMovements.Where(c => c.Type == CashMovementType.Refund).Sum(c => c.Amount);
        decimal cashExpenses = shift.Expenses.Where(e => e.Status == ExpenseStatus.Approved).Sum(e => e.Amount)
            + shift.CashMovements.Where(c => c.Type == CashMovementType.Expense).Sum(c => c.Amount);
        decimal cashOut = shift.CashMovements.Where(c => c.Type == CashMovementType.CashOut).Sum(c => c.Amount);

        decimal expectedCash = ShiftCashCalculator.CalculateExpectedCash(
            shift.OpeningCash,
            cashSales,
            cashTopUps,
            cashIn,
            cashRefunds,
            cashExpenses,
            cashOut
        );

        decimal difference = ShiftCashCalculator.CalculateDifference(request.ActualCash, expectedCash);

        shift.Status = ShiftStatus.Closed;
        shift.ClosedAtUtc = now;
        shift.ExpectedCash = expectedCash;
        shift.ActualCash = request.ActualCash;
        shift.Difference = difference;
        shift.ClosingNote = request.ClosingNote?.Trim();

        await _db.SaveChangesAsync(ct);
        return MapToDto(shift);
    }

    public async Task<ShiftSummaryDto> GetShiftSummaryAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var shift = await _db.Shifts
            .AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.CashMovements)
            .Include(s => s.Orders)
            .Include(s => s.PlaySessions)
            .Include(s => s.Payments)
            .Include(s => s.Expenses)
            .FirstOrDefaultAsync(s => s.Id == id && s.BranchId == branchId, ct);

        if (shift == null) throw new NotFoundException("Không tìm thấy ca làm việc.", "shift_not_found");

        decimal cashSales = shift.CashMovements.Where(c => c.Type == CashMovementType.Sale).Sum(c => c.Amount);
        decimal cashTopUps = shift.CashMovements.Where(c => c.Type == CashMovementType.TopUp).Sum(c => c.Amount);
        decimal cashIn = shift.CashMovements.Where(c => c.Type == CashMovementType.CashIn).Sum(c => c.Amount);
        decimal cashRefunds = shift.CashMovements.Where(c => c.Type == CashMovementType.Refund).Sum(c => c.Amount);
        decimal cashExpenses = shift.Expenses.Where(e => e.Status == ExpenseStatus.Approved).Sum(e => e.Amount)
            + shift.CashMovements.Where(c => c.Type == CashMovementType.Expense).Sum(c => c.Amount);
        decimal cashOut = shift.CashMovements.Where(c => c.Type == CashMovementType.CashOut).Sum(c => c.Amount);
        decimal otherPayments = shift.Payments.Where(p => p.Method != PaymentMethod.Cash && p.Status == PaymentStatus.Completed).Sum(p => p.Amount);

        decimal expectedCash = ShiftCashCalculator.CalculateExpectedCash(
            shift.OpeningCash,
            cashSales,
            cashTopUps,
            cashIn,
            cashRefunds,
            cashExpenses,
            cashOut
        );

        return new ShiftSummaryDto(
            MapToDto(shift),
            cashSales,
            cashTopUps,
            cashIn,
            cashRefunds,
            cashExpenses,
            cashOut,
            otherPayments,
            expectedCash,
            shift.Orders.Count,
            shift.PlaySessions.Count
        );
    }

    public async Task<CashMovementDto> AddCashMovementAsync(Guid shiftId, CreateCashMovementRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0) throw new BusinessRuleException("Số tiền thu/chi quỹ phải lớn hơn 0.", "invalid_amount");
        var currentUserId = _currentUser.UserId ?? throw new DomainException("Chưa đăng nhập.", "unauthenticated");

        var branchId = await GetBranchIdAsync(ct);
        var shift = await _db.Shifts.FirstOrDefaultAsync(s => s.Id == shiftId && s.BranchId == branchId, ct);
        if (shift == null) throw new NotFoundException("Không tìm thấy ca làm việc.", "shift_not_found");
        if (shift.Status != ShiftStatus.Open) throw new BusinessRuleException("Ca làm việc đã đóng.", "shift_closed");

        var movement = new CashMovement
        {
            Id = Guid.NewGuid(),
            ShiftId = shiftId,
            Type = request.Type,
            Amount = request.Amount,
            Description = request.Description.Trim(),
            OccurredAtUtc = DateTime.UtcNow,
            CreatedByUserId = currentUserId
        };

        _db.CashMovements.Add(movement);
        await _db.SaveChangesAsync(ct);

        return new CashMovementDto(movement.Id, movement.Type, movement.Amount, movement.Description, movement.OccurredAtUtc, _currentUser.Username ?? "System");
    }

    public async Task<IReadOnlyList<CashMovementDto>> GetCashMovementsAsync(Guid shiftId, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var shiftExists = await _db.Shifts.AnyAsync(s => s.Id == shiftId && s.BranchId == branchId, ct);
        if (!shiftExists) throw new NotFoundException("Không tìm thấy ca làm việc.", "shift_not_found");

        var movements = await _db.CashMovements
            .AsNoTracking()
            .Include(c => c.CreatedByUser)
            .Where(c => c.ShiftId == shiftId)
            .OrderByDescending(c => c.OccurredAtUtc)
            .Select(c => new CashMovementDto(
                c.Id,
                c.Type,
                c.Amount,
                c.Description,
                c.OccurredAtUtc,
                c.CreatedByUser.FullName
            ))
            .ToListAsync(ct);

        return movements;
    }

    private async Task<ShiftDto> GetShiftDtoByIdAsync(Guid id, CancellationToken ct)
    {
        var branchId = await GetBranchIdAsync(ct);
        var shift = await _db.Shifts.AsNoTracking().Include(s => s.User).FirstOrDefaultAsync(s => s.Id == id && s.BranchId == branchId, ct);
        if (shift == null) throw new NotFoundException("Không tìm thấy ca.", "shift_not_found");
        return MapToDto(shift);
    }

    private static ShiftDto MapToDto(Shift s)
    {
        return new ShiftDto(
            s.Id,
            s.BranchId,
            s.UserId,
            s.User?.FullName ?? s.User?.Username ?? "N/A",
            s.ShiftNumber,
            s.Status,
            s.OpenedAtUtc,
            s.ClosedAtUtc,
            s.OpeningCash,
            s.ExpectedCash,
            s.ActualCash,
            s.Difference,
            s.ClosingNote
        );
    }
}
