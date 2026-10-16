using CyberGame.Application.Common;
using CyberGame.Application.DTOs;
using CyberGame.Domain.Entities;
using CyberGame.Domain.Enums;
using CyberGame.Domain.Exceptions;
using CyberGame.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace CyberGame.Application.Services;

public interface IOrderService
{
    Task<PagedResult<OrderDto>> GetOrdersAsync(OrderStatus? status = null, Guid? sessionId = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<OrderDto> GetOrderByIdAsync(Guid id, CancellationToken ct = default);
    Task<OrderDto> CreateOrderAsync(CreateOrderRequest request, bool autoConfirm = true, CancellationToken ct = default);
    Task<OrderDto> UpdateOrderAsync(Guid id, UpdateOrderRequest request, CancellationToken ct = default);
    Task<OrderDto> ConfirmOrderAsync(Guid id, CancellationToken ct = default);
    Task<OrderDto> FulfillOrderAsync(Guid id, CancellationToken ct = default);
    Task<OrderDto> CancelOrderAsync(Guid id, CancellationToken ct = default);
}

public class OrderService : IOrderService
{
    private readonly ICyberGameDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public OrderService(ICyberGameDbContext db, ICurrentUserService currentUser)
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

    public async Task<PagedResult<OrderDto>> GetOrdersAsync(OrderStatus? status = null, Guid? sessionId = null, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var query = _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.PlaySession).ThenInclude(s => s!.Machine)
            .Include(o => o.Customer)
            .Where(o => o.BranchId == branchId)
            .AsQueryable();

        if (status.HasValue) query = query.Where(o => o.Status == status.Value);
        if (sessionId.HasValue) query = query.Where(o => o.PlaySessionId == sessionId.Value);

        int total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(o => o.CreatedAtUtc)
                               .Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .ToListAsync(ct);

        var dtos = items.Select(MapToDto).ToList();
        return new PagedResult<OrderDto>(dtos, total, page, pageSize);
    }

