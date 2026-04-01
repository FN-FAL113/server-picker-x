using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ServerPickerX.Comparers;
using ServerPickerX.Constants;
using ServerPickerX.Models;
using ServerPickerX.Services.DependencyInjection;
using ServerPickerX.Services.Localizations;
using ServerPickerX.Services.Loggers;
using ServerPickerX.Services.MessageBoxes;
using ServerPickerX.Services.Versions;
using ServerPickerX.Settings;
using ServerPickerX.ViewModels;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace ServerPickerX.Views
{
    public partial class MainWindow : Window
    {
        // Singleton instance for accessing the main window on execution lifetime
        public static MainWindow? Instance { get; private set; }

        public static bool IsDebugBuild
        {
            get
            {
                #if DEBUG
                    return true;
                #else
                    return false;
                #endif
            }
        }

        private ListSortDirection pingSortDirection = ListSortDirection.Ascending;
        private bool _suppressPresetSelectionChanged;
        private ServerPresetModel? _previousPreset;

        private readonly ILoggerService _loggerService;
        private readonly JsonSetting _jsonSetting;
        private readonly IMessageBoxService _messageBoxService;
        private readonly IVersionService _versionService;
        private readonly ILocalizationService _localizationService;

        // Parameterless constructor, allows design previewer to create its own instance since it doesn't support DI
        public MainWindow()
        {
            InitializeComponent();
            Instance = this;

            _loggerService = ServiceLocator.GetRequiredService<ILoggerService>();
            _jsonSetting = ServiceLocator.GetRequiredService<JsonSetting>();
            _messageBoxService = ServiceLocator.GetRequiredService<IMessageBoxService>();
            _versionService = ServiceLocator.GetRequiredService<IVersionService>();
            _localizationService = ServiceLocator.GetRequiredService<ILocalizationService>();
        }

        // DI constructor, allows inversion of control and unit tests mocking
        public MainWindow(
            ILoggerService loggerService,
            JsonSetting jsonSetting,
            IMessageBoxService messageBoxService,
            IVersionService versionService,
            ILocalizationService localizationService
            )
        {
            InitializeComponent();
            Instance = this;

            _loggerService = loggerService;
            _messageBoxService = messageBoxService;
            _versionService = versionService;
            _jsonSetting = jsonSetting;
            _localizationService = localizationService;
        }

        private async void Window_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await InitializeApp();
        }

        private async void GameModeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            await HandleGameModeChangeAsync();
        }

        private async void PresetComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            ServerPresetModel? previousPreset = e.RemovedItems.Count > 0
                ? e.RemovedItems[0] as ServerPresetModel
                : _previousPreset;

            if (_suppressPresetSelectionChanged)
            {
                return;
            }

            if (PresetComboBox?.SelectedItem is not ServerPresetModel selectedPreset)
            {
                _previousPreset = null;
                return;
            }

            if (!PresetComboBox.IsDropDownOpen)
            {
                _previousPreset = selectedPreset;
                return;
            }

            await HandlePresetChangeAsync(selectedPreset, previousPreset);
        }

        private async void DataGrid_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
        {
            var source = e.Source;

            if (source is Border or TextBlock or Image)
            {
                var vm = DataContext as MainWindowViewModel;
                vm?.PingSelectedServer();
            }
        }

        private void TitleBar_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            e.Handled = true;
            var parentWindow = TopLevel.GetTopLevel(this) as Window;
            parentWindow?.BeginMoveDrag(e);
        }

        private void DataGridTextColumn_HeaderPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            pingSortDirection = pingSortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;

            ServerList.Columns[3].CustomSortComparer = new PingComparer(pingSortDirection);
        }

        private async void ClusterUnclusterBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm || !vm.ServerModelsInitialized)
            {
                return;
            }

            await vm.ClusterUnclusterServersAsync();
            SyncPresetSelection(vm.SelectedPreset);
            RefreshClusterButtonContent();
        }

        private async void SavePresetBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            PresetNameWindow presetNameWindow = new(vm.GetPresetNameSuggestion())
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            string? presetName = await presetNameWindow.ShowDialog<string?>(this);

            if (presetName == null)
            {
                return;
            }

            presetName = presetName.Trim();

            if (string.IsNullOrWhiteSpace(presetName))
            {
                await _messageBoxService.ShowMessageBoxAsync(
                    _localizationService.GetLocaleValue("MessageBoxInfoTitle"),
                    _localizationService.GetLocaleValue("PresetNameRequiredDialogue")
                    );

                return;
            }

            ServerPresetModel? existingPreset = vm.GetCurrentGamePreset(presetName);
            bool isSuggestedPresetName = vm.IsSuggestedPresetName(presetName);

            if (existingPreset != null && !isSuggestedPresetName)
            {
                bool overwriteResult = await _messageBoxService.ShowMessageBoxConfirmationAsync(
                    _localizationService.GetLocaleValue("MessageBoxInfoTitle"),
                    string.Format(_localizationService.GetLocaleValue("PresetOverwriteConfirmDialogue"), presetName),
                    MsBox.Avalonia.Enums.Icon.Setting
                    );

                if (!overwriteResult)
                {
                    return;
                }
            }

            _suppressPresetSelectionChanged = true;

            try
            {
                await vm.SavePresetAsync(presetName);

                SyncPresetSelection(vm.SelectedPreset);
            }
            finally
            {
                _suppressPresetSelectionChanged = false;
            }
        }

        private async void DeletePresetBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm || vm.SelectedPreset == null)
            {
                return;
            }

            bool deleteResult = await _messageBoxService.ShowMessageBoxConfirmationAsync(
                _localizationService.GetLocaleValue("MessageBoxInfoTitle"),
                string.Format(_localizationService.GetLocaleValue("PresetDeleteConfirmDialogue"), vm.SelectedPreset.Name),
                MsBox.Avalonia.Enums.Icon.Warning
                );

            if (!deleteResult)
            {
                return;
            }

            _suppressPresetSelectionChanged = true;

            try
            {
                await vm.DeleteSelectedPresetAsync();

                SyncPresetSelection(vm.SelectedPreset);
            }
            finally
            {
                _suppressPresetSelectionChanged = false;
            }
        }

        public async Task InitializeApp()
        {
            await _jsonSetting.LoadSettingsAsync();

            await SetLanguage();

            await ConfigureControls();

            var vm = ServiceLocator.GetRequiredService<MainWindowViewModel>();

            await vm.LoadServersAsync();

            DataContext = vm;

            if (vm.ServersLoaded)
            {
                await SyncServersAsync(vm);
                vm.LoadPresetPickerItems();
                await vm.RestoreLastSelectedPresetAsync();
            }

            ConfigurePresetControls(vm);
            RefreshClusterButtonContent();

            await _versionService.CheckVersionAsync();
        }

        private async Task SetLanguage()
        {
            // Extract language code from enum text
            var language = _jsonSetting.language.Replace(" ", "").Split("|")[1];

            await _localizationService.SetLanguage(language);
        }

        private async Task ConfigureControls()
        {
            try
            {
                switch (_jsonSetting.game_mode)
                {
                    case GameModes.CounterStrike2 or GameModes.CounterStrike2PerfectWorld:
                        GameModeComboBox.SelectedIndex = !_jsonSetting.game_mode.Contains("Perfect") ? 0 : 1;
                        break;
                    case GameModes.Deadlock:
                        GameModeComboBox.SelectedIndex = 2;
                        break;
                    case GameModes.Marathon:
                        GameModeComboBox.SelectedIndex = 3;
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported game mode: {_jsonSetting.game_mode}");
                };
            }
            catch (NotSupportedException ex)
            {
                await _loggerService.LogErrorAsync("An error has occured while setting game mode combo box", ex.Message);

                throw;
            }

            RefreshClusterButtonContent();
        }

        private void ConfigurePresetControls(MainWindowViewModel vm)
        {
            SyncPresetSelection(vm.SelectedPreset);
        }

        private async Task SyncServersAsync(MainWindowViewModel vm)
        {
            var localRevision = await _jsonSetting.GetRevisionByGameModeAsync();

            var fetchedRevision = vm.GetServerDataService().GetFetchedRevision();

            bool isCounterStrikeFamilyGame = _jsonSetting.game_mode is
                GameModes.CounterStrike2 or GameModes.CounterStrike2PerfectWorld;
            bool hasAffectedPresets = isCounterStrikeFamilyGame
                ? _jsonSetting.GetPresetsByGameMode(GameModes.CounterStrike2).Count > 0 ||
                  _jsonSetting.GetPresetsByGameMode(GameModes.CounterStrike2PerfectWorld).Count > 0
                : _jsonSetting.GetPresetsByGameMode(_jsonSetting.game_mode).Count > 0;

            // Store the initial revision without a reset when this game has no saved presets yet.
            if (localRevision == "-1" && !hasAffectedPresets)
            {
                await _jsonSetting.SetRevisionByGameModeAsync(fetchedRevision);
                return;
            }

            // Skip server unblocking and revision sync if local revision is equal to fetched revision
            if (localRevision == fetchedRevision)
            {
                return;
            }

            // This only happens on successful load and sync on startup or game switch
            await _messageBoxService.ShowMessageBoxAsync(
                    _localizationService.GetLocaleValue("MessageBoxInfoTitle"),
                    _localizationService.GetLocaleValue("SyncServersUnblockAllDialogue"),
                    MsBox.Avalonia.Enums.Icon.Setting
                    );

            bool unblocked = await vm.UnblockCurrentGameServersAsync();

            if (!unblocked)
            {
                return;
            }

            await vm.PruneCurrentGamePresetEntriesAsync();

            if (isCounterStrikeFamilyGame)
            {
                if (!await vm.PruneCounterStrikeFamilyPresetEntriesAsync())
                {
                    return;
                }
            }

            await _jsonSetting.SetRevisionByGameModeAsync(fetchedRevision);
        }

        private async Task HandleGameModeChangeAsync()
        {
            if (DataContext is not MainWindowViewModel vm || GameModeComboBox?.SelectedItem == null)
            {
                return;
            }

            bool result = await _messageBoxService.ShowMessageBoxConfirmationAsync(
                    _localizationService.GetLocaleValue("MessageBoxInfoTitle"),
                    _localizationService.GetLocaleValue("SwapGameModeUnblockAllConflict"),
                    MsBox.Avalonia.Enums.Icon.Setting
                    );

            if (!result)
            {
                // Revert back selection without triggering event handler
                GameModeComboBox.SelectionChanged -= GameModeComboBox_SelectionChanged;
                GameModeComboBox.SelectedItem = _jsonSetting.game_mode;
                GameModeComboBox.SelectionChanged += GameModeComboBox_SelectionChanged;

                return;
            }

            // Clear the currently loaded game's rules before changing game mode
            await vm.UnblockCurrentGameServersAsync();

            // Update json setting game mode and serialize it
            await _jsonSetting.SetGameModeAsync((string)GameModeComboBox.SelectedItem);

            await InitializeApp();
        }

        private async Task HandlePresetChangeAsync(
            ServerPresetModel selectedPreset,
            ServerPresetModel? previousPreset
            )
        {
            if (DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            if (AreSamePresetSelection(selectedPreset, previousPreset))
            {
                return;
            }

            bool presetApplied = await vm.ApplyPresetAsync(selectedPreset);

            if (!presetApplied)
            {
                SyncPresetSelection(previousPreset);
                return;
            }

            SyncPresetSelection(vm.SelectedPreset);
            RefreshClusterButtonContent();
        }

        private void SyncPresetSelection(ServerPresetModel? preset)
        {
            _suppressPresetSelectionChanged = true;
            PresetComboBox.SelectedItem = preset;
            _suppressPresetSelectionChanged = false;
            _previousPreset = preset;
        }

        private static bool AreSamePresetSelection(ServerPresetModel? left, ServerPresetModel? right)
        {
            if (left == null || right == null)
            {
                return left == null && right == null;
            }

            return left.Equals(right);
        }

        private void RefreshClusterButtonContent()
        {
            ClusterUnclusterBtn.Content = _localizationService.GetLocaleValue(
                _jsonSetting.is_clustered ? "UnclusterServers" : "ClusterServers"
                );
        }
    }
}
