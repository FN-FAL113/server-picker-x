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
        private readonly ILoggerService _loggerServiceService;
        private readonly IMessageBoxService _messageBoxService;
        private readonly IServerDataService _serverDataService;
        private readonly ISystemFirewallService _systemFirewallService;
        private readonly JsonSetting _jsonSetting;

        // Parameterless constructors for windows and viewmodels, access services instead through the container
        // DI through constructors doesn't work with design previewer since it has no clue on providing parameters
        public MainWindowViewModel() 
        {
            _loggerServiceService = App.ServiceProvider.GetRequiredService<ILoggerService>();
            _messageBoxService = App.ServiceProvider.GetRequiredService<IMessageBoxService>(); 
            _serverDataService = App.ServiceProvider.GetRequiredService<IServerDataService>(); 
            _systemFirewallService = App.ServiceProvider.GetRequiredService<ISystemFirewallService>();
            _jsonSetting = App.ServiceProvider.GetRequiredService<JsonSetting>();
        }

        public ObservableCollectionExtended<ServerModel> ServerModels { get; set; } = [];

        // Property resolve through expression body that react to changes from another observable property
        public ObservableCollectionExtended<ServerModel> FilteredServerModels =>
             string.IsNullOrWhiteSpace(SearchText)
                ? ServerModels
                : new ObservableCollectionExtended<ServerModel>(ServerModels.Where(s =>
                    s.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    s.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                ));

        public ServerModel? SelectedDataGridItem { get; set; }

        // Mvvm tool kit will auto generate source code to make this property observable
        // When updating this property, reference it by its auto property name (PascalCase)
        [ObservableProperty]
        public bool showProgressBar = false;

        [ObservableProperty]
        public string searchText = string.Empty;

        public bool PendingOperation = false;

        public bool ServersInitialized = false;

        partial void OnSearchTextChanged(string value)
        {
            // An observable collection only reacts to changes through add and remove
            // Dispatch prop changed event manually to signal the UI for data binding changes
            OnPropertyChanged(nameof(FilteredServerModels));
        }

        public async Task LoadServers()
        {
            await _serverDataService.LoadServersAsync();

            await ClusterUnclusterServers();

            ServersInitialized = true;
        }

        [RelayCommand]
        public async Task ClusterUnclusterServers()
        {
            // do not update json settings nor unblock servers on first app load
            if (ServersInitialized)
            {
                _jsonSetting.is_clustered = !_jsonSetting.is_clustered;

                await _jsonSetting.SaveSettingsAsync();

                await UnblockAll();
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
            } catch (InvalidOperationException ex) {
                // when user suddenly tries to cluster or uncluster the servers while ServerModels is being iterated
            }
        }

        public void PingSelectedServer()
        {
            if (SelectedDataGridItem == null)
            {
                return;
            }

            SelectedDataGridItem.PingServer();
        }

        [RelayCommand]
        public async Task BlockAll()
        {
            if (ServerModels.Count == 0)
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
                await _messageBoxService.ShowMessageBoxAsync("Info", "Hey! Please select at least one server to block");

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
                await _messageBoxService.ShowMessageBoxAsync("Info", "Hey! Please select at least one server to unblock");

                return;
            }

            var serverModels = new ObservableCollection<ServerModel>(selectedServers.Cast<ServerModel>());

            await performOperation(false, serverModels);
        }

        public async Task performOperation(bool shouldBlock, ObservableCollection<ServerModel> serverModels)
        {
            if (PendingOperation)
            {
                await _messageBoxService.ShowMessageBoxAsync(
                    "Info", 
                    "Whoa! There's already a pending operation. Please wait...", 
                    Icon.Setting
                    );

                return;
            }

            PendingOperation = true;
            ShowProgressBar = true;

            try
            {
                if (shouldBlock)
                {
                    // offload to background thread, process.waitForExit blocks the UI thread
                    await Task.Run(() => _systemFirewallService.BlockServersAsync(serverModels));

                    _loggerServiceService.LogInfo("Servers blocked successfully");
                } else
                {
                    await Task.Run(() => _systemFirewallService.UnblockServersAsync(serverModels));

                    _loggerServiceService.LogInfo("Servers unblocked successfully");
                }

                // Ping servers in the background (parallel operation)
                PingServers(serverModels);
            }
            catch (Exception ex)
            {
                _loggerServiceService.LogError("An error has occurred while blocking or unblocking servers.", ex.Message);

                await _messageBoxService.ShowMessageBoxAsync(
                    "Error",
                    "Oops! Something went wrong. Please upload the log file to GitHub."
                    );
            }

            PendingOperation = false;
            ShowProgressBar = false;
        }

        public IServerDataService GetServerDataService()
        { 
            return _serverDataService; 
        }

    }
}
