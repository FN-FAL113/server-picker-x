using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ServerPickerX.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace ServerPickerX.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        public ObservableCollection<ServerModel>? ServerModels { get; set; }

        public ServerModel? SelectedDataGridItem { get; set; }

        private List<Ping> pings = [];

        public async Task<MainWindowViewModel> LoadServersAsync()
        {
            using HttpClient httpClient = new HttpClient();

            string res = await httpClient.GetStringAsync("https://api.steampowered.com/ISteamApps/GetSDRConfig/v1/?appid=730");

            if (string.IsNullOrWhiteSpace(res))
                return this;

            JsonObject? mainJson = JsonObject.Parse(res) as JsonObject;

            if (mainJson?["revision"] == null)
                return this;

            System.Diagnostics.Debug.WriteLine("Server Revision: " + mainJson["revision"]);

            ObservableCollection<ServerModel> serverModels = [];

            foreach (var server in mainJson["pops"] as JsonObject)
            {
                if (server.Value?["relays"] == null)
                    continue;

                var serverModel = new ServerModel
                {
                    Flag = "/Assets/flags/"
                        + server.Value["desc"]?.ToString() + $" ({server.Key}).png",
                    Name = server.Key,
                    Description = server.Value["desc"]?.ToString()
                };

                foreach (JsonObject relay in server.Value["relays"] as JsonArray)
                {
                    serverModel.RelayModels.Add(new RelayModel
                    {
                        IPv4 = relay["ipv4"]?.ToString()
                    });
                }

                serverModels.Add(serverModel);
            }

            ServerModels = serverModels;

            return this;
        }

        [RelayCommand]
        public async Task PingServers()
        {
            if (ServerModels == null) return;

            if (pings.Count > 0)
            {
                await CancelAllPings();
            }

            Ping ping = new Ping();

            pings.Add(ping);

            foreach (ServerModel serverModel in ServerModels) {
                serverModel.Ping = "Pinging server";

                foreach (RelayModel relay in serverModel.RelayModels) {
                    try {
                        var res = await ping.SendPingAsync(relay.IPv4, timeout: 1000);

                        if (res.RoundtripTime > 0)
                        {
                            serverModel.Ping = res.RoundtripTime + "ms";
                            break;
                        }
                    } catch {
                        continue;
                    }
                }
            }

            ping.Dispose();
        }

        public async Task PingServer()
        {
            if (SelectedDataGridItem == null) return;

            ServerModel? serverModel = SelectedDataGridItem;

            Ping ping = new Ping();

            foreach (RelayModel relay in serverModel.RelayModels) {
                try
                {
                    var res = await ping.SendPingAsync(relay.IPv4, timeout: 1000);

                    if (res.RoundtripTime > 0)
                    {
                        serverModel.Ping = res.RoundtripTime + "ms";
                        break;
                    }
                }
                catch
                {
                    continue;
                }
            }
        }

        public async Task CancelAllPings()
        {
            foreach (Ping ping in pings)
            {
                ping.SendAsyncCancel();
                ping.Dispose();
            }
        }
    }
}
