using Avalonia.Markup.Xaml.Styling;
using ServerPickerX.Helpers;
using System;
using System.Text;
using System.Threading;

namespace ServerPickerX.Services.Localizations
{
    public class LocalizationService : ILocalizationService
    {
#pragma warning disable IL2026
        // Reflection is partially used here and might not be trim-compatible unless JsonSerializerIsReflectionEnabledByDefault is set to true in .csproj
        public void SetLanguage(string language)
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo(language);
            Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(language);

            // Clear merged dictionaries
            App.Current!.Resources.MergedDictionaries.Clear();

            Uri resourceUri = ResourceHelper.CreateResourceUriFromPath("/Locale/Locale_" + language + ".axaml");
            ResourceInclude resource = new(resourceUri) { Source = resourceUri };

            // Add a single resource dictionary, this will trigger a UI update for the ones using DynamicResource
            App.Current!.Resources.MergedDictionaries.Add(resource);
        }

        public string GetLocaleValue(string key)
        {
            object value;

            // Merged dictionaries are cleared and inserted with a single resource dictionary
            App.Current!.Resources.MergedDictionaries[0].TryGetResource(key, null, out value);

            return value?.ToString() ?? "";
        }
    }
}
