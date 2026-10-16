using CyberGame.Application.Common;
using CyberGame.Application.DTOs;
using CyberGame.Domain.Entities;
using CyberGame.Domain.Enums;
using CyberGame.Domain.Exceptions;
using CyberGame.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace CyberGame.Application.Services;

public interface ISessionService
{
    Task<IReadOnlyList<PlaySessionDto>> GetActiveSessionsAsync(CancellationToken ct = default);
    Task<PagedResult<PlaySessionDto>> GetSessionsAsync(Guid? machineId = null, Guid? customerId = null, DateTime? date = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<PlaySessionDto> GetSessionByIdAsync(Guid id, CancellationToken ct = default);
    Task<PlaySessionDto> StartSessionAsync(StartSessionRequest request, CancellationToken ct = default);
    Task<PlaySessionDto> PauseSessionAsync(Guid id, CancellationToken ct = default);
    Task<PlaySessionDto> ResumeSessionAsync(Guid id, CancellationToken ct = default);
    Task<PlaySessionDto> TransferSessionAsync(Guid id, TransferSessionRequest request, CancellationToken ct = default);
    Task<InvoiceDto> CompleteSessionAsync(Guid id, CompleteSessionRequest request, CancellationToken ct = default);
    Task<PlaySessionDto> CancelSessionAsync(Guid id, CancelSessionRequest request, CancellationToken ct = default);
    Task<SessionQuoteDto> GetSessionQuoteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SessionEventDto>> GetSessionEventsAsync(Guid id, CancellationToken ct = default);
}

public class SessionService : ISessionService
{
    private readonly ICyberGameDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPricingService _pricingService;

    public SessionService(
        ICyberGameDbContext db,
        ICurrentUserService currentUser,
        IPricingService pricingService)
    {
        _db = db;
        _currentUser = currentUser;
        _pricingService = pricingService;
    }

    private async Task<Guid> GetBranchIdAsync(CancellationToken ct)
    {
        if (_currentUser.BranchId.HasValue) return _currentUser.BranchId.Value;
        var b = await _db.Branches.FirstOrDefaultAsync(x => x.IsActive, ct);
        return b?.Id ?? throw new NotFoundException("Không tìm thấy chi nhánh.", "branch_not_found");
    }

    public async Task<IReadOnlyList<PlaySessionDto>> GetActiveSessionsAsync(CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var sessions = await _db.PlaySessions
            .AsNoTracking()
            .Include(s => s.Machine)
            .Include(s => s.Customer)
            .Include(s => s.ChargeSegments)
            .Include(s => s.Orders)
            .Where(s => s.BranchId == branchId && (s.Status == PlaySessionStatus.Open || s.Status == PlaySessionStatus.Paused))
            .OrderBy(s => s.StartedAtUtc)
            .ToListAsync(ct);

        return sessions.Select(MapToDto).ToList();
    }

    public async Task<PagedResult<PlaySessionDto>> GetSessionsAsync(Guid? machineId = null, Guid? customerId = null, DateTime? date = null, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var query = _db.PlaySessions
            .AsNoTracking()
            .Include(s => s.Machine)
            .Include(s => s.Customer)
            .Include(s => s.ChargeSegments)
            .Include(s => s.Orders)
            .Where(s => s.BranchId == branchId)
            .AsQueryable();

        if (machineId.HasValue) query = query.Where(s => s.MachineId == machineId.Value);
        if (customerId.HasValue) query = query.Where(s => s.CustomerId == customerId.Value);
        if (date.HasValue)
        {
            var dayStart = date.Value.Date;
            var dayEnd = dayStart.AddDays(1);
            query = query.Where(s => s.StartedAtUtc >= dayStart && s.StartedAtUtc < dayEnd);
        }

        int total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(s => s.StartedAtUtc)
                               .Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .ToListAsync(ct);

        var dtos = items.Select(MapToDto).ToList();
        return new PagedResult<PlaySessionDto>(dtos, total, page, pageSize);
    }

