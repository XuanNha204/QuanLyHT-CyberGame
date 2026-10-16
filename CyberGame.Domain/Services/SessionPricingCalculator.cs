namespace CyberGame.Domain.Services;

public static class SessionPricingCalculator
{
    /// <summary>
    /// Tính phí sử dụng cho một khoảng thời gian (giây) theo đơn giá giờ.
    /// </summary>
    public static decimal CalculateUsage(decimal ratePerHour, int billableSeconds)
    {
        if (ratePerHour <= 0 || billableSeconds <= 0)
        {
            return 0m;
        }

        // Đơn giá mỗi giây = ratePerHour / 3600
        decimal exactCost = (ratePerHour / 3600m) * billableSeconds;
        return Math.Round(exactCost, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Làm tròn tiền thanh toán (thường làm tròn đến hàng nghìn VND hoặc hàng trăm tùy quy định).
    /// </summary>
    public static decimal RoundForInvoice(decimal amount, int unit = 500)
    {
        if (amount <= 0 || unit <= 0) return 0m;
        // Làm tròn lên hoặc gần nhất tới đơn vị unit (mặc định 500đ)
        decimal remainder = amount % unit;
        if (remainder == 0) return amount;
        return amount - remainder + (remainder >= (unit / 2m) ? unit : 0m);
    }

    /// <summary>
    /// Tính số giây có thể sử dụng dựa trên số dư và đơn giá mỗi giờ.
    /// </summary>
    public static int CalculateAvailableSeconds(decimal balance, decimal ratePerHour)
    {
        if (balance <= 0 || ratePerHour <= 0) return 0;
        decimal ratePerSecond = ratePerHour / 3600m;
        int seconds = (int)Math.Floor(balance / ratePerSecond);
        return Math.Max(0, seconds);
    }

    /// <summary>
    /// Tính tổng số giây thực tế giữa hai thời điểm, trừ đi tổng giây tạm dừng.
    /// </summary>
    public static int CalculateBillableSeconds(DateTime startedAtUtc, DateTime endedAtUtc, int totalPausedSeconds = 0)
    {
        if (endedAtUtc <= startedAtUtc) return 0;
        int totalSeconds = (int)(endedAtUtc - startedAtUtc).TotalSeconds;
        int billable = totalSeconds - Math.Max(0, totalPausedSeconds);
        return Math.Max(0, billable);
    }
}
