using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using ServerPickerX.Helpers;
using ServerPickerX.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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
                    "- Verify your internet connection or firewall" + Environment.NewLine +
                    "- Make sure to run the app as admin or with sudo perms");
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

            if (PendingOperation)
            {
                await MessageBoxHelper.ShowMessageBox("Info", "Pending block all operation. Please wait...");
                return;
            }

            PendingOperation = true;

            ShowProgressBar = true;

            if (OperatingSystem.IsWindows())
            {
                // offload to another thread, process.waitForExit is blocking the UI thread
                await Task.Run(() => ServerHelper.BlockUnblockServersWindows(shouldBlock: true, ServerModels));
            }

            PendingOperation = false;

            ShowProgressBar = false;
        }

        [RelayCommand]
        public async Task BlockSelected(IList selectedServers)
        {
            if (PendingOperation)
            {
                await MessageBoxHelper.ShowMessageBox("Info", "Pending block selected operation. Please wait...");
                return;
            }

            if (selectedServers.Count == 0)
            {
                await MessageBoxHelper.ShowMessageBox("Info", "Please select servers to unblock");
                return;
            }

            var serverModels = new ObservableCollection<ServerModel>(selectedServers.Cast<ServerModel>());

            PendingOperation = true;

            ShowProgressBar = true;

            if (OperatingSystem.IsWindows())
            {
                // offload to another thread, process.waitForExit is blocking the UI thread
                await Task.Run(() => ServerHelper.BlockUnblockServersWindows(shouldBlock: true, serverModels));
            }

            PendingOperation = false;

            ShowProgressBar = false;
        }

        [RelayCommand]
        public async Task UnblockAll()
        {
            if (ServerModels == null || ServerModels.Count == 0)
            {
                return;
            }

            if (PendingOperation)
            {
                await MessageBoxHelper.ShowMessageBox("Info", "Pending unblock all operation. Please wait...");
                return;
            }

            PendingOperation = true;

            ShowProgressBar = true;

            if (OperatingSystem.IsWindows())
            {
                // offload to another thread, process.waitForExit is blocking the UI thread
                await Task.Run(() => ServerHelper.BlockUnblockServersWindows(shouldBlock: false, ServerModels));
            }

            PendingOperation = false;

            ShowProgressBar = false;
        }


        [RelayCommand]
        public async Task UnblockSelected(IList selectedServers)
        {
            if (PendingOperation)
            {
                await MessageBoxHelper.ShowMessageBox("Info", "Pending unblock selected operation. Please wait...");
                return;
            }

            if (selectedServers.Count == 0)
            {
                await MessageBoxHelper.ShowMessageBox("Info", "Please select servers to unblock");
                return;
            }

            var serverModels = new ObservableCollection<ServerModel>(selectedServers.Cast<ServerModel>());

            PendingOperation = true;

            ShowProgressBar = true;

            if (OperatingSystem.IsWindows())
            {
                // offload to another thread, process.waitForExit is blocking the UI thread
                await Task.Run(() => ServerHelper.BlockUnblockServersWindows(shouldBlock: false, serverModels));
            }

            PendingOperation = false;

            ShowProgressBar = false;
        }

    }
}
