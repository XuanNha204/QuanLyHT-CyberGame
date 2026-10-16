namespace CyberGame.Domain.Services;

public static class ShiftCashCalculator
{
    /// <summary>
    /// Tính số tiền mặt kỳ vọng khi đóng ca.
    /// ExpectedCash = OpeningCash + CashInflows - CashOutflows
    /// </summary>
    public static decimal CalculateExpectedCash(
        decimal openingCash,
        decimal cashSales,
        decimal cashTopUps,
        decimal cashOtherIn,
        decimal cashRefunds,
        decimal cashExpenses,
        decimal cashOtherOut)
    {
        decimal inflows = cashSales + cashTopUps + cashOtherIn;
        decimal outflows = cashRefunds + cashExpenses + cashOtherOut;
        return openingCash + inflows - outflows;
    }

    /// <summary>
    /// Tính độ lệch tiền mặt: Chênh lệch = Tiền thực đếm - Tiền kỳ vọng
    /// Số âm: thiếu tiền, Số dương: thừa tiền
    /// </summary>
    public static decimal CalculateDifference(decimal actualCash, decimal expectedCash)
    {
        return actualCash - expectedCash;
    }
}
