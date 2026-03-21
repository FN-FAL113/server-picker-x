using System.Collections.Generic;

namespace ServerPickerX.Constants
{
    public static class Locales
    {
        public const string English = "English | en-us";
        public const string Spanish = "Spanish | es-es";
        public const string Chinese = "Chinese | zh-cn";
        public const string Japanese = "Japanese | ja-jp";
        public const string Swedish = "Swedish | sv-se";
        public const string Russian = "Russian | ru-ru";
        public const string German = "German | de-de";
        public const string Polish = "Polish | pl-pl";
        public const string Turkish = "Turkish | tr-tr";

        // Read‑only list used as ItemsSource for the Language ComboBox
        public static readonly IReadOnlyList<string> All = [
            English,
            Spanish,
            Chinese,
            Japanese,
            Swedish,
            Russian,
            German,
            Polish,
            Turkish
        ];
    }
}
