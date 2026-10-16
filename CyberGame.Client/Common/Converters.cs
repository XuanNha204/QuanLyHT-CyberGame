using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using CyberGame.Domain.Enums;

namespace CyberGame.Client.Common;

public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool boolVal = value is bool b && b;
        return boolVal ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is Visibility v && v == Visibility.Visible;
    }
}

public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool boolVal = value is bool b && b;
        return boolVal ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is Visibility v && v != Visibility.Visible;
    }
}

public class CurrencyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal d)
        {
            return $"{d:N0} đ";
        }
        if (value is double dbl)
        {
            return $"{dbl:N0} đ";
        }
        if (value is int i)
        {
            return $"{i:N0} đ";
        }
        return "0 đ";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public class SecondsToTimeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int secs)
        {
            var span = TimeSpan.FromSeconds(secs);
            return $"{(int)span.TotalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2}";
        }
        return "00:00:00";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public class MachineStatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is MachineStatus status)
        {
            return status switch
            {
                MachineStatus.Available => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#15803D")), // Success
                MachineStatus.InUse => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0369A1")),     // Info
                MachineStatus.Maintenance => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C2410C")),// Accent / Warning
                MachineStatus.Offline => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")),    // Muted / Offline
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"))
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public class MachineStatusToBadgeBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is MachineStatus status)
        {
            return status switch
            {
                MachineStatus.Available => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCFCE7")), // Light green
                MachineStatus.InUse => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0F2FE")),     // Light blue
                MachineStatus.Maintenance => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEDD5")),// Light orange
                MachineStatus.Offline => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9")),    // Light gray
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9"))
            };
        }
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9"));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public class MachineStatusToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is MachineStatus status)
        {
            return status switch
            {
                MachineStatus.Available => "Sẵn sàng",
                MachineStatus.InUse => "Đang sử dụng",
                MachineStatus.Maintenance => "Bảo trì",
                MachineStatus.Offline => "Ngoại tuyến",
                _ => status.ToString()
            };
        }
        return "Không rõ";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value != null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public class StringNullOrEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public class PaymentMethodToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is PaymentMethod method
            ? method switch
            {
                PaymentMethod.Cash => "Tiền mặt",
                PaymentMethod.Wallet => "Ví hội viên",
                PaymentMethod.BankTransfer => "ATM / MoMo",
                _ => method.ToString()
            }
            : string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public class PaymentStatusToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is PaymentStatus status
            ? status switch
            {
                PaymentStatus.Pending => "Chờ thanh toán",
                PaymentStatus.Completed => "Đã thanh toán",
                PaymentStatus.Failed => "Thanh toán lỗi",
                PaymentStatus.Voided => "Đã hoàn tiền",
                _ => status.ToString()
            }
            : string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}
