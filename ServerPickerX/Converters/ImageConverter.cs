using Avalonia.Data.Converters;
using ServerPickerX.Helpers;
using System;

namespace ServerPickerX.Converters
{
    public class ImageConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType,
            object? parameter, System.Globalization.CultureInfo culture)
        {
            return ResourceHelper.LoadImageFromResource((string)value!);
        }

        public object? ConvertBack(object? value, Type targetType,
            object? parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException("Not implemented.");
        }
    }
}
