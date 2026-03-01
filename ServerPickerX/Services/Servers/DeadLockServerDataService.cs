using ServerPickerX.Settings;
using ServerPickerX.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ServerPickerX.Services.Loggers;
using ServerPickerX.Services.MessageBoxes;

namespace ServerPickerX.Services.Servers
{
    public class DeadLockServerDataService(
        ILoggerService _logger,
        IMessageBoxService _messageBoxService,
        HttpClient _httpClient,
        JsonSetting _jsonSettings
        ) : IServerDataService
    {
        private ServerData _serverData = new();

        public async Task<bool> LoadServersAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("https://api.steampowered.com/ISteamApps/GetSDRConfig/v1/?appid=1422450");

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception(
                        "Failed to load servers!" + Environment.NewLine + Environment.NewLine +
                        "- Verify your internet connection or firewall is enabled and working properly" + Environment.NewLine +
                        "- Make sure to run the app as admin or with sudo access"
                    );
                }

                using var stream = await response.Content.ReadAsStreamAsync();
                var mainJson = await JsonNode.ParseAsync(stream) as JsonObject;

                if (mainJson?["revision"] == null || mainJson?["pops"] == null)
                {
                    throw new Exception("Server relay data unavailable. Please try again later.");
                }

                string revision = mainJson["revision"]!.ToString();

                _serverData.Revision = revision;

                // Update settings if app is initialized for the first time
                if (_jsonSettings.deadlock_server_revision == "-1")
                {
                    _jsonSettings.deadlock_server_revision = revision;

                    await _jsonSettings.SaveSettingsAsync();
                }

                ProcessServers(mainJson, _serverData);
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync("Failed to load deadlock servers", ex.Message);

                await _messageBoxService.ShowMessageBoxAsync("Error", ex.Message);

                return false;
            }

            return true;
        }

        private void ProcessServers(JsonObject mainJson, ServerData serverData)
        {
            var unclusteredServers = new List<ServerModel>();
            var clusteredServers = new List<ServerModel>();

            foreach (KeyValuePair<string, JsonNode?> server in (JsonObject)mainJson["pops"]!)
            {
                if (server.Value?["relays"] == null)
                {
                    continue;
                }

                string serverDescription = server.Value["desc"]!.ToString();

                var serverModel = new ServerModel
                {
                    Flag = "/Assets/flags/" + serverDescription + $" ({server.Key}).png",
                    Name = server.Key,
                    Description = serverDescription,
                };

                foreach (JsonObject? relay in (JsonArray)server.Value["relays"]!)
                {
                    serverModel.RelayModels.Add(new RelayModel
                    {
                        IPv4 = relay!["ipv4"]?.ToString() ?? ""
                    });
                }

                unclusteredServers.Add(serverModel);

                string clusterName = GetClusterKeywords().FirstOrDefault(keyword => serverDescription.Contains(keyword), "");
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
            return _serverData.Revision;
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