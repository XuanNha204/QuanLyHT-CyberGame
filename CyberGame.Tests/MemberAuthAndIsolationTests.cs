using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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

public class MemberAuthAndIsolationTests
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

    private (Branch branch, Machine machine, Customer customer) SeedMemberData(CyberGameDbContext db)
    {
        var branch = new Branch
        {
            Id = Guid.NewGuid(),
            Code = "CN-Q1",
            Name = "Chi nhánh Quận 1",
            TimeZoneId = "SE Asia Standard Time",
            IsActive = true
        };
        db.Branches.Add(branch);

        var staffUser = new AppUser
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            Username = "cashier01",
            NormalizedUsername = "CASHIER01",
            FullName = "Thu ngân 01",
            PasswordHash = "test",
            IsActive = true
        };
        db.Users.Add(staffUser);

        db.Shifts.Add(new Shift
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            UserId = staffUser.Id,
            ShiftNumber = "CA-TEST",
            OpenedAtUtc = DateTime.UtcNow.AddHours(-1),
            OpeningCash = 0m,
            Status = ShiftStatus.Open
        });

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
            Status = MachineStatus.Available,
            IsActive = true
        };
        db.Machines.Add(machine);

        var passwordHasher = new PasswordHasher();
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            HomeBranchId = branch.Id,
            Code = "KH-001",
            FullName = "Nguyễn Văn Member",
            MemberPinHash = passwordHasher.HashPassword("123456"),
            MemberFailedLoginCount = 0,
            Status = CustomerStatus.Active
        };
        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Balance = 100000m
        };
        customer.Wallet = wallet;
        db.Customers.Add(customer);
        db.Wallets.Add(wallet);
        db.SaveChanges();

        return (branch, machine, customer);
    }

    [Fact]
    public async Task MemberLogin_ValidCredentials_ReturnsTokenAndInfo()
    {
        using var db = CreateInMemoryDb();
        var (_, machine, _) = SeedMemberData(db);

        var jwtService = CreateJwtTokenService();
        var passwordHasher = new PasswordHasher();
        var currentUser = new TestCurrentUserService();

        var memberService = new MemberService(db, currentUser, passwordHasher, jwtService);

        var result = await memberService.LoginAsync(new MemberLoginRequest("KH-001", "123456", machine.Code));

        Assert.NotNull(result);
        Assert.NotEmpty(result.Token);
        Assert.Equal("KH-001", result.Member.Code);
        Assert.Equal(machine.Code, result.Machine.Code);
        Assert.Equal(MachineStatus.InUse, machine.Status);
        var session = await db.PlaySessions.SingleAsync(s => s.MachineId == machine.Id);
        Assert.Equal(result.Member.Id, session.CustomerId);
        Assert.Equal(PlaySessionStatus.Open, session.Status);
        Assert.Equal(BillingMode.PrepaidWallet, session.BillingMode);

        // Verify customer login count reset and last login updated
        var updatedCustomer = await db.Customers.FirstAsync(c => c.Code == "KH-001");
        Assert.Equal(0, updatedCustomer.MemberFailedLoginCount);
        Assert.NotNull(updatedCustomer.MemberLastLoginAtUtc);
    }

    [Fact]
    public async Task MemberMachineList_ReturnsAllActiveMachinesWithLiveStatus()
    {
        using var db = CreateInMemoryDb();
        var (branch, machine, _) = SeedMemberData(db);
        var secondMachine = new Machine
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            MachineGroupId = machine.MachineGroupId,
            Code = "PC-02",
            DisplayName = "Máy VIP 02",
            Status = MachineStatus.Maintenance,
            IsActive = true
        };
        db.Machines.Add(secondMachine);
        await db.SaveChangesAsync();

        var service = new MemberService(db, new TestCurrentUserService(), new PasswordHasher(), CreateJwtTokenService());
        var machines = await service.GetMachinesAsync();

        Assert.Equal(2, machines.Count);
        Assert.Contains(machines, m => m.Id == machine.Id && m.Status == MachineStatus.Available);
        Assert.Contains(machines, m => m.Id == secondMachine.Id && m.Status == MachineStatus.Maintenance);
    }

    [Fact]
    public async Task MemberLogin_SameMachineTwice_ReusesCurrentSession()
    {
        using var db = CreateInMemoryDb();
        var (_, machine, _) = SeedMemberData(db);
        var service = new MemberService(db, new TestCurrentUserService(), new PasswordHasher(), CreateJwtTokenService());

        await service.LoginAsync(new MemberLoginRequest("KH-001", "123456", machine.Code, machine.Id));
        await service.LoginAsync(new MemberLoginRequest("KH-001", "123456", machine.Code, machine.Id));

        Assert.Equal(1, await db.PlaySessions.CountAsync(s =>
            s.MachineId == machine.Id &&
            (s.Status == PlaySessionStatus.Open || s.Status == PlaySessionStatus.Paused)));
    }

    [Fact]
    public async Task MemberLogin_MachineOccupiedByAnotherMember_IsRejected()
    {
        using var db = CreateInMemoryDb();
        var (branch, machine, _) = SeedMemberData(db);
        var passwordHasher = new PasswordHasher();
        var secondCustomer = new Customer
        {
            Id = Guid.NewGuid(),
            HomeBranchId = branch.Id,
            Code = "KH-002",
            FullName = "Hội viên thứ hai",
            MemberPinHash = passwordHasher.HashPassword("654321"),
            Status = CustomerStatus.Active,
            Wallet = new Wallet
            {
                Id = Guid.NewGuid(),
                Balance = 100000m
            }
        };
        secondCustomer.Wallet.CustomerId = secondCustomer.Id;
        db.Customers.Add(secondCustomer);
        await db.SaveChangesAsync();

        var service = new MemberService(db, new TestCurrentUserService(), passwordHasher, CreateJwtTokenService());
        await service.LoginAsync(new MemberLoginRequest("KH-001", "123456", machine.Code, machine.Id));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.LoginAsync(new MemberLoginRequest("KH-002", "654321", machine.Code, machine.Id)));

        Assert.Equal("machine_not_available", ex.Code);
        Assert.Single(await db.PlaySessions.Where(s => s.MachineId == machine.Id).ToListAsync());
    }

    [Fact]
    public async Task StaffCreatedMember_WithInitialPin_CanLoginImmediately()
    {
        using var db = CreateInMemoryDb();
        var (branch, machine, _) = SeedMemberData(db);
        var passwordHasher = new PasswordHasher();
        var staffContext = new TestCurrentUserService { BranchId = branch.Id };
        var customerService = new CustomerWalletService(db, staffContext, passwordHasher);

        var created = await customerService.CreateCustomerAsync(new CreateCustomerRequest(
            null, "HV-MOI", "Hội viên mới", "0900000000", null, null, "654321", 0, null));

        Assert.NotEqual("654321", (await db.Customers.FindAsync(created.Id))!.MemberPinHash);
        (await db.Wallets.SingleAsync(w => w.CustomerId == created.Id)).Balance = 100000m;
        await db.SaveChangesAsync();
        var memberService = new MemberService(db, new TestCurrentUserService(), passwordHasher, CreateJwtTokenService());
        var login = await memberService.LoginAsync(new MemberLoginRequest("HV-MOI", "654321", machine.Code));
        Assert.Equal(created.Id, login.Member.Id);
    }

    [Fact]
    public async Task ChangePin_InvalidatesOldPinAndAcceptsNewPin()
    {
        using var db = CreateInMemoryDb();
        var (branch, machine, customer) = SeedMemberData(db);
        var passwordHasher = new PasswordHasher();
        var memberContext = new MemberTestCurrentUserService
        {
            CustomerId = customer.Id,
            BranchId = branch.Id,
            MachineCode = machine.Code,
            ActorType = "member"
        };
        var memberService = new MemberService(db, memberContext, passwordHasher, CreateJwtTokenService());

        await memberService.ChangePinAsync(new ChangeMemberPinRequest("123456", "654321"));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            memberService.LoginAsync(new MemberLoginRequest(customer.Code, "123456", machine.Code)));
        var login = await memberService.LoginAsync(new MemberLoginRequest(customer.Code, "654321", machine.Code));
        Assert.Equal(customer.Id, login.Member.Id);
    }

    [Fact]
    public async Task MemberLogin_WrongPin_IncrementsFailedCount()
    {
        using var db = CreateInMemoryDb();
        var (_, machine, _) = SeedMemberData(db);

        var jwtService = CreateJwtTokenService();
        var passwordHasher = new PasswordHasher();
        var currentUser = new TestCurrentUserService();

        var memberService = new MemberService(db, currentUser, passwordHasher, jwtService);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(async () =>
        {
            await memberService.LoginAsync(new MemberLoginRequest("KH-001", "999999", machine.Code));
        });

        Assert.Contains("Mã PIN không chính xác", ex.Message);

        var updatedCustomer = await db.Customers.FirstAsync(c => c.Code == "KH-001");
        Assert.Equal(1, updatedCustomer.MemberFailedLoginCount);
    }

    [Fact]
    public async Task MemberLogin_FiveWrongPins_LocksAccount()
    {
        using var db = CreateInMemoryDb();
        var (_, machine, _) = SeedMemberData(db);

        var jwtService = CreateJwtTokenService();
        var passwordHasher = new PasswordHasher();
        var currentUser = new TestCurrentUserService();

        var memberService = new MemberService(db, currentUser, passwordHasher, jwtService);

        for (int i = 1; i <= 4; i++)
        {
            await Assert.ThrowsAsync<BusinessRuleException>(async () =>
            {
                await memberService.LoginAsync(new MemberLoginRequest("KH-001", "999999", machine.Code));
            });
        }

        var fifthEx = await Assert.ThrowsAsync<BusinessRuleException>(async () =>
        {
            await memberService.LoginAsync(new MemberLoginRequest("KH-001", "999999", machine.Code));
        });

        Assert.Contains("Tài khoản đã bị tạm khóa 15 phút", fifthEx.Message);

        var lockedCustomer = await db.Customers.FirstAsync(c => c.Code == "KH-001");
        Assert.Equal(5, lockedCustomer.MemberFailedLoginCount);
        Assert.NotNull(lockedCustomer.MemberLockoutEndUtc);
        Assert.True(lockedCustomer.MemberLockoutEndUtc > DateTime.UtcNow);

        // Attempting even with correct PIN while locked
        var lockedEx = await Assert.ThrowsAsync<BusinessRuleException>(async () =>
        {
            await memberService.LoginAsync(new MemberLoginRequest("KH-001", "123456", machine.Code));
        });

        Assert.Contains("Tài khoản bị tạm khóa", lockedEx.Message);
    }

    [Fact]
    public async Task MemberLogin_MachineBranchMismatch_Fails()
    {
        using var db = CreateInMemoryDb();
        var (_, _, _) = SeedMemberData(db);

        // Another branch machine
        var otherBranch = new Branch
        {
            Id = Guid.NewGuid(),
            Code = "CN-Q3",
            Name = "Chi nhánh Quận 3",
            TimeZoneId = "SE Asia Standard Time",
            IsActive = true
        };
        db.Branches.Add(otherBranch);

        var otherGroup = new MachineGroup
        {
            Id = Guid.NewGuid(),
            BranchId = otherBranch.Id,
            Code = "STD",
            Name = "Khu Thường",
            IsActive = true
        };
        db.MachineGroups.Add(otherGroup);

        var otherMachine = new Machine
        {
            Id = Guid.NewGuid(),
            BranchId = otherBranch.Id,
            MachineGroupId = otherGroup.Id,
            Code = "PC-Q3-01",
            DisplayName = "Máy Q3 01",
            Status = MachineStatus.Available,
            IsActive = true
        };
        db.Machines.Add(otherMachine);
        db.SaveChanges();

        var jwtService = CreateJwtTokenService();
        var passwordHasher = new PasswordHasher();
        var currentUser = new TestCurrentUserService();

        var memberService = new MemberService(db, currentUser, passwordHasher, jwtService);

        // Member is registered at CN-Q1, tries to login at PC-Q3-01
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(async () =>
        {
            await memberService.LoginAsync(new MemberLoginRequest("KH-001", "123456", otherMachine.Code));
        });

        Assert.Contains("không thuộc chi nhánh", ex.Message);
    }

    [Fact]
    public void JwtTokenService_GeneratesDistinctActorTypes_ForStaffAndMember()
    {
        var jwtService = CreateJwtTokenService();
        var tokenHandler = new JwtSecurityTokenHandler();

        var staffUserId = Guid.NewGuid();
        var staffBranchId = Guid.NewGuid();
        var staffRoles = new List<string> { "Cashier" };

        var staffToken = jwtService.GenerateAccessToken(staffUserId, "staff_cashier", staffBranchId, staffRoles);
        var staffJwt = tokenHandler.ReadJwtToken(staffToken);

        Assert.NotNull(staffJwt);
        Assert.Equal("staff", staffJwt.Claims.FirstOrDefault(c => c.Type == "actor_type")?.Value);
        Assert.Equal(staffUserId.ToString(), staffJwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub")?.Value);

        var customerId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var memberCode = "KH-001";

        var memberToken = jwtService.GenerateMemberAccessToken(customerId, memberCode, branchId, "PC-01");
        var memberJwt = tokenHandler.ReadJwtToken(memberToken);

        Assert.NotNull(memberJwt);
        Assert.Equal("member", memberJwt.Claims.FirstOrDefault(c => c.Type == "actor_type")?.Value);
        Assert.Equal("PC-01", memberJwt.Claims.FirstOrDefault(c => c.Type == "machine_code")?.Value);
        Assert.Equal(customerId.ToString(), memberJwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub")?.Value);
        Assert.Equal(customerId.ToString(), memberJwt.Claims.FirstOrDefault(c => c.Type == "customer_id")?.Value);
    }
}
