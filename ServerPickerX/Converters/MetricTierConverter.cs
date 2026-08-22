using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace ServerPickerX.Converters
{
    public enum MetricKind
    {
        Ping,
        PacketLoss
    }

    // Classifies a ping or packet loss reading and returns whether it matches the
    // tier passed as the converter parameter. Bound to a style class instead of a
    // brush so the colour resolves from the current theme dictionary.
    public class MetricTierConverter : IValueConverter
    {
        private const string Good = "good";
        private const string Warn = "warn";
        private const string Bad = "bad";
        private const string Pending = "pending";

        public MetricKind Kind { get; set; } = MetricKind.Ping;

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string requestedTier = parameter as string ?? string.Empty;

            return ResolveTier(value as string, culture)
                .Equals(requestedTier, StringComparison.OrdinalIgnoreCase);
        }

        private string ResolveTier(string? rawValue, CultureInfo culture)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return Pending;
            }

            string numericPart = rawValue
                .Replace("ms", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("%", string.Empty)
                .Trim();

            if (!double.TryParse(numericPart, NumberStyles.Any, culture, out double reading))
            {
                return Pending;
            }

            if (Kind == MetricKind.Ping)
            {
                if (reading <= 75) return Good;
                if (reading <= 150) return Warn;

                return Bad;
            }

            if (reading < 5) return Good;
            if (reading <= 20) return Warn;

            return Bad;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
