using System.Collections.Generic;

namespace ServerPickerX.Constants
{
    public static class Locales
    {
        public const string English = "English | en-us";
        public const string Spanish = "Spanish | es-es";
        public const string Chinese = "Chinese | zh-cn";

        // Read‑only list used as ItemsSource for the Language ComboBox
        public static readonly IReadOnlyList<string> All = [English, Spanish, Chinese];
    }
}
