using System.Globalization;

namespace WidePlay.Converters;

// Flips a bool for binding — e.g. show a "Connect" button only when NOT connected.
// Exposed as a static Instance so XAML can use it without declaring a resource.
public class InverseBoolConverter : IValueConverter
{
    public static readonly InverseBoolConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value!;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value!;
}
