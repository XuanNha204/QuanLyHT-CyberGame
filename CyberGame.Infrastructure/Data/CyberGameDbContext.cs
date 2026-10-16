using CyberGame.Application.Common;
using CyberGame.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CyberGame.Infrastructure.Data;

public class CyberGameDbContext : DbContext, ICyberGameDbContext
{
    public CyberGameDbContext(DbContextOptions<CyberGameDbContext> options) : base(options)
    {
    }

    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<MachineGroup> MachineGroups => Set<MachineGroup>();
    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<PricingPlan> PricingPlans => Set<PricingPlan>();
    public DbSet<PricingRule> PricingRules => Set<PricingRule>();

    public DbSet<CustomerGroup> CustomerGroups => Set<CustomerGroup>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<MemberTopUpRequest> MemberTopUpRequests => Set<MemberTopUpRequest>();

    public DbSet<PlaySession> PlaySessions => Set<PlaySession>();
    public DbSet<SessionChargeSegment> SessionChargeSegments => Set<SessionChargeSegment>();
    public DbSet<SessionEvent> SessionEvents => Set<SessionEvent>();

    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<InventoryBalance> InventoryBalances => Set<InventoryBalance>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<Expense> Expenses => Set<Expense>();

    public DbSet<MaintenanceTicket> MaintenanceTickets => Set<MaintenanceTicket>();
    public DbSet<MaintenanceLog> MaintenanceLogs => Set<MaintenanceLog>();

