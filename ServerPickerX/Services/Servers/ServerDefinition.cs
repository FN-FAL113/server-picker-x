using ServerPickerX.Models;
using System.Collections.Generic;

namespace ServerPickerX.Services.Servers
{
    public class ServerDefinition
    {
        public string GameMode { get; set; } = "";
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public int AppId { get; set; }
        public string KeywordFilterMode { get; set; } = "none";
        public List<string> Keywords { get; set; } = [];
        public List<string> ClusterKeywords { get; set; } = [];
        public string ResponseUrlTemplate { get; set; } = "https://api.steampowered.com/ISteamApps/GetSDRConfig/v1/?appid={0}";
    }
    public class ServerData
    {
        public string Revision { get; set; } = string.Empty;
        public List<ServerModel> UnclusteredServers { get; set; } = [];
        public List<ServerModel> ClusteredServers { get; set; } = [];
    }
        
}