using CyberGame.Application.Common;
using CyberGame.Application.DTOs;
using CyberGame.Domain.Entities;
using CyberGame.Domain.Enums;
using CyberGame.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CyberGame.Application.Services;

public interface ICustomerWalletService
{
    Task<PagedResult<CustomerDto>> GetCustomersAsync(string? search = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<CustomerDto> GetCustomerByIdAsync(Guid id, CancellationToken ct = default);
    Task<CustomerDto> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken ct = default);
    Task<CustomerDto> UpdateCustomerAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct = default);
    Task<PagedResult<WalletTransactionDto>> GetWalletTransactionsAsync(Guid customerId, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<CustomerDto> TopUpWalletAsync(Guid customerId, TopUpWalletRequest request, CancellationToken ct = default);
    Task<CustomerDto> AdjustWalletAsync(Guid customerId, AdjustWalletRequest request, CancellationToken ct = default);
    Task<CustomerDto> SetMemberPinAsync(Guid customerId, string pin, CancellationToken ct = default);
    Task<IReadOnlyList<StaffMemberTopUpRequestDto>> GetMemberTopUpRequestsAsync(MemberTopUpRequestStatus? status = null, CancellationToken ct = default);
    Task<StaffMemberTopUpRequestDto> ApproveMemberTopUpRequestAsync(Guid requestId, CancellationToken ct = default);
    Task<StaffMemberTopUpRequestDto> RejectMemberTopUpRequestAsync(Guid requestId, CancellationToken ct = default);
}

public class CustomerWalletService : ICustomerWalletService
{
    private readonly ICyberGameDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher? _passwordHasher;

    public CustomerWalletService(ICyberGameDbContext db, ICurrentUserService currentUser)
        : this(db, currentUser, null)
    {
    }

    public CustomerWalletService(ICyberGameDbContext db, ICurrentUserService currentUser, IPasswordHasher? passwordHasher)
    {
        _db = db;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
    }

    private async Task<Guid> GetBranchIdAsync(CancellationToken ct)
    {
        if (_currentUser.BranchId.HasValue) return _currentUser.BranchId.Value;
        var b = await _db.Branches.FirstOrDefaultAsync(x => x.IsActive, ct);
        return b?.Id ?? throw new NotFoundException("Không tìm thấy chi nhánh.", "branch_not_found");
    }

    public async Task<PagedResult<CustomerDto>> GetCustomersAsync(string? search = null, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var query = _db.Customers
            .AsNoTracking()
            .Include(c => c.CustomerGroup)
            .Include(c => c.Wallet)
            .Where(c => c.HomeBranchId == branchId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(c => c.FullName.ToLower().Contains(s) ||
                                     c.Code.ToLower().Contains(s) ||
                                     (c.Phone != null && c.Phone.Contains(s)));
        }

        int total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(c => c.JoinedAtUtc)
                               .Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .ToListAsync(ct);

        var dtos = items.Select(MapToDto).ToList();
        return new PagedResult<CustomerDto>(dtos, total, page, pageSize);
    }

    public async Task<CustomerDto> GetCustomerByIdAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var customer = await _db.Customers
            .AsNoTracking()
            .Include(c => c.CustomerGroup)
            .Include(c => c.Wallet)
            .FirstOrDefaultAsync(c => c.Id == id && c.HomeBranchId == branchId, ct);

