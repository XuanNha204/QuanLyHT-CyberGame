using CyberGame.Application.Common;
using CyberGame.Application.DTOs;
using CyberGame.Application.Services;
using CyberGame.Domain.Entities;
using CyberGame.Domain.Enums;
using CyberGame.Domain.Exceptions;
using CyberGame.Infrastructure.Data;
using CyberGame.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CyberGame.Tests;

public class MemberTestCurrentUserService : ICurrentUserService
{
    public Guid? UserId { get; set; }
    public Guid? BranchId { get; set; }
    public string? Username { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public bool IsAuthenticated => true;
    public bool IsInRole(string role) => Roles.Contains(role);
    public string? ActorType { get; set; } = "member";
    public Guid? CustomerId { get; set; }
    public string? MachineCode { get; set; }
}

public class MemberSessionAndOrderTests
{
    private CyberGameDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<CyberGameDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new CyberGameDbContext(options);
    }

    private static IJwtTokenService CreateJwtTokenService()
    {
        var config = new TestConfiguration(new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = "12345678901234567890123456789012",
            ["Jwt:Issuer"] = "CyberGameServer",
            ["Jwt:Audience"] = "CyberGameClient",
            ["Jwt:ExpiryMinutes"] = "60"
        });
        return new JwtTokenService(config);
    }

    private (Branch branch, Machine machine, Customer customer, Shift shift, PlaySession session, Product product) SetupSessionContext(CyberGameDbContext db)
    {
        var branch = new Branch
        {
            Id = Guid.NewGuid(),
            Code = "CN-01",
            Name = "Chi nhánh 1",
            TimeZoneId = "SE Asia Standard Time",
            IsActive = true
        };
        db.Branches.Add(branch);

        var staffUser = new AppUser
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            Username = "cashier01",
            FullName = "Thu Ngân 01",
            IsActive = true
        };
        db.Users.Add(staffUser);

        var group = new MachineGroup
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            Code = "VIP",
            Name = "Khu VIP",
            IsActive = true
        };
        db.MachineGroups.Add(group);

        var machine = new Machine
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            MachineGroupId = group.Id,
            Code = "PC-01",
            DisplayName = "Máy VIP 01",
            Status = MachineStatus.InUse,
            IsActive = true
        };
        db.Machines.Add(machine);

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            HomeBranchId = branch.Id,
            Code = "KH-001",
            FullName = "Nguyễn Văn Member",
            Status = CustomerStatus.Active
        };
        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Balance = 200000m
        };
        customer.Wallet = wallet;
        db.Customers.Add(customer);
        db.Wallets.Add(wallet);

        var shift = new Shift
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            UserId = staffUser.Id,
            ShiftNumber = "CA-01",
            OpenedAtUtc = DateTime.UtcNow.AddHours(-2),
            OpeningCash = 1000000m,
            Status = ShiftStatus.Open,
            User = staffUser
        };
        db.Shifts.Add(shift);

        var session = new PlaySession
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            MachineId = machine.Id,
            CustomerId = customer.Id,
            ShiftId = shift.Id,
            SessionNumber = "PS-001",
            BillingMode = BillingMode.PrepaidWallet,
            Status = PlaySessionStatus.Open,
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-30), // 30 minutes = 1800 seconds
            TotalPausedSeconds = 0,
            Machine = machine,
            Customer = customer,
            Shift = shift
        };
        db.PlaySessions.Add(session);

        var segment = new SessionChargeSegment
        {
            Id = Guid.NewGuid(),
            PlaySessionId = session.Id,
            StartedAtUtc = session.StartedAtUtc,
            RatePerHour = 36000m // 10 VND per second
        };
        db.SessionChargeSegments.Add(segment);

        var category = new ProductCategory
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            Name = "Nước uống",
            IsActive = true
        };
        db.ProductCategories.Add(category);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            CategoryId = category.Id,
            Sku = "STING-D",
            Name = "Sting dâu",
            SalePrice = 15000m,
            CostPrice = 10000m,
            Unit = "Chai",
            TrackInventory = true,
            IsActive = true
        };
        var inventoryBalance = new InventoryBalance
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            ProductId = product.Id,
            QuantityOnHand = 10
        };
        product.Balance = inventoryBalance;
        db.Products.Add(product);
        db.InventoryBalances.Add(inventoryBalance);

        db.SaveChanges();
        return (branch, machine, customer, shift, session, product);
    }

    [Fact]
    public async Task GetSessionQuote_AccuratelyCalculatesBillableTimeAndCharges()
    {
        using var db = CreateInMemoryDb();
        var (branch, machine, customer, _, session, _) = SetupSessionContext(db);

        var jwtService = CreateJwtTokenService();
        var passwordHasher = new PasswordHasher();
        var currentUser = new MemberTestCurrentUserService
        {
            CustomerId = customer.Id,
            BranchId = branch.Id,
            ActorType = "member",
            MachineCode = machine.Code
        };

        var memberService = new MemberService(db, currentUser, passwordHasher, jwtService);

        var quote = await memberService.GetSessionQuoteAsync();

        Assert.NotNull(quote);
        Assert.Equal(session.Id, quote.SessionId);
        Assert.True(quote.TotalBillableSeconds >= 1800); // 30+ minutes
        Assert.True(quote.UsageAmount > 0);
        Assert.Equal(customer.Wallet!.Balance, quote.WalletBalance);
        Assert.Equal(customer.Wallet.Balance - quote.UsageAmount, quote.AvailableBalance);
    }

    [Fact]
    public async Task GetSessionQuote_WhenPaused_DoesNotIncreaseBillableTime()
    {
        using var db = CreateInMemoryDb();
        var (branch, machine, customer, _, session, _) = SetupSessionContext(db);

        // Pause the session 10 minutes ago
        session.Status = PlaySessionStatus.Paused;
        session.PausedAtUtc = session.StartedAtUtc.AddMinutes(20); // Played for 20 mins = 1200 seconds
        var segment = await db.SessionChargeSegments.FirstAsync(s => s.PlaySessionId == session.Id);
        segment.EndedAtUtc = session.PausedAtUtc;
        segment.BillableSeconds = 1200;
        segment.Amount = 1200 * (36000m / 3600m);
        await db.SaveChangesAsync();

        var jwtService = CreateJwtTokenService();
        var passwordHasher = new PasswordHasher();
        var currentUser = new MemberTestCurrentUserService
        {
            CustomerId = customer.Id,
            BranchId = branch.Id,
            ActorType = "member",
            MachineCode = machine.Code
        };

        var memberService = new MemberService(db, currentUser, passwordHasher, jwtService);

        var quote = await memberService.GetSessionQuoteAsync();

        Assert.Equal(PlaySessionStatus.Paused, quote.Status);
        Assert.Equal(1200, quote.TotalBillableSeconds);
        Assert.Equal(12000m, quote.UsageAmount); // 1200 * 10
    }

    [Fact]
    public async Task CreateOrder_ValidOrder_WaitsForStaffApprovalBeforeDeductingStock()
    {
        using var db = CreateInMemoryDb();
        var (branch, machine, customer, shift, session, product) = SetupSessionContext(db);

        var jwtService = CreateJwtTokenService();
        var passwordHasher = new PasswordHasher();
        var currentUser = new MemberTestCurrentUserService
        {
            CustomerId = customer.Id,
            BranchId = branch.Id,
            ActorType = "member",
            MachineCode = machine.Code
        };

        var memberService = new MemberService(db, currentUser, passwordHasher, jwtService);

        var orderRequest = new CreateMemberOrderRequest(
            new List<MemberOrderItemRequest>
            {
                new(product.Id, 2, "Uống lạnh ít đá")
            }
        );

        var order = await memberService.CreateOrderAsync(orderRequest);

        Assert.NotNull(order);
        Assert.Equal(OrderStatus.Draft, order.Status);
        Assert.Equal(30000m, order.TotalAmount); // 2 * 15,000

        // A request from MemberClient must not change stock before staff approval.
        var updatedBalance = await db.InventoryBalances.FirstOrDefaultAsync(b => b.ProductId == product.Id);
        Assert.Equal(10, updatedBalance!.QuantityOnHand);
        Assert.False(await db.InventoryTransactions.AnyAsync(t => t.ProductId == product.Id));

        var memberQuote = await memberService.GetSessionQuoteAsync();
        Assert.Equal(0m, memberQuote.ServiceAmount);

        var staffUser = new MemberTestCurrentUserService
        {
            UserId = shift.UserId,
            BranchId = branch.Id,
            ActorType = "staff",
            Roles = new[] { "Cashier" }
        };
        var orderService = new OrderService(db, staffUser);
        var confirmed = await orderService.ConfirmOrderAsync(order.Id);

        Assert.Equal(OrderStatus.Confirmed, confirmed.Status);

        // Stock and session service charge change together only after approval.
        updatedBalance = await db.InventoryBalances.FirstOrDefaultAsync(b => b.ProductId == product.Id);
        Assert.Equal(8, updatedBalance!.QuantityOnHand);

        var invTx = await db.InventoryTransactions.FirstOrDefaultAsync(t => t.ProductId == product.Id);
        Assert.NotNull(invTx);
        Assert.Equal(-2, invTx.Quantity);
        Assert.Equal(shift.UserId, invTx.CreatedByUserId);

        var approvedQuote = await memberService.GetSessionQuoteAsync();
        Assert.Equal(30000m, approvedQuote.ServiceAmount);
        Assert.Equal(200000m, customer.Wallet!.Balance);
        Assert.Equal(PaymentStatus.Completed, confirmed.PaymentStatus);
        Assert.Equal(PaymentMethod.Cash, confirmed.PaymentMethod);

        var cashPayment = await db.Payments.SingleAsync(p => p.OrderId == order.Id && p.Type == PaymentType.Payment);
        Assert.Equal(PaymentMethod.Cash, cashPayment.Method);
        Assert.Equal(30000m, cashPayment.Amount);
        Assert.True(await db.CashMovements.AnyAsync(m =>
            m.ReferenceId == order.Id && m.Type == CashMovementType.Sale && m.Amount == 30000m));

        var summary = await memberService.CompleteSessionAsync();
        Assert.Equal(200000m - summary.UsageAmount, customer.Wallet.Balance);
        Assert.Equal(summary.TotalAmount, await db.Payments
            .Where(p => p.InvoiceId == session.InvoiceId && p.Status == PaymentStatus.Completed)
            .SumAsync(p => p.Amount));
    }

    [Fact]
    public async Task CreateOrder_WithWallet_AutoApprovesAndDoesNotChargeFoodTwiceAtSessionEnd()
    {
        using var db = CreateInMemoryDb();
        var (branch, machine, customer, _, session, product) = SetupSessionContext(db);
        var currentUser = new MemberTestCurrentUserService
        {
            CustomerId = customer.Id,
            BranchId = branch.Id,
            ActorType = "member",
            MachineCode = machine.Code
        };
        var memberService = new MemberService(db, currentUser, new PasswordHasher(), CreateJwtTokenService());

        var order = await memberService.CreateOrderAsync(new CreateMemberOrderRequest(
            new List<MemberOrderItemRequest> { new(product.Id, 1, null) },
            PaymentMethod.Wallet));

        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Equal(PaymentStatus.Completed, order.PaymentStatus);
        Assert.Equal(PaymentMethod.Wallet, order.PaymentMethod);
        Assert.Equal(185000m, customer.Wallet!.Balance);
        Assert.Equal(9m, (await db.InventoryBalances.SingleAsync(b => b.ProductId == product.Id)).QuantityOnHand);
        Assert.True(await db.Payments.AnyAsync(p =>
            p.OrderId == order.Id && p.Method == PaymentMethod.Wallet && p.Amount == 15000m));
        Assert.True(await db.WalletTransactions.AnyAsync(t =>
            t.ReferenceId == order.Id && t.Amount == -15000m));

        var quote = await memberService.GetSessionQuoteAsync();
        Assert.Equal(15000m, quote.ServiceAmount);
        Assert.Equal(185000m, quote.WalletBalance);
        Assert.Equal(quote.WalletBalance - quote.UsageAmount, quote.AvailableBalance);

        var summary = await memberService.CompleteSessionAsync();
        Assert.Equal(200000m - 15000m - summary.UsageAmount, customer.Wallet.Balance);
        Assert.Equal(summary.TotalAmount, await db.Payments
            .Where(p => p.InvoiceId == session.InvoiceId && p.Status == PaymentStatus.Completed)
            .SumAsync(p => p.Amount));
    }

    [Fact]
    public async Task CreateOrder_WithAtmOrMomo_IsRejectedUntilGatewayIsImplemented()
    {
        using var db = CreateInMemoryDb();
        var (branch, machine, customer, _, _, product) = SetupSessionContext(db);
        var currentUser = new MemberTestCurrentUserService
        {
            CustomerId = customer.Id,
            BranchId = branch.Id,
            ActorType = "member",
            MachineCode = machine.Code
        };
        var memberService = new MemberService(db, currentUser, new PasswordHasher(), CreateJwtTokenService());

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => memberService.CreateOrderAsync(
            new CreateMemberOrderRequest(
                new List<MemberOrderItemRequest> { new(product.Id, 1, null) },
                PaymentMethod.BankTransfer)));

        Assert.Equal("payment_method_not_available", ex.Code);
        Assert.False(await db.Orders.AnyAsync());
    }

    [Fact]
    public async Task CancelPaidWalletOrder_RefundsWalletAndInventory()
    {
        using var db = CreateInMemoryDb();
        var (branch, machine, customer, shift, _, product) = SetupSessionContext(db);
        var memberUser = new MemberTestCurrentUserService
        {
            CustomerId = customer.Id,
            BranchId = branch.Id,
            ActorType = "member",
            MachineCode = machine.Code
        };
        var memberService = new MemberService(db, memberUser, new PasswordHasher(), CreateJwtTokenService());
        var order = await memberService.CreateOrderAsync(new CreateMemberOrderRequest(
            new List<MemberOrderItemRequest> { new(product.Id, 1, null) },
            PaymentMethod.Wallet));

        var staffUser = new MemberTestCurrentUserService
        {
            UserId = shift.UserId,
            BranchId = branch.Id,
            ActorType = "staff",
            Roles = new[] { "Cashier" }
        };
        var cancelled = await new OrderService(db, staffUser).CancelOrderAsync(order.Id);

        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        Assert.Equal(PaymentStatus.Voided, cancelled.PaymentStatus);
        Assert.Equal(200000m, customer.Wallet!.Balance);
        Assert.Equal(10m, (await db.InventoryBalances.SingleAsync(b => b.ProductId == product.Id)).QuantityOnHand);
        Assert.True(await db.Payments.AnyAsync(p =>
            p.OrderId == order.Id && p.Type == PaymentType.Refund && p.Amount == -15000m));
        Assert.True(await db.WalletTransactions.AnyAsync(t =>
            t.ReferenceId == order.Id && t.Type == WalletTransactionType.Refund && t.Amount == 15000m));
    }

    [Fact]
    public async Task CompleteSession_WithPendingOrder_IsBlocked()
    {
        using var db = CreateInMemoryDb();
        var (branch, machine, customer, _, _, product) = SetupSessionContext(db);
        var currentUser = new MemberTestCurrentUserService
        {
            CustomerId = customer.Id,
            BranchId = branch.Id,
            ActorType = "member",
            MachineCode = machine.Code
        };
        var memberService = new MemberService(db, currentUser, new PasswordHasher(), CreateJwtTokenService());

        await memberService.CreateOrderAsync(new CreateMemberOrderRequest(
            new List<MemberOrderItemRequest> { new(product.Id, 1, null) }));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => memberService.CompleteSessionAsync());
        Assert.Equal("pending_orders_exist", ex.Code);
    }

    [Fact]
    public async Task CreateOrder_InsufficientStock_ThrowsException()
    {
        using var db = CreateInMemoryDb();
        var (branch, machine, customer, _, _, product) = SetupSessionContext(db);

        var jwtService = CreateJwtTokenService();
        var passwordHasher = new PasswordHasher();
        var currentUser = new MemberTestCurrentUserService
        {
            CustomerId = customer.Id,
            BranchId = branch.Id,
            ActorType = "member",
            MachineCode = machine.Code
        };

        var memberService = new MemberService(db, currentUser, passwordHasher, jwtService);

        var orderRequest = new CreateMemberOrderRequest(
            new List<MemberOrderItemRequest>
            {
                new(product.Id, 15, null) // Only 10 in stock
            }
        );

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(async () =>
        {
            await memberService.CreateOrderAsync(orderRequest);
        });

        Assert.Contains("không đủ tồn kho", ex.Message);
    }

    [Fact]
    public async Task CreateOrder_InsufficientAvailableBalance_ThrowsException()
    {
        using var db = CreateInMemoryDb();
        var (branch, machine, customer, _, _, product) = SetupSessionContext(db);

        // Reduce customer balance to very low
        customer.Wallet!.Balance = 20000m;
        await db.SaveChangesAsync();

        var jwtService = CreateJwtTokenService();
        var passwordHasher = new PasswordHasher();
        var currentUser = new MemberTestCurrentUserService
        {
            CustomerId = customer.Id,
            BranchId = branch.Id,
            ActorType = "member",
            MachineCode = machine.Code
        };

        var memberService = new MemberService(db, currentUser, passwordHasher, jwtService);

        // 2 drinks = 30,000 VND > 20,000 VND wallet balance
        var orderRequest = new CreateMemberOrderRequest(
            new List<MemberOrderItemRequest>
            {
                new(product.Id, 2, null)
            },
            PaymentMethod.Wallet
        );

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(async () =>
        {
            await memberService.CreateOrderAsync(orderRequest);
        });

        Assert.Contains("Số dư khả dụng không đủ", ex.Message);
    }

    [Fact]
    public async Task CompleteSession_DeductsWallet_MarksMachineAvailable_GeneratesInvoice()
    {
        using var db = CreateInMemoryDb();
        var (branch, machine, customer, _, session, _) = SetupSessionContext(db);

        var jwtService = CreateJwtTokenService();
        var passwordHasher = new PasswordHasher();
        var currentUser = new MemberTestCurrentUserService
        {
            CustomerId = customer.Id,
            BranchId = branch.Id,
            ActorType = "member",
            MachineCode = machine.Code
        };

        var memberService = new MemberService(db, currentUser, passwordHasher, jwtService);

        var summary = await memberService.CompleteSessionAsync();

        Assert.NotNull(summary);
        Assert.Equal(session.Id, summary.SessionId);
        Assert.True(summary.TotalAmount > 0);
        Assert.NotNull(summary.InvoiceNumber);

        // Verify session status
        var updatedSession = await db.PlaySessions.FindAsync(session.Id);
        Assert.Equal(PlaySessionStatus.Completed, updatedSession!.Status);
        Assert.NotNull(updatedSession.EndedAtUtc);

        // Verify machine status
        var updatedMachine = await db.Machines.FindAsync(machine.Id);
        Assert.Equal(MachineStatus.Available, updatedMachine!.Status);

        // Verify customer wallet deducted
        var updatedWallet = await db.Wallets.FirstOrDefaultAsync(w => w.CustomerId == customer.Id);
        Assert.Equal(200000m - summary.TotalAmount, updatedWallet!.Balance);

        // Verify wallet transaction created
        var walletTx = await db.WalletTransactions.FirstOrDefaultAsync(w => w.WalletId == customer.Wallet!.Id && w.Type == WalletTransactionType.Usage);
        Assert.NotNull(walletTx);
        Assert.Equal(-summary.TotalAmount, walletTx.Amount);

        // Idempotency: Calling complete again on already completed session returns summary safely without deducting twice
        var secondSummary = await memberService.CompleteSessionAsync();
        Assert.Equal(summary.SessionId, secondSummary.SessionId);
        var finalWallet = await db.Wallets.FirstOrDefaultAsync(w => w.CustomerId == customer.Id);
        Assert.Equal(updatedWallet.Balance, finalWallet!.Balance);
    }

    [Fact]
    public async Task CrossMachine_SessionAccess_BlockedByTokenMachineBinding()
    {
        using var db = CreateInMemoryDb();
        var (branch, machine1, customer1, shift, session1, product) = SetupSessionContext(db);

        // Create a second machine PC-02 and customer 2
        var machine2 = new Machine
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            MachineGroupId = machine1.MachineGroupId,
            Code = "PC-02",
            DisplayName = "Máy VIP 02",
            Status = MachineStatus.InUse,
            IsActive = true
        };
        db.Machines.Add(machine2);

        var customer2 = new Customer
        {
            Id = Guid.NewGuid(),
            HomeBranchId = branch.Id,
            Code = "KH-002",
            FullName = "Khách Hàng 02",
            Status = CustomerStatus.Active
        };
        customer2.Wallet = new Wallet { Id = Guid.NewGuid(), CustomerId = customer2.Id, Balance = 500000m };
        db.Customers.Add(customer2);

        var session2 = new PlaySession
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            MachineId = machine2.Id,
            CustomerId = customer2.Id,
            ShiftId = shift.Id,
            SessionNumber = "PS-002",
            BillingMode = BillingMode.PrepaidWallet,
            Status = PlaySessionStatus.Open,
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            Machine = machine2,
            Customer = customer2,
            Shift = shift
        };
        db.PlaySessions.Add(session2);
        db.SessionChargeSegments.Add(new SessionChargeSegment
        {
            Id = Guid.NewGuid(),
            PlaySessionId = session2.Id,
            StartedAtUtc = session2.StartedAtUtc,
            RatePerHour = 36000m
        });
        await db.SaveChangesAsync();

        var jwtService = CreateJwtTokenService();
        var passwordHasher = new PasswordHasher();

        // Customer 1 token bound to PC-02 (where Customer 2 is playing)
        var currentUserImpersonating = new MemberTestCurrentUserService
        {
            CustomerId = customer1.Id,
            BranchId = branch.Id,
            ActorType = "member",
            MachineCode = "PC-02"
        };
        var service = new MemberService(db, currentUserImpersonating, passwordHasher, jwtService);

        // Cannot view active session of another customer on PC-02
        var activeSession = await service.GetActiveSessionAsync();
        Assert.Null(activeSession);

        // Cannot quote another customer's session on PC-02
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetSessionQuoteAsync());

        // Cannot order on PC-02 (no active session for this member on PC-02)
        var orderRequest = new CreateMemberOrderRequest(new List<MemberOrderItemRequest> { new(product.Id, 1, null) });
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateOrderAsync(orderRequest));

        // Cannot complete session on PC-02 (no active session for this member on PC-02)
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CompleteSessionAsync());
    }

    [Fact]
    public async Task GetMe_ThrowsNotFound_WhenTokenMachineNotFound_NeverFallsBackToFirstMachine()
    {
        using var db = CreateInMemoryDb();
        var (branch, machine, customer, _, _, _) = SetupSessionContext(db);

        var jwtService = CreateJwtTokenService();
        var passwordHasher = new PasswordHasher();

        // Token contains a machine code that does not exist in the branch
        var currentUser = new MemberTestCurrentUserService
        {
            CustomerId = customer.Id,
            BranchId = branch.Id,
            ActorType = "member",
            MachineCode = "PC-UNKNOWN-99"
        };
        var service = new MemberService(db, currentUser, passwordHasher, jwtService);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => service.GetMeAsync());
        Assert.Contains("PC-UNKNOWN-99", ex.Message);
    }
}
