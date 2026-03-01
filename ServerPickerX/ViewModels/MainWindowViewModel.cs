using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using MsBox.Avalonia.Enums;
using ServerPickerX.Extensions;
using ServerPickerX.Models;
using ServerPickerX.Services.Loggers;
using ServerPickerX.Services.MessageBoxes;
using ServerPickerX.Services.Servers;
using ServerPickerX.Services.SystemFirewalls;
using ServerPickerX.Settings;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ServerPickerX.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        public ObservableCollectionExtended<ServerModel> ServerModels { get; set; } = [];

        // Property resolve through expression body that react to changes from another observable property
        public ObservableCollectionExtended<ServerModel> FilteredServerModels =>
             string.IsNullOrWhiteSpace(SearchText)
                ? ServerModels
                : new(ServerModels.Where(s =>
                    s.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    s.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                ));

        public ServerModel? SelectedDataGridServerModel { get; set; }

        // Mvvm tool kit will auto generate source code to make this property observable
        // When updating this property, reference it by its auto property name (PascalCase)
        [ObservableProperty]
        public bool showProgressBar = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredServerModels))]
        public string searchText = string.Empty;

        [ObservableProperty]
        public bool serversLoaded = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsOperationAllowed))]
        public bool serverModelsInitialized = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsOperationAllowed))]
        public bool pendingOperation = false;

        // Dependent/Computed prop for main UI buttons `IsEnabled` state
        public bool IsOperationAllowed => !PendingOperation && ServerModelsInitialized;

        private readonly ILoggerService _loggerService;
        private readonly IMessageBoxService _messageBoxService;
        private readonly IServerDataService _serverDataService;
        private readonly ISystemFirewallService _systemFirewallService;
        private readonly JsonSetting _jsonSetting;

        // Parameterless constructor, allows design previewer to instantiate this class since it doesn't support DI
        public MainWindowViewModel()
        {
            _loggerService = App.ServiceProvider.GetRequiredService<ILoggerService>();
            _messageBoxService = App.ServiceProvider.GetRequiredService<IMessageBoxService>();
            _serverDataService = App.ServiceProvider.GetRequiredService<IServerDataService>();
            _systemFirewallService = App.ServiceProvider.GetRequiredService<ISystemFirewallService>();
            _jsonSetting = App.ServiceProvider.GetRequiredService<JsonSetting>();
        }

        // DI constructor, allows inversion of control and unit tests mocking
        public MainWindowViewModel(
            ILoggerService loggerService,
            IMessageBoxService messageBoxService,
            IServerDataService serverDataService,
            ISystemFirewallService systemFirewallService,
            JsonSetting jsonSetting
            )
        {
            _loggerService = loggerService;
            _messageBoxService = messageBoxService;
            _serverDataService = serverDataService;
            _systemFirewallService = systemFirewallService;
            _jsonSetting = jsonSetting;
        }

        public async Task LoadServersAsync()
        {
            ServersLoaded = await _serverDataService.LoadServersAsync();

            if (!ServersLoaded) return;

            await ClusterUnclusterServersAsync();

            ServerModelsInitialized = true;
        }

        [RelayCommand]
        public async Task ClusterUnclusterServersAsync()
        {
            if (!ServersLoaded) return;

            // Update json settings and unblock all servers only after servers are initialized on first load
            if (ServerModelsInitialized)
            {
                _jsonSetting.is_clustered = !_jsonSetting.is_clustered;

                await _jsonSetting.SaveSettingsAsync();

                await UnblockAllAsync();
            }

            ServerData serverData = _serverDataService.GetServerData();

            List<ServerModel> serverModels = _jsonSetting.is_clustered ?
                serverData.ClusteredServers : serverData.UnclusteredServers;

            ServerModels.Clear();

            ServerModels.AddRange(serverModels);

            PingServers(serverModels);
        }

        [RelayCommand]
        public void PingServers(ICollection<ServerModel> serverModels)
        {
            if (serverModels.Count == 0)
            {
                return;
            }

            try
            {
                foreach (ServerModel serverModel in serverModels)
                {
                    serverModel.PingServer();
                }
            }
            catch (InvalidOperationException)
            {
                // when user suddenly tries to cluster or uncluster the servers while ServerModels is being iterated
            }
        }

        public void PingSelectedServer()
        {
            if (SelectedDataGridServerModel == null)
            {
                return;
            }

            SelectedDataGridServerModel.PingServer();
        }

        [RelayCommand]
        public async Task<bool> BlockAllAsync()
        {
            if (ServerModels.Count == 0)
            {
                return false;
            }

            return await PerformOperationAsync(true, FilteredServerModels);
        }

        [RelayCommand]
        public async Task<bool> BlockSelectedAsync(IList selectedServers)
        {
            if (selectedServers.Count == 0)
            {
                await _messageBoxService.ShowMessageBoxAsync("Info", "Hey! Please select at least one server to block");

                return false;
            }

            var serverModels = new ObservableCollection<ServerModel>(selectedServers.Cast<ServerModel>());

            return await PerformOperationAsync(true, serverModels);
        }

        [RelayCommand]
        public async Task<bool> UnblockAllAsync()
        {
            if (ServerModels == null || ServerModels.Count == 0)
            {
                return false;
            }

            return await PerformOperationAsync(false, FilteredServerModels);
        }


        [RelayCommand]
        public async Task<bool> UnblockSelectedAsync(IList selectedServers)
        {
            if (selectedServers.Count == 0)
            {
                await _messageBoxService.ShowMessageBoxAsync("Info", "Hey! Please select at least one server to unblock");

                return false;
            }

            var serverModels = new ObservableCollection<ServerModel>(selectedServers.Cast<ServerModel>());

            return await PerformOperationAsync(false, serverModels);
        }

        public async Task<bool> PerformOperationAsync(bool shouldBlock, ObservableCollection<ServerModel> serverModels)
        {
            if (PendingOperation)
            {
                await _messageBoxService.ShowMessageBoxAsync(
                    "Info",
                    "Whoa! There's already a pending operation. Please wait...",
                    Icon.Setting
                    );

                return false;
            }

            // Prevent executing another operation while there is pending task,
            // else a task cancellation token can be implemented if needed
            PendingOperation = true;
            ShowProgressBar = true;

            try
            {
                if (shouldBlock)
                {
                    // offload to background thread, process.waitForExit blocks the UI thread
                    await Task.Run(() => _systemFirewallService.BlockServersAsync(serverModels));

                    await _loggerService.LogInfoAsync("Servers blocked successfully");
                }
                else
                {
                    await Task.Run(() => _systemFirewallService.UnblockServersAsync(serverModels));

                    await _loggerService.LogInfoAsync("Servers unblocked successfully");
                }

                // Ping servers (parallel operation)
                PingServers(serverModels);
            }
            catch (Exception ex)
            {
                await _loggerService.LogErrorAsync("An error has occurred while blocking or unblocking servers.", ex.Message);

                await _messageBoxService.ShowMessageBoxAsync(
                    "Error",
                    "Oops! Something went wrong. Please upload the log file to GitHub."
                    );

                return false;
            }

            PendingOperation = false;
            ShowProgressBar = false;

            return true;
        }

        public IServerDataService GetServerDataService()
        {
            return _serverDataService;
        }

    }
}
