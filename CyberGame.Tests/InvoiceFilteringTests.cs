using CyberGame.Application.Common;
using CyberGame.Application.Services;
using CyberGame.Domain.Entities;
using CyberGame.Domain.Enums;
using CyberGame.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CyberGame.Tests;

public class InvoiceFilteringTests
{
    private class FakeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId { get; set; } = Guid.NewGuid();
        public Guid? BranchId { get; set; }
        public string? Username { get; set; } = "tester";
        public IReadOnlyList<string> Roles { get; set; } = new[] { "Owner" };
        public bool IsAuthenticated => true;
        public bool IsInRole(string role) => true;
    }

    private CyberGameDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<CyberGameDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new CyberGameDbContext(options);
    }

    [Fact]
    public async Task GetInvoicesAsync_FiltersByInclusiveDateRange_Properly()
    {
        using var db = CreateInMemoryDb();
        var branch = new Branch
        {
            Id = Guid.NewGuid(),
            Code = "B01",
            Name = "Branch 1",
            Address = "123 St",
            TimeZoneId = "SE Asia Standard Time"
        };
        db.Branches.Add(branch);

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            Username = "cashier1",
            NormalizedUsername = "CASHIER1",
            FullName = "Cashier One",
            PasswordHash = "hash"
        };
        db.Users.Add(user);

        var shiftId = Guid.NewGuid();

        // Invoice 1: 2026-09-09 23:59:59 UTC (Before range)
        var invBefore = new Invoice
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            ShiftId = shiftId,
            InvoiceNumber = "INV-001",
            Status = InvoiceStatus.Paid,
            TotalAmount = 10000,
            PaidAmount = 10000,
            IssuedAtUtc = new DateTime(2026, 9, 9, 23, 59, 59, DateTimeKind.Utc)
        };

        // Invoice 2: 2026-09-10 08:00:00 UTC (Start of range)
        var invStart = new Invoice
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            ShiftId = shiftId,
            InvoiceNumber = "INV-002",
            Status = InvoiceStatus.Paid,
            TotalAmount = 20000,
            PaidAmount = 20000,
            IssuedAtUtc = new DateTime(2026, 9, 10, 8, 0, 0, DateTimeKind.Utc)
        };

        // Invoice 3: 2026-09-15 23:30:00 UTC (End day of range - inclusive!)
        var invEndDay = new Invoice
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            ShiftId = shiftId,
            InvoiceNumber = "INV-003",
            Status = InvoiceStatus.Paid,
            TotalAmount = 30000,
            PaidAmount = 30000,
            IssuedAtUtc = new DateTime(2026, 9, 15, 23, 30, 0, DateTimeKind.Utc)
        };

        // Invoice 4: 2026-09-16 00:00:01 UTC (After range)
        var invAfter = new Invoice
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            ShiftId = shiftId,
            InvoiceNumber = "INV-004",
            Status = InvoiceStatus.Paid,
            TotalAmount = 40000,
            PaidAmount = 40000,
            IssuedAtUtc = new DateTime(2026, 9, 16, 0, 0, 1, DateTimeKind.Utc)
        };

        db.Invoices.AddRange(invBefore, invStart, invEndDay, invAfter);
        await db.SaveChangesAsync();

        var currentUser = new FakeCurrentUserService { BranchId = branch.Id, UserId = user.Id };
        var billingService = new BillingService(db, currentUser);

        // Filter range: from 2026-09-10 to 2026-09-15 (inclusive of both days)
        var fromDate = new DateTime(2026, 9, 10);
        var toDate = new DateTime(2026, 9, 15);
        var result = await billingService.GetInvoicesAsync(fromDate: fromDate, toDate: toDate, page: 1, pageSize: 50);

        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.Contains(result.Items, i => i.InvoiceNumber == "INV-002");
        Assert.Contains(result.Items, i => i.InvoiceNumber == "INV-003");
        Assert.DoesNotContain(result.Items, i => i.InvoiceNumber == "INV-001");
        Assert.DoesNotContain(result.Items, i => i.InvoiceNumber == "INV-004");
    }
}
