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

            ToolTip.SetTip(gameComboBox, "Select game mode");
            ToolTip.SetTip(refreshBtn, "Refresh all server ping");
        }

        private async void gameComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
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
            serverList.Columns[3].CustomSortComparer = new PingComparer(pingSortDirection);
        }

        private void clusterUnclusterBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (!(DataContext as MainWindowViewModel)?.ServersInitialized ?? true) return;

            // Update UI content by inverse value
            clusterUnclusterBtn.Content = clusterUnclusterBtn?.Content?.ToString() == "Cluster Servers"
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

            await SyncServersAsync(vm);

            await _versionService.CheckVersionAsync();
        }

        private void ConfigureControls()
        {
            bool isGameModeCS2 = _jsonSetting.game_mode == GameModes.CounterStrike2;

            // Update game mode combo box selection base on json settings
            gameComboBox.SelectedIndex = isGameModeCS2 ? 0 : 1;

            // Update cluster button content based on json settings
            clusterUnclusterBtn.Content = _jsonSetting.is_clustered
                ? "Uncluster Servers"
                : "Cluster Servers";

            // Set tooltip info for (un)cluster button
            var action = _jsonSetting.is_clustered ? "Uncluster" : "Cluster";

            ToolTip.SetTip(clusterUnclusterBtn, $"Click to {action.ToLower()} servers");
        }

        private async Task SyncServersAsync(MainWindowViewModel vm)
        {
            var currentRevision = _jsonSetting.game_mode == GameModes.CounterStrike2
                ? _jsonSetting.cs2_server_revision
                : _jsonSetting.deadlock_server_revision;

            // Skip server revision syncing and unblocking if current json setting revision is equal to server data revision
            if (currentRevision == vm.GetServerDataService().GetServerData().Revision)
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
                _jsonSetting.cs2_server_revision = vm.GetServerDataService().GetServerData().Revision;
            else
                _jsonSetting.deadlock_server_revision = vm.GetServerDataService().GetServerData().Revision;

            await _jsonSetting.SaveSettingsAsync();
        }

        private async Task HandleGameModeChangeAsync()
        {
            if (DataContext is not MainWindowViewModel vm) return;

            // Unblock all servers first before changing game mode
            await vm.UnblockAllAsync();

            // Update json setting game mode and serialize it
            _jsonSetting.game_mode = (string)gameComboBox.SelectedItem!;
            await _jsonSetting.SaveSettingsAsync();

            await InitializeApp();
        }
    }
}
