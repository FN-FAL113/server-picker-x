using ServerPickerX.Settings;
using ServerPickerX.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ServerPickerX.Services.Loggers;

namespace ServerPickerX.Services.Servers
{
    public class CS2ServerDataService(
        ILoggerService _logger, 
        HttpClient _httpClient, 
        JsonSetting _jsonSettings
        ) : IServerDataService
    {
        private ServerData _serverData = new();

        public async Task LoadServersAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("https://api.steampowered.com/ISteamApps/GetSDRConfig/v1/?appid=730");

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception(
                        "Failed to load servers!" + Environment.NewLine + Environment.NewLine +
                        "- Verify your internet connection or firewall are working and enabled" + Environment.NewLine +
                        "- Make sure to run the app as admin or with sudo level execution"
                    );
                }

                using var stream = await response.Content.ReadAsStreamAsync();
                var mainJson = await JsonNode.ParseAsync(stream) as JsonObject;

                if (mainJson?["revision"] == null || mainJson?["pops"] == null)
                {
                    throw new Exception("Server relay data unavailable. Please try again later.");
                }

                string revision = mainJson["revision"].ToString();

                _serverData.Revision = revision;

                // Update settings if app is initialized for the first time
                if (_jsonSettings.cs2_server_revision == "-1")
                {
                    _jsonSettings.cs2_server_revision = revision;

                    await _jsonSettings.SaveSettingsAsync();
                }

                ProcessServers(mainJson, _serverData);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load cs2 servers", ex.Message);

                throw;
            }
        }

        private void ProcessServers(JsonObject mainJson, ServerData serverData)
        {
            var unclusteredServers = new List<ServerModel>();
            var clusteredServers = new List<ServerModel>();

            foreach (KeyValuePair<string, JsonNode> server in mainJson["pops"] as JsonObject)
            {
                if (server.Value?["relays"] == null)
                {
                    continue;
                }

                string serverDescription = server.Value["desc"].ToString();
                string clusterName = GetClusterKeywords().FirstOrDefault(keyword => serverDescription.Contains(keyword), "");

                var serverModel = new ServerModel
                {
                    Flag = "/Assets/flags/" + serverDescription + $" ({server.Key}).png",
                    Name = server.Key,
                    Description = serverDescription,
                };

                foreach (JsonObject relay in server.Value["relays"] as JsonArray)
                {
                    serverModel.RelayModels.Add(new RelayModel
                    {
                        IPv4 = relay["ipv4"]?.ToString()
                    });
                }

                unclusteredServers.Add(serverModel);

                if (!string.IsNullOrEmpty(clusterName))
                {
                    var clusteredServer = clusteredServers.FirstOrDefault(s => s.Description == clusterName, new ServerModel());

                    clusteredServer.RelayModels.AddRange(serverModel.RelayModels);

                    if (string.IsNullOrEmpty(clusteredServer.Description))
                    {
                        clusteredServer.Flag = serverModel.Flag;
                        clusteredServer.Name = "cluster";
                        clusteredServer.Description = clusterName;

                        clusteredServers.Add(clusteredServer);
                    }
                }
                else
                {
                    clusteredServers.Add(serverModel);
                }
            }

            serverData.UnclusteredServers = unclusteredServers;
            serverData.ClusteredServers = clusteredServers;
        }

        public string GetCurrentRevision()
        {
            return _jsonSettings.cs2_server_revision;
        }

        public ServerData GetServerData()
        {
            return _serverData;
        }

        public List<string> GetClusterKeywords()
        {
            return new List<string>
            {
                "China", "Hong Kong", "Sweden", "India", "Netherlands"
            };
        }
    }
}