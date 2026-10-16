using CyberGame.Application.Common;
using CyberGame.Domain.Entities;
using CyberGame.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CyberGame.Infrastructure.Data;

public class DbInitializer
{
    private readonly CyberGameDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IConfiguration _config;
    private readonly ILogger<DbInitializer> _logger;

    public DbInitializer(
        CyberGameDbContext db,
        IPasswordHasher passwordHasher,
        IConfiguration config,
        ILogger<DbInitializer> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _config = config;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Bắt đầu khởi tạo cơ sở dữ liệu và seed dữ liệu mặc định...");

        // Migrate database
        await _db.Database.MigrateAsync(ct);

        bool seedEnabled = bool.TryParse(_config["SeedData:Enabled"], out bool enabled) && enabled;
        if (!seedEnabled)
        {
            _logger.LogInformation("SeedData:Enabled là false hoặc chưa được cấu hình. Bỏ qua khởi tạo dữ liệu mẫu.");
            return;
        }

        // 1. Roles
        var roleOwner = await EnsureRoleAsync("Owner", "Chủ phòng game", ct);
        var roleManager = await EnsureRoleAsync("Manager", "Quản lý chi nhánh", ct);
        var roleCashier = await EnsureRoleAsync("Cashier", "Thu ngân", ct);
        var roleTechnician = await EnsureRoleAsync("Technician", "Kỹ thuật viên", ct);

        // 2. Branch
        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.Code == "CG-TT01", ct);
        if (branch == null)
        {
            branch = new Branch
            {
                Id = Guid.NewGuid(),
                Code = "CG-TT01",
                Name = "CyberGame Trung Tâm",
                Address = "Số 123 Đường Công Nghệ, Quận 1, TP. Hồ Chí Minh",
                TimeZoneId = "SE Asia Standard Time",
                Phone = "028.3899.9999",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.Branches.Add(branch);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Đã seed chi nhánh: {BranchName}", branch.Name);
        }

        // 3. Seed Users with Configurable Development Credentials
        // Lấy từ config: SeedCredentials:Users:... Không có fallback hardcode mật khẩu
        string ownerPass = _config["SeedCredentials:Users:OwnerPassword"] 
            ?? throw new InvalidOperationException("Thiếu cấu hình mật khẩu cho tài khoản Owner (SeedCredentials:Users:OwnerPassword).");
        string cashierPass = _config["SeedCredentials:Users:CashierPassword"] 
            ?? throw new InvalidOperationException("Thiếu cấu hình mật khẩu cho tài khoản Cashier (SeedCredentials:Users:CashierPassword).");
        string techPass = _config["SeedCredentials:Users:TechPassword"] 
            ?? throw new InvalidOperationException("Thiếu cấu hình mật khẩu cho tài khoản Tech (SeedCredentials:Users:TechPassword).");

        var userOwner = await EnsureUserAsync("owner", "Quản Trị Viên Chủ Quán", ownerPass, branch.Id, new[] { roleOwner, roleManager }, ct);
        var userCashier = await EnsureUserAsync("cashier", "Thu Ngân Ca Sáng", cashierPass, branch.Id, new[] { roleCashier }, ct);
        var userTech = await EnsureUserAsync("tech", "Kỹ Thuật Viên Phần Cứng", techPass, branch.Id, new[] { roleTechnician }, ct);

        // 4. MachineGroups
        var groupStandard = await _db.MachineGroups.FirstOrDefaultAsync(g => g.BranchId == branch.Id && g.Code == "STD", ct);
        if (groupStandard == null)
        {
            groupStandard = new MachineGroup
            {
                Id = Guid.NewGuid(),
                BranchId = branch.Id,
                Code = "STD",
                Name = "Khu Tiêu Chuẩn (Standard)",
                SortOrder = 1,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.MachineGroups.Add(groupStandard);
        }

        var groupVip = await _db.MachineGroups.FirstOrDefaultAsync(g => g.BranchId == branch.Id && g.Code == "VIP", ct);
        if (groupVip == null)
        {
            groupVip = new MachineGroup
            {
                Id = Guid.NewGuid(),
                BranchId = branch.Id,
                Code = "VIP",
                Name = "Khu VIP Thi Đấu (VIP)",
                SortOrder = 2,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.MachineGroups.Add(groupVip);
        }
        await _db.SaveChangesAsync(ct);

        // 5. Machines (12 máy PC-01 -> PC-12)
        if (!await _db.Machines.AnyAsync(m => m.BranchId == branch.Id, ct))
        {
            for (int i = 1; i <= 12; i++)
            {
                string code = $"PC-{i:D2}";
                var group = i <= 8 ? groupStandard : groupVip;
                var machine = new Machine
                {
                    Id = Guid.NewGuid(),
                    BranchId = branch.Id,
                    MachineGroupId = group.Id,
                    Code = code,
                    DisplayName = $"Máy {i:D2} ({group.Name.Split(' ')[1]})",
                    IpAddress = $"192.168.1.{100 + i}",
                    MacAddress = $"00:1A:2B:3C:4D:{i:X2}",
                    Status = MachineStatus.Available,
                    Notes = i <= 8 ? "Cấu hình: i5-13400F, RTX 4060, 16GB RAM, Màn hình 240Hz" : "Cấu hình VIP: i7-14700K, RTX 4080, 32GB RAM, Màn hình 360Hz",
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                };
                _db.Machines.Add(machine);
            }
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Đã seed 12 máy trạm PC-01 -> PC-12.");
        }

        // 6. Pricing Plans & Rules
        if (!await _db.PricingPlans.AnyAsync(p => p.BranchId == branch.Id, ct))
        {
            var planStd = new PricingPlan
            {
                Id = Guid.NewGuid(),
                BranchId = branch.Id,
                MachineGroupId = groupStandard.Id,
                Name = "Bảng giá Khu Standard - 10.000đ/h",
                EffectiveFromUtc = DateTime.UtcNow.AddYears(-1),
                IsDefault = true,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            planStd.PricingRules.Add(new PricingRule
            {
                Id = Guid.NewGuid(),
                PricingPlanId = planStd.Id,
                DayOfWeekMask = 127,
                StartTime = TimeSpan.Zero,
                EndTime = new TimeSpan(23, 59, 59),
                HourlyRate = 10000m,
                Priority = 1,
                CreatedAtUtc = DateTime.UtcNow
            });

            var planVip = new PricingPlan
            {
                Id = Guid.NewGuid(),
                BranchId = branch.Id,
                MachineGroupId = groupVip.Id,
                Name = "Bảng giá Khu VIP - 15.000đ/h",
                EffectiveFromUtc = DateTime.UtcNow.AddYears(-1),
                IsDefault = false,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            planVip.PricingRules.Add(new PricingRule
            {
                Id = Guid.NewGuid(),
                PricingPlanId = planVip.Id,
                DayOfWeekMask = 127,
                StartTime = TimeSpan.Zero,
                EndTime = new TimeSpan(23, 59, 59),
                HourlyRate = 15000m,
                Priority = 2,
                CreatedAtUtc = DateTime.UtcNow
            });

            _db.PricingPlans.AddRange(planStd, planVip);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Đã seed bảng giá: Standard 10.000đ/h, VIP 15.000đ/h.");
        }

        // 7. Customer Groups & Customers
        var groupCustVip = await _db.CustomerGroups.FirstOrDefaultAsync(g => g.BranchId == branch.Id && g.Name == "Hội viên VIP", ct);
        if (groupCustVip == null)
        {
            groupCustVip = new CustomerGroup
            {
                Id = Guid.NewGuid(),
                BranchId = branch.Id,
                Name = "Hội viên VIP",
                DiscountPercent = 10m,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.CustomerGroups.Add(groupCustVip);
        }

        var groupCustStd = await _db.CustomerGroups.FirstOrDefaultAsync(g => g.BranchId == branch.Id && g.Name == "Hội viên Thường", ct);
        if (groupCustStd == null)
        {
            groupCustStd = new CustomerGroup
            {
                Id = Guid.NewGuid(),
                BranchId = branch.Id,
                Name = "Hội viên Thường",
                DiscountPercent = 0m,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.CustomerGroups.Add(groupCustStd);
        }
        await _db.SaveChangesAsync(ct);

        string defaultMemberPin = _config["SeedCredentials:Members:DefaultPin"] ?? "123456";
        string defaultPinHash = _passwordHasher.HashPassword(defaultMemberPin);

        if (!await _db.Customers.AnyAsync(c => c.HomeBranchId == branch.Id, ct))
        {
            // Customer 1: Nguyễn Văn An - 100k ví
            var c1 = new Customer
            {
                Id = Guid.NewGuid(),
                HomeBranchId = branch.Id,
                CustomerGroupId = groupCustVip.Id,
                Code = "KH-001",
                FullName = "Nguyễn Văn An",
                Phone = "0901234567",
                Email = "an.nguyen@example.com",
                DateOfBirth = new DateOnly(2000, 5, 15),
                Status = CustomerStatus.Active,
                MemberPinHash = defaultPinHash,
                MemberFailedLoginCount = 0,
                JoinedAtUtc = DateTime.UtcNow.AddMonths(-3),
                CreatedAtUtc = DateTime.UtcNow
            };
            var w1 = new Wallet
            {
                Id = Guid.NewGuid(),
                CustomerId = c1.Id,
                Balance = 100000m,
                UpdatedAtUtc = DateTime.UtcNow
            };
            w1.Transactions.Add(new WalletTransaction
            {
                Id = Guid.NewGuid(),
                WalletId = w1.Id,
                BranchId = branch.Id,
                Type = WalletTransactionType.TopUp,
                Amount = 100000m,
                BalanceBefore = 0m,
                BalanceAfter = 100000m,
                Description = "Nạp tiền ban đầu khi mở tài khoản",
                OccurredAtUtc = DateTime.UtcNow.AddMonths(-3),
                CreatedByUserId = userOwner.Id
            });

            // Customer 2: Trần Thị Bình - 50k ví
            var c2 = new Customer
            {
                Id = Guid.NewGuid(),
                HomeBranchId = branch.Id,
                CustomerGroupId = groupCustStd.Id,
                Code = "KH-002",
                FullName = "Trần Thị Bình",
                Phone = "0912345678",
                Email = "binh.tran@example.com",
                DateOfBirth = new DateOnly(2002, 8, 20),
                Status = CustomerStatus.Active,
                MemberPinHash = defaultPinHash,
                MemberFailedLoginCount = 0,
                JoinedAtUtc = DateTime.UtcNow.AddMonths(-1),
                CreatedAtUtc = DateTime.UtcNow
            };
            var w2 = new Wallet
            {
                Id = Guid.NewGuid(),
                CustomerId = c2.Id,
                Balance = 50000m,
                UpdatedAtUtc = DateTime.UtcNow
            };
            w2.Transactions.Add(new WalletTransaction
            {
                Id = Guid.NewGuid(),
                WalletId = w2.Id,
                BranchId = branch.Id,
                Type = WalletTransactionType.TopUp,
                Amount = 50000m,
                BalanceBefore = 0m,
                BalanceAfter = 50000m,
                Description = "Nạp tiền ban đầu khi mở tài khoản",
                OccurredAtUtc = DateTime.UtcNow.AddMonths(-1),
                CreatedByUserId = userOwner.Id
            });

            _db.Customers.AddRange(c1, c2);
            _db.Wallets.AddRange(w1, w2);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Đã seed hội viên mẫu: KH-001 (100.000đ, PIN: {Pin}), KH-002 (50.000đ, PIN: {Pin}).", defaultMemberPin, defaultMemberPin);
        }
        else
        {
            var customersWithoutPin = await _db.Customers.Where(c => c.MemberPinHash == null).ToListAsync(ct);
            if (customersWithoutPin.Count > 0)
            {
                foreach (var cust in customersWithoutPin)
                {
                    cust.MemberPinHash = defaultPinHash;
                    cust.MemberFailedLoginCount = 0;
                }
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Đã cập nhật mã PIN mặc định ({Pin}) cho {Count} hội viên.", defaultMemberPin, customersWithoutPin.Count);
            }
        }

        // 8. Product Categories & Products & Stock
        var catDrink = await _db.ProductCategories.FirstOrDefaultAsync(c => c.BranchId == branch.Id && c.Name == "Nước giải khát", ct);
        if (catDrink == null)
        {
            catDrink = new ProductCategory
            {
                Id = Guid.NewGuid(),
                BranchId = branch.Id,
                Name = "Nước giải khát",
                SortOrder = 1,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.ProductCategories.Add(catDrink);
        }

        var catFood = await _db.ProductCategories.FirstOrDefaultAsync(c => c.BranchId == branch.Id && c.Name == "Đồ ăn nhanh", ct);
        if (catFood == null)
        {
            catFood = new ProductCategory
            {
                Id = Guid.NewGuid(),
                BranchId = branch.Id,
                Name = "Đồ ăn nhanh",
                SortOrder = 2,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.ProductCategories.Add(catFood);
        }
        await _db.SaveChangesAsync(ct);

        if (!await _db.Products.AnyAsync(p => p.BranchId == branch.Id, ct))
        {
            var productsToSeed = new (string sku, string name, string unit, decimal sale, decimal cost, decimal stock, Guid catId)[]
            {
                ("STING", "Sting Dâu Đỏ 330ml", "chai", 12000m, 7500m, 50, catDrink.Id),
                ("AQUAFINA", "Nước Suối Aquafina 500ml", "chai", 8000m, 4000m, 60, catDrink.Id),
                ("CF-SUA", "Cà Phê Sữa Đá Pha Phin", "ly", 15000m, 6000m, 30, catDrink.Id),
                ("C2", "Trà Xanh C2 Hương Chanh 455ml", "chai", 10000m, 6000m, 45, catDrink.Id),
                ("REDBULL", "Nước Tăng Lực RedBull 250ml", "lon", 18000m, 11000m, 40, catDrink.Id),
                ("MITOM-TRUNG", "Mì Tôm Trứng Xúc Xích", "tô", 25000m, 12000m, 35, catFood.Id),
                ("BANHMI-PATE", "Bánh Mì Kẹp Pate Trứng", "ổ", 20000m, 10000m, 25, catFood.Id),
                ("SNACK-OISHI", "Snack Khoai Tây Cay Oishi", "gói", 10000m, 5500m, 50, catFood.Id),
                ("COM-SUON", "Cơm Sườn Trứng Ốp La", "đĩa", 35000m, 18000m, 20, catFood.Id)
            };

            foreach (var item in productsToSeed)
            {
                var prod = new Product
                {
                    Id = Guid.NewGuid(),
                    BranchId = branch.Id,
                    CategoryId = item.catId,
                    Sku = item.sku,
                    Name = item.name,
                    Unit = item.unit,
                    SalePrice = item.sale,
                    CostPrice = item.cost,
                    TrackInventory = true,
                    ReorderLevel = 10,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                };

                var bal = new InventoryBalance
                {
                    Id = Guid.NewGuid(),
                    BranchId = branch.Id,
                    ProductId = prod.Id,
                    QuantityOnHand = item.stock,
                    UpdatedAtUtc = DateTime.UtcNow
                };

                var tx = new InventoryTransaction
                {
                    Id = Guid.NewGuid(),
                    BranchId = branch.Id,
                    ProductId = prod.Id,
                    Type = InventoryTransactionType.Opening,
                    Quantity = item.stock,
                    QuantityBefore = 0,
                    QuantityAfter = item.stock,
                    UnitCost = item.cost,
                    Note = "Tồn kho ban đầu",
                    OccurredAtUtc = DateTime.UtcNow,
                    CreatedByUserId = userOwner.Id
                };

                _db.Products.Add(prod);
                _db.InventoryBalances.Add(bal);
                _db.InventoryTransactions.Add(tx);
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Đã seed danh mục sản phẩm và tồn kho mẫu.");
        }

        // 9. Expense Categories
        if (!await _db.ExpenseCategories.AnyAsync(e => e.BranchId == branch.Id, ct))
        {
            var expCats = new[] { "Tiền điện sinh hoạt & máy móc", "Tiền mạng Internet cáp quang", "Vật tư & vệ sinh phòng máy", "Chi phí khác" };
            foreach (var ec in expCats)
            {
                _db.ExpenseCategories.Add(new ExpenseCategory
                {
                    Id = Guid.NewGuid(),
                    BranchId = branch.Id,
                    Name = ec,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
            await _db.SaveChangesAsync(ct);
        }

        _logger.LogInformation("Khởi tạo cơ sở dữ liệu và seed dữ liệu hoàn tất thành công.");
    }

    private async Task<Role> EnsureRoleAsync(string code, string name, CancellationToken ct)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Code == code, ct);
        if (role == null)
        {
            role = new Role { Id = Guid.NewGuid(), Code = code, Name = name };
            _db.Roles.Add(role);
            await _db.SaveChangesAsync(ct);
        }
        return role;
    }

    private async Task<AppUser> EnsureUserAsync(string username, string fullName, string password, Guid branchId, IEnumerable<Role> roles, CancellationToken ct)
    {
        string normalized = username.Trim().ToUpperInvariant();
        var user = await _db.Users.Include(u => u.UserRoles).FirstOrDefaultAsync(u => u.NormalizedUsername == normalized, ct);
        if (user == null)
        {
            user = new AppUser
            {
                Id = Guid.NewGuid(),
                BranchId = branchId,
                Username = username,
                NormalizedUsername = normalized,
                FullName = fullName,
                PasswordHash = _passwordHasher.HashPassword(password),
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            foreach (var r in roles)
            {
                user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = r.Id });
            }

            _db.Users.Add(user);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Đã tạo tài khoản mẫu: {Username} ({FullName})", username, fullName);
        }
        return user;
    }
}
