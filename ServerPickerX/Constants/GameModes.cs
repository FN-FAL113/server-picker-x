using System.Collections.Generic;

namespace ServerPickerX.Constants
{
    public static class GameModes
    {
        public const string CounterStrike2 = "Counter Strike 2";
        public const string Deadlock = "Deadlock";

        // Read‑only list used as ItemsSource for the Game Mode ComboBox
        public static readonly IReadOnlyList<string> All = new[] { CounterStrike2, Deadlock };
    }
}