    public async Task<PlaySessionDto> GetSessionByIdAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var session = await _db.PlaySessions
            .AsNoTracking()
            .Include(s => s.Machine)
            .Include(s => s.Customer)
            .Include(s => s.ChargeSegments)
            .Include(s => s.Orders)
            .FirstOrDefaultAsync(s => s.Id == id && s.BranchId == branchId, ct);

        if (session == null) throw new NotFoundException("Không tìm thấy phiên chơi.", "session_not_found");
        return MapToDto(session);
    }

    public async Task<PlaySessionDto> StartSessionAsync(StartSessionRequest request, CancellationToken ct = default)
    {
        var currentUserId = _currentUser.UserId ?? throw new DomainException("Chưa xác thực người dùng.", "unauthenticated");
        var branchId = await GetBranchIdAsync(ct);

        // 1. Kiểm tra ca làm việc mở
        var openShift = await _db.Shifts.FirstOrDefaultAsync(s => s.UserId == currentUserId && s.BranchId == branchId && s.Status == ShiftStatus.Open, ct);
        if (openShift == null)
        {
            throw new BusinessRuleException("Thu ngân phải mở ca làm việc trước khi mở phiên chơi.", "shift_required");
        }

        // 2. Kiểm tra máy
        var machine = await _db.Machines.Include(m => m.MachineGroup).FirstOrDefaultAsync(m => m.Id == request.MachineId && m.BranchId == branchId, ct);
        if (machine == null) throw new NotFoundException("Không tìm thấy máy trạm.", "machine_not_found");
        if (machine.Status != MachineStatus.Available)
        {
            throw new BusinessRuleException($"Máy {machine.Code} không ở trạng thái sẵn sàng (Hiện tại: {machine.Status}).", "machine_not_available");
        }

        // 3. Kiểm tra khách hàng & ví
        Customer? customer = null;
        if (request.CustomerId.HasValue)
        {
            customer = await _db.Customers.Include(c => c.Wallet).FirstOrDefaultAsync(c => c.Id == request.CustomerId.Value && c.HomeBranchId == branchId, ct);
            if (customer == null) throw new NotFoundException("Không tìm thấy khách hàng.", "customer_not_found");
            if (customer.Status != CustomerStatus.Active) throw new BusinessRuleException("Tài khoản hội viên đang bị khóa hoặc ngưng hoạt động.", "customer_inactive");

            if (request.BillingMode == BillingMode.PrepaidWallet)
            {
                if (customer.Wallet == null || customer.Wallet.Balance <= 0)
                {
                    throw new BusinessRuleException("Số dư ví của hội viên không đủ để mở phiên trả trước.", "insufficient_wallet_balance");
                }
            }
        }
        else
        {
            if (request.BillingMode == BillingMode.PrepaidWallet)
            {
                throw new BusinessRuleException("Khách vãng lai không thể sử dụng hình thức ví trả trước. Vui lòng chọn Trả sau hoặc tạo tài khoản hội viên.", "prepaid_requires_customer");
            }
        }

        // 4. Lấy đơn giá giờ chơi
        var now = DateTime.UtcNow;
        var hourlyRate = await _pricingService.GetEffectiveRatePerHourAsync(machine.BranchId, machine.MachineGroupId, now, ct);

        // 5. Tạo session & segment
        var session = new PlaySession
        {
            Id = Guid.NewGuid(),
            BranchId = machine.BranchId,
            MachineId = machine.Id,
            CustomerId = customer?.Id,
            ShiftId = openShift.Id,
            SessionNumber = $"SES-{now:yyyyMMddHHmmssfff}",
            BillingMode = request.BillingMode,
            Status = PlaySessionStatus.Open,
            StartedAtUtc = now,
            TotalPausedSeconds = 0,
            UsageAmount = 0m,
            ServiceAmount = 0m,
            TotalAmount = 0m,
            CreatedAtUtc = now
        };

        var initialSegment = new SessionChargeSegment
        {
            Id = Guid.NewGuid(),
            PlaySessionId = session.Id,
            StartedAtUtc = now,
            RatePerHour = hourlyRate,
            BillableSeconds = 0,
            Amount = 0m
        };

        var sessionEvent = new SessionEvent
        {
            Id = Guid.NewGuid(),
            PlaySessionId = session.Id,
            EventType = "Started",
            OccurredAtUtc = now,
            UserId = currentUserId,
            Note = $"Bắt đầu phiên cho {(customer != null ? customer.FullName : "Khách vãng lai")}"
        };

        machine.Status = MachineStatus.InUse;
        machine.UpdatedAtUtc = now;

        session.ChargeSegments.Add(initialSegment);
        session.Events.Add(sessionEvent);

        _db.PlaySessions.Add(session);
        await _db.SaveChangesAsync(ct);

        return await GetSessionByIdAsync(session.Id, ct);
    }

