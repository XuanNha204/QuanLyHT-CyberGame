using CyberGame.Application.Common;
using CyberGame.Application.DTOs;
using CyberGame.Application.Services;
using CyberGame.Domain.Entities;
using CyberGame.Domain.Enums;
using CyberGame.Domain.Exceptions;
using CyberGame.Infrastructure.Data;
using CyberGame.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace CyberGame.Tests;

public class TestConfiguration : IConfiguration
{
    private readonly Dictionary<string, string?> _data;
    public TestConfiguration(Dictionary<string, string?> data) => _data = data;
    public string? this[string key]
    {
        get => _data.TryGetValue(key, out var val) ? val : null;
        set => _data[key] = value;
    }
    public IEnumerable<IConfigurationSection> GetChildren() => [];
    public IChangeToken GetReloadToken() => throw new NotImplementedException();
    public IConfigurationSection GetSection(string key) => throw new NotImplementedException();
}

public class BranchIsolationTests
{
    private CyberGameDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<CyberGameDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new CyberGameDbContext(options);
    }

    [Fact]
    public async Task AuthService_EnforcesBranchIsolation_ForManager()
    {
        using var db = CreateInMemoryDb();
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();

        db.Branches.Add(new Branch { Id = branchA, Code = "BA", Name = "Chi nhánh A", IsActive = true });
        db.Branches.Add(new Branch { Id = branchB, Code = "BB", Name = "Chi nhánh B", IsActive = true });

        var passwordHasher = new PasswordHasher();
        var config = new TestConfiguration(new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = "12345678901234567890123456789012",
            ["Jwt:Issuer"] = "CyberGameServer",
            ["Jwt:Audience"] = "CyberGameClient"
        });
        var jwtTokenService = new JwtTokenService(config);

        var userA = new AppUser
        {
            Id = Guid.NewGuid(),
            BranchId = branchA,
            Username = "user_a",
            NormalizedUsername = "USER_A",
            FullName = "User A",
            PasswordHash = passwordHasher.HashPassword("Pass@123"),
            IsActive = true
        };
        var userB = new AppUser
        {
            Id = Guid.NewGuid(),
            BranchId = branchB,
            Username = "user_b",
            NormalizedUsername = "USER_B",
            FullName = "User B",
            PasswordHash = passwordHasher.HashPassword("Pass@123"),
            IsActive = true
        };
        db.Users.AddRange(userA, userB);
        await db.SaveChangesAsync();

        var managerA = new TestCurrentUserService
        {
            UserId = Guid.NewGuid(),
            BranchId = branchA,
            Roles = new[] { "Manager" }
        };

        var authService = new AuthService(db, passwordHasher, jwtTokenService, managerA);

        // 1. GetUsersAsync chỉ trả về user của Branch A
        var pagedUsers = await authService.GetUsersAsync();
        Assert.Single(pagedUsers.Items);
        Assert.Equal(userA.Id, pagedUsers.Items[0].Id);

        // 2. CreateUserAsync với Branch B bị từ chối
        var exCreate = await Assert.ThrowsAsync<DomainException>(() =>
            authService.CreateUserAsync(new CreateUserRequest(branchB, "new_b", "New B", "Pass@123", new List<string> { "Staff" })));
        Assert.Equal("cross_branch_access", exCreate.Code);

        // 3. UpdateUserAsync trên user của Branch B ném NotFoundException
        await Assert.ThrowsAsync<NotFoundException>(() =>
            authService.UpdateUserAsync(userB.Id, new UpdateUserRequest(branchB, "User B Modified", true, new List<string> { "Staff" })));

        // 4. ResetPasswordAsync trên user của Branch B ném NotFoundException
        await Assert.ThrowsAsync<NotFoundException>(() =>
            authService.ResetPasswordAsync(userB.Id, new ResetPasswordRequest("NewPass@123")));
    }

    [Fact]
    public async Task Services_RejectCrossBranchQueries()
    {
        using var db = CreateInMemoryDb();
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();

        db.Branches.Add(new Branch { Id = branchA, Code = "BA", Name = "Chi nhánh A", IsActive = true });
        db.Branches.Add(new Branch { Id = branchB, Code = "BB", Name = "Chi nhánh B", IsActive = true });

        var machineB = new Machine
        {
            Id = Guid.NewGuid(),
            BranchId = branchB,
            Code = "PC-B01",
            DisplayName = "Máy B01",
            Status = MachineStatus.Available,
            IsActive = true
        };
        db.Machines.Add(machineB);

        var customerB = new Customer
        {
            Id = Guid.NewGuid(),
            HomeBranchId = branchB,
            Code = "CUST-01",
            FullName = "Khách B",
            Phone = "0987654321",
            Status = CustomerStatus.Active
        };
        db.Customers.Add(customerB);

        var invoiceB = new Invoice
        {
            Id = Guid.NewGuid(),
            BranchId = branchB,
            InvoiceNumber = "INV-B01",
            CustomerId = customerB.Id,
            Status = InvoiceStatus.Issued,
            Subtotal = 10000m,
            TotalAmount = 10000m,
            IssuedAtUtc = DateTime.UtcNow
        };
        db.Invoices.Add(invoiceB);

        await db.SaveChangesAsync();

        // Caller tại branch A
        var userInBranchA = new TestCurrentUserService
        {
            UserId = Guid.NewGuid(),
            BranchId = branchA,
            Roles = new[] { "Manager" }
        };

        // BranchMachineService
        var machineService = new BranchMachineService(db, userInBranchA);
        await Assert.ThrowsAsync<NotFoundException>(() => machineService.GetMachineByIdAsync(machineB.Id));

        // CustomerWalletService
        var customerService = new CustomerWalletService(db, userInBranchA);
        await Assert.ThrowsAsync<NotFoundException>(() => customerService.GetCustomerByIdAsync(customerB.Id));

        // BillingService
        var billingService = new BillingService(db, userInBranchA);
        await Assert.ThrowsAsync<NotFoundException>(() => billingService.GetInvoiceByIdAsync(invoiceB.Id));
    }
}
