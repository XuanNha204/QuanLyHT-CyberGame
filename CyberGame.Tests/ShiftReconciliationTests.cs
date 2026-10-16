using CyberGame.Application.Common;
using CyberGame.Application.DTOs;
using CyberGame.Application.Services;
using CyberGame.Domain.Entities;
using CyberGame.Domain.Enums;
using CyberGame.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CyberGame.Tests;

public class ShiftReconciliationTests
{
    private CyberGameDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<CyberGameDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new CyberGameDbContext(options);
    }

    [Fact]
    public async Task ShiftReconciliation_IncludesCashMovementExpense_InSummaryAndCloseShift()
    {
        using var db = CreateInMemoryDb();
        var branchId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var branch = new Branch
        {
            Id = branchId,
            Code = "B1",
            Name = "Chi nhánh 1",
            IsActive = true
        };
        var user = new AppUser
        {
            Id = userId,
            BranchId = branchId,
            Username = "cashier1",
            NormalizedUsername = "CASHIER1",
            FullName = "Thu ngân 1",
            IsActive = true
        };
        db.Branches.Add(branch);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var currentUser = new TestCurrentUserService
        {
            UserId = userId,
            BranchId = branchId,
            Username = "cashier1",
            Roles = new[] { "Cashier" }
        };

        var shiftService = new ShiftService(db, currentUser);

        // 1. Mở ca với 1.000.000đ
        var shift = await shiftService.OpenShiftAsync(new OpenShiftRequest(1000000m));

        // 2. Thêm chi phí thông qua CashMovement với loại Expense (ví dụ mua đá, giấy in) 150.000đ
        await shiftService.AddCashMovementAsync(shift.Id, new CreateCashMovementRequest(
            CashMovementType.Expense,
            150000m,
            "Mua giấy in nhiệt"
        ));

        // 3. Thêm chi phí thông qua CashMovement loại CashOut 50.000đ
        await shiftService.AddCashMovementAsync(shift.Id, new CreateCashMovementRequest(
            CashMovementType.CashOut,
            50000m,
            "Chi tiền lẻ"
        ));

        // 4. Thêm thu thông qua CashMovement loại CashIn 200.000đ
        await shiftService.AddCashMovementAsync(shift.Id, new CreateCashMovementRequest(
            CashMovementType.CashIn,
            200000m,
            "Thu tiền hoàn cọc thẻ"
        ));

        // Expected Cash = 1.000.000 (Mở) + 200.000 (CashIn) - 150.000 (Expense) - 50.000 (CashOut) = 1.000.000
        var summary = await shiftService.GetShiftSummaryAsync(shift.Id);
        Assert.Equal(150000m, summary.TotalCashExpenses);
        Assert.Equal(50000m, summary.TotalCashOut);
        Assert.Equal(200000m, summary.TotalCashIn);
        Assert.Equal(1000000m, summary.ExpectedCash);

        // 5. Đóng ca với tiền thực tế 1.000.000đ -> Chênh lệch = 0
        var closedShift = await shiftService.CloseShiftAsync(shift.Id, new CloseShiftRequest(1000000m, "Khớp tiền tuyệt đối"));
        Assert.Equal(ShiftStatus.Closed, closedShift.Status);
        Assert.Equal(1000000m, closedShift.ActualCash);
        Assert.Equal(1000000m, closedShift.ExpectedCash);
        Assert.Equal(0m, closedShift.Difference);
    }
}
