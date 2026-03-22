using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using ServerPickerX.Helpers;
using ServerPickerX.Services.Loggers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServerPickerX.Services.Localizations
{
    public class LocalizationService(ILoggerService _loggerService) : ILocalizationService
    {
        private IResourceProvider? _currentLocaleResource;

        #pragma warning disable IL2026
        // Reflection is partially used here and might not be trim-compatible
        // unless JsonSerializerIsReflectionEnabledByDefault is set to true in .csproj
        public async Task SetLanguage(string language)
        {
            try
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

                // Add only one locale resource dictionary at a time, this triggers UI controls that bind to DynamicResource
                mergedDictionaries.Add(localeResource);

                await _loggerService.LogInfoAsync($"Successfully changed locale to {language}");
            } catch (Exception ex) {
                await _loggerService.LogErrorAsync("An error has occured while changing locale", ex.Message);

                throw;
            }
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
