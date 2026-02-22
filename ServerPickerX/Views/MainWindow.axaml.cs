using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using ServerPickerX.Comparers;
using ServerPickerX.Constants;
using ServerPickerX.Services.MessageBoxes;
using ServerPickerX.Services.Versions;
using ServerPickerX.Settings;
using ServerPickerX.ViewModels;
using System;
using System.ComponentModel;
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

        private readonly IMessageBoxService _messageBoxService;
        private readonly IVersionService _versionService;
        private readonly JsonSetting _jsonSetting;

        // Parameterless constructor, allows design previewer to instantiate this class since it doesn't support DI
        public MainWindow()
        {
            InitializeComponent();
            Instance = this;

            _messageBoxService = App.ServiceProvider.GetRequiredService<IMessageBoxService>();
            _versionService = App.ServiceProvider.GetRequiredService<VersionService>();
            _jsonSetting = App.ServiceProvider.GetRequiredService<JsonSetting>();
        }

        // DI constructor, allows inversion of control and unit tests mocking
        public MainWindow(
            IMessageBoxService messageBoxService,
            IVersionService versionService,
            JsonSetting jsonSetting
            )
        {
            InitializeComponent();
            Instance = this;

            _messageBoxService = messageBoxService;
            _versionService = versionService;
            _jsonSetting = jsonSetting;
        }

        private async void Window_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await InitializeApp();

            ToolTip.SetTip(GameModeComboBox, "Select game mode");
            ToolTip.SetTip(ClusterUnclusterBtn, $"Group or ungroup servers");
            ToolTip.SetTip(RefreshBtn, "Refresh all server ping");
        }

        private async void GameModeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            await HandleGameModeChangeAsync();
        }

        private async void DataGrid_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
        {
            var source = e.Source;
            if (source is Border || source is TextBlock || source is Image)
                (DataContext as MainWindowViewModel)?.PingSelectedServer();
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

        private void ClusterUnclusterBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (!((DataContext as MainWindowViewModel)?.ServerModelsInitialized ?? false)) return;

            // Update UI content by inverse value
            ClusterUnclusterBtn.Content = ClusterUnclusterBtn?.Content?.ToString() == "Cluster Servers"
                ? "Uncluster Servers"
                : "Cluster Servers";
        }

        public async Task InitializeApp()
        {
            await _jsonSetting.LoadSettingsAsync();

            ConfigureControls();

            var vm = App.ServiceProvider.GetRequiredService<MainWindowViewModel>();
            
            await vm.LoadServersAsync();

            DataContext = vm;

            if (vm.ServersLoaded)
            {
                await SyncServersAsync(vm);
            }

            await _versionService.CheckVersionAsync();
        }

        private void ConfigureControls()
        {
            bool isGameModeCS2 = _jsonSetting.game_mode == GameModes.CounterStrike2;

            // Update game mode combo box selection base on json settings
            GameModeComboBox.SelectedIndex = isGameModeCS2 ? 0 : 1;

            // Update cluster button content based on json settings
            ClusterUnclusterBtn.Content = _jsonSetting.is_clustered
                ? "Uncluster Servers"
                : "Cluster Servers";
        }

        private async Task SyncServersAsync(MainWindowViewModel vm)
        {
            var localRevision = _jsonSetting.game_mode == GameModes.CounterStrike2
                ? _jsonSetting.cs2_server_revision
                : _jsonSetting.deadlock_server_revision;

            var fetchedRevision = vm.GetServerDataService().GetServerData().Revision;

            // Skip server revision syncing and unblocking if local revision is equal to the fetched revision
            if (localRevision == fetchedRevision)
            {
                return;
            }

            await _messageBoxService.ShowMessageBoxAsync(
                    "Please Standby",
                    "Server data just got updated by Valve! All blocked servers "
                    + Environment.NewLine +
                    "will be unblocked in order to synchronize new server data",
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
            if (DataContext is not MainWindowViewModel vm) return;

            bool result = await _messageBoxService.ShowMessageBoxConfirmationAsync(
                    "Info",
                    "This action will unblock all servers first to prevent firewall conflicts.",
                    MsBox.Avalonia.Enums.Icon.Setting
                    );

            if (!result || vm.PendingOperation)
            {
                // Revert back selection without triggering the handler
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
