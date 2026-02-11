using System.Globalization;

namespace FiatLux;

public class BoolToConnectionColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isConnected && isConnected)
            return Color.FromArgb("#4ADE80"); // Green when connected
        return Color.FromArgb("#EF4444"); // Red when disconnected
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
