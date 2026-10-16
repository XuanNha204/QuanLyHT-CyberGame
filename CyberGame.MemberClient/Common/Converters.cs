using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using CyberGame.Domain.Enums;

namespace CyberGame.MemberClient.Common;

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
            var span = TimeSpan.FromSeconds(Math.Max(0, secs));
            return $"{(int)span.TotalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2}";
        }
        if (value is long lSecs)
        {
            var span = TimeSpan.FromSeconds(Math.Max(0, lSecs));
            return $"{(int)span.TotalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2}";
        }
        return "00:00:00";
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

public class TopUpStatusToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is MemberTopUpRequestStatus status)
        {
            return status switch
            {
                MemberTopUpRequestStatus.Pending => "Chờ duyệt",
                MemberTopUpRequestStatus.Approved => "Đã duyệt",
                MemberTopUpRequestStatus.Rejected => "Từ chối",
                _ => status.ToString()
            };
        }
        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public class TopUpStatusToBadgeBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is MemberTopUpRequestStatus status)
        {
            return status switch
            {
                MemberTopUpRequestStatus.Pending => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B")), // Warning
                MemberTopUpRequestStatus.Approved => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22C55E")), // Success
                MemberTopUpRequestStatus.Rejected => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444")), // Danger
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"))
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public class OrderStatusToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is OrderStatus status)
        {
            return status switch
            {
                OrderStatus.Draft => "Chờ duyệt",
                OrderStatus.Confirmed => "Đang chuẩn bị",
                OrderStatus.Fulfilled => "Đã phục vụ",
                OrderStatus.Cancelled => "Đã từ chối",
                _ => status.ToString()
            };
        }
        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public class OrderStatusToBadgeBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is OrderStatus status)
        {
            return status switch
            {
                OrderStatus.Draft => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")),
                OrderStatus.Confirmed => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6")),
                OrderStatus.Fulfilled => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22C55E")),
                OrderStatus.Cancelled => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444")),
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"))
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
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

public class PaymentStatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var color = value is PaymentStatus status
            ? status switch
            {
                PaymentStatus.Pending => "#F59E0B",
                PaymentStatus.Completed => "#22C55E",
                PaymentStatus.Failed => "#EF4444",
                PaymentStatus.Voided => "#94A3B8",
                _ => "#CBD5E1"
            }
            : "#CBD5E1";

        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public class MachineStatusToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is MachineStatus status
            ? status switch
            {
                MachineStatus.Available => "Sẵn sàng",
                MachineStatus.InUse => "Đang sử dụng",
                MachineStatus.Maintenance => "Bảo trì",
                MachineStatus.Offline => "Ngoại tuyến",
                _ => status.ToString()
            }
            : string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public class MachineStatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var color = value is MachineStatus status
            ? status switch
            {
                MachineStatus.Available => "#22C55E",
                MachineStatus.InUse => "#3B82F6",
                MachineStatus.Maintenance => "#F59E0B",
                MachineStatus.Offline => "#64748B",
                _ => "#64748B"
            }
            : "#64748B";
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public class MachineStatusToBadgeBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var color = value is MachineStatus status
            ? status switch
            {
                MachineStatus.Available => "#143524",
                MachineStatus.InUse => "#172E55",
                MachineStatus.Maintenance => "#3D2A05",
                MachineStatus.Offline => "#253044",
                _ => "#253044"
            }
            : "#253044";
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b ? !b : false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b ? !b : false;
    }
}

public class IntegerToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intVal && parameter != null)
        {
            if (int.TryParse(parameter.ToString(), out int target))
            {
                return intVal == target;
            }
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b && parameter != null)
        {
            if (int.TryParse(parameter.ToString(), out int target))
            {
                return target;
            }
        }
        return Binding.DoNothing;
    }
}

public class BooleanToBrushConverter : IValueConverter
{
    public Brush TrueBrush { get; set; } = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#15803D")); // Green
    public Brush FalseBrush { get; set; } = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155")); // Slate

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isTrue = value is bool b && b;
        return isTrue ? TrueBrush : FalseBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}
