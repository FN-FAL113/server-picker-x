using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MsBox.Avalonia.Enums;
using ServerPickerX.Helpers;
using ServerPickerX.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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

        public List<Ping> Pings = [];

        // Mvvm tool kit will auto generate source to make this property observable
        // When updating this property, reference it by its auto property name (PascalCase)
        [ObservableProperty]
        public bool showProgressBar;

        public bool PendingOperation = false;

        public async Task<MainWindowViewModel> LoadServersAsync()
        {
            using HttpClient httpClient = new HttpClient();

            string res = await httpClient.GetStringAsync("https://api.steampowered.com/ISteamApps/GetSDRConfig/v1/?appid=730");

            if (string.IsNullOrWhiteSpace(res))
            {
                await MessageBoxHelper.ShowMessageBox(
                    "Error", 
                    "Failed to load servers..." + Environment.NewLine + Environment.NewLine +
                    "- Verify your internet connection or firewall are working and enabled" + Environment.NewLine +
                    "- Make sure to run the app as admin or with sudo level execution");
                return this;
            }

            JsonObject? mainJson = JsonObject.Parse(res) as JsonObject;

            if (mainJson?["revision"] == null)
            {
                return this;
            }

            Debug.WriteLine("Server Revision: " + mainJson["revision"]);

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
            if (ServerModels == null)
            {
                return;
            }

            if (Pings.Count > 0)
            {
                await PingHelper.CancelAllPings(Pings);
            }

            Ping ping = new Ping();

            Pings.Add(ping);

            foreach (ServerModel serverModel in ServerModels) {
                await PingHelper.PingServer(serverModel);
            } 

            ping.Dispose();
        }

        public async Task PingSelectedServer()
        {
            if (SelectedDataGridItem == null)
            {
                return;
            }

            await PingHelper.PingServer(SelectedDataGridItem);
        }

        [RelayCommand]
        public async Task BlockAll()
        {
            if (ServerModels == null || ServerModels.Count == 0)
            {
                return;
            }

            await performOperation(true, ServerModels);
        }

        [RelayCommand]
        public async Task BlockSelected(IList selectedServers)
        {
            if (selectedServers.Count == 0)
            {
                await MessageBoxHelper.ShowMessageBox("Info", "Please select any server to block");
                return;
            }

            var serverModels = new ObservableCollection<ServerModel>(selectedServers.Cast<ServerModel>());

            await performOperation(true, serverModels);
        }

        [RelayCommand]
        public async Task UnblockAll()
        {
            if (ServerModels == null || ServerModels.Count == 0)
            {
                return;
            }

            await performOperation(false, ServerModels);
        }


        [RelayCommand]
        public async Task UnblockSelected(IList selectedServers)
        {
            if (selectedServers.Count == 0)
            {
                await MessageBoxHelper.ShowMessageBox("Info", "Please select any server to unblock");
                return;
            }

            var serverModels = new ObservableCollection<ServerModel>(selectedServers.Cast<ServerModel>());

            await performOperation(false, serverModels);

        }

        public async Task performOperation(bool shouldBlock, ObservableCollection<ServerModel> serverModels)
        {
            if (PendingOperation)
            {
                await MessageBoxHelper.ShowMessageBox("Info", "Pending operation. Please wait...");
                return;
            }

            PendingOperation = true;

            ShowProgressBar = true;

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    // offload to background thread, process.waitForExit blocks the UI thread
                    await Task.Run(() => ServerHelper.BlockUnblockServersWindows(shouldBlock, serverModels));
                }
                else if (OperatingSystem.IsLinux())
                {
                    await Task.Run(() => ServerHelper.BlockUnblockServersLinux(shouldBlock, serverModels));
                }
            }
            catch (Exception ex)
            {
                await MessageBoxHelper.ShowMessageBox(
                        "Error",
                        "An error has occured! Please upload generated error file to github.",
                        ButtonEnum.Ok
                    );

                await LogHelper.LogErrorToFile(ex.Message, "An error has occured while blocking or unblocking servers.");
            }

            PendingOperation = false;

            ShowProgressBar = false;
        }

    }
}
