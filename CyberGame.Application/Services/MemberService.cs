using CyberGame.Application.Common;
using CyberGame.Application.DTOs;
using CyberGame.Domain.Entities;
using CyberGame.Domain.Enums;
using CyberGame.Domain.Exceptions;
using CyberGame.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace CyberGame.Application.Services;

public class MemberService : IMemberService
{
    private readonly ICyberGameDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IPricingService _pricingService;

    public MemberService(
        ICyberGameDbContext db,
        ICurrentUserService currentUser,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IPricingService? pricingService = null)
    {
        _db = db;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _pricingService = pricingService ?? new PricingService(db, currentUser);
    }

    public async Task<IReadOnlyList<MemberMachineSelectionDto>> GetMachinesAsync(CancellationToken ct = default)
    {
        var machines = await _db.Machines
            .AsNoTracking()
            .Include(m => m.Branch)
            .Include(m => m.MachineGroup)
            .Where(m => m.IsActive && m.Branch.IsActive && m.MachineGroup.IsActive)
            .OrderBy(m => m.Branch.Name)
            .ThenBy(m => m.MachineGroup.SortOrder)
            .ThenBy(m => m.Code)
            .ToListAsync(ct);

        return machines.Select(m => new MemberMachineSelectionDto(
            m.Id,
            m.Code,
            m.DisplayName,
            m.MachineGroup.Name,
            m.BranchId,
            m.Branch.Name,
            m.Status
        )).ToList();
    }

