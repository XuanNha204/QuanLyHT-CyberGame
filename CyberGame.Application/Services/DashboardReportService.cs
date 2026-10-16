using CyberGame.Application.Common;
using CyberGame.Application.DTOs;
using CyberGame.Domain.Entities;
using CyberGame.Domain.Enums;
using CyberGame.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CyberGame.Application.Services;

public interface IDashboardReportService
{
    Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RevenueReportDto>> GetRevenueReportAsync(DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken ct = default);
    Task<IReadOnlyList<MachineUtilizationDto>> GetMachineUtilizationReportAsync(DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken ct = default);
    Task<IReadOnlyList<TopProductReportDto>> GetTopProductsReportAsync(int limit = 10, CancellationToken ct = default);
    Task<WalletLiabilityReportDto> GetWalletLiabilityReportAsync(CancellationToken ct = default);
}

public class DashboardReportService : IDashboardReportService
{
    private readonly ICyberGameDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBranchMachineService _branchMachineService;
    private readonly IShiftService _shiftService;

    public DashboardReportService(
        ICyberGameDbContext db,
        ICurrentUserService currentUser,
        IBranchMachineService branchMachineService,
        IShiftService shiftService)
    {
        _db = db;
        _currentUser = currentUser;
        _branchMachineService = branchMachineService;
        _shiftService = shiftService;
    }

    private async Task<Guid> GetBranchIdAsync(CancellationToken ct)
    {
        if (_currentUser.BranchId.HasValue) return _currentUser.BranchId.Value;
        var b = await _db.Branches.FirstOrDefaultAsync(x => x.IsActive, ct);
        return b?.Id ?? throw new NotFoundException("Không tìm thấy chi nhánh.", "branch_not_found");
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var machines = await _branchMachineService.GetMachinesAsync(null, null, ct);

        int totalMachines = machines.Count;
        int available = machines.Count(m => m.Status == MachineStatus.Available);
        int inUse = machines.Count(m => m.Status == MachineStatus.InUse);
        int maintenance = machines.Count(m => m.Status == MachineStatus.Maintenance);
        int offline = machines.Count(m => m.Status == MachineStatus.Offline);

        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        // Doanh thu hôm nay = Các hóa đơn đã thanh toán hoặc tiền thanh toán phát sinh hôm nay
        decimal todayRevenue = await _db.Payments
            .AsNoTracking()
            .Where(p => p.BranchId == branchId && p.PaidAtUtc >= today && p.PaidAtUtc < tomorrow && p.Status == PaymentStatus.Completed)
            .SumAsync(p => p.Amount, ct);

        int todayOrders = await _db.Orders
            .AsNoTracking()
            .Where(o => o.BranchId == branchId && o.CreatedAtUtc >= today && o.CreatedAtUtc < tomorrow && o.Status != OrderStatus.Cancelled)
            .CountAsync(ct);

        int activeSessions = await _db.PlaySessions
            .AsNoTracking()
            .Where(s => s.BranchId == branchId && (s.Status == PlaySessionStatus.Open || s.Status == PlaySessionStatus.Paused))
            .CountAsync(ct);

        int lowStock = await _db.Products
            .AsNoTracking()
            .Include(p => p.Balance)
            .Where(p => p.BranchId == branchId && p.IsActive && p.TrackInventory && p.Balance != null && p.Balance.QuantityOnHand <= p.ReorderLevel)
            .CountAsync(ct);

        var currentShift = await _shiftService.GetCurrentShiftAsync(ct);

        return new DashboardSummaryDto(
            totalMachines,
            available,
            inUse,
            maintenance,
            offline,
            todayRevenue,
            todayOrders,
            activeSessions,
            lowStock,
            currentShift,
            machines
        );
    }

