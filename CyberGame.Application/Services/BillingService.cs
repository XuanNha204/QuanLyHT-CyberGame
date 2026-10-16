using CyberGame.Application.Common;
using CyberGame.Application.DTOs;
using CyberGame.Domain.Entities;
using CyberGame.Domain.Enums;
using CyberGame.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CyberGame.Application.Services;

public interface IBillingService
{
    Task<PagedResult<InvoiceDto>> GetInvoicesAsync(InvoiceStatus? status = null, DateTime? date = null, DateTime? fromDate = null, DateTime? toDate = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<InvoiceDto> GetInvoiceByIdAsync(Guid id, CancellationToken ct = default);
    Task<InvoiceDto> AddPaymentAsync(Guid invoiceId, CreatePaymentRequest request, CancellationToken ct = default);
    Task<InvoiceDto> VoidInvoiceAsync(Guid invoiceId, VoidInvoiceRequest request, CancellationToken ct = default);
}

public class BillingService : IBillingService
{
    private readonly ICyberGameDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public BillingService(ICyberGameDbContext db, ICurrentUserService currentUser)
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

    public async Task<PagedResult<InvoiceDto>> GetInvoicesAsync(InvoiceStatus? status = null, DateTime? date = null, DateTime? fromDate = null, DateTime? toDate = null, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var query = _db.Invoices
            .AsNoTracking()
            .Include(i => i.Lines)
            .Include(i => i.Payments).ThenInclude(p => p.CreatedByUser)
            .Include(i => i.Customer)
            .Include(i => i.PlaySession).ThenInclude(s => s!.Machine)
            .Where(i => i.BranchId == branchId)
            .AsQueryable();

        if (status.HasValue) query = query.Where(i => i.Status == status.Value);
        if (fromDate.HasValue || toDate.HasValue)
        {
            if (fromDate.HasValue)
            {
                var start = DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Utc);
                query = query.Where(i => i.IssuedAtUtc >= start);
            }
            if (toDate.HasValue)
            {
                var end = DateTime.SpecifyKind(toDate.Value.Date.AddDays(1), DateTimeKind.Utc);
                query = query.Where(i => i.IssuedAtUtc < end);
            }
        }
        else if (date.HasValue)
        {
            var start = DateTime.SpecifyKind(date.Value.Date, DateTimeKind.Utc);
            var end = start.AddDays(1);
            query = query.Where(i => i.IssuedAtUtc >= start && i.IssuedAtUtc < end);
        }

        int total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(i => i.IssuedAtUtc)
                               .Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .ToListAsync(ct);

        var dtos = items.Select(MapToDto).ToList();
        return new PagedResult<InvoiceDto>(dtos, total, page, pageSize);
    }

    public async Task<InvoiceDto> GetInvoiceByIdAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var invoice = await _db.Invoices
            .AsNoTracking()
            .Include(i => i.Lines)
            .Include(i => i.Payments).ThenInclude(p => p.CreatedByUser)
            .Include(i => i.Customer)
            .Include(i => i.PlaySession).ThenInclude(s => s!.Machine)
            .FirstOrDefaultAsync(i => i.Id == id && i.BranchId == branchId, ct);

