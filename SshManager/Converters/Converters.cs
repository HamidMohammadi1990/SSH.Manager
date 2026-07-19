using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using SshManager.Models;

namespace SshManager.Converters;

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var invert = parameter?.ToString() == "Invert";
        var visible = value is true;
        if (invert) visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class ConnectionStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ConnectionStatus status)
        {
            return status switch
            {
                ConnectionStatus.Online => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                ConnectionStatus.Offline => new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                ConnectionStatus.Testing => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
                _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))
            };
        }
        return new SolidColorBrush(Color.FromRgb(158, 158, 158));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class ExecutionStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ExecutionStatus status)
        {
            return status switch
            {
                ExecutionStatus.Success => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                ExecutionStatus.Failed => new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                ExecutionStatus.Running => new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                ExecutionStatus.Skipped => new SolidColorBrush(Color.FromRgb(158, 158, 158)),
                _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))
            };
        }
        return new SolidColorBrush(Color.FromRgb(158, 158, 158));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var invert = parameter?.ToString() == "Invert";
        var hasValue = value != null && (value is not string s || !string.IsNullOrWhiteSpace(s));
        if (invert) hasValue = !hasValue;
        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class TimeSpanToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
                return ts.ToString(@"h\:mm\:ss\.fff");
            return ts.TotalSeconds < 1
                ? $"{ts.TotalMilliseconds:F0} ms"
                : ts.ToString(@"m\:ss\.fff");
        }
        return "—";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class ConnectionTypeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is ConnectionType ct ? ct.ToString().ToUpperInvariant() : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