    public async Task<PlaySessionDto> PauseSessionAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var currentUserId = _currentUser.UserId ?? throw new DomainException("Chưa đăng nhập.", "unauthenticated");
        var session = await _db.PlaySessions
            .Include(s => s.Machine)
            .Include(s => s.ChargeSegments)
            .FirstOrDefaultAsync(s => s.Id == id && s.BranchId == branchId, ct);

        if (session == null) throw new NotFoundException("Không tìm thấy phiên chơi.", "session_not_found");
        if (session.Status != PlaySessionStatus.Open)
        {
            throw new BusinessRuleException("Chỉ có thể tạm dừng phiên đang mở.", "invalid_session_status");
        }

        var now = DateTime.UtcNow;
        session.Status = PlaySessionStatus.Paused;
        session.PausedAtUtc = now;
        session.UpdatedAtUtc = now;

        // Chốt segment hiện tại
        var currentSegment = session.ChargeSegments.OrderByDescending(s => s.StartedAtUtc).FirstOrDefault();
        if (currentSegment != null && currentSegment.EndedAtUtc == null)
        {
            currentSegment.EndedAtUtc = now;
            int segSeconds = (int)(now - currentSegment.StartedAtUtc).TotalSeconds;
            currentSegment.BillableSeconds += Math.Max(0, segSeconds);
            currentSegment.Amount = SessionPricingCalculator.CalculateUsage(currentSegment.RatePerHour, currentSegment.BillableSeconds);
        }

        _db.SessionEvents.Add(new SessionEvent
        {
            Id = Guid.NewGuid(),
            PlaySessionId = session.Id,
            EventType = "Paused",
            OccurredAtUtc = now,
            UserId = currentUserId,
            Note = "Tạm dừng phiên chơi"
        });