    public DbSet<BranchSetting> BranchSettings => Set<BranchSetting>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (Database.ProviderName?.EndsWith(".InMemory") == true)
        {
            return Task.FromResult<IDbContextTransaction>(new NullDbContextTransaction());
        }
        return Database.BeginTransactionAsync(cancellationToken);
    }

    private sealed class NullDbContextTransaction : IDbContextTransaction
    {
        public Guid TransactionId { get; } = Guid.NewGuid();
        public void Commit() { }
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Rollback() { }
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Branches
        modelBuilder.Entity<Branch>(b =>
        {
            b.ToTable("Branches");
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).HasMaxLength(32).IsRequired();
            b.HasIndex(x => x.Code).IsUnique();
            b.Property(x => x.Name).HasMaxLength(160).IsRequired();
            b.Property(x => x.Address).HasMaxLength(400);
            b.Property(x => x.TimeZoneId).HasMaxLength(100).IsRequired();
            b.Property(x => x.Phone).HasMaxLength(30);
            b.Property(x => x.RowVersion);
        });

        // Roles
        modelBuilder.Entity<Role>(b =>
        {
            b.ToTable("Roles");
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).HasMaxLength(40).IsRequired();
            b.HasIndex(x => x.Code).IsUnique();
            b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        });

        // AppUsers
        modelBuilder.Entity<AppUser>(b =>
        {
            b.ToTable("AppUsers");
            b.HasKey(x => x.Id);
            b.Property(x => x.Username).HasMaxLength(64).IsRequired();
            b.Property(x => x.NormalizedUsername).HasMaxLength(64).IsRequired();
            b.HasIndex(x => x.NormalizedUsername).IsUnique();
            b.Property(x => x.FullName).HasMaxLength(160).IsRequired();
            b.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            b.Property(x => x.RowVersion);

            b.HasOne(x => x.Branch)
             .WithMany(br => br.Users)
             .HasForeignKey(x => x.BranchId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // UserRoles
        modelBuilder.Entity<UserRole>(b =>
        {
            b.ToTable("UserRoles");
            b.HasKey(x => new { x.UserId, x.RoleId });

            b.HasOne(x => x.User)
             .WithMany(u => u.UserRoles)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Role)
             .WithMany(r => r.UserRoles)
             .HasForeignKey(x => x.RoleId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // RefreshTokens
        modelBuilder.Entity<RefreshToken>(b =>
        {
            b.ToTable("RefreshTokens");
            b.HasKey(x => x.Id);
            b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            b.HasIndex(x => x.TokenHash).IsUnique();
            b.Property(x => x.DeviceName).HasMaxLength(200);

            b.HasOne(x => x.User)
             .WithMany(u => u.RefreshTokens)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // MachineGroups
        modelBuilder.Entity<MachineGroup>(b =>
        {
            b.ToTable("MachineGroups");
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).HasMaxLength(32).IsRequired();
            b.Property(x => x.Name).HasMaxLength(120).IsRequired();
            b.HasIndex(x => new { x.BranchId, x.Code }).IsUnique();

            b.HasOne(x => x.Branch)
             .WithMany(br => br.MachineGroups)
             .HasForeignKey(x => x.BranchId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // Machines
        modelBuilder.Entity<Machine>(b =>
        {
            b.ToTable("Machines");
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).HasMaxLength(32).IsRequired();
            b.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
            b.Property(x => x.IpAddress).HasMaxLength(45);
            b.Property(x => x.MacAddress).HasMaxLength(17);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
            b.Property(x => x.Notes).HasMaxLength(1000);
            b.Property(x => x.RowVersion);
            b.HasIndex(x => new { x.BranchId, x.Code }).IsUnique();
            b.HasIndex(x => new { x.BranchId, x.Status, x.MachineGroupId });

            b.HasOne(x => x.Branch)
             .WithMany(br => br.Machines)
             .HasForeignKey(x => x.BranchId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.MachineGroup)
             .WithMany(g => g.Machines)
             .HasForeignKey(x => x.MachineGroupId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // PricingPlans
        modelBuilder.Entity<PricingPlan>(b =>
        {
            b.ToTable("PricingPlans");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(160).IsRequired();

            b.HasOne(x => x.Branch)
             .WithMany(br => br.PricingPlans)
             .HasForeignKey(x => x.BranchId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.MachineGroup)
             .WithMany(g => g.PricingPlans)
             .HasForeignKey(x => x.MachineGroupId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // PricingRules
        modelBuilder.Entity<PricingRule>(b =>
        {
            b.ToTable("PricingRules");
            b.HasKey(x => x.Id);
            b.Property(x => x.HourlyRate).HasPrecision(18, 2);

            b.HasOne(x => x.PricingPlan)
             .WithMany(p => p.PricingRules)
             .HasForeignKey(x => x.PricingPlanId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // CustomerGroups
        modelBuilder.Entity<CustomerGroup>(b =>
        {
            b.ToTable("CustomerGroups");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(120).IsRequired();
            b.Property(x => x.DiscountPercent).HasPrecision(5, 2);
            b.HasIndex(x => new { x.BranchId, x.Name }).IsUnique();

            b.HasOne(x => x.Branch)
             .WithMany(br => br.CustomerGroups)
             .HasForeignKey(x => x.BranchId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // Customers
        modelBuilder.Entity<Customer>(b =>
        {
            b.ToTable("Customers");
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).HasMaxLength(32).IsRequired();
            b.HasIndex(x => x.Code).IsUnique();
            b.Property(x => x.FullName).HasMaxLength(160).IsRequired();
            b.Property(x => x.Phone).HasMaxLength(30);
            b.Property(x => x.Email).HasMaxLength(254);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            b.Property(x => x.MemberPinHash).HasMaxLength(500);
            b.Property(x => x.MemberFailedLoginCount).HasDefaultValue(0);
            b.Property(x => x.MemberLockoutEndUtc);
            b.Property(x => x.MemberLastLoginAtUtc);
            b.Property(x => x.Notes).HasMaxLength(1000);
            b.Property(x => x.RowVersion);

            b.HasOne(x => x.HomeBranch)
             .WithMany(br => br.Customers)
             .HasForeignKey(x => x.HomeBranchId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.CustomerGroup)
             .WithMany(g => g.Customers)
             .HasForeignKey(x => x.CustomerGroupId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // MemberTopUpRequests
        modelBuilder.Entity<MemberTopUpRequest>(b =>
        {
            b.ToTable("MemberTopUpRequests");
            b.HasKey(x => x.Id);
            b.Property(x => x.Amount).HasPrecision(18, 2);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
            b.Property(x => x.Note).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();

            b.HasIndex(x => x.CustomerId)
             .IsUnique()
             .HasFilter("[Status] = 'Pending'")
             .HasDatabaseName("IX_MemberTopUpRequests_CustomerId_Pending");

            b.HasIndex(x => x.WalletTransactionId)
             .IsUnique()
             .HasFilter("[WalletTransactionId] IS NOT NULL")
             .HasDatabaseName("IX_MemberTopUpRequests_WalletTransactionId");

            b.HasOne(x => x.Branch)
             .WithMany()
             .HasForeignKey(x => x.BranchId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Customer)
             .WithMany(c => c.MemberTopUpRequests)
             .HasForeignKey(x => x.CustomerId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.ProcessedByUser)
             .WithMany()
             .HasForeignKey(x => x.ProcessedByUserId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.WalletTransaction)
             .WithMany()
             .HasForeignKey(x => x.WalletTransactionId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // Wallets
        modelBuilder.Entity<Wallet>(b =>
        {
            b.ToTable("Wallets");
            b.HasKey(x => x.Id);
            b.Property(x => x.Balance).HasPrecision(18, 2);
            b.Property(x => x.RowVersion);
            b.HasIndex(x => x.CustomerId).IsUnique();

            b.HasOne(x => x.Customer)
             .WithOne(c => c.Wallet)
             .HasForeignKey<Wallet>(x => x.CustomerId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // WalletTransactions
        modelBuilder.Entity<WalletTransaction>(b =>
        {
            b.ToTable("WalletTransactions");
            b.HasKey(x => x.Id);
            b.Property(x => x.Type).HasConversion<string>().HasMaxLength(24).IsRequired();
            b.Property(x => x.Amount).HasPrecision(18, 2);
            b.Property(x => x.BalanceBefore).HasPrecision(18, 2);
            b.Property(x => x.BalanceAfter).HasPrecision(18, 2);
            b.Property(x => x.ReferenceType).HasMaxLength(40);
            b.Property(x => x.Description).HasMaxLength(500).IsRequired();
            b.HasIndex(x => new { x.WalletId, x.OccurredAtUtc });

            b.HasOne(x => x.Wallet)
             .WithMany(w => w.Transactions)
             .HasForeignKey(x => x.WalletId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Branch)
             .WithMany()
             .HasForeignKey(x => x.BranchId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Shift)
             .WithMany(s => s.WalletTransactions)
             .HasForeignKey(x => x.ShiftId)
             .OnDelete(DeleteBehavior.SetNull);

            b.HasOne(x => x.CreatedByUser)
             .WithMany()
             .HasForeignKey(x => x.CreatedByUserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // Shifts
        modelBuilder.Entity<Shift>(b =>
        {
            b.ToTable("Shifts");
            b.HasKey(x => x.Id);
            b.Property(x => x.ShiftNumber).HasMaxLength(40).IsRequired();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
            b.Property(x => x.OpeningCash).HasPrecision(18, 2);
            b.Property(x => x.ExpectedCash).HasPrecision(18, 2);
            b.Property(x => x.ActualCash).HasPrecision(18, 2);
            b.Property(x => x.Difference).HasPrecision(18, 2);
            b.Property(x => x.ClosingNote).HasMaxLength(1000);
            b.Property(x => x.RowVersion);
            b.HasIndex(x => new { x.BranchId, x.ShiftNumber }).IsUnique();
            b.HasIndex(x => new { x.BranchId, x.UserId })
             .IsUnique()
             .HasFilter("[Status] = 'Open'")
             .HasDatabaseName("IX_Shifts_BranchId_UserId_Open");

            b.HasOne(x => x.Branch)
             .WithMany(br => br.Shifts)
             .HasForeignKey(x => x.BranchId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.User)
             .WithMany(u => u.Shifts)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // PlaySessions
        modelBuilder.Entity<PlaySession>(b =>
        {
            b.ToTable("PlaySessions");
            b.HasKey(x => x.Id);
            b.Property(x => x.SessionNumber).HasMaxLength(40).IsRequired();
            b.Property(x => x.BillingMode).HasConversion<string>().HasMaxLength(24).IsRequired();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
            b.Property(x => x.UsageAmount).HasPrecision(18, 2);
            b.Property(x => x.ServiceAmount).HasPrecision(18, 2);
            b.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            b.Property(x => x.TotalAmount).HasPrecision(18, 2);
            b.Property(x => x.RowVersion);
            b.HasIndex(x => new { x.BranchId, x.SessionNumber }).IsUnique();
            b.HasIndex(x => new { x.BranchId, x.StartedAtUtc });
            b.HasIndex(x => new { x.CustomerId, x.StartedAtUtc });
            b.HasIndex(x => x.MachineId)
             .IsUnique()
             .HasFilter("[Status] IN ('Open', 'Paused')")
             .HasDatabaseName("IX_PlaySessions_MachineId_Active");

            b.HasOne(x => x.Branch)
             .WithMany()
             .HasForeignKey(x => x.BranchId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Machine)
             .WithMany(m => m.PlaySessions)
             .HasForeignKey(x => x.MachineId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Customer)
             .WithMany(c => c.PlaySessions)
             .HasForeignKey(x => x.CustomerId)
             .OnDelete(DeleteBehavior.SetNull);

            b.HasOne(x => x.Shift)
             .WithMany(s => s.PlaySessions)
             .HasForeignKey(x => x.ShiftId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // SessionChargeSegments
        modelBuilder.Entity<SessionChargeSegment>(b =>
        {
            b.ToTable("SessionChargeSegments");
            b.HasKey(x => x.Id);
            b.Property(x => x.RatePerHour).HasPrecision(18, 2);
            b.Property(x => x.Amount).HasPrecision(18, 2);

            b.HasOne(x => x.PlaySession)
             .WithMany(s => s.ChargeSegments)
             .HasForeignKey(x => x.PlaySessionId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.PricingRule)
             .WithMany()
             .HasForeignKey(x => x.PricingRuleId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // SessionEvents
        modelBuilder.Entity<SessionEvent>(b =>
        {
            b.ToTable("SessionEvents");
            b.HasKey(x => x.Id);
            b.Property(x => x.EventType).HasMaxLength(32).IsRequired();
            b.Property(x => x.Note).HasMaxLength(1000);

            b.HasOne(x => x.PlaySession)
             .WithMany(s => s.Events)
             .HasForeignKey(x => x.PlaySessionId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.User)
             .WithMany()
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ProductCategories
        modelBuilder.Entity<ProductCategory>(b =>
        {
            b.ToTable("ProductCategories");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(120).IsRequired();
            b.HasIndex(x => new { x.BranchId, x.Name }).IsUnique();

            b.HasOne(x => x.Branch)
             .WithMany(br => br.ProductCategories)
             .HasForeignKey(x => x.BranchId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // Products
        modelBuilder.Entity<Product>(b =>
        {
            b.ToTable("Products");
            b.HasKey(x => x.Id);
            b.Property(x => x.Sku).HasMaxLength(48).IsRequired();
            b.Property(x => x.Name).HasMaxLength(160).IsRequired();
            b.Property(x => x.Unit).HasMaxLength(32).IsRequired();
            b.Property(x => x.SalePrice).HasPrecision(18, 2);
            b.Property(x => x.CostPrice).HasPrecision(18, 2);
            b.Property(x => x.ReorderLevel).HasPrecision(18, 3);
            b.Property(x => x.RowVersion);
            b.HasIndex(x => new { x.BranchId, x.Sku }).IsUnique();

            b.HasOne(x => x.Branch)
             .WithMany(br => br.Products)
             .HasForeignKey(x => x.BranchId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Category)
             .WithMany(c => c.Products)
             .HasForeignKey(x => x.CategoryId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // InventoryBalances
        modelBuilder.Entity<InventoryBalance>(b =>
        {
            b.ToTable("InventoryBalances");
            b.HasKey(x => x.Id);
            b.Property(x => x.QuantityOnHand).HasPrecision(18, 3);
            b.Property(x => x.RowVersion);
            b.HasIndex(x => new { x.BranchId, x.ProductId }).IsUnique();

            b.HasOne(x => x.Branch)
             .WithMany()
             .HasForeignKey(x => x.BranchId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Product)
             .WithOne(p => p.Balance)
             .HasForeignKey<InventoryBalance>(x => x.ProductId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // InventoryTransactions
        modelBuilder.Entity<InventoryTransaction>(b =>
        {
            b.ToTable("InventoryTransactions");
            b.HasKey(x => x.Id);
            b.Property(x => x.Type).HasConversion<string>().HasMaxLength(24).IsRequired();
            b.Property(x => x.Quantity).HasPrecision(18, 3);
            b.Property(x => x.QuantityBefore).HasPrecision(18, 3);
            b.Property(x => x.QuantityAfter).HasPrecision(18, 3);
            b.Property(x => x.UnitCost).HasPrecision(18, 2);
            b.Property(x => x.ReferenceType).HasMaxLength(40);
            b.Property(x => x.Note).HasMaxLength(1000);
            b.HasIndex(x => new { x.BranchId, x.ProductId, x.OccurredAtUtc });

            b.HasOne(x => x.Branch)
             .WithMany()
             .HasForeignKey(x => x.BranchId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Product)
             .WithMany(p => p.InventoryTransactions)
             .HasForeignKey(x => x.ProductId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.CreatedByUser)
             .WithMany()
             .HasForeignKey(x => x.CreatedByUserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // Orders
        modelBuilder.Entity<Order>(b =>
        {
            b.ToTable("Orders");
            b.HasKey(x => x.Id);
            b.Property(x => x.OrderNumber).HasMaxLength(40).IsRequired();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
            b.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(24).IsRequired();
            b.Property(x => x.PaymentStatus).HasConversion<string>().HasMaxLength(16).IsRequired();
            b.Property(x => x.Subtotal).HasPrecision(18, 2);
            b.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            b.Property(x => x.TotalAmount).HasPrecision(18, 2);
            b.Property(x => x.RowVersion);
            b.HasIndex(x => new { x.BranchId, x.OrderNumber }).IsUnique();

            b.HasOne(x => x.Branch)
             .WithMany()
             .HasForeignKey(x => x.BranchId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.PlaySession)
             .WithMany(s => s.Orders)
             .HasForeignKey(x => x.PlaySessionId)
             .OnDelete(DeleteBehavior.SetNull);

            b.HasOne(x => x.Customer)
             .WithMany(c => c.Orders)
             .HasForeignKey(x => x.CustomerId)
             .OnDelete(DeleteBehavior.SetNull);

            b.HasOne(x => x.Shift)
             .WithMany(s => s.Orders)
             .HasForeignKey(x => x.ShiftId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasMany(x => x.Payments)
             .WithOne(p => p.Order)
             .HasForeignKey(p => p.OrderId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // OrderItems
        modelBuilder.Entity<OrderItem>(b =>
        {
            b.ToTable("OrderItems");
            b.HasKey(x => x.Id);
            b.Property(x => x.ProductName).HasMaxLength(160).IsRequired();
            b.Property(x => x.Sku).HasMaxLength(48).IsRequired();
            b.Property(x => x.Unit).HasMaxLength(32).IsRequired();
            b.Property(x => x.Quantity).HasPrecision(18, 3);
            b.Property(x => x.UnitPrice).HasPrecision(18, 2);
            b.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            b.Property(x => x.LineTotal).HasPrecision(18, 2);
            b.Property(x => x.Note).HasMaxLength(500);

            b.HasOne(x => x.Order)
             .WithMany(o => o.Items)
             .HasForeignKey(x => x.OrderId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Product)
             .WithMany(p => p.OrderItems)
             .HasForeignKey(x => x.ProductId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // Invoices
        modelBuilder.Entity<Invoice>(b =>
        {
            b.ToTable("Invoices");
            b.HasKey(x => x.Id);
            b.Property(x => x.InvoiceNumber).HasMaxLength(40).IsRequired();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
            b.Property(x => x.Subtotal).HasPrecision(18, 2);
            b.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            b.Property(x => x.TotalAmount).HasPrecision(18, 2);
            b.Property(x => x.PaidAmount).HasPrecision(18, 2);
            b.Property(x => x.ChangeAmount).HasPrecision(18, 2);
            b.Property(x => x.VoidReason).HasMaxLength(1000);
            b.Property(x => x.RowVersion);
            b.HasIndex(x => new { x.BranchId, x.InvoiceNumber }).IsUnique();
            b.HasIndex(x => new { x.BranchId, x.IssuedAtUtc });

            b.HasOne(x => x.Branch)
             .WithMany()
             .HasForeignKey(x => x.BranchId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Customer)
             .WithMany(c => c.Invoices)
             .HasForeignKey(x => x.CustomerId)
             .OnDelete(DeleteBehavior.SetNull);

            b.HasOne(x => x.Shift)
             .WithMany(s => s.Invoices)
             .HasForeignKey(x => x.ShiftId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.PlaySession)
             .WithOne(s => s.Invoice)
             .HasForeignKey<Invoice>(x => x.PlaySessionId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // InvoiceLines
        modelBuilder.Entity<InvoiceLine>(b =>
        {
            b.ToTable("InvoiceLines");
            b.HasKey(x => x.Id);
            b.Property(x => x.LineType).HasConversion<string>().HasMaxLength(24).IsRequired();
            b.Property(x => x.Description).HasMaxLength(300).IsRequired();
            b.Property(x => x.Quantity).HasPrecision(18, 3);
            b.Property(x => x.UnitPrice).HasPrecision(18, 2);
            b.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            b.Property(x => x.LineTotal).HasPrecision(18, 2);

            b.HasOne(x => x.Invoice)
             .WithMany(i => i.Lines)
             .HasForeignKey(x => x.InvoiceId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Payments
        modelBuilder.Entity<Payment>(b =>
        {
            b.ToTable("Payments");
            b.HasKey(x => x.Id);
            b.Property(x => x.PaymentNumber).HasMaxLength(40).IsRequired();
            b.Property(x => x.Type).HasConversion<string>().HasMaxLength(16).IsRequired();
            b.Property(x => x.Method).HasConversion<string>().HasMaxLength(24).IsRequired();
            b.Property(x => x.Amount).HasPrecision(18, 2);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
            b.Property(x => x.ExternalReference).HasMaxLength(160);
            b.Property(x => x.Note).HasMaxLength(1000);
            b.HasIndex(x => new { x.BranchId, x.PaymentNumber }).IsUnique();
            b.HasIndex(x => new { x.BranchId, x.PaidAtUtc });
            b.HasIndex(x => x.OrderId);

            b.HasOne(x => x.Branch)
             .WithMany()
             .HasForeignKey(x => x.BranchId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Invoice)
             .WithMany(i => i.Payments)
             .HasForeignKey(x => x.InvoiceId)
             .OnDelete(DeleteBehavior.SetNull);

            b.HasOne(x => x.Customer)
             .WithMany()
             .HasForeignKey(x => x.CustomerId)
             .OnDelete(DeleteBehavior.SetNull);

            b.HasOne(x => x.Shift)
             .WithMany(s => s.Payments)
             .HasForeignKey(x => x.ShiftId)
             .OnDelete(DeleteBehavior.SetNull);

            b.HasOne(x => x.CreatedByUser)
             .WithMany()
             .HasForeignKey(x => x.CreatedByUserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // CashMovements
        modelBuilder.Entity<CashMovement>(b =>
        {
            b.ToTable("CashMovements");
            b.HasKey(x => x.Id);
            b.Property(x => x.Type).HasConversion<string>().HasMaxLength(24).IsRequired();
            b.Property(x => x.Amount).HasPrecision(18, 2);
            b.Property(x => x.ReferenceType).HasMaxLength(40);
            b.Property(x => x.Description).HasMaxLength(500).IsRequired();

            b.HasOne(x => x.Shift)
             .WithMany(s => s.CashMovements)
             .HasForeignKey(x => x.ShiftId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.CreatedByUser)
             .WithMany()
             .HasForeignKey(x => x.CreatedByUserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ExpenseCategories
        modelBuilder.Entity<ExpenseCategory>(b =>
        {
            b.ToTable("ExpenseCategories");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(120).IsRequired();
            b.HasIndex(x => new { x.BranchId, x.Name }).IsUnique();

            b.HasOne(x => x.Branch)
             .WithMany()
             .HasForeignKey(x => x.BranchId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // Expenses
        modelBuilder.Entity<Expense>(b =>
        {
            b.ToTable("Expenses");
            b.HasKey(x => x.Id);
            b.Property(x => x.Amount).HasPrecision(18, 2);
            b.Property(x => x.Description).HasMaxLength(1000).IsRequired();
            b.Property(x => x.ReceiptNumber).HasMaxLength(100);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
            b.Property(x => x.RowVersion);

            b.HasOne(x => x.Branch)
             .WithMany()
             .HasForeignKey(x => x.BranchId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Shift)
             .WithMany(s => s.Expenses)
             .HasForeignKey(x => x.ShiftId)
             .OnDelete(DeleteBehavior.SetNull);

            b.HasOne(x => x.Category)
             .WithMany(c => c.Expenses)
             .HasForeignKey(x => x.CategoryId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.CreatedByUser)
             .WithMany()
             .HasForeignKey(x => x.CreatedByUserId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.ApprovedByUser)
             .WithMany()
             .HasForeignKey(x => x.ApprovedByUserId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // MaintenanceTickets
        modelBuilder.Entity<MaintenanceTicket>(b =>
        {
            b.ToTable("MaintenanceTickets");
            b.HasKey(x => x.Id);
            b.Property(x => x.TicketNumber).HasMaxLength(40).IsRequired();
            b.Property(x => x.Priority).HasConversion<string>().HasMaxLength(16).IsRequired();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            b.Property(x => x.Title).HasMaxLength(200).IsRequired();
            b.Property(x => x.Description).HasMaxLength(2000).IsRequired();
            b.Property(x => x.Resolution).HasMaxLength(2000);
            b.Property(x => x.Cost).HasPrecision(18, 2);
            b.Property(x => x.RowVersion);
            b.HasIndex(x => new { x.BranchId, x.TicketNumber }).IsUnique();

            b.HasOne(x => x.Branch)
             .WithMany()
             .HasForeignKey(x => x.BranchId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Machine)
             .WithMany(m => m.MaintenanceTickets)
             .HasForeignKey(x => x.MachineId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.AssignedToUser)
             .WithMany()
             .HasForeignKey(x => x.AssignedToUserId)
             .OnDelete(DeleteBehavior.SetNull);

            b.HasOne(x => x.CreatedByUser)
             .WithMany()
             .HasForeignKey(x => x.CreatedByUserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // MaintenanceLogs
        modelBuilder.Entity<MaintenanceLog>(b =>
        {
            b.ToTable("MaintenanceLogs");
            b.HasKey(x => x.Id);
            b.Property(x => x.StatusFrom).HasMaxLength(20);
            b.Property(x => x.StatusTo).HasMaxLength(20);
            b.Property(x => x.Note).HasMaxLength(2000).IsRequired();
            b.Property(x => x.CostDelta).HasPrecision(18, 2);

            b.HasOne(x => x.Ticket)
             .WithMany(t => t.Logs)
             .HasForeignKey(x => x.TicketId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.User)
             .WithMany()
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // BranchSettings
        modelBuilder.Entity<BranchSetting>(b =>
        {
            b.ToTable("BranchSettings");
            b.HasKey(x => x.Id);
            b.Property(x => x.Key).HasMaxLength(120).IsRequired();
            b.Property(x => x.ValueJson).IsRequired();
            b.HasIndex(x => new { x.BranchId, x.Key }).IsUnique();

            b.HasOne(x => x.Branch)
             .WithMany()
             .HasForeignKey(x => x.BranchId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.UpdatedByUser)
             .WithMany()
             .HasForeignKey(x => x.UpdatedByUserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // AuditLogs
        modelBuilder.Entity<AuditLog>(b =>
        {
            b.ToTable("AuditLogs");
            b.HasKey(x => x.Id);
            b.Property(x => x.Action).HasMaxLength(100).IsRequired();
            b.Property(x => x.EntityType).HasMaxLength(120).IsRequired();
            b.Property(x => x.IpAddress).HasMaxLength(45);
            b.Property(x => x.CorrelationId).HasMaxLength(100).IsRequired();
            b.HasIndex(x => x.OccurredAtUtc);
            b.HasIndex(x => new { x.EntityType, x.EntityId });
        });

        // IdempotencyRecords
        modelBuilder.Entity<IdempotencyRecord>(b =>
        {
            b.ToTable("IdempotencyRecords");
            b.HasKey(x => x.Id);
            b.Property(x => x.Scope).HasMaxLength(256).IsRequired();
            b.Property(x => x.Key).HasMaxLength(100).IsRequired();
            b.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
            b.HasIndex(x => new { x.Scope, x.Key }).IsUnique();
        });

        if (Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var prop = entityType.FindProperty("RowVersion");
                if (prop != null && prop.ClrType == typeof(byte[]))
                {
                    prop.IsConcurrencyToken = true;
                    prop.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate;
                }
            }
        }
    }
}
