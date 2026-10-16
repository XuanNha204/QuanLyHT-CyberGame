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

public class MemberTopUpRequestTests
{
    private CyberGameDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<CyberGameDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new CyberGameDbContext(options);
    }

    private (Branch branch, AppUser cashier, Customer customer, Shift shift) SetupTopUpContext(CyberGameDbContext db)
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

        var cashier = new AppUser
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            Username = "cashier01",
            FullName = "Thu Ngân Test",
            IsActive = true
        };
        db.Users.Add(cashier);

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
            Balance = 50000m
        };
        customer.Wallet = wallet;
        db.Customers.Add(customer);
        db.Wallets.Add(wallet);

        var shift = new Shift
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            UserId = cashier.Id,
            ShiftNumber = "CA-01",
            OpenedAtUtc = DateTime.UtcNow.AddHours(-1),
            OpeningCash = 1000000m,
            Status = ShiftStatus.Open,
            User = cashier
        };
        db.Shifts.Add(shift);

        db.SaveChanges();
        return (branch, cashier, customer, shift);
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

    [Fact]
    public async Task CreateTopUpRequest_Valid_CreatesPendingRequest()
    {
        using var db = CreateInMemoryDb();
        var (branch, _, customer, _) = SetupTopUpContext(db);

        var jwtService = CreateJwtTokenService();
        var passwordHasher = new PasswordHasher();
        var currentUser = new MemberTestCurrentUserService
        {
            CustomerId = customer.Id,
            BranchId = branch.Id,
            ActorType = "member",
            MachineCode = "PC-01"
        };

        var memberService = new MemberService(db, currentUser, passwordHasher, jwtService);

        var request = await memberService.CreateTopUpRequestAsync(new CreateMemberTopUpRequest(50000m, "Nạp tại quầy"));

        Assert.NotNull(request);
        Assert.Equal(50000m, request.Amount);
        Assert.Equal(MemberTopUpRequestStatus.Pending, request.Status);
        Assert.Equal("Nạp tại quầy", request.Note);

        var inDb = await db.MemberTopUpRequests.FindAsync(request.Id);
        Assert.NotNull(inDb);
        Assert.Equal(customer.Id, inDb.CustomerId);
    }

    [Fact]
    public async Task CreateTopUpRequest_WhenPendingExists_ThrowsException()
    {
        using var db = CreateInMemoryDb();
        var (branch, _, customer, _) = SetupTopUpContext(db);

        var jwtService = CreateJwtTokenService();
        var passwordHasher = new PasswordHasher();
        var currentUser = new MemberTestCurrentUserService
        {
            CustomerId = customer.Id,
            BranchId = branch.Id,
            ActorType = "member",
            MachineCode = "PC-01"
        };

        var memberService = new MemberService(db, currentUser, passwordHasher, jwtService);

        await memberService.CreateTopUpRequestAsync(new CreateMemberTopUpRequest(50000m, "Lần 1"));

        // Second pending request
        var ex = await Assert.ThrowsAsync<ConflictException>(async () =>
        {
            await memberService.CreateTopUpRequestAsync(new CreateMemberTopUpRequest(100000m, "Lần 2"));
        });

        Assert.Contains("yêu cầu nạp tiền đang chờ", ex.Message);
    }

    [Fact]
    public async Task ApproveTopUpRequest_UpdatesWallet_CreatesPaymentAndCashMovement()
    {
        using var db = CreateInMemoryDb();
        var (branch, cashier, customer, shift) = SetupTopUpContext(db);

        // Member creates request
        var topUpRequest = new MemberTopUpRequest
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            CustomerId = customer.Id,
            Amount = 100000m,
            Note = "Nạp 100k tiền mặt",
            Status = MemberTopUpRequestStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.MemberTopUpRequests.Add(topUpRequest);
        await db.SaveChangesAsync();

        var staffUser = new TestCurrentUserService
        {
            UserId = cashier.Id,
            BranchId = branch.Id,
            Username = cashier.Username,
            Roles = new[] { "Cashier" }
        };

        var walletService = new CustomerWalletService(db, staffUser);

        var result = await walletService.ApproveMemberTopUpRequestAsync(topUpRequest.Id);

        Assert.NotNull(result);
        Assert.Equal(MemberTopUpRequestStatus.Approved, result.Status);
        Assert.NotNull(result.ProcessedAtUtc);

        // Verify customer wallet increased
        var updatedWallet = await db.Wallets.FirstOrDefaultAsync(w => w.CustomerId == customer.Id);
        Assert.Equal(150000m, updatedWallet!.Balance); // 50k + 100k

        // Verify wallet transaction created
        var walletTx = await db.WalletTransactions.FirstOrDefaultAsync(w => w.WalletId == customer.Wallet!.Id && w.Type == WalletTransactionType.TopUp);
        Assert.NotNull(walletTx);
        Assert.Equal(100000m, walletTx.Amount);

        // Verify payment record
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.CustomerId == customer.Id && p.Type == PaymentType.WalletTopUp);
        Assert.NotNull(payment);
        Assert.Equal(100000m, payment.Amount);
        Assert.Equal(PaymentMethod.Cash, payment.Method);

        // Verify cash movement in shift
        var cashMovement = await db.CashMovements.FirstOrDefaultAsync(c => c.ShiftId == shift.Id);
        Assert.NotNull(cashMovement);
        Assert.Equal(100000m, cashMovement.Amount);
        Assert.Equal(CashMovementType.TopUp, cashMovement.Type);

        // Double approval prevented
        var ex = await Assert.ThrowsAsync<ConflictException>(async () =>
        {
            await walletService.ApproveMemberTopUpRequestAsync(topUpRequest.Id);
        });
        Assert.Contains("đã được xử lý", ex.Message);
    }

    [Fact]
    public async Task RejectTopUpRequest_DoesNotChangeWalletBalance()
    {
        using var db = CreateInMemoryDb();
        var (branch, cashier, customer, _) = SetupTopUpContext(db);

        var topUpRequest = new MemberTopUpRequest
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            CustomerId = customer.Id,
            Amount = 100000m,
            Note = "Nạp tiền",
            Status = MemberTopUpRequestStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.MemberTopUpRequests.Add(topUpRequest);
        await db.SaveChangesAsync();

        var staffUser = new TestCurrentUserService
        {
            UserId = cashier.Id,
            BranchId = branch.Id,
            Username = cashier.Username,
            Roles = new[] { "Cashier" }
        };

        var walletService = new CustomerWalletService(db, staffUser);

        var result = await walletService.RejectMemberTopUpRequestAsync(topUpRequest.Id);

        Assert.Equal(MemberTopUpRequestStatus.Rejected, result.Status);

        // Customer wallet balance unchanged
        var updatedWallet = await db.Wallets.FirstOrDefaultAsync(w => w.CustomerId == customer.Id);
        Assert.Equal(50000m, updatedWallet!.Balance);

        // No payments or cash movements created
        Assert.False(await db.Payments.AnyAsync(p => p.CustomerId == customer.Id));
        Assert.False(await db.CashMovements.AnyAsync());
    }
}