        if (customer == null) throw new NotFoundException("Không tìm thấy hội viên.", "customer_not_found");
        return MapToDto(customer);
    }

    public async Task<CustomerDto> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var code = request.Code.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(request.InitialPin) || request.InitialPin.Trim().Length != 6 || !request.InitialPin.Trim().All(char.IsDigit))
        {
            throw new BusinessRuleException("Mã PIN ban đầu phải gồm đúng 6 chữ số.", "invalid_pin");
        }

        if (_passwordHasher == null)
        {
            throw new InvalidOperationException("Chưa cấu hình bộ băm mật khẩu.");
        }

        if (await _db.Customers.AnyAsync(c => c.Code == code, ct))
        {
            throw new ConflictException("Mã khách hàng đã tồn tại.", "customer_code_exists");
        }

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            HomeBranchId = branchId,
            CustomerGroupId = request.CustomerGroupId,
            Code = code,
            FullName = request.FullName.Trim(),
            Phone = request.Phone?.Trim(),
            Email = request.Email?.Trim(),
            DateOfBirth = request.DateOfBirth,
            Status = CustomerStatus.Active,
            MemberPinHash = _passwordHasher.HashPassword(request.InitialPin.Trim()),
            MemberFailedLoginCount = 0,
            MemberLockoutEndUtc = null,
            Notes = request.Notes?.Trim(),
            JoinedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        };

        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Balance = 0m,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _db.Customers.Add(customer);
        _db.Wallets.Add(wallet);

        if (request.InitialDeposit > 0)
        {
            // Kiểm tra ca mở
            var currentUserId = _currentUser.UserId ?? throw new DomainException("Chưa đăng nhập.", "unauthenticated");
            var openShift = await _db.Shifts.FirstOrDefaultAsync(s => s.UserId == currentUserId && s.Status == ShiftStatus.Open, ct);
            if (openShift == null)
            {
                throw new BusinessRuleException("Bạn phải mở ca làm việc trước khi thực hiện nạp tiền cho hội viên.", "shift_required");
            }

            wallet.Balance = request.InitialDeposit;

            var transaction = new WalletTransaction
            {
                Id = Guid.NewGuid(),
                WalletId = wallet.Id,
                BranchId = branchId,
                Type = WalletTransactionType.TopUp,
                Amount = request.InitialDeposit,
                BalanceBefore = 0m,
                BalanceAfter = request.InitialDeposit,
                Description = "Nạp tiền khởi tạo khi tạo tài khoản",
                OccurredAtUtc = DateTime.UtcNow,
                ShiftId = openShift.Id,
                CreatedByUserId = currentUserId
            };

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                BranchId = branchId,
                CustomerId = customer.Id,
                ShiftId = openShift.Id,
                PaymentNumber = $"PAY-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
                Type = PaymentType.WalletTopUp,
                Method = PaymentMethod.Cash,
                Amount = request.InitialDeposit,
                Status = PaymentStatus.Completed,
                PaidAtUtc = DateTime.UtcNow,
                Note = "Nạp tiền mở tài khoản",
                CreatedByUserId = currentUserId
            };

            var cashMovement = new CashMovement
            {
                Id = Guid.NewGuid(),
                ShiftId = openShift.Id,
                Type = CashMovementType.TopUp,
                Amount = request.InitialDeposit,
                ReferenceType = "CustomerWallet",
                ReferenceId = customer.Id,
                Description = $"Thu tiền nạp ví hội viên {customer.FullName} ({customer.Code})",
                OccurredAtUtc = DateTime.UtcNow,
                CreatedByUserId = currentUserId
            };

            _db.WalletTransactions.Add(transaction);
            _db.Payments.Add(payment);
            _db.CashMovements.Add(cashMovement);
        }

        await _db.SaveChangesAsync(ct);
        return await GetCustomerByIdAsync(customer.Id, ct);
    }

    public async Task<CustomerDto> UpdateCustomerAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var customer = await _db.Customers
            .Include(c => c.CustomerGroup)
            .Include(c => c.Wallet)
            .FirstOrDefaultAsync(c => c.Id == id && c.HomeBranchId == branchId, ct);

        if (customer == null) throw new NotFoundException("Không tìm thấy hội viên.", "customer_not_found");

        customer.CustomerGroupId = request.CustomerGroupId;
        customer.FullName = request.FullName.Trim();
        customer.Phone = request.Phone?.Trim();
        customer.Email = request.Email?.Trim();
        customer.DateOfBirth = request.DateOfBirth;
        customer.Status = request.Status;
        customer.Notes = request.Notes?.Trim();
        customer.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapToDto(customer);
    }

    public async Task<PagedResult<WalletTransactionDto>> GetWalletTransactionsAsync(Guid customerId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var customer = await _db.Customers.Include(c => c.Wallet).FirstOrDefaultAsync(c => c.Id == customerId && c.HomeBranchId == branchId, ct);
        if (customer?.Wallet == null) throw new NotFoundException("Không tìm thấy ví hội viên.", "wallet_not_found");

        var query = _db.WalletTransactions
            .AsNoTracking()
            .Include(t => t.CreatedByUser)
            .Where(t => t.WalletId == customer.Wallet.Id);

        int total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(t => t.OccurredAtUtc)
                               .Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .ToListAsync(ct);

        var dtos = items.Select(t => new WalletTransactionDto(
            t.Id,
            t.WalletId,
            t.Type,
            t.Amount,
            t.BalanceBefore,
            t.BalanceAfter,
            t.Description,
            t.OccurredAtUtc,
            t.CreatedByUser?.FullName ?? t.CreatedByUser?.Username ?? "Hệ thống"
        )).ToList();

        return new PagedResult<WalletTransactionDto>(dtos, total, page, pageSize);
    }

    public async Task<CustomerDto> TopUpWalletAsync(Guid customerId, TopUpWalletRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0)
        {
            throw new BusinessRuleException("Số tiền nạp phải lớn hơn 0.", "invalid_amount");
        }

        var currentUserId = _currentUser.UserId ?? throw new DomainException("Chưa đăng nhập.", "unauthenticated");
        var branchId = await GetBranchIdAsync(ct);
        var openShift = await _db.Shifts.FirstOrDefaultAsync(s => s.UserId == currentUserId && s.BranchId == branchId && s.Status == ShiftStatus.Open, ct);
        if (openShift == null)
        {
            throw new BusinessRuleException("Thu ngân phải mở ca làm việc trước khi nạp tiền.", "shift_required");
        }

        var customer = await _db.Customers
            .Include(c => c.CustomerGroup)
            .Include(c => c.Wallet)
            .FirstOrDefaultAsync(c => c.Id == customerId && c.HomeBranchId == branchId, ct);

        if (customer == null) throw new NotFoundException("Không tìm thấy hội viên.", "customer_not_found");
        if (customer.Status != CustomerStatus.Active)
        {
            throw new BusinessRuleException("Tài khoản hội viên hiện không hoạt động.", "customer_inactive");
        }

        var wallet = customer.Wallet ?? throw new NotFoundException("Không tìm thấy ví của hội viên.", "wallet_not_found");

        decimal balanceBefore = wallet.Balance;
        decimal balanceAfter = balanceBefore + request.Amount;

        wallet.Balance = balanceAfter;
        wallet.UpdatedAtUtc = DateTime.UtcNow;

        var transaction = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            BranchId = openShift.BranchId,
            Type = WalletTransactionType.TopUp,
            Amount = request.Amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceAfter,
            ReferenceType = "Payment",
            Description = request.Note ?? $"Nạp tiền ví qua {request.Method}",
            OccurredAtUtc = DateTime.UtcNow,
            ShiftId = openShift.Id,
            CreatedByUserId = currentUserId
        };

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            BranchId = openShift.BranchId,
            CustomerId = customer.Id,
            ShiftId = openShift.Id,
            PaymentNumber = $"PAY-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
            Type = PaymentType.WalletTopUp,
            Method = request.Method,
            Amount = request.Amount,
            Status = PaymentStatus.Completed,
            PaidAtUtc = DateTime.UtcNow,
            Note = request.Note,
            CreatedByUserId = currentUserId
        };

        _db.WalletTransactions.Add(transaction);
        _db.Payments.Add(payment);

        if (request.Method == PaymentMethod.Cash)
        {
            var cashMovement = new CashMovement
            {
                Id = Guid.NewGuid(),
                ShiftId = openShift.Id,
                Type = CashMovementType.TopUp,
                Amount = request.Amount,
                ReferenceType = "Payment",
                ReferenceId = payment.Id,
                Description = $"Nạp tiền ví hội viên {customer.FullName} ({customer.Code})",
                OccurredAtUtc = DateTime.UtcNow,
                CreatedByUserId = currentUserId
            };
            _db.CashMovements.Add(cashMovement);
        }

        await _db.SaveChangesAsync(ct);
        return MapToDto(customer);
    }

    public async Task<CustomerDto> AdjustWalletAsync(Guid customerId, AdjustWalletRequest request, CancellationToken ct = default)
    {
        if (request.Amount == 0)
        {
            throw new BusinessRuleException("Số tiền điều chỉnh phải khác 0.", "invalid_amount");
        }
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new BusinessRuleException("Vui lòng nhập lý do điều chỉnh số dư ví.", "reason_required");
        }

        var currentUserId = _currentUser.UserId ?? throw new DomainException("Chưa đăng nhập.", "unauthenticated");
        var branchId = await GetBranchIdAsync(ct);

        var customer = await _db.Customers
            .Include(c => c.CustomerGroup)
            .Include(c => c.Wallet)
            .FirstOrDefaultAsync(c => c.Id == customerId && c.HomeBranchId == branchId, ct);

        if (customer == null) throw new NotFoundException("Không tìm thấy hội viên.", "customer_not_found");
        var wallet = customer.Wallet ?? throw new NotFoundException("Không tìm thấy ví hội viên.", "wallet_not_found");

        decimal balanceBefore = wallet.Balance;
        decimal balanceAfter = balanceBefore + request.Amount;

        if (balanceAfter < 0)
        {
            throw new BusinessRuleException("Số dư ví không thể âm sau khi điều chỉnh.", "insufficient_wallet_balance");
        }

        wallet.Balance = balanceAfter;
        wallet.UpdatedAtUtc = DateTime.UtcNow;

        var transaction = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            BranchId = customer.HomeBranchId,
            Type = WalletTransactionType.Adjustment,
            Amount = request.Amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceAfter,
            ReferenceType = "Adjustment",
            Description = $"Điều chỉnh: {request.Reason.Trim()}",
            OccurredAtUtc = DateTime.UtcNow,
            CreatedByUserId = currentUserId
        };

        var audit = new AuditLog
        {
            Id = Guid.NewGuid(),
            BranchId = customer.HomeBranchId,
            UserId = currentUserId,
            Action = "WalletAdjustment",
            EntityType = "Wallet",
            EntityId = wallet.Id,
            OldValuesJson = $"{{\"Balance\": {balanceBefore}}}",
            NewValuesJson = $"{{\"Balance\": {balanceAfter}, \"Amount\": {request.Amount}, \"Reason\": \"{request.Reason.Trim()}\"}}",
            CorrelationId = Guid.NewGuid().ToString("N"),
            OccurredAtUtc = DateTime.UtcNow
        };

        _db.WalletTransactions.Add(transaction);
        _db.AuditLogs.Add(audit);
        await _db.SaveChangesAsync(ct);

        return MapToDto(customer);
    }

    public async Task<CustomerDto> SetMemberPinAsync(Guid customerId, string pin, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pin) || pin.Trim().Length != 6 || !pin.Trim().All(char.IsDigit))
        {
            throw new BusinessRuleException("Mã PIN phải gồm đúng 6 chữ số.", "invalid_pin");
        }

        if (_passwordHasher == null)
        {
            throw new InvalidOperationException("Chưa cấu hình bộ băm mật khẩu.");
        }

        var branchId = await GetBranchIdAsync(ct);
        var customer = await _db.Customers
            .Include(c => c.CustomerGroup)
            .Include(c => c.Wallet)
            .FirstOrDefaultAsync(c => c.Id == customerId && c.HomeBranchId == branchId, ct);

        if (customer == null)
        {
            throw new NotFoundException("Không tìm thấy hội viên.", "customer_not_found");
        }

        customer.MemberPinHash = _passwordHasher.HashPassword(pin.Trim());
        customer.MemberFailedLoginCount = 0;
        customer.MemberLockoutEndUtc = null;
        customer.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapToDto(customer);
    }

    public async Task<IReadOnlyList<StaffMemberTopUpRequestDto>> GetMemberTopUpRequestsAsync(MemberTopUpRequestStatus? status = null, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var query = _db.MemberTopUpRequests
            .AsNoTracking()
            .Include(r => r.Customer)
            .Include(r => r.ProcessedByUser)
            .Where(r => r.BranchId == branchId);

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        var items = await query.OrderByDescending(r => r.CreatedAtUtc).ToListAsync(ct);

        return items.Select(r => new StaffMemberTopUpRequestDto(
            r.Id,
            r.CustomerId,
            r.Customer.Code,
            r.Customer.FullName,
            r.Amount,
            r.Status,
            r.Note,
            r.CreatedAtUtc,
            r.ProcessedAtUtc,
            r.ProcessedByUser?.FullName ?? r.ProcessedByUser?.Username
        )).ToList();
    }

    public async Task<StaffMemberTopUpRequestDto> ApproveMemberTopUpRequestAsync(Guid requestId, CancellationToken ct = default)
    {
        var currentUserId = _currentUser.UserId ?? throw new DomainException("Chưa đăng nhập.", "unauthenticated");
        var branchId = await GetBranchIdAsync(ct);

        // Bắt buộc ca mở của thu ngân
        var openShift = await _db.Shifts.FirstOrDefaultAsync(s => s.UserId == currentUserId && s.BranchId == branchId && s.Status == ShiftStatus.Open, ct);
        if (openShift == null)
        {
            throw new BusinessRuleException("Thu ngân phải mở ca làm việc trước khi duyệt yêu cầu nạp tiền.", "shift_required");
        }

        var req = await _db.MemberTopUpRequests
            .Include(r => r.Customer)
                .ThenInclude(c => c.Wallet)
            .FirstOrDefaultAsync(r => r.Id == requestId && r.BranchId == branchId, ct);

        if (req == null)
        {
            throw new NotFoundException("Không tìm thấy yêu cầu nạp tiền.", "top_up_request_not_found");
        }

        if (req.Status != MemberTopUpRequestStatus.Pending)
        {
            throw new ConflictException("Yêu cầu nạp tiền này đã được xử lý trước đó.", "top_up_request_already_processed");
        }

        var wallet = req.Customer.Wallet ?? throw new NotFoundException("Không tìm thấy ví của hội viên.", "wallet_not_found");

        var now = DateTime.UtcNow;
        decimal balanceBefore = wallet.Balance;
        decimal balanceAfter = balanceBefore + req.Amount;
        wallet.Balance = balanceAfter;
        wallet.UpdatedAtUtc = now;

        var transaction = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            BranchId = branchId,
            Type = WalletTransactionType.TopUp,
            Amount = req.Amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceAfter,
            ReferenceType = "MemberTopUpRequest",
            ReferenceId = req.Id,
            Description = $"Duyệt nạp tiền mặt tại quầy (Yêu cầu {req.Id})",
            OccurredAtUtc = now,
            ShiftId = openShift.Id,
            CreatedByUserId = currentUserId
        };

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            CustomerId = req.CustomerId,
            ShiftId = openShift.Id,
            PaymentNumber = $"PAY-{now:yyyyMMddHHmmssfff}",
            Type = PaymentType.WalletTopUp,
            Method = PaymentMethod.Cash,
            Amount = req.Amount,
            Status = PaymentStatus.Completed,
            PaidAtUtc = now,
            Note = $"Duyệt nạp tiền tại quầy cho hội viên {req.Customer.FullName}",
            CreatedByUserId = currentUserId
        };

        var cashMovement = new CashMovement
        {
            Id = Guid.NewGuid(),
            ShiftId = openShift.Id,
            Type = CashMovementType.TopUp,
            Amount = req.Amount,
            ReferenceType = "MemberTopUpRequest",
            ReferenceId = req.Id,
            Description = $"Thu tiền nạp ví hội viên {req.Customer.FullName} ({req.Customer.Code})",
            OccurredAtUtc = now,
            CreatedByUserId = currentUserId
        };

        req.Status = MemberTopUpRequestStatus.Approved;
        req.ProcessedAtUtc = now;
        req.ProcessedByUserId = currentUserId;
        req.WalletTransactionId = transaction.Id;

        _db.WalletTransactions.Add(transaction);
        _db.Payments.Add(payment);
        _db.CashMovements.Add(cashMovement);

        await _db.SaveChangesAsync(ct);

        var staffUser = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == currentUserId, ct);

        return new StaffMemberTopUpRequestDto(
            req.Id,
            req.CustomerId,
            req.Customer.Code,
            req.Customer.FullName,
            req.Amount,
            req.Status,
            req.Note,
            req.CreatedAtUtc,
            req.ProcessedAtUtc,
            staffUser?.FullName ?? staffUser?.Username
        );
    }

    public async Task<StaffMemberTopUpRequestDto> RejectMemberTopUpRequestAsync(Guid requestId, CancellationToken ct = default)
    {
        var currentUserId = _currentUser.UserId ?? throw new DomainException("Chưa đăng nhập.", "unauthenticated");
        var branchId = await GetBranchIdAsync(ct);

        var req = await _db.MemberTopUpRequests
            .Include(r => r.Customer)
            .FirstOrDefaultAsync(r => r.Id == requestId && r.BranchId == branchId, ct);

        if (req == null)
        {
            throw new NotFoundException("Không tìm thấy yêu cầu nạp tiền.", "top_up_request_not_found");
        }

        if (req.Status != MemberTopUpRequestStatus.Pending)
        {
            throw new ConflictException("Yêu cầu nạp tiền này đã được xử lý trước đó.", "top_up_request_already_processed");
        }

        var now = DateTime.UtcNow;
        req.Status = MemberTopUpRequestStatus.Rejected;
        req.ProcessedAtUtc = now;
        req.ProcessedByUserId = currentUserId;

        await _db.SaveChangesAsync(ct);

        var staffUser = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == currentUserId, ct);

        return new StaffMemberTopUpRequestDto(
            req.Id,
            req.CustomerId,
            req.Customer.Code,
            req.Customer.FullName,
            req.Amount,
            req.Status,
            req.Note,
            req.CreatedAtUtc,
            req.ProcessedAtUtc,
            staffUser?.FullName ?? staffUser?.Username
        );
    }

    private static CustomerDto MapToDto(Customer c)
    {
        return new CustomerDto(
            c.Id,
            c.HomeBranchId,
            c.CustomerGroupId,
            c.CustomerGroup?.Name,
            c.Code,
            c.FullName,
            c.Phone,
            c.Email,
            c.DateOfBirth,
            c.Status,
            c.Wallet?.Balance ?? 0m,
            c.JoinedAtUtc,
            c.Notes
        );
    }
}
