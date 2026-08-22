using Avalonia;
using Avalonia.Styling;
using System;

namespace ServerPickerX.Services.Themes
{
    // Applies and cycles the app theme variant. Palette.axaml holds a dictionary
    // per variant, so switching repaints open windows without recreating them.
    public static class ThemeService
    {
        public const string SystemTheme = "System";

        public const string LightTheme = "Light";

        public const string DarkTheme = "Dark";

        public static void Apply(string? theme)
        {
            Application? application = Application.Current;

            if (application == null)
            {
                return;
            }

            application.RequestedThemeVariant = Resolve(theme);
        }

        public static ThemeVariant Resolve(string? theme)
        {
            if (LightTheme.Equals(theme, StringComparison.OrdinalIgnoreCase))
            {
                return ThemeVariant.Light;
            }

            if (DarkTheme.Equals(theme, StringComparison.OrdinalIgnoreCase))
            {
                return ThemeVariant.Dark;
            }

            return ThemeVariant.Default;
        }

        // Cycle order for the status bar toggle: system, dark, light
        public static string Next(string? current)
        {
            if (SystemTheme.Equals(current, StringComparison.OrdinalIgnoreCase))
            {
                return DarkTheme;
            }

            if (DarkTheme.Equals(current, StringComparison.OrdinalIgnoreCase))
            {
                return LightTheme;
            }

            return SystemTheme;
        }

        public static string GetIcon(string? theme)
        {
            if (DarkTheme.Equals(theme, StringComparison.OrdinalIgnoreCase))
            {
                return "fa-moon";
            }

            if (LightTheme.Equals(theme, StringComparison.OrdinalIgnoreCase))
            {
                return "fa-sun";
            }

            return "fa-desktop";
        }
    }
}
