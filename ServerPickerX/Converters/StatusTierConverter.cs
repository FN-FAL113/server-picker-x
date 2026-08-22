using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace ServerPickerX.Converters
{
    // Maps ServerModel.Status onto a style class for the reachability dot
    public class StatusTierConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string requestedState = parameter as string ?? string.Empty;
            string state = value as string ?? string.Empty;

            return state.Equals(requestedState, StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
