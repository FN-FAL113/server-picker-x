using System;
using System.Collections.Generic;

namespace ServerPickerX.Models
{
    public class ServerPresetModel
    {
        public string Name { get; set; } = string.Empty;

        public string GameMode { get; set; } = string.Empty;

        public bool IsClustered { get; set; }

        public List<string> BlockedServerKeys { get; set; } = [];

        public override bool Equals(object? obj)
        {
            return obj is ServerPresetModel other &&
                GameMode.Equals(other.GameMode, StringComparison.OrdinalIgnoreCase) &&
                Name.Equals(other.Name, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(GameMode),
                StringComparer.OrdinalIgnoreCase.GetHashCode(Name)
            );
        }
    }
}