        if (invoice == null) throw new NotFoundException("Không tìm thấy hóa đơn.", "invoice_not_found");
        return MapToDto(invoice);
    }

    public async Task<InvoiceDto> AddPaymentAsync(Guid invoiceId, CreatePaymentRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0)
        {
            throw new BusinessRuleException("Số tiền thanh toán phải lớn hơn 0.", "invalid_payment_amount");
        }

        var currentUserId = _currentUser.UserId ?? throw new DomainException("Chưa đăng nhập.", "unauthenticated");
        var branchId = await GetBranchIdAsync(ct);

        // Kiểm tra ca làm việc mở của thu ngân
        var openShift = await _db.Shifts.FirstOrDefaultAsync(s => s.UserId == currentUserId && s.BranchId == branchId && s.Status == ShiftStatus.Open, ct);
        if (openShift == null)
        {
            throw new BusinessRuleException("Thu ngân phải mở ca làm việc trước khi thực hiện thanh toán.", "shift_required");
        }

        var invoice = await _db.Invoices
            .Include(i => i.Customer).ThenInclude(c => c!.Wallet)
            .Include(i => i.Payments)
            .Include(i => i.Lines)
            .Include(i => i.PlaySession).ThenInclude(s => s!.Machine)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.BranchId == branchId, ct);

        if (invoice == null) throw new NotFoundException("Không tìm thấy hóa đơn.", "invoice_not_found");
        if (invoice.Status == InvoiceStatus.Paid)
        {
            throw new BusinessRuleException("Hóa đơn này đã được thanh toán đầy đủ.", "invoice_already_paid");
        }
        if (invoice.Status == InvoiceStatus.Voided)
        {
            throw new BusinessRuleException("Không thể thanh toán hóa đơn đã bị hủy bỏ.", "invoice_voided");
        }

        decimal remaining = invoice.TotalAmount - invoice.PaidAmount;
        if (remaining <= 0)
        {
            invoice.Status = InvoiceStatus.Paid;
            await _db.SaveChangesAsync(ct);
            return MapToDto(invoice);
        }

        decimal paymentAmount = request.Amount;
        decimal changeAmount = 0m;
        if (paymentAmount > remaining)
        {
            // Nếu trả dư bằng tiền mặt thì tính tiền thừa
            if (request.Method == PaymentMethod.Cash)
            {
                changeAmount = paymentAmount - remaining;
                paymentAmount = remaining;
                invoice.ChangeAmount += changeAmount;
            }
            else
            {
                paymentAmount = remaining;
            }
        }

        var now = DateTime.UtcNow;

        // Nếu thanh toán bằng ví, trừ ví hội viên
        if (request.Method == PaymentMethod.Wallet)
        {
            var customer = invoice.Customer;
            if (customer?.Wallet == null)
            {
                throw new BusinessRuleException("Hóa đơn không gắn với hội viên có ví tiền.", "no_wallet");
            }

            if (customer.Wallet.Balance < paymentAmount)
            {
                throw new BusinessRuleException($"Số dư ví không đủ (Số dư: {customer.Wallet.Balance:N0}đ, Cần thanh toán: {paymentAmount:N0}đ).", "insufficient_wallet_balance");
            }

            decimal balBefore = customer.Wallet.Balance;
            decimal balAfter = balBefore - paymentAmount;
            customer.Wallet.Balance = balAfter;
            customer.Wallet.UpdatedAtUtc = now;

            var walletTx = new WalletTransaction
            {
                Id = Guid.NewGuid(),
                WalletId = customer.Wallet.Id,
                BranchId = invoice.BranchId,
                Type = WalletTransactionType.Usage,
                Amount = -paymentAmount,
                BalanceBefore = balBefore,
                BalanceAfter = balAfter,
                ReferenceType = "Invoice",
                ReferenceId = invoice.Id,
                Description = $"Thanh toán hóa đơn {invoice.InvoiceNumber}",
                OccurredAtUtc = now,
                ShiftId = openShift.Id,
                CreatedByUserId = currentUserId
            };

            _db.WalletTransactions.Add(walletTx);
        }

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            BranchId = invoice.BranchId,
            InvoiceId = invoice.Id,
            CustomerId = invoice.CustomerId,
            ShiftId = openShift.Id,
            PaymentNumber = $"PAY-{now:yyyyMMddHHmmssfff}",
            Type = PaymentType.Payment,
            Method = request.Method,
            Amount = paymentAmount,
            Status = PaymentStatus.Completed,
            PaidAtUtc = now,
            Note = request.Note,
            CreatedByUserId = currentUserId
        };

        invoice.Payments.Add(payment);
        invoice.PaidAmount += paymentAmount;

        if (invoice.PaidAmount >= invoice.TotalAmount)
        {
            invoice.Status = InvoiceStatus.Paid;
            invoice.PaidAtUtc = now;
        }

        if (request.Method == PaymentMethod.Cash)
        {
            var cashMovement = new CashMovement
            {
                Id = Guid.NewGuid(),
                ShiftId = openShift.Id,
                Type = CashMovementType.Sale,
                Amount = paymentAmount,
                ReferenceType = "Invoice",
                ReferenceId = invoice.Id,
                Description = $"Thu tiền mặt hóa đơn {invoice.InvoiceNumber}",
                OccurredAtUtc = now,
                CreatedByUserId = currentUserId
            };
            _db.CashMovements.Add(cashMovement);
        }

        await _db.SaveChangesAsync(ct);
        return MapToDto(invoice);
    }

    public async Task<InvoiceDto> VoidInvoiceAsync(Guid invoiceId, VoidInvoiceRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new BusinessRuleException("Vui lòng cung cấp lý do hủy hóa đơn.", "reason_required");
        }

        var currentUserId = _currentUser.UserId ?? throw new DomainException("Chưa đăng nhập.", "unauthenticated");
        var branchId = await GetBranchIdAsync(ct);

        var invoice = await _db.Invoices
            .Include(i => i.Payments)
            .Include(i => i.Lines)
            .Include(i => i.Customer).ThenInclude(c => c!.Wallet)
            .Include(i => i.PlaySession).ThenInclude(s => s!.Machine)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.BranchId == branchId, ct);

        if (invoice == null) throw new NotFoundException("Không tìm thấy hóa đơn.", "invoice_not_found");
        if (invoice.Status == InvoiceStatus.Voided)
        {
            throw new BusinessRuleException("Hóa đơn này đã được hủy trước đó.", "already_voided");
        }

        var now = DateTime.UtcNow;

        // Hoàn lại tiền ví nếu đã thanh toán bằng ví
        var walletPayments = invoice.Payments.Where(p => p.Method == PaymentMethod.Wallet && p.Status == PaymentStatus.Completed).ToList();
        if (walletPayments.Any() && invoice.Customer?.Wallet != null)
        {
            var wallet = invoice.Customer.Wallet;
            foreach (var p in walletPayments)
            {
                decimal balBefore = wallet.Balance;
                decimal balAfter = balBefore + p.Amount;
                wallet.Balance = balAfter;
                wallet.UpdatedAtUtc = now;

                var walletTx = new WalletTransaction
                {
                    Id = Guid.NewGuid(),
                    WalletId = wallet.Id,
                    BranchId = invoice.BranchId,
                    Type = WalletTransactionType.Refund,
                    Amount = p.Amount,
                    BalanceBefore = balBefore,
                    BalanceAfter = balAfter,
                    ReferenceType = "InvoiceVoid",
                    ReferenceId = invoice.Id,
                    Description = $"Hoàn tiền hủy hóa đơn {invoice.InvoiceNumber}: {request.Reason.Trim()}",
                    OccurredAtUtc = now,
                    CreatedByUserId = currentUserId
                };
                _db.WalletTransactions.Add(walletTx);
            }
        }

        invoice.Status = InvoiceStatus.Voided;
        invoice.VoidedAtUtc = now;
        invoice.VoidReason = request.Reason.Trim();

        var audit = new AuditLog
        {
            Id = Guid.NewGuid(),
            BranchId = invoice.BranchId,
            UserId = currentUserId,
            Action = "VoidInvoice",
            EntityType = "Invoice",
            EntityId = invoice.Id,
            NewValuesJson = $"{{\"Reason\": \"{request.Reason.Trim()}\", \"PaidAmount\": {invoice.PaidAmount}}}",
            CorrelationId = Guid.NewGuid().ToString("N"),
            OccurredAtUtc = now
        };

        _db.AuditLogs.Add(audit);
        await _db.SaveChangesAsync(ct);

        return MapToDto(invoice);
    }

    private static InvoiceDto MapToDto(Invoice i)
    {
        return new InvoiceDto(
            i.Id,
            i.BranchId,
            i.InvoiceNumber,
            i.CustomerId,
            i.Customer?.FullName,
            i.PlaySessionId,
            i.PlaySession?.Machine?.Code,
            i.ShiftId,
            i.Status,
            i.Subtotal,
            i.DiscountAmount,
            i.TotalAmount,
            i.PaidAmount,
            i.ChangeAmount,
            i.IssuedAtUtc,
            i.PaidAtUtc,
            i.Lines.Select(l => new InvoiceLineDto(l.Id, l.LineType, l.Description, l.Quantity, l.UnitPrice, l.DiscountAmount, l.LineTotal)).ToList(),
            i.Payments.Select(p => new PaymentDto(p.Id, p.PaymentNumber, p.Type, p.Method, p.Amount, p.Status, p.PaidAtUtc, p.Note, p.CreatedByUser?.FullName ?? "System")).ToList()
        );
    }
}
