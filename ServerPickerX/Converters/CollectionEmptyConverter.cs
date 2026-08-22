using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace ServerPickerX.Converters
{
    // Returns true when a bound count is zero, used for the empty state visibility
    public class CollectionEmptyConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is int count && count == 0;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
