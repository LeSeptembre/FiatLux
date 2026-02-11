using System.Globalization;

namespace FiatLux;

public class BoolToManualModeColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isManualMode && isManualMode)
            return Color.FromArgb("#EF4444"); // Red border when manual mode
        return Color.FromArgb("#2A2A2A"); // Dark border when auto mode
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
