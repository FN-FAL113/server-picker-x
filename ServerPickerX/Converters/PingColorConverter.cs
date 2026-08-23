using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace ServerPickerX.Converters
{
    public class PingColorConverter : IValueConverter
    {
        // Fixed tiers rather than theme brushes, picked to stay readable
        // against both the light and dark window backgrounds
        private static readonly SolidColorBrush Good = new(Color.Parse("#2E9E5B"));

        private static readonly SolidColorBrush Fair = new(Color.Parse("#B8791A"));

        private static readonly SolidColorBrush Poor = new(Color.Parse("#C94A42"));

        private static readonly SolidColorBrush Unknown = new(Color.Parse("#808080"));

        public object? Convert(object? value, Type targetType,
            object? parameter, System.Globalization.CultureInfo culture)
        {
            string str = value as string ?? "0ms";
            if (double.TryParse(str.Replace("ms", ""), NumberStyles.Any, culture, out double val))
            {
                if (val <= 75) return Good;
                if (val <= 150) return Fair;
                return Poor;
            }
            return Unknown;
        }

        public object? ConvertBack(object? value, Type targetType,
            object? parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException("Not implemented.");
        }
    }
}