    public async Task<MemberLoginResponse> LoginAsync(MemberLoginRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.MemberCode))
        {
            throw new BusinessRuleException("Mã hội viên không được để trống.", "member_code_required");
        }
        if (string.IsNullOrWhiteSpace(request.Pin) || request.Pin.Trim().Length != 6 || !request.Pin.Trim().All(char.IsDigit))
        {
            throw new BusinessRuleException("Mã PIN phải gồm đúng 6 chữ số.", "invalid_pin");
        }
        if (string.IsNullOrWhiteSpace(request.MachineCode))
        {
            throw new BusinessRuleException("Mã máy trạm không được để trống.", "machine_code_required");
        }

        var machineCode = request.MachineCode.Trim();
        var machine = await _db.Machines
            .Include(m => m.Branch)
            .Include(m => m.MachineGroup)
            .FirstOrDefaultAsync(m =>
                m.IsActive &&
                (request.MachineId.HasValue ? m.Id == request.MachineId.Value : m.Code == machineCode), ct);

        if (machine == null)
        {
            throw new NotFoundException($"Không tìm thấy máy trạm có mã '{machineCode}'.", "machine_not_found");
        }

        var memberCode = request.MemberCode.Trim();
        var customer = await _db.Customers
            .Include(c => c.Wallet)
            .FirstOrDefaultAsync(c => c.Code == memberCode, ct);

        if (customer == null)
        {
            throw new NotFoundException($"Không tìm thấy tài khoản hội viên '{memberCode}'.", "customer_not_found");
        }

        if (customer.HomeBranchId != machine.BranchId)
        {
            throw new BusinessRuleException("Tài khoản hội viên không thuộc chi nhánh của máy trạm này.", "branch_mismatch");
        }

        if (customer.Status != CustomerStatus.Active)
        {
            throw new BusinessRuleException("Tài khoản hội viên đang bị khóa hoặc ngưng hoạt động.", "customer_inactive");
        }

        var now = DateTime.UtcNow;
        if (customer.MemberLockoutEndUtc.HasValue && customer.MemberLockoutEndUtc.Value > now)
        {
            var remainingMins = Math.Ceiling((customer.MemberLockoutEndUtc.Value - now).TotalMinutes);
            throw new BusinessRuleException($"Tài khoản bị tạm khóa do nhập sai PIN quá 5 lần. Vui lòng thử lại sau {remainingMins} phút hoặc liên hệ thu ngân.", "account_locked_out");
        }

        if (string.IsNullOrEmpty(customer.MemberPinHash))
        {
            throw new BusinessRuleException("Tài khoản chưa được thiết lập mã PIN đăng nhập máy khách. Vui lòng liên hệ thu ngân để cài đặt PIN.", "pin_not_set");
        }

        if (!_passwordHasher.VerifyPassword(request.Pin, customer.MemberPinHash))
        {
            customer.MemberFailedLoginCount++;
            if (customer.MemberFailedLoginCount >= 5)
            {
                customer.MemberLockoutEndUtc = now.AddMinutes(15);
                await _db.SaveChangesAsync(ct);
                throw new BusinessRuleException("Tài khoản đã bị tạm khóa 15 phút do nhập sai mã PIN 5 lần liên tiếp.", "account_locked_out");
            }

            await _db.SaveChangesAsync(ct);
            throw new BusinessRuleException($"Mã PIN không chính xác (Lần sai {customer.MemberFailedLoginCount}/5).", "invalid_pin");
        }

        // Đăng nhập hợp lệ trên Member Client là nguồn duy nhất mở phiên chơi.
        // Cho phép đăng nhập lại đúng phiên hiện tại nhưng không tạo phiên trùng.
        var activeMemberSession = await _db.PlaySessions
            .Include(s => s.Machine)
            .FirstOrDefaultAsync(s =>
                s.CustomerId == customer.Id &&
                (s.Status == PlaySessionStatus.Open || s.Status == PlaySessionStatus.Paused), ct);

        if (activeMemberSession != null && activeMemberSession.MachineId != machine.Id)
        {
            throw new BusinessRuleException(
                $"Hội viên đang sử dụng máy {activeMemberSession.Machine.Code}. Vui lòng kết thúc phiên hiện tại trước khi chọn máy khác.",
                "member_session_already_active");
        }

        if (activeMemberSession == null)
        {
            var activeMachineSession = await _db.PlaySessions
                .AsNoTracking()
                .AnyAsync(s =>
                    s.MachineId == machine.Id &&
                    (s.Status == PlaySessionStatus.Open || s.Status == PlaySessionStatus.Paused), ct);

            if (machine.Status != MachineStatus.Available || activeMachineSession)
            {
                throw new BusinessRuleException($"Máy {machine.Code} hiện không còn trống. Vui lòng chọn máy khác.", "machine_not_available");
            }

            if (customer.Wallet == null || customer.Wallet.Balance <= 0)
            {
                throw new BusinessRuleException("Số dư ví hội viên không đủ để bắt đầu phiên chơi.", "insufficient_wallet_balance");
            }

            var openShift = await _db.Shifts
                .Where(s => s.BranchId == machine.BranchId && s.Status == ShiftStatus.Open)
                .OrderByDescending(s => s.OpenedAtUtc)
                .FirstOrDefaultAsync(ct);

            if (openShift == null)
            {
                throw new BusinessRuleException(
                    "Chi nhánh chưa có ca trực đang mở. Vui lòng liên hệ nhân viên quầy.",
                    "shift_required");
            }

            var hourlyRate = await _pricingService.GetEffectiveRatePerHourAsync(
                machine.BranchId,
                machine.MachineGroupId,
                now,
                ct);

            var session = new PlaySession
            {
                Id = Guid.NewGuid(),
                BranchId = machine.BranchId,
                MachineId = machine.Id,
                CustomerId = customer.Id,
                ShiftId = openShift.Id,
                SessionNumber = $"SES-{now:yyyyMMddHHmmssfff}",
                BillingMode = BillingMode.PrepaidWallet,
                Status = PlaySessionStatus.Open,
                StartedAtUtc = now,
                TotalPausedSeconds = 0,
                UsageAmount = 0m,
                ServiceAmount = 0m,
                TotalAmount = 0m,
                CreatedAtUtc = now
            };

            session.ChargeSegments.Add(new SessionChargeSegment
            {
                Id = Guid.NewGuid(),
                PlaySessionId = session.Id,
                StartedAtUtc = now,
                RatePerHour = hourlyRate,
                BillableSeconds = 0,
                Amount = 0m
            });

            session.Events.Add(new SessionEvent
            {
                Id = Guid.NewGuid(),
                PlaySessionId = session.Id,
                EventType = "StartedByMemberLogin",
                OccurredAtUtc = now,
                UserId = openShift.UserId,
                Note = $"Hội viên {customer.FullName} đăng nhập trực tiếp tại máy {machine.Code} từ Member Client"
            });

            machine.Status = MachineStatus.InUse;
            machine.UpdatedAtUtc = now;
            _db.PlaySessions.Add(session);
        }
        else if (machine.Status != MachineStatus.InUse)
        {
            machine.Status = MachineStatus.InUse;
            machine.UpdatedAtUtc = now;
        }

        // Đăng nhập thành công, reset lockout & đếm sai.
        customer.MemberFailedLoginCount = 0;
        customer.MemberLockoutEndUtc = null;
        customer.MemberLastLoginAtUtc = now;
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessRuleException(
                $"Máy {machine.Code} vừa được người khác sử dụng. Vui lòng chọn máy khác.",
                "machine_not_available");
        }

        var token = _jwtTokenService.GenerateMemberAccessToken(customer.Id, customer.Code, machine.BranchId, machine.Code);

        var profileDto = new MemberProfileDto(
            customer.Id,
            customer.Code,
            customer.FullName,
            customer.Phone,
            customer.Email,
            customer.Wallet?.Balance ?? 0m
        );

        var machineDto = new MemberMachineDto(
            machine.Id,
            machine.Code,
            machine.DisplayName,
            machine.BranchId,
            machine.Branch.Name
        );

        return new MemberLoginResponse(token, profileDto, machineDto);
    }

    public async Task<MemberMeResponse> GetMeAsync(CancellationToken ct = default)
    {
        var (customerId, branchId, machineCode) = GetAuthenticatedMemberContext();

        var customer = await _db.Customers
            .AsNoTracking()
            .Include(c => c.Wallet)
            .FirstOrDefaultAsync(c => c.Id == customerId && c.HomeBranchId == branchId, ct);

        if (customer == null)
        {
            throw new NotFoundException("Không tìm thấy thông tin hội viên.", "customer_not_found");
        }

        var machine = await _db.Machines
            .AsNoTracking()
            .Include(m => m.Branch)
            .FirstOrDefaultAsync(m => m.Code == machineCode && m.BranchId == branchId, ct);

        if (machine == null)
        {
            throw new NotFoundException($"Không tìm thấy máy trạm có mã '{machineCode}'.", "machine_not_found");
        }

        var profileDto = new MemberProfileDto(
            customer.Id,
            customer.Code,
            customer.FullName,
            customer.Phone,
            customer.Email,
            customer.Wallet?.Balance ?? 0m
        );

        var machineDto = new MemberMachineDto(
            machine.Id,
            machine.Code,
            machine.DisplayName,
            branchId,
            machine.Branch.Name
        );

        return new MemberMeResponse(profileDto, machineDto);
    }

    public async Task ChangePinAsync(ChangeMemberPinRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPin) ||
            request.CurrentPin.Trim().Length != 6 ||
            !request.CurrentPin.Trim().All(char.IsDigit))
        {
            throw new BusinessRuleException("Mã PIN hiện tại phải gồm đúng 6 chữ số.", "invalid_current_pin");
        }

        if (string.IsNullOrWhiteSpace(request.NewPin) ||
            request.NewPin.Trim().Length != 6 ||
            !request.NewPin.Trim().All(char.IsDigit))
        {
            throw new BusinessRuleException("Mã PIN mới phải gồm đúng 6 chữ số.", "invalid_new_pin");
        }

        var currentPin = request.CurrentPin.Trim();
        var newPin = request.NewPin.Trim();
        if (currentPin == newPin)
        {
            throw new BusinessRuleException("Mã PIN mới phải khác mã PIN hiện tại.", "pin_unchanged");
        }

        var (customerId, branchId, _) = GetAuthenticatedMemberContext();
        var customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId && c.HomeBranchId == branchId, ct);

        if (customer == null || customer.Status != CustomerStatus.Active)
        {
            throw new NotFoundException("Không tìm thấy hội viên đang hoạt động.", "customer_not_found");
        }

        if (string.IsNullOrWhiteSpace(customer.MemberPinHash) ||
            !_passwordHasher.VerifyPassword(currentPin, customer.MemberPinHash))
        {
            throw new BusinessRuleException("Mã PIN hiện tại không chính xác.", "invalid_current_pin");
        }

        customer.MemberPinHash = _passwordHasher.HashPassword(newPin);
        customer.MemberFailedLoginCount = 0;
        customer.MemberLockoutEndUtc = null;
        customer.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<MemberSessionDto?> GetActiveSessionAsync(CancellationToken ct = default)
    {
        var (customerId, branchId, machineCode) = GetAuthenticatedMemberContext();

        var session = await _db.PlaySessions
            .AsNoTracking()
            .Include(s => s.Machine)
            .Include(s => s.ChargeSegments)
            .Include(s => s.Orders)
            .Include(s => s.Customer)
                .ThenInclude(c => c!.Wallet)
            .Where(s => s.CustomerId == customerId && s.BranchId == branchId &&
                        s.Machine.Code == machineCode &&
                        (s.Status == PlaySessionStatus.Open || s.Status == PlaySessionStatus.Paused))
            .FirstOrDefaultAsync(ct);

        if (session == null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var (usageAmount, totalBillableSecs, activeRate) = CalculateAccruedUsage(session, now);
        var serviceAmount = session.Orders
            .Where(o => o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Fulfilled)
            .Sum(o => o.TotalAmount);
        var unpaidServiceAmount = session.Orders
            .Where(o => (o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Fulfilled) &&
                        o.PaymentStatus != PaymentStatus.Completed)
            .Sum(o => o.TotalAmount);
        var totalAmount = usageAmount + serviceAmount;
        var walletBalance = session.Customer?.Wallet?.Balance ?? 0m;
        var availableBalance = Math.Max(0, walletBalance - usageAmount - unpaidServiceAmount);

        return new MemberSessionDto(
            session.Id,
            session.SessionNumber,
            session.Status,
            session.BillingMode,
            session.Machine.Code,
            session.Machine.DisplayName,
            session.StartedAtUtc,
            session.PausedAtUtc,
            session.TotalPausedSeconds,
            activeRate,
            usageAmount,
            serviceAmount,
            totalAmount,
            walletBalance,
            availableBalance
        );
    }

    public async Task<MemberSessionQuoteDto> GetSessionQuoteAsync(CancellationToken ct = default)
    {
        var (customerId, branchId, machineCode) = GetAuthenticatedMemberContext();

        var session = await _db.PlaySessions
            .AsNoTracking()
            .Include(s => s.Machine)
            .Include(s => s.ChargeSegments)
            .Include(s => s.Orders)
            .Include(s => s.Customer)
                .ThenInclude(c => c!.Wallet)
            .Where(s => s.CustomerId == customerId && s.BranchId == branchId &&
                        s.Machine.Code == machineCode &&
                        (s.Status == PlaySessionStatus.Open || s.Status == PlaySessionStatus.Paused))
            .FirstOrDefaultAsync(ct);

        if (session == null)
        {
            throw new NotFoundException("Không tìm thấy phiên chơi đang mở cho hội viên trên máy này.", "session_not_found");
        }

        var now = DateTime.UtcNow;
        var (usageAmount, totalBillableSecs, activeRate) = CalculateAccruedUsage(session, now);
        var serviceAmount = session.Orders
            .Where(o => o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Fulfilled)
            .Sum(o => o.TotalAmount);
        var unpaidServiceAmount = session.Orders
            .Where(o => (o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Fulfilled) &&
                        o.PaymentStatus != PaymentStatus.Completed)
            .Sum(o => o.TotalAmount);
        var totalAmount = usageAmount + serviceAmount;
        var walletBalance = session.Customer?.Wallet?.Balance ?? 0m;
        var availableBalance = Math.Max(0, walletBalance - usageAmount - unpaidServiceAmount);

        return new MemberSessionQuoteDto(
            session.Id,
            activeRate,
            totalBillableSecs,
            usageAmount,
            serviceAmount,
            totalAmount,
            walletBalance,
            availableBalance,
            session.Status,
            now
        );
    }

    public async Task<MemberSessionSummaryDto> CompleteSessionAsync(CancellationToken ct = default)
    {
        var (customerId, branchId, machineCode) = GetAuthenticatedMemberContext();

        var session = await _db.PlaySessions
            .Include(s => s.Machine)
            .Include(s => s.Shift)
            .Include(s => s.ChargeSegments)
            .Include(s => s.Orders)
                .ThenInclude(o => o.Items)
            .Include(s => s.Orders)
                .ThenInclude(o => o.Payments)
            .Include(s => s.Customer)
                .ThenInclude(c => c!.Wallet)
            .Where(s => s.CustomerId == customerId && s.BranchId == branchId &&
                        s.Machine.Code == machineCode &&
                        (s.Status == PlaySessionStatus.Open || s.Status == PlaySessionStatus.Paused))
            .FirstOrDefaultAsync(ct);

        if (session == null)
        {
            // Kiểm tra xem có phiên chơi vừa hoàn tất gần đây (trong vòng 5 phút) hay không để đảm bảo tính Idempotent
            var recentCompleted = await _db.PlaySessions
                .Include(s => s.Machine)
                .Include(s => s.Customer)
                    .ThenInclude(c => c!.Wallet)
                .Where(s => s.CustomerId == customerId && s.BranchId == branchId &&
                            s.Machine.Code == machineCode &&
                            s.Status == PlaySessionStatus.Completed)
                .OrderByDescending(s => s.EndedAtUtc)
                .FirstOrDefaultAsync(ct);

            if (recentCompleted != null && recentCompleted.EndedAtUtc >= DateTime.UtcNow.AddMinutes(-5))
            {
                var inv = await _db.Invoices.FirstOrDefaultAsync(i => i.PlaySessionId == recentCompleted.Id, ct);
                int replayTotalSecs = (int)((recentCompleted.EndedAtUtc ?? DateTime.UtcNow) - recentCompleted.StartedAtUtc).TotalSeconds;
                decimal currentWalletBalance = recentCompleted.Customer?.Wallet?.Balance ?? 0m;
                return new MemberSessionSummaryDto(
                    recentCompleted.Id,
                    recentCompleted.SessionNumber,
                    recentCompleted.Machine.Code,
                    recentCompleted.StartedAtUtc,
                    recentCompleted.EndedAtUtc ?? DateTime.UtcNow,
                    replayTotalSecs,
                    recentCompleted.UsageAmount,
                    recentCompleted.ServiceAmount,
                    recentCompleted.TotalAmount,
                    currentWalletBalance,
                    currentWalletBalance,
                    inv?.InvoiceNumber
                );
            }

            throw new BusinessRuleException("Không tìm thấy phiên chơi đang hoạt động để kết thúc.", "session_not_found");
        }

        var now = DateTime.UtcNow;

        if (session.Orders.Any(o => o.Status == OrderStatus.Draft))
        {
            throw new BusinessRuleException(
                "Bạn còn đơn món đang chờ nhân viên duyệt. Vui lòng chờ đơn được duyệt hoặc từ chối trước khi kết thúc phiên.",
                "pending_orders_exist");
        }

        // 1. Chốt segment cuối cùng nếu đang mở
        var lastSegment = session.ChargeSegments.OrderByDescending(s => s.StartedAtUtc).FirstOrDefault();
        if (lastSegment != null && lastSegment.EndedAtUtc == null)
        {
            lastSegment.EndedAtUtc = now;
            int segSeconds = (int)(now - lastSegment.StartedAtUtc).TotalSeconds;
            lastSegment.BillableSeconds += Math.Max(0, segSeconds);
            lastSegment.Amount = SessionPricingCalculator.CalculateUsage(lastSegment.RatePerHour, lastSegment.BillableSeconds);
        }

        // 2. Tính tiền giờ và tiền dịch vụ
        decimal totalUsageAmount = session.ChargeSegments.Sum(s => s.Amount);
        decimal totalServiceAmount = session.Orders
            .Where(o => o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Fulfilled)
            .Sum(o => o.TotalAmount);

        session.UsageAmount = totalUsageAmount;
        session.ServiceAmount = totalServiceAmount;
        session.TotalAmount = totalUsageAmount + totalServiceAmount;
        session.EndedAtUtc = now;
        session.Status = PlaySessionStatus.Completed;
        session.UpdatedAtUtc = now;

        // 3. Trả máy về Available
        session.Machine.Status = MachineStatus.Available;
        session.Machine.UpdatedAtUtc = now;

        // Actor phụ trách nghiệp vụ là nhân viên phụ trách ca (Shift.UserId)
        var staffUserId = session.Shift?.UserId ?? Guid.Empty;
        if (staffUserId == Guid.Empty)
        {
            var defaultUser = await _db.Users.FirstOrDefaultAsync(u => u.BranchId == branchId && u.IsActive, ct);
            staffUserId = defaultUser?.Id ?? Guid.Empty;
        }

        // 4. Session Event & Audit
        _db.SessionEvents.Add(new SessionEvent
        {
            Id = Guid.NewGuid(),
            PlaySessionId = session.Id,
            EventType = "Completed",
            OccurredAtUtc = now,
            UserId = staffUserId,
            Note = $"Hội viên kết thúc phiên từ máy {session.Machine.Code}. Nguồn: MemberClient"
        });

        // 5. Tạo Invoice
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

        int totalBillableSecs = session.ChargeSegments.Sum(s => s.BillableSeconds);
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

        // Reuse payments already completed for food/drink orders. Linking them to
        // the final session invoice prevents charging the same order a second time.
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

        // 6. Trừ ví trả trước nếu PrepaidWallet
        decimal balanceBefore = session.Customer?.Wallet?.Balance ?? 0m;
        decimal balanceAfter = balanceBefore;

        if (session.BillingMode == BillingMode.PrepaidWallet && session.Customer?.Wallet != null)
        {
            var wallet = session.Customer.Wallet;
            decimal remainingAmount = Math.Max(0m, invoice.TotalAmount - invoice.PaidAmount);
            decimal deductAmount = Math.Min(remainingAmount, wallet.Balance);
            if (deductAmount > 0)
            {
                balanceAfter = balanceBefore - deductAmount;
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
                    Description = $"Thanh toán phiên chơi {session.SessionNumber} (MemberClient)",
                    OccurredAtUtc = now,
                    ShiftId = session.ShiftId,
                    CreatedByUserId = staffUserId
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
                    Note = "Thanh toán tự động bằng ví trả trước (MemberClient)",
                    CreatedByUserId = staffUserId
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

        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            BranchId = session.BranchId,
            UserId = staffUserId,
            Action = "CompleteSessionByMember",
            EntityType = "PlaySession",
            EntityId = session.Id,
            NewValuesJson = $"{{\"Source\":\"MemberClient\",\"MachineCode\":\"{session.Machine.Code}\",\"TotalAmount\":{session.TotalAmount}}}",
            CorrelationId = Guid.NewGuid().ToString("N"),
            OccurredAtUtc = now
        });

        await _db.SaveChangesAsync(ct);

        int totalSecs = (int)(now - session.StartedAtUtc).TotalSeconds;
        return new MemberSessionSummaryDto(
            session.Id,
            session.SessionNumber,
            session.Machine.Code,
            session.StartedAtUtc,
            now,
            totalSecs,
            session.UsageAmount,
            session.ServiceAmount,
            session.TotalAmount,
            balanceBefore,
            balanceAfter,
            invoice.InvoiceNumber
        );
    }

    public async Task<IReadOnlyList<MemberProductDto>> GetCatalogAsync(CancellationToken ct = default)
    {
        var (_, branchId, _) = GetAuthenticatedMemberContext();

        var products = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Balance)
            .Where(p => p.BranchId == branchId && p.IsActive)
            .OrderBy(p => p.Category != null ? p.Category.SortOrder : 99)
            .ThenBy(p => p.Name)
            .ToListAsync(ct);

        // Chỉ sản phẩm IsActive, đúng branch, còn tồn nếu TrackInventory = true
        var availableProducts = products
            .Where(p => !p.TrackInventory || (p.Balance != null && p.Balance.QuantityOnHand > 0))
            .Select(p => new MemberProductDto(
                p.Id,
                p.Name,
                p.Sku,
                p.Category?.Name,
                p.SalePrice,
                p.Unit,
                null,
                p.TrackInventory,
                p.TrackInventory ? p.Balance?.QuantityOnHand : null
            ))
            .ToList();

        return availableProducts;
    }

    public async Task<IReadOnlyList<MemberOrderDto>> GetOrdersAsync(CancellationToken ct = default)
    {
        var (customerId, branchId, machineCode) = GetAuthenticatedMemberContext();

        var session = await _db.PlaySessions
            .AsNoTracking()
            .Where(s => s.CustomerId == customerId && s.BranchId == branchId &&
                        s.Machine.Code == machineCode &&
                        (s.Status == PlaySessionStatus.Open || s.Status == PlaySessionStatus.Paused))
            .FirstOrDefaultAsync(ct);

        if (session == null)
        {
            return Array.Empty<MemberOrderDto>();
        }

        var orders = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.PlaySessionId == session.Id)
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync(ct);

        return orders.Select(o => new MemberOrderDto(
            o.Id,
            o.OrderNumber,
            o.Status,
            o.TotalAmount,
            o.CreatedAtUtc,
            o.Items.Select(i => new MemberOrderItemDto(
                i.ProductId ?? Guid.Empty,
                i.ProductName,
                i.Quantity,
                i.UnitPrice,
                i.LineTotal,
                i.Note
            )).ToList(),
            o.PaymentMethod,
            o.PaymentStatus,
            o.PaidAtUtc
        )).ToList();
    }

    public async Task<MemberOrderDto> CreateOrderAsync(CreateMemberOrderRequest request, CancellationToken ct = default)
    {
        if (request.Items == null || !request.Items.Any())
        {
            throw new BusinessRuleException("Đơn hàng phải có ít nhất 1 sản phẩm.", "empty_order");
        }

        if (request.PaymentMethod == PaymentMethod.BankTransfer)
        {
            throw new BusinessRuleException("Thanh toán ATM/MoMo đang được phát triển và chưa thể sử dụng.", "payment_method_not_available");
        }

        if (request.PaymentMethod != PaymentMethod.Cash && request.PaymentMethod != PaymentMethod.Wallet)
        {
            throw new BusinessRuleException("Phương thức thanh toán không hợp lệ.", "invalid_payment_method");
        }

        var (customerId, branchId, machineCode) = GetAuthenticatedMemberContext();

        var session = await _db.PlaySessions
            .Include(s => s.Machine)
            .Include(s => s.Shift)
            .Include(s => s.ChargeSegments)
            .Include(s => s.Orders)
            .Where(s => s.CustomerId == customerId && s.BranchId == branchId &&
                        s.Machine.Code == machineCode &&
                        (s.Status == PlaySessionStatus.Open || s.Status == PlaySessionStatus.Paused))
            .FirstOrDefaultAsync(ct);

        if (session == null)
        {
            throw new BusinessRuleException("Không tìm thấy phiên chơi đang hoạt động để gọi món.", "session_not_found");
        }

        if (session.ShiftId == Guid.Empty)
        {
            throw new BusinessRuleException("Phiên chơi không gắn với ca trực hợp lệ.", "shift_required");
        }

        var shift = await _db.Shifts.FirstOrDefaultAsync(s => s.Id == session.ShiftId, ct);
        if (shift == null || shift.Status != ShiftStatus.Open)
        {
            throw new BusinessRuleException("Ca làm việc của phiên này đã đóng, không thể gọi thêm món.", "shift_closed");
        }

        var now = DateTime.UtcNow;

        // Lấy danh sách sản phẩm
        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Include(p => p.Balance)
            .Where(p => productIds.Contains(p.Id) && p.BranchId == branchId && p.IsActive)
            .ToDictionaryAsync(p => p.Id, ct);

        decimal orderSubtotal = 0m;
        foreach (var itemReq in request.Items)
        {
            if (itemReq.Quantity <= 0)
            {
                throw new BusinessRuleException("Số lượng từng món phải lớn hơn 0.", "invalid_quantity");
            }

            if (!products.TryGetValue(itemReq.ProductId, out var product))
            {
                throw new NotFoundException($"Sản phẩm có ID {itemReq.ProductId} không khả dụng.", "product_not_found");
            }

            if (product.TrackInventory)
            {
                var currentStock = product.Balance?.QuantityOnHand ?? 0m;
                if (currentStock < itemReq.Quantity)
                {
                    throw new BusinessRuleException($"Sản phẩm '{product.Name}' không đủ tồn kho (Còn lại: {currentStock}, Yêu cầu: {itemReq.Quantity}).", "insufficient_inventory");
                }
            }

            orderSubtotal += product.SalePrice * itemReq.Quantity;
        }

        // Chỉ thanh toán bằng ví mới cần kiểm tra số dư. Đơn tiền mặt không được
        // giữ chỗ hoặc khấu trừ số dư ví của hội viên.
        var (accruedUsage, _, _) = CalculateAccruedUsage(session, now);
        var reservedWalletServiceAmount = session.Orders
            .Where(o => o.PaymentMethod == PaymentMethod.Wallet &&
                        o.PaymentStatus != PaymentStatus.Completed &&
                        (o.Status == OrderStatus.Draft || o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Fulfilled))
            .Sum(o => o.TotalAmount);

        var wallet = await _db.Wallets.FirstOrDefaultAsync(w => w.CustomerId == customerId, ct);
        var walletBalance = wallet?.Balance ?? 0m;
        var availableBalance = walletBalance - accruedUsage - reservedWalletServiceAmount;

        if (request.PaymentMethod == PaymentMethod.Wallet && orderSubtotal > availableBalance)
        {
            throw new BusinessRuleException(
                $"Số dư khả dụng không đủ để gọi món (Khả dụng: {availableBalance:N0} đ, Đơn món: {orderSubtotal:N0} đ). Vui lòng nạp thêm tiền tại quầy.",
                "insufficient_wallet_balance");
        }

        var order = new Order
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            OrderNumber = $"ORD-{now:yyyyMMddHHmmssfff}",
            PlaySessionId = session.Id,
            CustomerId = customerId,
            ShiftId = shift.Id,
            Status = request.PaymentMethod == PaymentMethod.Wallet ? OrderStatus.Confirmed : OrderStatus.Draft,
            PaymentMethod = request.PaymentMethod,
            PaymentStatus = request.PaymentMethod == PaymentMethod.Wallet ? PaymentStatus.Completed : PaymentStatus.Pending,
            Subtotal = orderSubtotal,
            DiscountAmount = 0m,
            TotalAmount = orderSubtotal,
            ConfirmedAtUtc = request.PaymentMethod == PaymentMethod.Wallet ? now : null,
            PaidAtUtc = request.PaymentMethod == PaymentMethod.Wallet ? now : null,
            CreatedAtUtc = now,
            UpdatedAtUtc = request.PaymentMethod == PaymentMethod.Wallet ? now : null
        };

        foreach (var itemReq in request.Items)
        {
            var product = products[itemReq.ProductId];
            decimal lineTotal = product.SalePrice * itemReq.Quantity;

            order.Items.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                ProductId = product.Id,
                ProductName = product.Name,
                Sku = product.Sku,
                Unit = product.Unit,
                Quantity = itemReq.Quantity,
                UnitPrice = product.SalePrice,
                DiscountAmount = 0m,
                LineTotal = lineTotal,
                Note = itemReq.Note
            });
        }

        if (request.PaymentMethod == PaymentMethod.Wallet)
        {
            if (wallet == null)
            {
                throw new BusinessRuleException("Hội viên chưa có ví để thanh toán đơn món.", "wallet_not_found");
            }

            foreach (var item in order.Items)
            {
                if (!item.ProductId.HasValue || !products.TryGetValue(item.ProductId.Value, out var product) || !product.TrackInventory)
                {
                    continue;
                }

                var balance = product.Balance!;
                var quantityBefore = balance.QuantityOnHand;
                var quantityAfter = quantityBefore - item.Quantity;
                balance.QuantityOnHand = quantityAfter;
                balance.UpdatedAtUtc = now;

                _db.InventoryTransactions.Add(new InventoryTransaction
                {
                    Id = Guid.NewGuid(),
                    BranchId = branchId,
                    ProductId = product.Id,
                    Type = InventoryTransactionType.Sale,
                    Quantity = -item.Quantity,
                    QuantityBefore = quantityBefore,
                    QuantityAfter = quantityAfter,
                    UnitCost = product.CostPrice,
                    ReferenceType = "Order",
                    ReferenceId = order.Id,
                    Note = $"Xuất bán tự động đơn ví {order.OrderNumber}",
                    OccurredAtUtc = now,
                    CreatedByUserId = shift.UserId
                });
            }

            var balanceBefore = wallet.Balance;
            var balanceAfter = balanceBefore - order.TotalAmount;
            wallet.Balance = balanceAfter;
            wallet.UpdatedAtUtc = now;

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                BranchId = branchId,
                OrderId = order.Id,
                CustomerId = customerId,
                ShiftId = shift.Id,
                PaymentNumber = $"PAY-{now:yyyyMMddHHmmssfff}",
                Type = PaymentType.Payment,
                Method = PaymentMethod.Wallet,
                Amount = order.TotalAmount,
                Status = PaymentStatus.Completed,
                ExternalReference = order.OrderNumber,
                PaidAtUtc = now,
                Note = "Hội viên thanh toán đơn món bằng ví; hệ thống tự động duyệt.",
                CreatedByUserId = shift.UserId
            };
            order.Payments.Add(payment);

            _db.WalletTransactions.Add(new WalletTransaction
            {
                Id = Guid.NewGuid(),
                WalletId = wallet.Id,
                BranchId = branchId,
                Type = WalletTransactionType.Usage,
                Amount = -order.TotalAmount,
                BalanceBefore = balanceBefore,
                BalanceAfter = balanceAfter,
                ReferenceType = "Order",
                ReferenceId = order.Id,
                Description = $"Thanh toán đơn món {order.OrderNumber}",
                OccurredAtUtc = now,
                ShiftId = shift.Id,
                CreatedByUserId = shift.UserId
            });

            session.ServiceAmount = session.Orders
                .Where(o => o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Fulfilled)
                .Sum(o => o.TotalAmount) + order.TotalAmount;
            session.TotalAmount = accruedUsage + session.ServiceAmount;
            session.UpdatedAtUtc = now;
        }

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        return new MemberOrderDto(
            order.Id,
            order.OrderNumber,
            order.Status,
            order.TotalAmount,
            order.CreatedAtUtc,
            order.Items.Select(i => new MemberOrderItemDto(
                i.ProductId ?? Guid.Empty,
                i.ProductName,
                i.Quantity,
                i.UnitPrice,
                i.LineTotal,
                i.Note
            )).ToList(),
            order.PaymentMethod,
            order.PaymentStatus,
            order.PaidAtUtc
        );
    }

    public async Task<IReadOnlyList<MemberTopUpRequestDto>> GetTopUpRequestsAsync(CancellationToken ct = default)
    {
        var (customerId, branchId, _) = GetAuthenticatedMemberContext();

        var requests = await _db.MemberTopUpRequests
            .AsNoTracking()
            .Where(r => r.CustomerId == customerId && r.BranchId == branchId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(20)
            .ToListAsync(ct);

        return requests.Select(r => new MemberTopUpRequestDto(
            r.Id,
            r.Amount,
            r.Status,
            r.Note,
            r.CreatedAtUtc,
            r.ProcessedAtUtc
        )).ToList();
    }

    public async Task<MemberTopUpRequestDto> CreateTopUpRequestAsync(CreateMemberTopUpRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0)
        {
            throw new BusinessRuleException("Số tiền nạp phải lớn hơn 0.", "invalid_amount");
        }

        var (customerId, branchId, _) = GetAuthenticatedMemberContext();

        // Kiểm tra xem đã có yêu cầu Pending nào chưa
        bool hasPending = await _db.MemberTopUpRequests
            .AnyAsync(r => r.CustomerId == customerId && r.Status == MemberTopUpRequestStatus.Pending, ct);

        if (hasPending)
        {
            throw new ConflictException("Bạn đang có một yêu cầu nạp tiền đang chờ thu ngân duyệt. Vui lòng đợi thu ngân xử lý.", "pending_request_exists");
        }

        var now = DateTime.UtcNow;
        var topUpRequest = new MemberTopUpRequest
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            CustomerId = customerId,
            Amount = request.Amount,
            Status = MemberTopUpRequestStatus.Pending,
            Note = request.Note?.Trim(),
            CreatedAtUtc = now
        };

        _db.MemberTopUpRequests.Add(topUpRequest);
        await _db.SaveChangesAsync(ct);

        return new MemberTopUpRequestDto(
            topUpRequest.Id,
            topUpRequest.Amount,
            topUpRequest.Status,
            topUpRequest.Note,
            topUpRequest.CreatedAtUtc,
            null
        );
    }

    private (Guid CustomerId, Guid BranchId, string MachineCode) GetAuthenticatedMemberContext()
    {
        var customerId = _currentUser.CustomerId;
        if (!customerId.HasValue && _currentUser.UserId.HasValue)
        {
            customerId = _currentUser.UserId.Value;
        }

        if (!customerId.HasValue)
        {
            throw new DomainException("Chưa xác thực hội viên.", "unauthenticated");
        }

        var branchId = _currentUser.BranchId;
        if (!branchId.HasValue)
        {
            throw new DomainException("Thiếu thông tin chi nhánh của hội viên.", "branch_not_found");
        }

        var machineCode = _currentUser.MachineCode;
        if (string.IsNullOrWhiteSpace(machineCode))
        {
            throw new DomainException("Thiếu thông tin máy trạm trong phiên đăng nhập.", "machine_code_required");
        }

        return (customerId.Value, branchId.Value, machineCode.Trim());
    }

    private static (decimal UsageAmount, int TotalBillableSeconds, decimal ActiveRate) CalculateAccruedUsage(PlaySession session, DateTime now)
    {
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

        return (usageAmount, totalBillableSecs, activeRate);
    }
}
