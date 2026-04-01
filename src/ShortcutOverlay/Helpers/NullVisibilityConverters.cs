using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ShortcutOverlay.Helpers;

/// <summary>
/// Returns Visible when the value is NOT null, Collapsed when null.
/// Use for elements that should only appear when data is present.
/// </summary>
public class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value != null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns Visible when the value IS null, Collapsed when not null.
/// Use for fallback elements that should only appear when data is missing.
/// </summary>
public class NullToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value == null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
