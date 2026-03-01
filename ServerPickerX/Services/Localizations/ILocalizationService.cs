using System;
using System.Collections.Generic;
using System.Text;

namespace ServerPickerX.Services.Localizations
{
    public interface ILocalizationService
    {
        void SetLanguage(string language);
        string GetLocaleValue(string key);
    }
}
