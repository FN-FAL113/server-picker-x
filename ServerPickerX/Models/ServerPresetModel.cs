using System.Collections.Generic;

namespace ServerPickerX.Models
{
    public class ServerPresetModel
    {
        public string Name { get; set; } = string.Empty;

        public string GameMode { get; set; } = string.Empty;

        public bool IsClustered { get; set; }

        public List<string> BlockedServerKeys { get; set; } = [];
    }
}
