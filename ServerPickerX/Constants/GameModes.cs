using System.Collections.Generic;

namespace ServerPickerX.Constants
{
    public static class GameModes
    {
        public const string CounterStrike2 = "Counter Strike 2";
        public const string CounterStrike2PerfectWorld = "Counter Strike 2 (Perfect World)";
        public const string Deadlock = "Deadlock";
        public const string Marathon = "Marathon";

        // Read‑only list used as ItemsSource for the Game Mode ComboBox
        public static readonly IReadOnlyList<string> All = [ CounterStrike2, CounterStrike2PerfectWorld, Deadlock, Marathon ];
    }
}
