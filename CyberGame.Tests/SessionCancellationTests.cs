using CyberGame.Application.Common;
using CyberGame.Application.DTOs;
using CyberGame.Application.Services;
using CyberGame.Domain.Entities;
using CyberGame.Domain.Enums;
using CyberGame.Domain.Exceptions;
using CyberGame.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CyberGame.Tests;

public class SessionCancellationTests
{
    private CyberGameDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<CyberGameDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new CyberGameDbContext(options);
    }

    [Fact]
    public async Task CancelSession_ThrowsWhenReasonIsEmpty()
    {
        using var db = CreateInMemoryDb();
        var branchId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var currentUser = new TestCurrentUserService
        {
            UserId = userId,
            BranchId = branchId,
            Roles = new[] { "Cashier" }
        };

        var pricingService = new PricingService(db, currentUser);
        var sessionService = new SessionService(db, currentUser, pricingService);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sessionService.CancelSessionAsync(Guid.NewGuid(), new CancelSessionRequest("   ")));

        Assert.Equal("reason_required", ex.Code);
    }

    [Fact]
    public async Task CancelSession_ThrowsWhenUncancelledOrdersExist()
    {
        using var db = CreateInMemoryDb();
        var branchId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var branch = new Branch { Id = branchId, Code = "B1", Name = "Chi nhánh 1", IsActive = true };
        var machine = new Machine { Id = Guid.NewGuid(), BranchId = branchId, Code = "M1", DisplayName = "Máy 1", Status = MachineStatus.InUse, IsActive = true };
        var session = new PlaySession
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            MachineId = machine.Id,
            SessionNumber = "SES-001",
            Status = PlaySessionStatus.Open,
            BillingMode = BillingMode.Postpaid,
            StartedAtUtc = DateTime.UtcNow
        };
        var order = new Order
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            PlaySessionId = session.Id,
            OrderNumber = "ORD-001",
            Status = OrderStatus.Confirmed, // Đơn chưa hủy
            Subtotal = 20000m,
            TotalAmount = 20000m
        };
        session.Orders.Add(order);

        db.Branches.Add(branch);
        db.Machines.Add(machine);
        db.PlaySessions.Add(session);
        await db.SaveChangesAsync();

        var currentUser = new TestCurrentUserService
        {
            UserId = userId,
            BranchId = branchId,
            Roles = new[] { "Cashier" }
        };

        var pricingService = new PricingService(db, currentUser);
        var sessionService = new SessionService(db, currentUser, pricingService);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sessionService.CancelSessionAsync(session.Id, new CancelSessionRequest("Khách hủy")));

        Assert.Equal("session_has_uncancelled_orders", ex.Code);
    }

    [Fact]
    public async Task CancelSession_ThrowsWhenAccruedChargesExist()
    {
        using var db = CreateInMemoryDb();
        var branchId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var branch = new Branch { Id = branchId, Code = "B1", Name = "Chi nhánh 1", IsActive = true };
        var machine = new Machine { Id = Guid.NewGuid(), BranchId = branchId, Code = "M1", DisplayName = "Máy 1", Status = MachineStatus.InUse, IsActive = true };
        var session = new PlaySession
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            MachineId = machine.Id,
            SessionNumber = "SES-002",
            Status = PlaySessionStatus.Open,
            BillingMode = BillingMode.Postpaid,
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-30) // Bắt đầu từ 30 phút trước
        };
        // Segment 10.000đ/giờ bắt đầu từ 30 phút trước
        session.ChargeSegments.Add(new SessionChargeSegment
        {
            Id = Guid.NewGuid(),
            PlaySessionId = session.Id,
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-30),
            RatePerHour = 10000m
        });

        db.Branches.Add(branch);
        db.Machines.Add(machine);
        db.PlaySessions.Add(session);
        await db.SaveChangesAsync();

        var currentUser = new TestCurrentUserService
        {
            UserId = userId,
            BranchId = branchId,
            Roles = new[] { "Cashier" }
        };

        var pricingService = new PricingService(db, currentUser);
        var sessionService = new SessionService(db, currentUser, pricingService);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sessionService.CancelSessionAsync(session.Id, new CancelSessionRequest("Khách muốn hủy")));

        Assert.Equal("session_has_accrued_charges", ex.Code);
    }

    [Fact]
    public async Task CancelSession_SucceedsWhenNoOrdersAndZeroCharges()
    {
        using var db = CreateInMemoryDb();
        var branchId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var branch = new Branch { Id = branchId, Code = "B1", Name = "Chi nhánh 1", IsActive = true };
        db.Branches.Add(branch);

        var group = new MachineGroup { Id = Guid.NewGuid(), BranchId = branchId, Code = "G1", Name = "Khu 1", IsActive = true };
        db.MachineGroups.Add(group);

        var user = new AppUser
        {
            Id = userId,
            BranchId = branchId,
            Username = "cashier1",
            NormalizedUsername = "CASHIER1",
            FullName = "Thu ngân 1",
            PasswordHash = "hash",
            IsActive = true
        };
        db.Users.Add(user);

        var shift = new Shift
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            UserId = userId,
            ShiftNumber = "SHF-001",
            Status = ShiftStatus.Open,
            OpenedAtUtc = DateTime.UtcNow
        };
        db.Shifts.Add(shift);

        var machine = new Machine
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            MachineGroupId = group.Id,
            Code = "M1",
            DisplayName = "Máy 1",
            Status = MachineStatus.InUse,
            IsActive = true
        };
        db.Machines.Add(machine);

        var session = new PlaySession
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            MachineId = machine.Id,
            ShiftId = shift.Id,
            SessionNumber = "SES-003",
            Status = PlaySessionStatus.Open,
            BillingMode = BillingMode.Postpaid,
            StartedAtUtc = DateTime.UtcNow
        };
        // 0 segments or 0 rate
        session.ChargeSegments.Add(new SessionChargeSegment
        {
            Id = Guid.NewGuid(),
            PlaySessionId = session.Id,
            StartedAtUtc = DateTime.UtcNow,
            RatePerHour = 0m
        });

        db.PlaySessions.Add(session);
        await db.SaveChangesAsync();

        var currentUser = new TestCurrentUserService
        {
            UserId = userId,
            BranchId = branchId,
            Roles = new[] { "Cashier" }
        };

        var pricingService = new PricingService(db, currentUser);
        var sessionService = new SessionService(db, currentUser, pricingService);

        var result = await sessionService.CancelSessionAsync(session.Id, new CancelSessionRequest("Máy bị lỗi phần cứng lúc mở"));

        Assert.Equal(PlaySessionStatus.Cancelled, result.Status);
        Assert.Equal(MachineStatus.Available, machine.Status);
    }
}
