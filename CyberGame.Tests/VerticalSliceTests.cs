using CyberGame.Application.Common;
using CyberGame.Application.DTOs;
using CyberGame.Application.Services;
using CyberGame.Domain.Entities;
using CyberGame.Domain.Enums;
using CyberGame.Infrastructure.Data;
using CyberGame.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CyberGame.Tests;

public class TestCurrentUserService : ICurrentUserService
{
    public Guid? UserId { get; set; }
    public Guid? BranchId { get; set; }
    public string? Username { get; set; } = "test_cashier";
    public IReadOnlyList<string> Roles { get; set; } = new[] { "Cashier" };
    public bool IsAuthenticated => true;
    public bool IsInRole(string role) => Roles.Contains(role);
}

public class VerticalSliceTests
{
    private CyberGameDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<CyberGameDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new CyberGameDbContext(options);
    }

    [Fact]
    public async Task CompleteVerticalSlice_EndToEnd_WorksAccurately()
    {
        using var db = CreateInMemoryDb();
        var currentUser = new TestCurrentUserService();
        var passwordHasher = new PasswordHasher();

        // 1. Setup Base Data
        var branch = new Branch
        {
            Id = Guid.NewGuid(),
            Code = "CG-TEST",
            Name = "CyberGame Test Branch",
            TimeZoneId = "SE Asia Standard Time",
            IsActive = true
        };
        db.Branches.Add(branch);

        var cashierUser = new AppUser
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            Username = "cashier01",
            NormalizedUsername = "CASHIER01",
            FullName = "Nguyễn Thu Ngân",
            PasswordHash = passwordHasher.HashPassword("Cashier@123"),
            IsActive = true
        };
        db.Users.Add(cashierUser);

        currentUser.UserId = cashierUser.Id;
        currentUser.BranchId = branch.Id;

        var stdGroup = new MachineGroup
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            Code = "STD",
            Name = "Khu Tiêu Chuẩn",
            SortOrder = 1,
            IsActive = true
        };
        db.MachineGroups.Add(stdGroup);

        var machine = new Machine
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            MachineGroupId = stdGroup.Id,
            Code = "PC-01",
            DisplayName = "Máy 01",
            Status = MachineStatus.Available,
            IsActive = true
        };
        db.Machines.Add(machine);

        var pricingPlan = new PricingPlan
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            MachineGroupId = stdGroup.Id,
            Name = "Bảng giá 10k",
            EffectiveFromUtc = DateTime.UtcNow.AddDays(-1),
            IsDefault = true,
            IsActive = true
        };
        pricingPlan.PricingRules.Add(new PricingRule
        {
            Id = Guid.NewGuid(),
            PricingPlanId = pricingPlan.Id,
            DayOfWeekMask = 127,
            StartTime = TimeSpan.Zero,
            EndTime = new TimeSpan(23, 59, 59),
            HourlyRate = 10000m,
            Priority = 1
        });
        db.PricingPlans.Add(pricingPlan);

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            HomeBranchId = branch.Id,
            Code = "KH-TEST",
            FullName = "Khách Hàng Test",
            Phone = "0988888888",
            Status = CustomerStatus.Active
        };
        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Balance = 0m,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.Customers.Add(customer);
        db.Wallets.Add(wallet);

        var category = new ProductCategory
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            Name = "Đồ ăn & Nước uống",
            SortOrder = 1,
            IsActive = true
        };
        db.ProductCategories.Add(category);

        var sting = new Product
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            CategoryId = category.Id,
            Sku = "STING",
            Name = "Nước ngọt Sting",
            Unit = "chai",
            SalePrice = 12000m,
            CostPrice = 8000m,
            TrackInventory = true,
            ReorderLevel = 5,
            IsActive = true
        };
        var stingBal = new InventoryBalance
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            ProductId = sting.Id,
            QuantityOnHand = 50m,
            UpdatedAtUtc = DateTime.UtcNow
        };
        sting.Balance = stingBal;

        var miTom = new Product
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            CategoryId = category.Id,
            Sku = "MITOM",
            Name = "Mì tôm trứng",
            Unit = "tô",
            SalePrice = 25000m,
            CostPrice = 12000m,
            TrackInventory = true,
            ReorderLevel = 5,
            IsActive = true
        };
        var miTomBal = new InventoryBalance
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            ProductId = miTom.Id,
            QuantityOnHand = 30m,
            UpdatedAtUtc = DateTime.UtcNow
        };
        miTom.Balance = miTomBal;

        db.Products.AddRange(sting, miTom);
        db.InventoryBalances.AddRange(stingBal, miTomBal);
        await db.SaveChangesAsync();

        // 2. Instantiate Services
        var branchService = new BranchMachineService(db, currentUser);
        var pricingService = new PricingService(db, currentUser);
        var customerWalletService = new CustomerWalletService(db, currentUser);
        var sessionService = new SessionService(db, currentUser, pricingService);
        var catalogService = new CatalogInventoryService(db, currentUser);
        var orderService = new OrderService(db, currentUser);
        var billingService = new BillingService(db, currentUser);
        var shiftService = new ShiftService(db, currentUser);

        // --- STEP 1: Mở ca làm việc với 500.000đ tiền đầu ca ---
        var openShift = await shiftService.OpenShiftAsync(new OpenShiftRequest(500000m));
        Assert.NotNull(openShift);
        Assert.Equal(ShiftStatus.Open, openShift.Status);
        Assert.Equal(500000m, openShift.OpeningCash);

        // --- STEP 2: Nạp tiền ví hội viên 200.000đ tiền mặt ---
        var updatedCust = await customerWalletService.TopUpWalletAsync(customer.Id, new TopUpWalletRequest(200000m, PaymentMethod.Cash, "Nạp ví test"));
        Assert.Equal(200000m, updatedCust.WalletBalance);

        // Kiểm tra ledger ví
        var walletTxs = await customerWalletService.GetWalletTransactionsAsync(customer.Id);
        Assert.Single(walletTxs.Items);
        Assert.Equal(WalletTransactionType.TopUp, walletTxs.Items[0].Type);
        Assert.Equal(200000m, walletTxs.Items[0].Amount);

        // --- STEP 3: Mở phiên chơi trên PC-01 bằng ví trả trước ---
        var session = await sessionService.StartSessionAsync(new StartSessionRequest(machine.Id, customer.Id, BillingMode.PrepaidWallet));
        Assert.NotNull(session);
        Assert.Equal(PlaySessionStatus.Open, session.Status);

        // Kiểm tra máy chuyển sang InUse
        var machineCheck = await branchService.GetMachineByIdAsync(machine.Id);
        Assert.Equal(MachineStatus.InUse, machineCheck.Status);

        // --- STEP 4: Gọi đồ ăn/nước uống (Sting x 1, Mì tôm x 1) gắn với phiên chơi ---
        var order = await orderService.CreateOrderAsync(new CreateOrderRequest(
            session.Id,
            customer.Id,
            new List<CreateOrderItemRequest>
            {
                new(sting.Id, 1, "Ít đá"),
                new(miTom.Id, 1, "Trứng lòng đào")
            }
        ), autoConfirm: true);

        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Equal(37000m, order.TotalAmount); // 12k + 25k = 37k

        // Kiểm tra tồn kho đã tự động bị trừ
        var inventoryCheck = await catalogService.GetInventoryAsync();
        var stingStock = inventoryCheck.First(i => i.ProductId == sting.Id);
        var miTomStock = inventoryCheck.First(i => i.ProductId == miTom.Id);
        Assert.Equal(49m, stingStock.QuantityOnHand); // 50 - 1 = 49
        Assert.Equal(29m, miTomStock.QuantityOnHand); // 30 - 1 = 29

        // --- STEP 5: Kết thúc phiên chơi ---
        var invoice = await sessionService.CompleteSessionAsync(session.Id, new CompleteSessionRequest(true));
        Assert.NotNull(invoice);
        Assert.True(invoice.TotalAmount >= 37000m); // Tiền giờ + 37k tiền dịch vụ

        // Máy trạm phải quay về trạng thái Available
        var machineAfter = await branchService.GetMachineByIdAsync(machine.Id);
        Assert.Equal(MachineStatus.Available, machineAfter.Status);

        // Khách dùng PrepaidWallet nên ví đã tự động được trừ
        var customerAfter = await customerWalletService.GetCustomerByIdAsync(customer.Id);
        Assert.True(customerAfter.WalletBalance < 200000m);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);

        // --- STEP 6: Đóng ca thu ngân ---
        // Tiền kỳ vọng = 500k (đầu ca) + 200k (nạp ví tiền mặt) = 700k
        var shiftSummary = await shiftService.GetShiftSummaryAsync(openShift.Id);
        Assert.Equal(200000m, shiftSummary.TotalCashTopUps);
        Assert.Equal(700000m, shiftSummary.ExpectedCash);

        var closedShift = await shiftService.CloseShiftAsync(openShift.Id, new CloseShiftRequest(700000m, "Ca chuẩn"));
        Assert.Equal(ShiftStatus.Closed, closedShift.Status);
        Assert.Equal(0m, closedShift.Difference);
    }
}
