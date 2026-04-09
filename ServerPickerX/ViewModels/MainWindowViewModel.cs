using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MsBox.Avalonia.Enums;
using ServerPickerX.Constants;
using ServerPickerX.Extensions;
using ServerPickerX.Models;
using ServerPickerX.Services.DependencyInjection;
using ServerPickerX.Services.Localizations;
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

        // Property resolved through expression body that react to changes from another observable property
        public ObservableCollectionExtended<ServerModel> FilteredServerModels =>
             string.IsNullOrWhiteSpace(SearchText)
                ? ServerModels
                : new(ServerModels.Where(s =>
                    s.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    s.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                ));

        public ObservableCollectionExtended<PresetModel> PresetItems { get; set; } = [];

        public ServerModel? SelectedDataGridServerModel { get; set; }

        // Mvvm tool kit will auto generate source code to make this property observable
        // When updating a data binding property, reference by its auto property name (PascalCase)
        [ObservableProperty]
        public bool showProgressBar = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredServerModels))]
        public string searchText = string.Empty;

        [ObservableProperty]
        public bool serversLoaded = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsOperationAllowed))]
        [NotifyPropertyChangedFor(nameof(CanSelectPresets))]
        public bool serverModelsInitialized = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsOperationAllowed))]
        [NotifyPropertyChangedFor(nameof(CanSelectPresets))]
        public bool pendingOperation = false;

        [ObservableProperty]
        public PresetModel? selectedPreset;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanSelectPresets))]
        public bool hasPresets = false;

        // Dependent/Computed prop for main UI buttons `IsEnabled` state
        public bool IsOperationAllowed => !PendingOperation && ServerModelsInitialized;

        public bool CanSelectPresets => IsOperationAllowed && HasPresets;

        private readonly ILoggerService _loggerService;
        private readonly IMessageBoxService _messageBoxService;
        private readonly ILocalizationService _localizationService;
        private readonly IServerDataService _serverDataService;
        private readonly ISystemFirewallService _systemFirewallService;
        private readonly JsonSetting _jsonSetting;

        // Parameterless constructor, allows design previewer to instantiate this class since it doesn't support DI
        public MainWindowViewModel()
        {
            _loggerService = ServiceLocator.GetRequiredService<ILoggerService>();
            _messageBoxService = ServiceLocator.GetRequiredService<IMessageBoxService>();
            _localizationService = ServiceLocator.GetRequiredService<ILocalizationService>();
            _serverDataService = ServiceLocator.GetRequiredService<IServerDataService>();
            _systemFirewallService = ServiceLocator.GetRequiredService<ISystemFirewallService>();
            _jsonSetting = ServiceLocator.GetRequiredService<JsonSetting>();
        }

        // DI constructor, allows inversion of control and unit tests mocking
        public MainWindowViewModel(
            ILoggerService loggerService,
            IMessageBoxService messageBoxService,
            ILocalizationService localizationService,
            IServerDataService serverDataService,
            ISystemFirewallService systemFirewallService,
            JsonSetting jsonSetting
            )
        {
            _loggerService = loggerService;
            _messageBoxService = messageBoxService;
            _localizationService = localizationService;
            _serverDataService = serverDataService;
            _systemFirewallService = systemFirewallService;
            _jsonSetting = jsonSetting;
        }

        public async Task LoadServersAsync()
        {
            ServersLoaded = await _serverDataService.LoadServersAsync();

            if (!ServersLoaded) return;

            await SetClusterStateAsync(_jsonSetting.is_clustered, false);

            ServerModelsInitialized = true;
        }

        [RelayCommand]
        public async Task ClusterUnclusterServersAsync()
        {
            await SetClusterStateAsync(!_jsonSetting.is_clustered, true);
        }

        public async Task SetClusterStateAsync(bool isClustered, bool shouldUnblockCurrentServers)
        {
            if (!ServersLoaded)
            {
                return;
            }

            bool clusterStateChanged = _jsonSetting.is_clustered != isClustered;

            // After initial load, clear the full current view before switching representations
            // so clustered/unclustered transitions do not carry stale rules forward
            if (shouldUnblockCurrentServers && ServerModelsInitialized && ServerModels.Count > 0)
            {
                bool unblocked = await PerformOperationAsync(false, ServerModels, false);

                if (!unblocked)
                {
                    return;
                }
            }

            if (clusterStateChanged)
            {
                _jsonSetting.is_clustered = isClustered;

                await _jsonSetting.SaveSettingsAsync();

                await MarkPresetSelectionDirtyAsync();
            }

            ServerData serverData = _serverDataService.GetServerData();
            List<ServerModel> serverModels = _jsonSetting.is_clustered
                ? serverData.ClusteredServers
                : serverData.UnclusteredServers;

            ServerModels.Clear();
            ServerModels.AddRange(serverModels);

            PingServers(serverModels);
        }

        public PresetModel? GetCurrentGamePreset(string presetName)
        {
            return _jsonSetting.GetPresetByGameMode(_jsonSetting.game_mode, presetName);
        }

        public void LoadPresetPickerItems()
        {
            string? selectedPresetName = SelectedPreset?.Name;
            List<PresetModel> presetItems = _jsonSetting.GetPresetsByGameMode(_jsonSetting.game_mode);

            PresetItems.Clear();

            if (presetItems.Count == 0)
            {
                HasPresets = false;
                ClearSelectedPreset();
                return;
            }

            HasPresets = true;
            PresetItems.AddRange(presetItems);

            if (!string.IsNullOrWhiteSpace(selectedPresetName))
            {
                SelectPresetByName(selectedPresetName);
                return;
            }

            ClearSelectedPreset();
        }

        public void SelectPresetByName(string presetName)
        {
            SelectedPreset = PresetItems.FirstOrDefault(preset =>
                preset.Name.Equals(presetName, StringComparison.OrdinalIgnoreCase));
        }

        public string GetCurrentGameMode() => _jsonSetting.game_mode;

        public IReadOnlyList<ServerModel> GetCurrentGameServerModels(bool isClustered)
        {
            ServerData serverData = _serverDataService.GetServerData();

            return isClustered
                ? serverData.ClusteredServers
                : serverData.UnclusteredServers;
        }

        public async Task DeletePresetAsync(PresetModel preset)
        {
            string deletedPresetName = preset.Name;

            await _jsonSetting.RemovePresetAsync(_jsonSetting.game_mode, deletedPresetName);

            if (_jsonSetting.GetLastSelectedPresetNameByGameMode().Equals(deletedPresetName, StringComparison.OrdinalIgnoreCase))
            {
                await _jsonSetting.ClearLastSelectedPresetNameByGameModeAsync();
            }

            LoadPresetPickerItems();

            if (SelectedPreset?.Equals(preset) == true)
            {
                ClearSelectedPreset();
            }
        }

        public async Task<bool> ApplyPresetAsync(PresetModel preset)
        {
            if (!ServersLoaded)
            {
                return false;
            }

            if (!preset.GameMode.Equals(_jsonSetting.game_mode, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            bool presetApplied = await ApplyPresetWithResetAsync(preset);

            if (!presetApplied)
            {
                return false;
            }

            SelectPresetByName(preset.Name);

            await _jsonSetting.SetLastSelectedPresetNameByGameModeAsync(preset.Name);

            return true;
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
                await _messageBoxService.ShowMessageBoxAsync(
                    _localizationService.GetLocaleValue("MessageBoxInfoTitle"),
                    _localizationService.GetLocaleValue("SelectOneServerToBlockDialogue")
                    );

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

        public async Task<bool> UnblockCurrentGameServersAsync()
        {
            if (ServerModels.Count == 0)
            {
                return true;
            }

            return await PerformOperationAsync(false, new ObservableCollection<ServerModel>(ServerModels), false);
        }

        [RelayCommand]
        public async Task<bool> UnblockSelectedAsync(IList selectedServers)
        {
            if (selectedServers.Count == 0)
            {
                await _messageBoxService.ShowMessageBoxAsync(
                    _localizationService.GetLocaleValue("MessageBoxInfoTitle"),
                    _localizationService.GetLocaleValue("SelectOneServerToUnblockDialogue")
                    );

                return false;
            }

            var serverModels = new ObservableCollection<ServerModel>(selectedServers.Cast<ServerModel>());

            return await PerformOperationAsync(false, serverModels);
        }

        public async Task<bool> PerformOperationAsync(
            bool shouldBlock,
            ObservableCollection<ServerModel> serverModels,
            bool shouldUpdatePresetSelection = true
            )
        {
            if (PendingOperation)
            {
                await _messageBoxService.ShowMessageBoxAsync(
                    _localizationService.GetLocaleValue("MessageBoxInfoTitle"),
                    _localizationService.GetLocaleValue("PendingOperationDialogue"),
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
                    await _systemFirewallService.BlockServersAsync(serverModels);

                    await _loggerService.LogInfoAsync("Servers blocked successfully");
                }
                else
                {
                    await _systemFirewallService.UnblockServersAsync(serverModels);

                    await _loggerService.LogInfoAsync("Servers unblocked successfully");
                }

                if (shouldUpdatePresetSelection)
                {
                    await MarkPresetSelectionDirtyAsync();
                }

                // Ping servers (parallel operation)
                PingServers(serverModels);

                return true;
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
            finally
            {
                PendingOperation = false;
                ShowProgressBar = false;
            }
        }

        public IServerDataService GetServerDataService()
        {
            return _serverDataService;
        }

        public async Task<bool> PruneCurrentGamePresetEntriesAsync()
        {
            if (!ServersLoaded)
            {
                return false;
            }

            return await PrunePresetEntriesAsync(_jsonSetting.game_mode, _serverDataService.GetServerData());
        }

        public async Task<bool> PruneCounterStrikeFamilyPresetEntriesAsync()
        {
            // CS2 and Perfect World share one revision bucket, but their filtered server sets differ.
            // When either mode syncs, prune the sibling mode too before marking the shared revision current.
            if (_jsonSetting.game_mode == GameModes.CounterStrike2)
            {
                CS2PerfectWorldServerDataService perfectWorldServerDataService =
                    ServiceLocator.GetRequiredService<CS2PerfectWorldServerDataService>();

                if (!await perfectWorldServerDataService.LoadServersAsync())
                {
                    return false;
                }

                await PrunePresetEntriesAsync(
                    GameModes.CounterStrike2PerfectWorld,
                    perfectWorldServerDataService.GetServerData()
                    );

                return true;
            }

            CS2ServerDataService counterStrikeServerDataService =
                ServiceLocator.GetRequiredService<CS2ServerDataService>();

            if (!await counterStrikeServerDataService.LoadServersAsync())
            {
                return false;
            }

            await PrunePresetEntriesAsync(
                GameModes.CounterStrike2,
                counterStrikeServerDataService.GetServerData()
                );

            return true;
        }

        public async Task<bool> PrunePresetEntriesAsync(string gameMode, ServerData serverData)
        {
            HashSet<string> clusteredServerKeys = serverData.ClusteredServers
                .Select(serverModel => serverModel.Description)
                .Where(serverKey => !string.IsNullOrWhiteSpace(serverKey))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            HashSet<string> unclusteredServerKeys = serverData.UnclusteredServers
                .Select(serverModel => serverModel.Name)
                .Where(serverKey => !string.IsNullOrWhiteSpace(serverKey))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            bool presetsPruned = await _jsonSetting.PrunePresetEntriesByGameModeAsync(
                gameMode,
                clusteredServerKeys,
                unclusteredServerKeys
                );

            return presetsPruned;
        }

        public string GetServerKey(ServerModel serverModel, bool isClustered)
        {
            return isClustered
                ? serverModel.Description
                : serverModel.Name;
        }

        private string GetServerKey(ServerModel serverModel)
        {
            return GetServerKey(serverModel, _jsonSetting.is_clustered);
        }

        public async Task RestoreLastSelectedPresetAsync()
        {
            if (!HasPresets)
            {
                await _jsonSetting.ClearLastSelectedPresetNameByGameModeAsync();

                ClearSelectedPreset();
                return;
            }

            string lastSelectedPresetName = _jsonSetting.GetLastSelectedPresetNameByGameMode();

            if (string.IsNullOrWhiteSpace(lastSelectedPresetName))
            {
                ClearSelectedPreset();
                return;
            }

            PresetModel? lastSelectedPreset = _jsonSetting.GetPresetByGameMode(_jsonSetting.game_mode, lastSelectedPresetName);

            if (lastSelectedPreset == null)
            {
                await _jsonSetting.ClearLastSelectedPresetNameByGameModeAsync();
                ClearSelectedPreset();
                return;
            }

            bool restored = await ApplyPresetAsync(lastSelectedPreset);

            if (!restored)
            {
                ClearSelectedPreset();
            }
        }

        private async Task<bool> ApplyPresetWithResetAsync(PresetModel serverPreset)
        {
            if (ServerModels.Count > 0)
            {
                bool unblocked = await PerformOperationAsync(false, ServerModels, false);

                if (!unblocked)
                {
                    return false;
                }
            }

            await SetClusterStateAsync(serverPreset.IsClustered, false);

            ObservableCollection<ServerModel> matchingServerModels = GetMatchingServerModels(serverPreset);

            if (matchingServerModels.Count == 0)
            {
                return true;
            }

            return await PerformOperationAsync(true, matchingServerModels, false);
        }

        private ObservableCollection<ServerModel> GetMatchingServerModels(PresetModel serverPreset)
        {
            return new ObservableCollection<ServerModel>(
                ServerModels.Where(serverModel =>
                    serverPreset.BlockedServerKeys
                        .Contains(GetServerKey(serverModel), StringComparer.OrdinalIgnoreCase))
                );
        }

        private async Task MarkPresetSelectionDirtyAsync()
        {
            await _jsonSetting.ClearLastSelectedPresetNameByGameModeAsync();

            ClearSelectedPreset();
        }

        private void ClearSelectedPreset()
        {
            SelectedPreset = null;
        }
    }
}
