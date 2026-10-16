using CyberGame.Domain.Services;
using Xunit;

namespace CyberGame.Tests;

public class PricingCalculationTests
{
    [Theory]
    [InlineData(10000, 3600, 10000.00)]    // 1 tiếng đúng với 10k/h -> 10.000đ
    [InlineData(10000, 1800, 5000.00)]     // 30 phút với 10k/h -> 5.000đ
    [InlineData(15000, 7200, 30000.00)]    // 2 tiếng với 15k/h -> 30.000đ
    [InlineData(12000, 900, 3000.00)]      // 15 phút với 12k/h -> 3.000đ
    [InlineData(10000, 0, 0.00)]           // 0 giây -> 0đ
    [InlineData(0, 3600, 0.00)]            // Giá 0 -> 0đ
    public void CalculateUsage_CalculatesCorrectAmount(decimal ratePerHour, int seconds, decimal expectedAmount)
    {
        var actual = SessionPricingCalculator.CalculateUsage(ratePerHour, seconds);
        Assert.Equal(expectedAmount, actual);
    }

    [Theory]
    [InlineData(4800, 500, 5000)]
    [InlineData(5200, 500, 5000)]
    [InlineData(5250, 500, 5500)]
    [InlineData(5000, 500, 5000)]
    [InlineData(0, 500, 0)]
    public void RoundForInvoice_RoundsToNearestUnit(decimal amount, int unit, decimal expected)
    {
        var actual = SessionPricingCalculator.RoundForInvoice(amount, unit);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CalculateAvailableSeconds_ReturnsCorrectDuration()
    {
        // Số dư 10.000đ, giá 10.000đ/giờ -> đúng 3.600 giây
        int seconds = SessionPricingCalculator.CalculateAvailableSeconds(10000m, 10000m);
        Assert.Equal(3600, seconds);

        // Số dư 50.000đ, giá 10.000đ/giờ -> 18.000 giây (5 tiếng)
        int fiveHours = SessionPricingCalculator.CalculateAvailableSeconds(50000m, 10000m);
        Assert.Equal(18000, fiveHours);
    }

    [Fact]
    public void CalculateBillableSeconds_SubtractsPausedSeconds()
    {
        var start = DateTime.UtcNow;
        var end = start.AddMinutes(60); // 3600s
        int pausedSeconds = 600; // 10 phút tạm dừng

        int billable = SessionPricingCalculator.CalculateBillableSeconds(start, end, pausedSeconds);
        Assert.Equal(3000, billable);
    }
}
