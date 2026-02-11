using System.Globalization;

namespace FiatLux;

public class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isSelected && isSelected)
            return Color.FromArgb("#FFFFFF"); // White border when selected
        return Color.FromArgb("#2A2A2A"); // Dark border when not selected
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
