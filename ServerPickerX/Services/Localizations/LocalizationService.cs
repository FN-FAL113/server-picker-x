using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using ServerPickerX.Helpers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ServerPickerX.Services.Localizations
{
    public class LocalizationService : ILocalizationService
    {
        private IResourceProvider? _currentLocaleResource;

#pragma warning disable IL2026
        // Reflection is partially used here and might not be trim-compatible unless JsonSerializerIsReflectionEnabledByDefault is set to true in .csproj
        public void SetLanguage(string language)
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo(language);
            Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(language);

            var mergedDictionaries = App.Current!.Resources.MergedDictionaries;

            // Create a copy for iteration and prevent modifying the original collection while iterating
            var mergedDictionariesCopy = new List<IResourceProvider>(mergedDictionaries);

            // Remove locale resource dictionaries instead of clearing the list for flexibility if there are non-locale resources
            foreach (IResourceProvider dictionary in mergedDictionariesCopy)
            {
                if (dictionary.TryGetResource("LanguageCode", null, out object? value))
                {
                    mergedDictionaries.Remove(dictionary);
                }
            }

            Uri resourceUri = ResourceHelper.CreateResourceUriFromPath("/Locales/Locale_" + language + ".axaml");
            ResourceInclude localeResource = new(resourceUri) { Source = resourceUri };

            _currentLocaleResource = localeResource;

            // Add only one locale resource dictionary, it triggers UI updates on controls that bind to DynamicResource
            mergedDictionaries.Add(localeResource);
        }

        // Locale resolver for backend/code-behind strings
        public string GetLocaleValue(string key)
        {
            if (_currentLocaleResource == null) return "Resource dictionary not found";

            _currentLocaleResource.TryGetResource(key, null, out object? value);

            return value?.ToString() ?? "Invalid Locale Key";
        }
    }
}
