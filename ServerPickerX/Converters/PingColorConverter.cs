using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace ServerPickerX.Converters
{
    public class PingColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType,
            object? parameter, System.Globalization.CultureInfo culture)
        {
            string str = value as string ?? "0ms";
            if (double.TryParse(str.Replace("ms", ""), NumberStyles.Any, culture, out double val))
            {
                if (val <= 75) return Brushes.LimeGreen;
                if (val <= 150) return Brushes.Orange;
                return Brushes.Red;
            }
            return Brushes.White;
        }

        public object? ConvertBack(object? value, Type targetType,
            object? parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException("Not implemented.");
        }
    }
}