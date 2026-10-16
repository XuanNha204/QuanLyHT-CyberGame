using System.Globalization;
using System.Windows;
using System.Windows.Data;
using CyberGame.Client.Common;
using CyberGame.Domain.Enums;
using Xunit;

namespace CyberGame.Tests;

public class ConverterTests
{
    [Fact]
    public void DisplayOnlyConverters_ConvertBack_ReturnsBindingDoNothing()
    {
        var currencyConv = new CurrencyConverter();
        var secondsConv = new SecondsToTimeConverter();
        var brushConv = new MachineStatusToBrushConverter();
        var badgeConv = new MachineStatusToBadgeBackgroundConverter();
        var textConv = new MachineStatusToTextConverter();
        var nullVisConv = new NullToVisibilityConverter();
        var strNullVisConv = new StringNullOrEmptyToVisibilityConverter();

        Assert.Equal(Binding.DoNothing, currencyConv.ConvertBack("1000", typeof(decimal), null, CultureInfo.InvariantCulture));
        Assert.Equal(Binding.DoNothing, secondsConv.ConvertBack("01:00:00", typeof(int), null, CultureInfo.InvariantCulture));
        Assert.Equal(Binding.DoNothing, brushConv.ConvertBack(null, typeof(MachineStatus), null, CultureInfo.InvariantCulture));
        Assert.Equal(Binding.DoNothing, badgeConv.ConvertBack(null, typeof(MachineStatus), null, CultureInfo.InvariantCulture));
        Assert.Equal(Binding.DoNothing, textConv.ConvertBack("Sẵn sàng", typeof(MachineStatus), null, CultureInfo.InvariantCulture));
        Assert.Equal(Binding.DoNothing, nullVisConv.ConvertBack(Visibility.Visible, typeof(object), null, CultureInfo.InvariantCulture));
        Assert.Equal(Binding.DoNothing, strNullVisConv.ConvertBack(Visibility.Visible, typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void CurrencyConverter_Convert_FormatsCorrectly()
    {
        var conv = new CurrencyConverter();
        var result = conv.Convert(50000m, typeof(string), null, new CultureInfo("vi-VN"));
        Assert.NotNull(result);
        Assert.Contains("50", result.ToString());
        Assert.Contains("đ", result.ToString());
    }

    [Fact]
    public void SecondsToTimeConverter_Convert_FormatsCorrectly()
    {
        var conv = new SecondsToTimeConverter();
        var result = conv.Convert(3665, typeof(string), null, CultureInfo.InvariantCulture);
        Assert.Equal("01:01:05", result);
    }

    [Fact]
    public void MachineStatusToTextConverter_Convert_TranslatesStatus()
    {
        var conv = new MachineStatusToTextConverter();
        Assert.Equal("Sẵn sàng", conv.Convert(MachineStatus.Available, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal("Đang sử dụng", conv.Convert(MachineStatus.InUse, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal("Bảo trì", conv.Convert(MachineStatus.Maintenance, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal("Ngoại tuyến", conv.Convert(MachineStatus.Offline, typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void BooleanToVisibilityConverter_TwoWay_WorksCorrectly()
    {
        var conv = new BooleanToVisibilityConverter();
        Assert.Equal(Visibility.Visible, conv.Convert(true, typeof(Visibility), null, CultureInfo.InvariantCulture));
        Assert.Equal(Visibility.Collapsed, conv.Convert(false, typeof(Visibility), null, CultureInfo.InvariantCulture));

        Assert.Equal(true, conv.ConvertBack(Visibility.Visible, typeof(bool), null, CultureInfo.InvariantCulture));
        Assert.Equal(false, conv.ConvertBack(Visibility.Collapsed, typeof(bool), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ShiftCashMovementType_EnumChoice_IncludesExpenseAndCashIn()
    {
        // Kiểm tra enum CashMovementType có sẵn Expense và CashIn khớp với ComboBox trong ShiftView.xaml
        Assert.True(Enum.IsDefined(typeof(CashMovementType), CashMovementType.Expense));
        Assert.True(Enum.IsDefined(typeof(CashMovementType), CashMovementType.CashIn));

        // Kiểm tra parse thành công
        var parsedExpense = Enum.Parse<CashMovementType>("Expense");
        var parsedCashIn = Enum.Parse<CashMovementType>("CashIn");

        Assert.Equal(CashMovementType.Expense, parsedExpense);
        Assert.Equal(CashMovementType.CashIn, parsedCashIn);
    }

    [Fact]
    public void MemberClient_BooleanConverters_WorkAccurately()
    {
        var invConv = new CyberGame.MemberClient.Common.InverseBooleanConverter();
        Assert.Equal(false, invConv.Convert(true, typeof(bool), null, CultureInfo.InvariantCulture));
        Assert.Equal(true, invConv.Convert(false, typeof(bool), null, CultureInfo.InvariantCulture));
        Assert.Equal(false, invConv.ConvertBack(true, typeof(bool), null, CultureInfo.InvariantCulture));
        Assert.Equal(true, invConv.ConvertBack(false, typeof(bool), null, CultureInfo.InvariantCulture));

        var intToBoolConv = new CyberGame.MemberClient.Common.IntegerToBooleanConverter();
        Assert.Equal(true, intToBoolConv.Convert(1, typeof(bool), "1", CultureInfo.InvariantCulture));
        Assert.Equal(false, intToBoolConv.Convert(1, typeof(bool), "2", CultureInfo.InvariantCulture));
        Assert.Equal(2, intToBoolConv.ConvertBack(true, typeof(int), "2", CultureInfo.InvariantCulture));
        Assert.Equal(Binding.DoNothing, intToBoolConv.ConvertBack(false, typeof(int), "2", CultureInfo.InvariantCulture));

        var brushConv = new CyberGame.MemberClient.Common.BooleanToBrushConverter();
        Assert.Equal(brushConv.TrueBrush, brushConv.Convert(true, typeof(System.Windows.Media.Brush), null, CultureInfo.InvariantCulture));
        Assert.Equal(brushConv.FalseBrush, brushConv.Convert(false, typeof(System.Windows.Media.Brush), null, CultureInfo.InvariantCulture));
        Assert.Equal(Binding.DoNothing, brushConv.ConvertBack(brushConv.TrueBrush, typeof(bool), null, CultureInfo.InvariantCulture));
    }
}
