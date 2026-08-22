using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ServerPickerX.Converters
{
    public class PacketLossColorConverter : IValueConverter
    {
        // Fixed tiers rather than theme brushes, picked to stay readable
        // against both the light and dark window backgrounds
        private static readonly SolidColorBrush Good = new(Color.Parse("#2E9E5B"));

        private static readonly SolidColorBrush Fair = new(Color.Parse("#B8791A"));

        private static readonly SolidColorBrush Poor = new(Color.Parse("#C94A42"));

        private static readonly SolidColorBrush Unknown = new(Color.Parse("#808080"));

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string str = value as string ?? "0%";
            if (double.TryParse(str.Replace("%", ""), NumberStyles.Any, culture, out double val))
            {
                if (val < 5) return Good;
                if (val <= 20) return Fair;
                return Poor;
            }
            return Unknown;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}