using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ServerPickerX.Converters
{
    public class PacketLossColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string str = value as string ?? "0%";
            if (double.TryParse(str.Replace("%", ""), NumberStyles.Any, culture, out double val))
            {
                if (val < 5) return Brushes.LimeGreen;
                if (val <= 20) return Brushes.Orange;
                return Brushes.Red;
            }
            return Brushes.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}