    public async Task<OrderDto> GetOrderByIdAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.PlaySession).ThenInclude(s => s!.Machine)
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == id && o.BranchId == branchId, ct);

        if (order == null) throw new NotFoundException("Không tìm thấy đơn hàng.", "order_not_found");
        return MapToDto(order);
    }

    public async Task<OrderDto> CreateOrderAsync(CreateOrderRequest request, bool autoConfirm = true, CancellationToken ct = default)
    {
        if (request.Items == null || !request.Items.Any())
        {
            throw new BusinessRuleException("Đơn hàng phải có ít nhất 1 sản phẩm.", "empty_order");
        }

        var currentUserId = _currentUser.UserId ?? throw new DomainException("Chưa đăng nhập.", "unauthenticated");
        var branchId = await GetBranchIdAsync(ct);

        // Kiểm tra ca làm việc mở
        var openShift = await _db.Shifts.FirstOrDefaultAsync(s => s.UserId == currentUserId && s.BranchId == branchId && s.Status == ShiftStatus.Open, ct);
        if (openShift == null)
        {
            throw new BusinessRuleException("Thu ngân phải mở ca làm việc trước khi tạo đơn bán hàng.", "shift_required");
        }

        PlaySession? session = null;
        if (request.PlaySessionId.HasValue)
        {
            session = await _db.PlaySessions.Include(s => s.Machine).FirstOrDefaultAsync(s => s.Id == request.PlaySessionId.Value && s.BranchId == branchId, ct);
            if (session == null) throw new NotFoundException("Không tìm thấy phiên chơi.", "session_not_found");
            if (session.Status != PlaySessionStatus.Open && session.Status != PlaySessionStatus.Paused)
            {
                throw new BusinessRuleException("Phiên chơi đã đóng, không thể gọi thêm món.", "session_closed");
            }
        }

        var now = DateTime.UtcNow;
        var order = new Order
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            OrderNumber = $"ORD-{now:yyyyMMddHHmmssfff}",
            PlaySessionId = request.PlaySessionId,
            CustomerId = request.CustomerId ?? session?.CustomerId,
            ShiftId = openShift.Id,
            Status = OrderStatus.Draft,
            Subtotal = 0m,
            DiscountAmount = 0m,
            TotalAmount = 0m,
            CreatedAtUtc = now
        };

        // Lấy danh sách sản phẩm từ DB
        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Include(p => p.Balance)
            .Where(p => productIds.Contains(p.Id) && p.BranchId == branchId)
            .ToDictionaryAsync(p => p.Id, ct);

        decimal subtotal = 0m;

        foreach (var itemReq in request.Items)
        {
            if (itemReq.Quantity <= 0) continue;
            if (!products.TryGetValue(itemReq.ProductId, out var product))
            {
                throw new NotFoundException($"Không tìm thấy sản phẩm có ID: {itemReq.ProductId}", "product_not_found");
            }

            decimal lineTotal = product.SalePrice * itemReq.Quantity;
            subtotal += lineTotal;

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

        order.Subtotal = subtotal;
        order.TotalAmount = subtotal;

        if (autoConfirm)
        {
            foreach (var item in order.Items)
            {
                if (!item.ProductId.HasValue || !products.TryGetValue(item.ProductId.Value, out var product) || !product.TrackInventory)
                {
                    continue;
                }

                var balance = product.Balance;
                decimal qtyBefore = balance?.QuantityOnHand ?? 0m;
                decimal qtyAfter = qtyBefore - item.Quantity;
                if (qtyAfter < 0)
                {
                    throw new BusinessRuleException(
                        $"Sản phẩm '{product.Name}' không đủ tồn kho (Còn lại: {qtyBefore}, Yêu cầu: {item.Quantity}).",
                        "insufficient_inventory");
                }

                if (balance == null)
                {
                    balance = new InventoryBalance
                    {
                        Id = Guid.NewGuid(),
                        BranchId = branchId,
                        ProductId = product.Id,
                        QuantityOnHand = qtyBefore,
                        UpdatedAtUtc = now
                    };
                    product.Balance = balance;
                    _db.InventoryBalances.Add(balance);
                }

                balance.QuantityOnHand = qtyAfter;
                balance.UpdatedAtUtc = now;

                _db.InventoryTransactions.Add(new InventoryTransaction
                {
                    Id = Guid.NewGuid(),
                    BranchId = branchId,
                    ProductId = product.Id,
                    Type = InventoryTransactionType.Sale,
                    Quantity = -item.Quantity,
                    QuantityBefore = qtyBefore,
                    QuantityAfter = qtyAfter,
                    UnitCost = product.CostPrice,
                    ReferenceType = "Order",
                    ReferenceId = order.Id,
                    Note = $"Xuất bán đơn hàng {order.OrderNumber}",
                    OccurredAtUtc = now,
                    CreatedByUserId = currentUserId
                });
            }

            order.Status = OrderStatus.Confirmed;
            order.ConfirmedAtUtc = now;
            order.UpdatedAtUtc = now;
        }

        // Persist the order, inventory balances and inventory ledger in one
        // SaveChanges transaction. This is compatible with SQL Server's
        // retrying execution strategy and preserves all-or-nothing behavior.
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);
        return await GetOrderByIdAsync(order.Id, ct);
    }

    public async Task<OrderDto> UpdateOrderAsync(Guid id, UpdateOrderRequest request, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id && o.BranchId == branchId, ct);
        if (order == null) throw new NotFoundException("Không tìm thấy đơn hàng.", "order_not_found");
        if (order.Status != OrderStatus.Draft)
        {
            throw new BusinessRuleException("Chỉ có thể sửa đơn hàng ở trạng thái Nháp.", "invalid_order_status");
        }

        order.Items.Clear();

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id) && p.BranchId == branchId).ToDictionaryAsync(p => p.Id, ct);

        decimal subtotal = 0m;
        foreach (var itemReq in request.Items)
        {
            if (itemReq.Quantity <= 0) continue;
            if (!products.TryGetValue(itemReq.ProductId, out var product))
            {
                throw new NotFoundException($"Không tìm thấy sản phẩm có ID: {itemReq.ProductId}", "product_not_found");
            }

            decimal lineTotal = product.SalePrice * itemReq.Quantity;
            subtotal += lineTotal;

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

        order.Subtotal = subtotal;
        order.TotalAmount = subtotal;
        order.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetOrderByIdAsync(id, ct);
    }

    public async Task<OrderDto> ConfirmOrderAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var currentUserId = _currentUser.UserId ?? throw new DomainException("Chưa đăng nhập.", "unauthenticated");
        var order = await _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Include(o => o.Shift)
            .Include(o => o.PlaySession)!
                .ThenInclude(s => s!.ChargeSegments)
            .Include(o => o.PlaySession)!
                .ThenInclude(s => s!.Orders)
            .Include(o => o.PlaySession)!
                .ThenInclude(s => s!.Customer)!
                    .ThenInclude(c => c!.Wallet)
            .FirstOrDefaultAsync(o => o.Id == id && o.BranchId == branchId, ct);

        if (order == null) throw new NotFoundException("Không tìm thấy đơn hàng.", "order_not_found");
        if (order.Status != OrderStatus.Draft)
        {
            throw new BusinessRuleException("Đơn hàng đã được xác nhận trước đó.", "already_confirmed");
        }

        if (order.PaymentMethod == PaymentMethod.BankTransfer)
        {
            throw new BusinessRuleException(
                "Thanh toán ATM/MoMo đang được phát triển và chưa thể duyệt.",
                "payment_method_not_available");
        }

        var now = DateTime.UtcNow;
        if (order.PlaySession != null)
        {
            var session = order.PlaySession;
            if (session.Status != PlaySessionStatus.Open && session.Status != PlaySessionStatus.Paused)
            {
                throw new BusinessRuleException("Phiên chơi đã kết thúc, không thể duyệt đơn món.", "session_closed");
            }

            if (order.Shift == null || order.Shift.Status != ShiftStatus.Open)
            {
                throw new BusinessRuleException("Ca làm việc của phiên đã đóng, không thể duyệt đơn món.", "shift_closed");
            }

            if (order.PaymentMethod == PaymentMethod.Wallet && order.PaymentStatus != PaymentStatus.Completed)
            {
                var walletBalance = session.Customer?.Wallet?.Balance ?? 0m;
                var accruedUsage = CalculateAccruedUsage(session, now);
                var reservedWalletAmount = session.Orders
                    .Where(o => o.Id != order.Id &&
                                o.PaymentMethod == PaymentMethod.Wallet &&
                                o.PaymentStatus != PaymentStatus.Completed &&
                                (o.Status == OrderStatus.Draft || o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Fulfilled))
                    .Sum(o => o.TotalAmount);
                var availableBalance = walletBalance - accruedUsage - reservedWalletAmount;

                if (order.TotalAmount > availableBalance)
                {
                    throw new BusinessRuleException(
                        $"Số dư khả dụng của hội viên không đủ để duyệt đơn (Khả dụng: {Math.Max(0, availableBalance):N0} đ, Đơn món: {order.TotalAmount:N0} đ).",
                        "insufficient_wallet_balance");
                }
            }
        }

        var productIds = order.Items.Where(i => i.ProductId.HasValue).Select(i => i.ProductId!.Value).Distinct().ToList();
        var products = await _db.Products
            .Include(p => p.Balance)
            .Where(p => productIds.Contains(p.Id) && p.BranchId == branchId)
            .ToDictionaryAsync(p => p.Id, ct);

        // Trừ kho cho từng sản phẩm quản lý tồn
        foreach (var item in order.Items)
        {
            if (!item.ProductId.HasValue) continue;
            if (products.TryGetValue(item.ProductId.Value, out var product) && product.TrackInventory)
            {
                if (product.Balance == null)
                {
                    product.Balance = new InventoryBalance
                    {
                        Id = Guid.NewGuid(),
                        BranchId = order.BranchId,
                        ProductId = product.Id,
                        QuantityOnHand = 0m,
                        UpdatedAtUtc = now
                    };
                    _db.InventoryBalances.Add(product.Balance);
                }

                decimal qtyBefore = product.Balance.QuantityOnHand;
                decimal qtyAfter = qtyBefore - item.Quantity;

                if (qtyAfter < 0)
                {
                    throw new BusinessRuleException($"Sản phẩm '{product.Name}' không đủ tồn kho (Còn lại: {qtyBefore}, Yêu cầu: {item.Quantity}).", "insufficient_inventory");
                }

                product.Balance.QuantityOnHand = qtyAfter;
                product.Balance.UpdatedAtUtc = now;

                var invTx = new InventoryTransaction
                {
                    Id = Guid.NewGuid(),
                    BranchId = order.BranchId,
                    ProductId = product.Id,
                    Type = InventoryTransactionType.Sale,
                    Quantity = -item.Quantity,
                    QuantityBefore = qtyBefore,
                    QuantityAfter = qtyAfter,
                    UnitCost = product.CostPrice,
                    ReferenceType = "Order",
                    ReferenceId = order.Id,
                    Note = $"Xuất bán đơn hàng {order.OrderNumber}",
                    OccurredAtUtc = now,
                    CreatedByUserId = currentUserId
                };

                _db.InventoryTransactions.Add(invTx);
            }
        }

        order.Status = OrderStatus.Confirmed;
        order.ConfirmedAtUtc = now;
        order.UpdatedAtUtc = now;

        if (order.PaymentStatus != PaymentStatus.Completed)
        {
            if (order.Shift == null || order.Shift.Status != ShiftStatus.Open)
            {
                throw new BusinessRuleException("Ca làm việc của đơn hàng đã đóng, không thể ghi nhận thanh toán.", "shift_closed");
            }

            if (order.PaymentMethod == PaymentMethod.Wallet)
            {
                var wallet = order.PlaySession?.Customer?.Wallet;
                if (wallet == null || wallet.Balance < order.TotalAmount)
                {
                    throw new BusinessRuleException("Số dư ví hội viên không đủ để thanh toán đơn món.", "insufficient_wallet_balance");
                }

                var balanceBefore = wallet.Balance;
                var balanceAfter = balanceBefore - order.TotalAmount;
                wallet.Balance = balanceAfter;
                wallet.UpdatedAtUtc = now;

                _db.WalletTransactions.Add(new WalletTransaction
                {
                    Id = Guid.NewGuid(),
                    WalletId = wallet.Id,
                    BranchId = order.BranchId,
                    Type = WalletTransactionType.Usage,
                    Amount = -order.TotalAmount,
                    BalanceBefore = balanceBefore,
                    BalanceAfter = balanceAfter,
                    ReferenceType = "Order",
                    ReferenceId = order.Id,
                    Description = $"Thanh toán đơn món {order.OrderNumber}",
                    OccurredAtUtc = now,
                    ShiftId = order.ShiftId,
                    CreatedByUserId = currentUserId
                });
            }

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                BranchId = order.BranchId,
                OrderId = order.Id,
                CustomerId = order.CustomerId,
                ShiftId = order.ShiftId,
                PaymentNumber = $"PAY-{now:yyyyMMddHHmmssfff}",
                Type = PaymentType.Payment,
                Method = order.PaymentMethod,
                Amount = order.TotalAmount,
                Status = PaymentStatus.Completed,
                ExternalReference = order.OrderNumber,
                PaidAtUtc = now,
                Note = order.PaymentMethod == PaymentMethod.Cash
                    ? "Nhân viên xác nhận đã thu tiền mặt và duyệt đơn món."
                    : "Thanh toán đơn món bằng ví hội viên.",
                CreatedByUserId = currentUserId
            };
            _db.Payments.Add(payment);

            if (order.PaymentMethod == PaymentMethod.Cash)
            {
                _db.CashMovements.Add(new CashMovement
                {
                    Id = Guid.NewGuid(),
                    ShiftId = order.ShiftId,
                    Type = CashMovementType.Sale,
                    Amount = order.TotalAmount,
                    ReferenceType = "Order",
                    ReferenceId = order.Id,
                    Description = $"Thu tiền mặt đơn món {order.OrderNumber}",
                    OccurredAtUtc = now,
                    CreatedByUserId = currentUserId
                });
            }

            order.PaymentStatus = PaymentStatus.Completed;
            order.PaidAtUtc = now;
        }

        if (order.PlaySession != null)
        {
            var approvedServiceAmount = order.PlaySession.Orders
                .Where(o => o.Id != order.Id &&
                            (o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Fulfilled))
                .Sum(o => o.TotalAmount) + order.TotalAmount;
            var accruedUsage = CalculateAccruedUsage(order.PlaySession, now);
            order.PlaySession.ServiceAmount = approvedServiceAmount;
            order.PlaySession.TotalAmount = accruedUsage + approvedServiceAmount;
            order.PlaySession.UpdatedAtUtc = now;
        }

        await _db.SaveChangesAsync(ct);
        return await GetOrderByIdAsync(id, ct);
    }

    public async Task<OrderDto> FulfillOrderAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id && o.BranchId == branchId, ct);
        if (order == null) throw new NotFoundException("Không tìm thấy đơn hàng.", "order_not_found");
        if (order.Status != OrderStatus.Confirmed)
        {
            throw new BusinessRuleException("Chỉ có thể hoàn tất phục vụ đơn hàng đã xác nhận.", "invalid_order_status");
        }

        order.Status = OrderStatus.Fulfilled;
        order.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetOrderByIdAsync(id, ct);
    }

    public async Task<OrderDto> CancelOrderAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var currentUserId = _currentUser.UserId ?? throw new DomainException("Chưa đăng nhập.", "unauthenticated");
        var order = await _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Include(o => o.Shift)
            .Include(o => o.Customer)
                .ThenInclude(c => c!.Wallet)
            .Include(o => o.PlaySession)!
                .ThenInclude(s => s!.ChargeSegments)
            .Include(o => o.PlaySession)!
                .ThenInclude(s => s!.Orders)
            .FirstOrDefaultAsync(o => o.Id == id && o.BranchId == branchId, ct);

        if (order == null) throw new NotFoundException("Không tìm thấy đơn hàng.", "order_not_found");
        if (order.Status == OrderStatus.Cancelled)
        {
            throw new BusinessRuleException("Đơn hàng đã được hủy trước đó.", "already_cancelled");
        }

        var now = DateTime.UtcNow;

        // Nếu đơn hàng đã confirmed/fulfilled thì hoàn lại tồn kho
        if (order.Status == OrderStatus.Confirmed || order.Status == OrderStatus.Fulfilled)
        {
            var productIds = order.Items.Where(i => i.ProductId.HasValue).Select(i => i.ProductId!.Value).Distinct().ToList();
            var products = await _db.Products
                .Include(p => p.Balance)
                .Where(p => productIds.Contains(p.Id) && p.BranchId == branchId)
                .ToDictionaryAsync(p => p.Id, ct);

            foreach (var item in order.Items)
            {
                if (!item.ProductId.HasValue) continue;
                if (products.TryGetValue(item.ProductId.Value, out var product) && product.TrackInventory)
                {
                    if (product.Balance != null)
                    {
                        decimal before = product.Balance.QuantityOnHand;
                        decimal after = before + item.Quantity;
                        product.Balance.QuantityOnHand = after;
                        product.Balance.UpdatedAtUtc = now;

                        var invTx = new InventoryTransaction
                        {
                            Id = Guid.NewGuid(),
                            BranchId = order.BranchId,
                            ProductId = product.Id,
                            Type = InventoryTransactionType.Return,
                            Quantity = item.Quantity,
                            QuantityBefore = before,
                            QuantityAfter = after,
                            UnitCost = product.CostPrice,
                            ReferenceType = "OrderCancel",
                            ReferenceId = order.Id,
                            Note = $"Hoàn trả tồn kho do hủy đơn {order.OrderNumber}",
                            OccurredAtUtc = now,
                            CreatedByUserId = currentUserId
                        };

                        _db.InventoryTransactions.Add(invTx);
                    }
                }
            }
        }

        if (order.PaymentStatus == PaymentStatus.Completed)
        {
            var originalPayment = order.Payments
                .Where(p => p.Type == PaymentType.Payment && p.Status == PaymentStatus.Completed)
                .OrderByDescending(p => p.PaidAtUtc)
                .FirstOrDefault();

            if (order.PaymentMethod == PaymentMethod.Wallet)
            {
                var wallet = order.Customer?.Wallet;
                if (wallet == null)
                {
                    throw new BusinessRuleException("Không tìm thấy ví hội viên để hoàn tiền đơn món.", "wallet_not_found");
                }

                var balanceBefore = wallet.Balance;
                var balanceAfter = balanceBefore + order.TotalAmount;
                wallet.Balance = balanceAfter;
                wallet.UpdatedAtUtc = now;

                _db.WalletTransactions.Add(new WalletTransaction
                {
                    Id = Guid.NewGuid(),
                    WalletId = wallet.Id,
                    BranchId = order.BranchId,
                    Type = WalletTransactionType.Refund,
                    Amount = order.TotalAmount,
                    BalanceBefore = balanceBefore,
                    BalanceAfter = balanceAfter,
                    ReferenceType = "OrderCancel",
                    ReferenceId = order.Id,
                    Description = $"Hoàn tiền hủy đơn món {order.OrderNumber}",
                    OccurredAtUtc = now,
                    ShiftId = order.ShiftId,
                    CreatedByUserId = currentUserId
                });
            }
            else if (order.PaymentMethod == PaymentMethod.Cash)
            {
                _db.CashMovements.Add(new CashMovement
                {
                    Id = Guid.NewGuid(),
                    ShiftId = order.ShiftId,
                    Type = CashMovementType.Refund,
                    Amount = order.TotalAmount,
                    ReferenceType = "OrderCancel",
                    ReferenceId = order.Id,
                    Description = $"Hoàn tiền mặt hủy đơn món {order.OrderNumber}",
                    OccurredAtUtc = now,
                    CreatedByUserId = currentUserId
                });
            }

            _db.Payments.Add(new Payment
            {
                Id = Guid.NewGuid(),
                BranchId = order.BranchId,
                OrderId = order.Id,
                CustomerId = order.CustomerId,
                ShiftId = order.ShiftId,
                PaymentNumber = $"REF-{now:yyyyMMddHHmmssfff}",
                Type = PaymentType.Refund,
                Method = order.PaymentMethod,
                Amount = -order.TotalAmount,
                Status = PaymentStatus.Completed,
                ExternalReference = originalPayment?.PaymentNumber ?? order.OrderNumber,
                PaidAtUtc = now,
                Note = $"Hoàn tiền do hủy đơn {order.OrderNumber}",
                CreatedByUserId = currentUserId
            });

            order.PaymentStatus = PaymentStatus.Voided;
        }

        order.Status = OrderStatus.Cancelled;
        order.CancelledAtUtc = now;
        order.UpdatedAtUtc = now;

        if (order.PlaySession != null &&
            (order.PlaySession.Status == PlaySessionStatus.Open || order.PlaySession.Status == PlaySessionStatus.Paused))
        {
            var approvedServiceAmount = order.PlaySession.Orders
                .Where(o => o.Id != order.Id &&
                            (o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Fulfilled))
                .Sum(o => o.TotalAmount);
            var accruedUsage = CalculateAccruedUsage(order.PlaySession, now);
            order.PlaySession.ServiceAmount = approvedServiceAmount;
            order.PlaySession.TotalAmount = accruedUsage + approvedServiceAmount;
            order.PlaySession.UpdatedAtUtc = now;
        }

        await _db.SaveChangesAsync(ct);
        return await GetOrderByIdAsync(id, ct);
    }

    private static decimal CalculateAccruedUsage(PlaySession session, DateTime now)
    {
        decimal amount = 0m;
        foreach (var segment in session.ChargeSegments)
        {
            if (segment.EndedAtUtc.HasValue)
            {
                amount += segment.Amount;
                continue;
            }

            var seconds = Math.Max(0, (int)(now - segment.StartedAtUtc).TotalSeconds);
            amount += SessionPricingCalculator.CalculateUsage(segment.RatePerHour, seconds);
        }

        return amount;
    }

    private static OrderDto MapToDto(Order o)
    {
        return new OrderDto(
            o.Id,
            o.BranchId,
            o.OrderNumber,
            o.PlaySessionId,
            o.PlaySession?.Machine?.Code,
            o.CustomerId,
            o.Customer?.FullName,
            o.ShiftId,
            o.Status,
            o.Subtotal,
            o.DiscountAmount,
            o.TotalAmount,
            o.ConfirmedAtUtc,
            o.CreatedAtUtc,
            o.Items.Select(i => new OrderItemDto(
                i.Id,
                i.ProductId,
                i.ProductName,
                i.Sku,
                i.Unit,
                i.Quantity,
                i.UnitPrice,
                i.DiscountAmount,
                i.LineTotal,
                i.Note
            )).ToList(),
            o.PaymentMethod,
            o.PaymentStatus,
            o.PaidAtUtc
        );
    }
}
