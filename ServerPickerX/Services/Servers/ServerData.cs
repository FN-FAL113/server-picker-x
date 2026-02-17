using ServerPickerX.Models;
using System.Collections.Generic;

namespace ServerPickerX.Services.Servers
{
    public class ServerData
    {
        public string Revision { get; set; } = string.Empty;
        public List<ServerModel> UnclusteredServers { get; set; } = [];
        public List<ServerModel> ClusteredServers { get; set; } = [];
    }
}