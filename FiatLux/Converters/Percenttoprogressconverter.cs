using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace FiatLux.Converters
{
    public class PercentToProgressConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int percent)
            {
                // ProgressBar attend une valeur entre 0.0 et 1.0
                return percent / 100.0;
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}