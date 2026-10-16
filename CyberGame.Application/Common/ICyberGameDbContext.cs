using CyberGame.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CyberGame.Application.Common;

public interface ICyberGameDbContext
{
    DbSet<Branch> Branches { get; }
    DbSet<Role> Roles { get; }
    DbSet<AppUser> Users { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<MachineGroup> MachineGroups { get; }
    DbSet<Machine> Machines { get; }
    DbSet<PricingPlan> PricingPlans { get; }
    DbSet<PricingRule> PricingRules { get; }

    DbSet<CustomerGroup> CustomerGroups { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Wallet> Wallets { get; }
    DbSet<WalletTransaction> WalletTransactions { get; }
    DbSet<MemberTopUpRequest> MemberTopUpRequests { get; }

    DbSet<PlaySession> PlaySessions { get; }
    DbSet<SessionChargeSegment> SessionChargeSegments { get; }
    DbSet<SessionEvent> SessionEvents { get; }

    DbSet<ProductCategory> ProductCategories { get; }
    DbSet<Product> Products { get; }
    DbSet<InventoryBalance> InventoryBalances { get; }
    DbSet<InventoryTransaction> InventoryTransactions { get; }

    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }

    DbSet<Invoice> Invoices { get; }
    DbSet<InvoiceLine> InvoiceLines { get; }
    DbSet<Payment> Payments { get; }

    DbSet<Shift> Shifts { get; }
    DbSet<CashMovement> CashMovements { get; }
    DbSet<ExpenseCategory> ExpenseCategories { get; }
    DbSet<Expense> Expenses { get; }

    DbSet<MaintenanceTicket> MaintenanceTickets { get; }
    DbSet<MaintenanceLog> MaintenanceLogs { get; }

    DbSet<BranchSetting> BranchSettings { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<IdempotencyRecord> IdempotencyRecords { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