    public async Task<IReadOnlyList<RevenueReportDto>> GetRevenueReportAsync(DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var end = (toUtc ?? DateTime.UtcNow).Date.AddDays(1);
        var start = (fromUtc ?? DateTime.UtcNow.AddDays(-7)).Date;

        var invoices = await _db.Invoices
            .AsNoTracking()
            .Include(i => i.Lines)
            .Where(i => i.BranchId == branchId && i.IssuedAtUtc >= start && i.IssuedAtUtc < end && i.Status != InvoiceStatus.Voided)
            .ToListAsync(ct);

        var orders = await _db.Orders
            .AsNoTracking()
            .Where(o => o.BranchId == branchId && o.CreatedAtUtc >= start && o.CreatedAtUtc < end && o.Status != OrderStatus.Cancelled)
            .ToListAsync(ct);

        var sessions = await _db.PlaySessions
            .AsNoTracking()
            .Where(s => s.BranchId == branchId && s.StartedAtUtc >= start && s.StartedAtUtc < end && s.Status != PlaySessionStatus.Cancelled)
            .ToListAsync(ct);

        var result = new List<RevenueReportDto>();
        for (var d = start; d < end; d = d.AddDays(1))
        {
            var nextD = d.AddDays(1);
            var dayInvoices = invoices.Where(i => i.IssuedAtUtc >= d && i.IssuedAtUtc < nextD).ToList();
            var dayOrders = orders.Where(o => o.CreatedAtUtc >= d && o.CreatedAtUtc < nextD).ToList();
            var daySessions = sessions.Where(s => s.StartedAtUtc >= d && s.StartedAtUtc < nextD).ToList();

            decimal machineRev = dayInvoices.SelectMany(i => i.Lines).Where(l => l.LineType == InvoiceLineType.ComputerUsage).Sum(l => l.LineTotal);
            decimal serviceRev = dayInvoices.SelectMany(i => i.Lines).Where(l => l.LineType == InvoiceLineType.Product).Sum(l => l.LineTotal);
            decimal totalRev = dayInvoices.Sum(i => i.TotalAmount);

            result.Add(new RevenueReportDto(
                DateOnly.FromDateTime(d),
                machineRev,
                serviceRev,
                totalRev,
                dayOrders.Count,
                daySessions.Count
            ));
        }

        return result;
    }

    public async Task<IReadOnlyList<MachineUtilizationDto>> GetMachineUtilizationReportAsync(DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var end = (toUtc ?? DateTime.UtcNow).Date.AddDays(1);
        var start = (fromUtc ?? DateTime.UtcNow.AddDays(-30)).Date;

        var machines = await _db.Machines
            .AsNoTracking()
            .Include(m => m.MachineGroup)
            .Include(m => m.PlaySessions.Where(s => s.StartedAtUtc >= start && s.StartedAtUtc < end && s.Status == PlaySessionStatus.Completed))
                .ThenInclude(s => s.ChargeSegments)
            .Where(m => m.BranchId == branchId && m.IsActive)
            .ToListAsync(ct);

        return machines.Select(m =>
        {
            int sessionCount = m.PlaySessions.Count;
            int totalSeconds = m.PlaySessions.SelectMany(s => s.ChargeSegments).Sum(c => c.BillableSeconds);
            decimal totalHours = Math.Round(totalSeconds / 3600m, 2);
            decimal totalRev = m.PlaySessions.Sum(s => s.UsageAmount);

            return new MachineUtilizationDto(
                m.Id,
                m.Code,
                m.DisplayName,
                m.MachineGroup.Name,
                sessionCount,
                totalHours,
                totalRev
            );
        }).OrderByDescending(m => m.TotalRevenue).ToList();
    }

    public async Task<IReadOnlyList<TopProductReportDto>> GetTopProductsReportAsync(int limit = 10, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var orderItems = await _db.OrderItems
            .AsNoTracking()
            .Include(i => i.Product).ThenInclude(p => p!.Category)
            .Include(i => i.Order)
            .Where(i => i.Order.BranchId == branchId && i.ProductId.HasValue && i.Order.Status != OrderStatus.Cancelled)
            .ToListAsync(ct);

        var grouped = orderItems
            .GroupBy(i => i.ProductId!.Value)
            .Select(g =>
            {
                var first = g.First();
                return new TopProductReportDto(
                    g.Key,
                    first.Sku,
                    first.ProductName,
                    first.Product?.Category?.Name ?? "Chung",
                    g.Sum(x => x.Quantity),
                    g.Sum(x => x.LineTotal)
                );
            })
            .OrderByDescending(x => x.QuantitySold)
            .Take(limit)
            .ToList();

        return grouped;
    }

    public async Task<WalletLiabilityReportDto> GetWalletLiabilityReportAsync(CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var walletsWithBalance = await _db.Wallets
            .AsNoTracking()
            .Include(w => w.Customer)
            .Where(w => w.Customer.HomeBranchId == branchId && w.Balance > 0)
            .ToListAsync(ct);

        return new WalletLiabilityReportDto(
            walletsWithBalance.Count,
            walletsWithBalance.Sum(w => w.Balance)
        );
    }
}
