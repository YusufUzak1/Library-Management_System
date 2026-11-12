using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Scriptorium.Core;

public sealed class StatusKindToBackgroundConverter : IValueConverter
{
    private static readonly SolidColorBrush SuccessBrush = Freeze(new(Color.FromRgb(0xE6, 0xF2, 0xEB)));
    private static readonly SolidColorBrush WarningBrush = Freeze(new(Color.FromRgb(0xFD, 0xF3, 0xE4)));
    private static readonly SolidColorBrush DangerBrush = Freeze(new(Color.FromRgb(0xFB, 0xEA, 0xEB)));
    private static readonly SolidColorBrush InfoBrush = Freeze(new(Color.FromRgb(0xE8, 0xF0, 0xF8)));
    private static readonly SolidColorBrush NeutralBrush = Freeze(new(Color.FromRgb(0xEF, 0xEC, 0xE6)));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value as string) switch
        {
            "Success" => SuccessBrush,
            "Warning" => WarningBrush,
            "Danger" => DangerBrush,
            "Info" => InfoBrush,
            _ => NeutralBrush
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;

    private static SolidColorBrush Freeze(SolidColorBrush b) { b.Freeze(); return b; }
}

public sealed class StatusKindToForegroundConverter : IValueConverter
{
    private static readonly SolidColorBrush SuccessBrush = Freeze(new(Color.FromRgb(0x1B, 0x5E, 0x3A)));
    private static readonly SolidColorBrush WarningBrush = Freeze(new(Color.FromRgb(0x96, 0x5A, 0x16)));
    private static readonly SolidColorBrush DangerBrush = Freeze(new(Color.FromRgb(0x9E, 0x2A, 0x2B)));
    private static readonly SolidColorBrush InfoBrush = Freeze(new(Color.FromRgb(0x23, 0x52, 0x7C)));
    private static readonly SolidColorBrush NeutralBrush = Freeze(new(Color.FromRgb(0x52, 0x5B, 0x56)));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value as string) switch
        {
            "Success" => SuccessBrush,
            "Warning" => WarningBrush,
            "Danger" => DangerBrush,
            "Info" => InfoBrush,
            _ => NeutralBrush
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;

    private static SolidColorBrush Freeze(SolidColorBrush b) { b.Freeze(); return b; }
}

public sealed class StatusKindToBorderConverter : IValueConverter
{
    private static readonly SolidColorBrush SuccessBrush = Freeze(new(Color.FromRgb(0xB8, 0xDC, 0xC7)));
    private static readonly SolidColorBrush WarningBrush = Freeze(new(Color.FromRgb(0xEB, 0xD0, 0xA4)));
    private static readonly SolidColorBrush DangerBrush = Freeze(new(Color.FromRgb(0xE8, 0xB8, 0xB9)));
    private static readonly SolidColorBrush InfoBrush = Freeze(new(Color.FromRgb(0xBD, 0xD4, 0xEA)));
    private static readonly SolidColorBrush NeutralBrush = Freeze(new(Color.FromRgb(0xDC, 0xD8, 0xCF)));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value as string) switch
        {
            "Success" => SuccessBrush,
            "Warning" => WarningBrush,
            "Danger" => DangerBrush,
            "Info" => InfoBrush,
            _ => NeutralBrush
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;

    private static SolidColorBrush Freeze(SolidColorBrush b) { b.Freeze(); return b; }
}

public sealed class HexToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush DefaultCover = new(Color.FromRgb(0x1E, 0x4D, 0x3B));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
            catch
            {
                return DefaultCover;
            }
        }
        return DefaultCover;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class StringMatchToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var current = value?.ToString() ?? string.Empty;
        var target = parameter?.ToString() ?? string.Empty;
        return string.Equals(current, target, StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class StringMatchToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var current = value?.ToString() ?? string.Empty;
        var target = parameter?.ToString() ?? string.Empty;
        return string.Equals(current, target, StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true && parameter is not null)
            return parameter.ToString()!;
        return Binding.DoNothing;
    }
}

public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

