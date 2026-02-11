using System.Globalization;

namespace FiatLux;

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            return status.ToLower() switch
            {
                "online" => Color.FromArgb("#4ADE80"),
                "offline" => Color.FromArgb("#6B6B6B"),
                _ => Color.FromArgb("#6B6B6B")
            };
        }
        return Color.FromArgb("#6B6B6B");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class LampStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            return status.ToLower() switch
            {
                "on" => Color.FromArgb("#4ADE80"),
                "off" => Color.FromArgb("#6B6B6B"),
                "error" => Color.FromArgb("#EF4444"),
                _ => Color.FromArgb("#6B6B6B")
            };
        }
        return Color.FromArgb("#6B6B6B");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class TimestampToDateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is long timestamp)
        {
            var date = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime;
            var now = DateTime.Now;
            var diff = now - date;

            if (diff.TotalSeconds < 60)
                return "Just now";
            if (diff.TotalMinutes < 60)
                return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24)
                return $"{(int)diff.TotalHours}h ago";
            
            return date.ToString("MMM dd, HH:mm");
        }
        return "Unknown";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
