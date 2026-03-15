using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using ServerPickerX.Comparers;
using ServerPickerX.Constants;
using ServerPickerX.Services.DependencyInjection;
using ServerPickerX.Services.Localizations;
using ServerPickerX.Services.MessageBoxes;
using ServerPickerX.Services.Versions;
using ServerPickerX.Settings;
using ServerPickerX.ViewModels;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

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

        private readonly JsonSetting _jsonSetting;
        private readonly IMessageBoxService _messageBoxService;
        private readonly IVersionService _versionService;
        private readonly ILocalizationService _localizationService;

        // Parameterless constructor, allows design previewer to create its own instance since it doesn't support DI
        public MainWindow()
        {
            InitializeComponent();
            Instance = this;

            _jsonSetting = ServiceLocator.GetRequiredService<JsonSetting>();
            _messageBoxService = ServiceLocator.GetRequiredService<IMessageBoxService>();
            _versionService = ServiceLocator.GetRequiredService<IVersionService>();
            _localizationService = ServiceLocator.GetRequiredService<ILocalizationService>();
        }

        // DI constructor, allows inversion of control and unit tests mocking
        public MainWindow(
            JsonSetting jsonSetting,
            IMessageBoxService messageBoxService,
            IVersionService versionService,
            ILocalizationService localizationService
            )
        {
            InitializeComponent();
            Instance = this;

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

        private async void DataGrid_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
        {
            var source = e.Source;

            if (source is  Border or TextBlock or Image)
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
            var vm = DataContext as MainWindowViewModel;

            // Check if servers are loaded and initialized
            if (!vm?.ServerModelsInitialized ?? false)
            {
                return;
            }

            // Update button DynamicResource binding base on language setting
            ClusterUnclusterBtn.Bind(
                Button.ContentProperty,
                new DynamicResourceExtension(_jsonSetting.is_clustered ? "ClusterServers" : "UnclusterServers")
            );
        }

        public async Task InitializeApp()
        {
            await _jsonSetting.LoadSettingsAsync();

            SetLanguage();

            ConfigureControls();

            var vm = ServiceLocator.GetRequiredService<MainWindowViewModel>();

            await vm.LoadServersAsync();

            DataContext = vm;

           
            if (vm.ServersLoaded)
            {
                await SyncServersAsync(vm);
            }

            await _versionService.CheckVersionAsync();
        }

        private void SetLanguage()
        {
            // Extract language code from enum text
            var language = _jsonSetting.language.Replace(" ", "").Split("|")[1];

            _localizationService.SetLanguage(language);
        }

        private void ConfigureControls()
        {
            bool isGameModeCS2 = _jsonSetting.game_mode == GameModes.CounterStrike2;

            // Swap game mode combo box selection based on json settings
            GameModeComboBox.SelectedIndex = isGameModeCS2 ? 0 : 1;

            // Update button DynamicResource binding base on language setting
            ClusterUnclusterBtn.Bind(
                Button.ContentProperty,
                new DynamicResourceExtension(_jsonSetting.is_clustered ? "UnclusterServers" : "ClusterServers")
                );
        }

        private async Task SyncServersAsync(MainWindowViewModel vm)
        {
            // If Steam SDR API data got updated, sync the changes
            var localRevision = _jsonSetting.game_mode == GameModes.CounterStrike2 ? 
                _jsonSetting.cs2_server_revision : _jsonSetting.deadlock_server_revision;

            var fetchedRevision = vm.GetServerDataService().GetServerData().Revision;

            // Skip server revision syncing and unblocking if local revision is equal to the fetched revision
            if (localRevision == fetchedRevision)
            {
                return;
            }

            await _messageBoxService.ShowMessageBoxAsync(
                    _localizationService.GetLocaleValue("MessageBoxInfoTitle"),
                    _localizationService.GetLocaleValue("SyncServersUnblockAllDialogue"),
                    MsBox.Avalonia.Enums.Icon.Setting
                    );

            await vm.UnblockAllAsync();

            if (_jsonSetting.game_mode == GameModes.CounterStrike2)
                _jsonSetting.cs2_server_revision = fetchedRevision;
            else
                _jsonSetting.deadlock_server_revision = fetchedRevision;

            await _jsonSetting.SaveSettingsAsync();
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

            if (!result || vm.PendingOperation)
            {
                // Revert back selection without triggering event handler
                GameModeComboBox.SelectionChanged -= GameModeComboBox_SelectionChanged;
                GameModeComboBox.SelectedItem = _jsonSetting.game_mode;
                GameModeComboBox.SelectionChanged += GameModeComboBox_SelectionChanged;

                return;
            }

            // Unblock all servers first before changing game mode
            await vm.UnblockAllAsync();

            // Update json setting game mode and serialize it
            _jsonSetting.game_mode = (string)GameModeComboBox.SelectedItem;
            await _jsonSetting.SaveSettingsAsync();

            await InitializeApp();
        }
    }
}
