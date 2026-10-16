using CyberGame.Domain.Services;
using Xunit;

namespace CyberGame.Tests;

public class ShiftCalculationTests
{
    [Fact]
    public void CalculateExpectedCash_CorrectlyAccountsForAllMovements()
    {
        decimal openingCash = 1000000m; // 1 triệu
        decimal cashSales = 500000m;    // 500k bán hàng
        decimal cashTopUps = 300000m;   // 300k nạp ví
        decimal cashIn = 50000m;        // 50k thu khác
        decimal cashRefunds = 20000m;   // 20k hoàn tiền
        decimal cashExpenses = 150000m; // 150k chi tiền điện nước
        decimal cashOut = 30000m;       // 30k chi khác

        // Expected = 1.000.000 + 500.000 + 300.000 + 50.000 - 20.000 - 150.000 - 30.000 = 1.650.000
        decimal expected = ShiftCashCalculator.CalculateExpectedCash(
            openingCash,
            cashSales,
            cashTopUps,
            cashIn,
            cashRefunds,
            cashExpenses,
            cashOut
        );

        Assert.Equal(1650000m, expected);
    }

    [Theory]
    [InlineData(1650000, 1650000, 0)]      // Khớp tiền
    [InlineData(1700000, 1650000, 50000)]  // Thừa 50k
    [InlineData(1600000, 1650000, -50000)] // Thiếu 50k
    public void CalculateDifference_ReturnsAccurateVariance(decimal actual, decimal expected, decimal expectedDiff)
    {
        decimal diff = ShiftCashCalculator.CalculateDifference(actual, expected);
        Assert.Equal(expectedDiff, diff);
    }
}