        await _db.SaveChangesAsync(ct);
        return await GetSessionByIdAsync(id, ct);
    }

    public async Task<PlaySessionDto> ResumeSessionAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var currentUserId = _currentUser.UserId ?? throw new DomainException("Chưa đăng nhập.", "unauthenticated");
        var session = await _db.PlaySessions
            .Include(s => s.Machine)
            .Include(s => s.ChargeSegments)
            .FirstOrDefaultAsync(s => s.Id == id && s.BranchId == branchId, ct);

        if (session == null) throw new NotFoundException("Không tìm thấy phiên chơi.", "session_not_found");
        if (session.Status != PlaySessionStatus.Paused)
        {
            throw new BusinessRuleException("Chỉ có thể tiếp tục phiên đang tạm dừng.", "invalid_session_status");
        }

        var now = DateTime.UtcNow;
        if (session.PausedAtUtc.HasValue)
        {
            int pausedSecs = (int)(now - session.PausedAtUtc.Value).TotalSeconds;
            session.TotalPausedSeconds += Math.Max(0, pausedSecs);
        }

        session.Status = PlaySessionStatus.Open;
        session.PausedAtUtc = null;
        session.UpdatedAtUtc = now;

        // Lấy lại đơn giá theo thời điểm hiện tại
        var hourlyRate = await _pricingService.GetEffectiveRatePerHourAsync(session.BranchId, session.Machine.MachineGroupId, now, ct);

        // Tạo segment mới tiếp tục
        var newSegment = new SessionChargeSegment
        {
            Id = Guid.NewGuid(),
            PlaySessionId = session.Id,
            StartedAtUtc = now,
            RatePerHour = hourlyRate,
            BillableSeconds = 0,
            Amount = 0m
        };

        _db.SessionChargeSegments.Add(newSegment);

        _db.SessionEvents.Add(new SessionEvent
        {
            Id = Guid.NewGuid(),
            PlaySessionId = session.Id,
            EventType = "Resumed",
            OccurredAtUtc = now,
            UserId = currentUserId,
            Note = "Tiếp tục phiên chơi"
        });

        await _db.SaveChangesAsync(ct);
        return await GetSessionByIdAsync(id, ct);
    }

    public async Task<PlaySessionDto> TransferSessionAsync(Guid id, TransferSessionRequest request, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var currentUserId = _currentUser.UserId ?? throw new DomainException("Chưa đăng nhập.", "unauthenticated");
        var session = await _db.PlaySessions
            .Include(s => s.Machine)
            .Include(s => s.ChargeSegments)
            .FirstOrDefaultAsync(s => s.Id == id && s.BranchId == branchId, ct);

        if (session == null) throw new NotFoundException("Không tìm thấy phiên chơi.", "session_not_found");
        if (session.Status != PlaySessionStatus.Open && session.Status != PlaySessionStatus.Paused)
        {
            throw new BusinessRuleException("Chỉ có thể chuyển máy cho phiên đang hoạt động.", "invalid_session_status");
        }

        var newMachine = await _db.Machines.Include(m => m.MachineGroup).FirstOrDefaultAsync(m => m.Id == request.NewMachineId && m.BranchId == branchId, ct);
        if (newMachine == null) throw new NotFoundException("Không tìm thấy máy trạm mới trong chi nhánh này.", "new_machine_not_found");
        if (newMachine.Status != MachineStatus.Available)
        {
            throw new BusinessRuleException($"Máy mới {newMachine.Code} không khả dụng (Hiện tại: {newMachine.Status}).", "machine_not_available");
        }

        var now = DateTime.UtcNow;

        // 1. Chốt segment cũ trên máy cũ
        var currentSegment = session.ChargeSegments.OrderByDescending(s => s.StartedAtUtc).FirstOrDefault();
        if (currentSegment != null && currentSegment.EndedAtUtc == null)
        {
            currentSegment.EndedAtUtc = now;
            int segSeconds = (int)(now - currentSegment.StartedAtUtc).TotalSeconds;
            currentSegment.BillableSeconds += Math.Max(0, segSeconds);
            currentSegment.Amount = SessionPricingCalculator.CalculateUsage(currentSegment.RatePerHour, currentSegment.BillableSeconds);
        }

        // 2. Chuyển máy cũ về Available, máy mới sang InUse
        var oldMachine = session.Machine;
        oldMachine.Status = MachineStatus.Available;
        oldMachine.UpdatedAtUtc = now;

        newMachine.Status = MachineStatus.InUse;
        newMachine.UpdatedAtUtc = now;

        session.MachineId = newMachine.Id;
        session.Machine = newMachine;
        session.UpdatedAtUtc = now;

        // 3. Tạo segment mới theo đơn giá của máy mới
        var newHourlyRate = await _pricingService.GetEffectiveRatePerHourAsync(newMachine.BranchId, newMachine.MachineGroupId, now, ct);
        var newSegment = new SessionChargeSegment
        {
            Id = Guid.NewGuid(),
            PlaySessionId = session.Id,
            StartedAtUtc = now,
            RatePerHour = newHourlyRate,
            BillableSeconds = 0,
            Amount = 0m
        };
        _db.SessionChargeSegments.Add(newSegment);

        _db.SessionEvents.Add(new SessionEvent
        {
            Id = Guid.NewGuid(),
            PlaySessionId = session.Id,
            EventType = "Transferred",
            OccurredAtUtc = now,
            UserId = currentUserId,
            Note = $"Chuyển phiên từ máy {oldMachine.Code} sang máy {newMachine.Code}"
        });

        await _db.SaveChangesAsync(ct);
        return await GetSessionByIdAsync(id, ct);
    }

    public async Task<InvoiceDto> CompleteSessionAsync(Guid id, CompleteSessionRequest request, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var currentUserId = _currentUser.UserId ?? throw new DomainException("Chưa đăng nhập.", "unauthenticated");
        var session = await _db.PlaySessions
            .Include(s => s.Machine)
            .Include(s => s.Customer)
                .ThenInclude(c => c!.Wallet)
            .Include(s => s.ChargeSegments)
            .Include(s => s.Orders)
                .ThenInclude(o => o.Items)
            .Include(s => s.Orders)
                .ThenInclude(o => o.Payments)
            .FirstOrDefaultAsync(s => s.Id == id && s.BranchId == branchId, ct);

        if (session == null) throw new NotFoundException("Không tìm thấy phiên chơi.", "session_not_found");
        if (session.Status != PlaySessionStatus.Open && session.Status != PlaySessionStatus.Paused)
        {
            throw new BusinessRuleException("Phiên chơi này đã kết thúc hoặc bị hủy.", "session_already_closed");
        }

        if (session.Orders.Any(o => o.Status == OrderStatus.Draft))
        {
            throw new BusinessRuleException(
                "Phiên còn đơn món thanh toán tiền mặt đang chờ thu tiền. Hãy duyệt hoặc từ chối đơn trước khi kết thúc phiên.",
                "pending_orders_exist");
        }

        var now = DateTime.UtcNow;

        // 1. Chốt segment cuối cùng
        var lastSegment = session.ChargeSegments.OrderByDescending(s => s.StartedAtUtc).FirstOrDefault();
        if (lastSegment != null && lastSegment.EndedAtUtc == null)
        {
            lastSegment.EndedAtUtc = now;
            int segSeconds = (int)(now - lastSegment.StartedAtUtc).TotalSeconds;
            lastSegment.BillableSeconds += Math.Max(0, segSeconds);
            lastSegment.Amount = SessionPricingCalculator.CalculateUsage(lastSegment.RatePerHour, lastSegment.BillableSeconds);
        }

        // 2. Tính tổng tiền giờ chơi từ các segments
        decimal totalUsageAmount = session.ChargeSegments.Sum(s => s.Amount);
        session.UsageAmount = totalUsageAmount;

        // 3. Tính tiền dịch vụ từ các đơn hàng gắn với phiên này
        decimal totalServiceAmount = session.Orders
            .Where(o => o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Fulfilled)
            .Sum(o => o.TotalAmount);
        session.ServiceAmount = totalServiceAmount;

        session.TotalAmount = totalUsageAmount + totalServiceAmount;
        session.EndedAtUtc = now;
        session.Status = PlaySessionStatus.Completed;
        session.UpdatedAtUtc = now;

        // Trả trạng thái máy về Available
        session.Machine.Status = MachineStatus.Available;
        session.Machine.UpdatedAtUtc = now;

        _db.SessionEvents.Add(new SessionEvent
        {
            Id = Guid.NewGuid(),
            PlaySessionId = session.Id,
            EventType = "Completed",
            OccurredAtUtc = now,
            UserId = currentUserId,
            Note = $"Kết thúc phiên chơi. Tổng tiền: {session.TotalAmount:N0}đ (Giờ chơi: {session.UsageAmount:N0}đ, Dịch vụ: {session.ServiceAmount:N0}đ)"
        });

        // 4. Tạo Invoice hợp nhất (tiền giờ + tiền dịch vụ)
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            BranchId = session.BranchId,
            InvoiceNumber = $"INV-{now:yyyyMMddHHmmssfff}",
            CustomerId = session.CustomerId,
            PlaySessionId = session.Id,
            ShiftId = session.ShiftId,
            Status = InvoiceStatus.Issued,
            Subtotal = session.TotalAmount,
            DiscountAmount = 0m,
            TotalAmount = session.TotalAmount,
            PaidAmount = 0m,
            ChangeAmount = 0m,
            IssuedAtUtc = now,
            CreatedAtUtc = now
        };

        // Thêm InvoiceLine cho giờ chơi
        int totalBillableSecs = session.ChargeSegments.Sum(s => s.BillableSeconds);
        decimal avgRate = session.ChargeSegments.Any() ? session.ChargeSegments.Average(s => s.RatePerHour) : 0m;
        invoice.Lines.Add(new InvoiceLine
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            LineType = InvoiceLineType.ComputerUsage,
            ReferenceId = session.Id,
            Description = $"Tiền giờ máy {session.Machine.Code} ({totalBillableSecs / 60} phút)",
            Quantity = 1,
            UnitPrice = totalUsageAmount,
            DiscountAmount = 0m,
            LineTotal = totalUsageAmount
        });

        // Thêm InvoiceLine cho các món dịch vụ
        foreach (var order in session.Orders.Where(o => o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Fulfilled))
        {
            foreach (var item in order.Items)
            {
                invoice.Lines.Add(new InvoiceLine
                {
                    Id = Guid.NewGuid(),
                    InvoiceId = invoice.Id,
                    LineType = InvoiceLineType.Product,
                    ReferenceId = item.ProductId,
                    Description = $"{item.ProductName} ({item.Quantity} {item.Unit})",
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    DiscountAmount = item.DiscountAmount,
                    LineTotal = item.LineTotal
                });
            }
        }

        // Các món đã thanh toán riêng (tiền mặt hoặc ví) được gắn vào hóa đơn
        // cuối phiên để tổng doanh thu đầy đủ nhưng không bị thu lại lần thứ hai.
        var completedOrderPayments = session.Orders
            .Where(o => o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Fulfilled)
            .SelectMany(o => o.Payments)
            .Where(p => p.Type == PaymentType.Payment && p.Status == PaymentStatus.Completed)
            .ToList();

        foreach (var orderPayment in completedOrderPayments)
        {
            orderPayment.InvoiceId = invoice.Id;
            invoice.Payments.Add(orderPayment);
        }

        invoice.PaidAmount = Math.Min(invoice.TotalAmount, completedOrderPayments.Sum(p => p.Amount));

        // 5. Nếu là PrepaidWallet, chỉ tự động trừ phần còn thiếu.
        if (session.BillingMode == BillingMode.PrepaidWallet && session.Customer?.Wallet != null)
        {
            var wallet = session.Customer.Wallet;
            decimal remainingAmount = Math.Max(0m, invoice.TotalAmount - invoice.PaidAmount);
            decimal deductAmount = Math.Min(remainingAmount, wallet.Balance);
            if (deductAmount > 0)
            {
                decimal balanceBefore = wallet.Balance;
                decimal balanceAfter = balanceBefore - deductAmount;
                wallet.Balance = balanceAfter;
                wallet.UpdatedAtUtc = now;

                var walletTx = new WalletTransaction
                {
                    Id = Guid.NewGuid(),
                    WalletId = wallet.Id,
                    BranchId = session.BranchId,
                    Type = WalletTransactionType.Usage,
                    Amount = -deductAmount,
                    BalanceBefore = balanceBefore,
                    BalanceAfter = balanceAfter,
                    ReferenceType = "Invoice",
                    ReferenceId = invoice.Id,
                    Description = $"Thanh toán phiên chơi {session.SessionNumber}",
                    OccurredAtUtc = now,
                    ShiftId = session.ShiftId,
                    CreatedByUserId = currentUserId
                };

                var payment = new Payment
                {
                    Id = Guid.NewGuid(),
                    BranchId = session.BranchId,
                    InvoiceId = invoice.Id,
                    CustomerId = session.CustomerId,
                    ShiftId = session.ShiftId,
                    PaymentNumber = $"PAY-{now:yyyyMMddHHmmssfff}",
                    Type = PaymentType.Payment,
                    Method = PaymentMethod.Wallet,
                    Amount = deductAmount,
                    Status = PaymentStatus.Completed,
                    PaidAtUtc = now,
                    Note = "Thanh toán tự động bằng ví trả trước",
                    CreatedByUserId = currentUserId
                };

                invoice.Payments.Add(payment);
                invoice.PaidAmount += deductAmount;
                if (invoice.PaidAmount >= invoice.TotalAmount)
                {
                    invoice.Status = InvoiceStatus.Paid;
                    invoice.PaidAtUtc = now;
                }

                _db.WalletTransactions.Add(walletTx);
            }
        }

        if (invoice.PaidAmount >= invoice.TotalAmount)
        {
            invoice.Status = InvoiceStatus.Paid;
            invoice.PaidAtUtc = now;
        }

        session.InvoiceId = invoice.Id;
        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync(ct);

        return new InvoiceDto(
            invoice.Id,
            invoice.BranchId,
            invoice.InvoiceNumber,
            session.CustomerId,
            session.Customer?.FullName,
            session.Id,
            session.Machine.Code,
            invoice.ShiftId,
            invoice.Status,
            invoice.Subtotal,
            invoice.DiscountAmount,
            invoice.TotalAmount,
            invoice.PaidAmount,
            invoice.ChangeAmount,
            invoice.IssuedAtUtc,
            invoice.PaidAtUtc,
            invoice.Lines.Select(l => new InvoiceLineDto(l.Id, l.LineType, l.Description, l.Quantity, l.UnitPrice, l.DiscountAmount, l.LineTotal)).ToList(),
            invoice.Payments.Select(p => new PaymentDto(p.Id, p.PaymentNumber, p.Type, p.Method, p.Amount, p.Status, p.PaidAtUtc, p.Note, _currentUser.Username ?? "System")).ToList()
        );
    }

    public async Task<PlaySessionDto> CancelSessionAsync(Guid id, CancelSessionRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request?.Reason))
        {
            throw new BusinessRuleException("Lý do hủy phiên là bắt buộc.", "reason_required");
        }

        var branchId = await GetBranchIdAsync(ct);
        var currentUserId = _currentUser.UserId ?? throw new DomainException("Chưa đăng nhập.", "unauthenticated");
        var session = await _db.PlaySessions
            .Include(s => s.Machine)
            .Include(s => s.ChargeSegments)
            .Include(s => s.Orders)
            .FirstOrDefaultAsync(s => s.Id == id && s.BranchId == branchId, ct);

        if (session == null) throw new NotFoundException("Không tìm thấy phiên chơi.", "session_not_found");
        if (session.Status == PlaySessionStatus.Completed || session.Status == PlaySessionStatus.Cancelled)
        {
            throw new BusinessRuleException("Không thể hủy phiên chơi đã đóng.", "invalid_session_status");
        }

        if (session.Orders.Any(o => o.Status != OrderStatus.Cancelled))
        {
            throw new BusinessRuleException("Không thể hủy phiên chơi khi còn đơn dịch vụ chưa hủy.", "session_has_uncancelled_orders");
        }

        var now = DateTime.UtcNow;
        decimal accruedUsage = 0m;
        foreach (var seg in session.ChargeSegments)
        {
            if (seg.EndedAtUtc.HasValue)
            {
                accruedUsage += seg.Amount;
            }
            else
            {
                int currentSecs = (int)(now - seg.StartedAtUtc).TotalSeconds;
                int segBillable = Math.Max(0, currentSecs);
                accruedUsage += SessionPricingCalculator.CalculateUsage(seg.RatePerHour, segBillable);
            }
        }

        if (accruedUsage > 0)
        {
            throw new BusinessRuleException("Không thể hủy phiên chơi đã phát sinh cước giờ chơi.", "session_has_accrued_charges");
        }

        session.Status = PlaySessionStatus.Cancelled;
        session.EndedAtUtc = now;
        session.UpdatedAtUtc = now;

        session.Machine.Status = MachineStatus.Available;
        session.Machine.UpdatedAtUtc = now;

        _db.SessionEvents.Add(new SessionEvent
        {
            Id = Guid.NewGuid(),
            PlaySessionId = session.Id,
            EventType = "Cancelled",
            OccurredAtUtc = now,
            UserId = currentUserId,
            Note = $"Hủy phiên: {request.Reason.Trim()}"
        });

        var audit = new AuditLog
        {
            Id = Guid.NewGuid(),
            BranchId = session.BranchId,
            UserId = currentUserId,
            Action = "CancelSession",
            EntityType = "PlaySession",
            EntityId = session.Id,
            NewValuesJson = $"{{\"Reason\": \"{request.Reason.Trim()}\"}}",
            CorrelationId = Guid.NewGuid().ToString("N"),
            OccurredAtUtc = now
        };

        _db.AuditLogs.Add(audit);
        await _db.SaveChangesAsync(ct);

        return await GetSessionByIdAsync(id, ct);
    }

    public async Task<SessionQuoteDto> GetSessionQuoteAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var session = await _db.PlaySessions
            .AsNoTracking()
            .Include(s => s.ChargeSegments)
            .Include(s => s.Orders.Where(o => o.Status != OrderStatus.Cancelled))
            .FirstOrDefaultAsync(s => s.Id == id && s.BranchId == branchId, ct);

        if (session == null) throw new NotFoundException("Không tìm thấy phiên chơi.", "session_not_found");

        var now = DateTime.UtcNow;
        decimal usageAmount = 0m;
        int totalBillableSecs = 0;
        decimal activeRate = 10000m;

        foreach (var seg in session.ChargeSegments)
        {
            activeRate = seg.RatePerHour;
            if (seg.EndedAtUtc.HasValue)
            {
                totalBillableSecs += seg.BillableSeconds;
                usageAmount += seg.Amount;
            }
            else
            {
                int currentSecs = (int)(now - seg.StartedAtUtc).TotalSeconds;
                int segBillable = Math.Max(0, currentSecs);
                totalBillableSecs += segBillable;
                usageAmount += SessionPricingCalculator.CalculateUsage(seg.RatePerHour, segBillable);
            }
        }

        decimal serviceAmount = session.Orders.Sum(o => o.TotalAmount);
        decimal totalAmount = usageAmount + serviceAmount;

        return new SessionQuoteDto(
            session.Id,
            activeRate,
            totalBillableSecs,
            usageAmount,
            serviceAmount,
            totalAmount
        );
    }

    public async Task<IReadOnlyList<SessionEventDto>> GetSessionEventsAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var sessionExists = await _db.PlaySessions.AnyAsync(s => s.Id == id && s.BranchId == branchId, ct);
        if (!sessionExists) throw new NotFoundException("Không tìm thấy phiên chơi.", "session_not_found");

        var events = await _db.SessionEvents
            .AsNoTracking()
            .Include(e => e.User)
            .Where(e => e.PlaySessionId == id)
            .OrderBy(e => e.OccurredAtUtc)
            .Select(e => new SessionEventDto(
                e.Id,
                e.EventType,
                e.OccurredAtUtc,
                e.User.FullName,
                e.Note
            ))
            .ToListAsync(ct);

        return events;
    }

    private static PlaySessionDto MapToDto(PlaySession s)
    {
        var now = DateTime.UtcNow;
        decimal activeRate = s.ChargeSegments.OrderByDescending(c => c.StartedAtUtc).FirstOrDefault()?.RatePerHour ?? 10000m;
        int totalSecs = 0;

        foreach (var seg in s.ChargeSegments)
        {
            if (seg.EndedAtUtc.HasValue)
            {
                totalSecs += seg.BillableSeconds;
            }
            else
            {
                totalSecs += (int)Math.Max(0, (now - seg.StartedAtUtc).TotalSeconds);
            }
        }

        decimal currentUsage = s.Status == PlaySessionStatus.Completed ? s.UsageAmount : SessionPricingCalculator.CalculateUsage(activeRate, totalSecs);
        decimal currentService = s.Orders.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.TotalAmount);

        return new PlaySessionDto(
            s.Id,
            s.BranchId,
            s.MachineId,
            s.Machine?.Code ?? string.Empty,
            s.Machine?.DisplayName ?? string.Empty,
            s.CustomerId,
            s.Customer?.FullName,
            s.Customer?.Code,
            s.ShiftId,
            s.SessionNumber,
            s.BillingMode,
            s.Status,
            s.StartedAtUtc,
            s.PausedAtUtc,
            s.EndedAtUtc,
            s.TotalPausedSeconds,
            currentUsage,
            currentService,
            0m,
            currentUsage + currentService,
            s.InvoiceId,
            activeRate,
            totalSecs / 60
        );
    }
}
