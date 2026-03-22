using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ServerPickerX.Services.Localizations
{
    public interface ILocalizationService
    {
        Task SetLanguage(string language);
        string GetLocaleValue(string key);
    }
}
