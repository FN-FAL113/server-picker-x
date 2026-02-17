using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using ServerPickerX.Comparers;
using ServerPickerX.Helpers;
using ServerPickerX.Services.Loggers;
using ServerPickerX.Services.MessageBoxes;
using ServerPickerX.Services.Processes;
using ServerPickerX.Services.Servers;
using ServerPickerX.Services.SystemFirewalls;
using ServerPickerX.Services.Versions;
using ServerPickerX.Settings;
using ServerPickerX.ViewModels;
using System;
using System.ComponentModel;
using System.Net.Http;
using System.Threading.Tasks;

namespace ServerPickerX.Views
{
    public partial class MainWindow : Window
    {
        // Initialize a static singleton object for accessing main window instance
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

        // Parameterless constructors for windows and viewmodels, access services instead through the container
        // DI through constructors doesn't work with design previewer since it has no clue on providing parameters
        public MainWindow()
        {
            InitializeComponent();

            Instance = this;

            _messageBoxService = App.ServiceProvider.GetRequiredService<IMessageBoxService>();
            _versionService = App.ServiceProvider.GetRequiredService<VersionService>();
            _jsonSetting = App.ServiceProvider.GetRequiredService<JsonSetting>();
        }

        private async void Window_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await InitializeApp();
        }

        private async void gameComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            // User changed game mode, re-initialize app with updated services and settings
            MainWindowViewModel viewModel = (MainWindowViewModel)DataContext;

            if (viewModel == null) return;

            // Unblock servers first to prevent conflicting firewall entries between both games
            await viewModel.UnblockAll();

            // Update json setting gameMode prop and serialize locally
            _jsonSetting.game_mode = ((ComboBoxItem)gameComboBox.SelectedItem).Content.ToString();

            await _jsonSetting.SaveSettingsAsync();

            await InitializeApp();
        }

        public async Task InitializeApp()
        {
            await _jsonSetting.LoadSettingsAsync();

            bool isGameModeCS2 = _jsonSetting.game_mode == "Counter Strike 2";

            // Set UI control content values based on jsonSetting values
            gameComboBox.SelectedIndex = isGameModeCS2 ? 0 : 1;
            clusterUnclusterBtn.Content = _jsonSetting.is_clustered ? "Uncluster Servers" : "Cluster Servers";

            // Load servers and attach view model to window data context
            var viewModel = App.ServiceProvider.GetRequiredService<MainWindowViewModel>(); 
            await viewModel.LoadServers();

            DataContext = viewModel;

            // Unblock all server to sync new data if Steam SDR API has been updated
            var serverRevision = isGameModeCS2 ? _jsonSetting.cs2_server_revision : _jsonSetting.deadlock_server_revision;
            if (serverRevision != viewModel.GetServerDataService().GetServerData().Revision)
            {
                await _messageBoxService.ShowMessageBoxAsync(
                    "Please Standby",
                    "Server data just got updated by Valve! All blocked servers " + Environment.NewLine +
                    "will be unblocked in order to synchronize new server data",
                    MsBox.Avalonia.Enums.Icon.Setting
                );

                await viewModel.UnblockAll();

                if (isGameModeCS2)
                {
                    _jsonSetting.cs2_server_revision = viewModel.GetServerDataService().GetServerData().Revision;
                } else
                {
                    _jsonSetting.deadlock_server_revision = viewModel.GetServerDataService().GetServerData().Revision;
                }

                await _jsonSetting.SaveSettingsAsync();
            }

            // Check for latest version through github releases API
            await _versionService.CheckVersionAsync();
        }

        private async void DataGrid_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
        {
            var source = e.Source;

            // A cell is double clicked, ping the selected server
            if (source is Border || source is TextBlock || source is Image)
            {
                (DataContext as MainWindowViewModel)?.PingSelectedServer();
            }
        }

        private void TitleBar_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            // Prevent other mouse event listeners from being triggered
            e.Handled = true;

            var parentWindow = TopLevel.GetTopLevel(this) as Window;

            parentWindow?.BeginMoveDrag(e);
        }

        private void DataGridTextColumn_HeaderPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            // Custom sort comparer for ping column due to having a suffix "ms"
            pingSortDirection = pingSortDirection == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;

            serverList.Columns[3].CustomSortComparer = new PingComparer(pingSortDirection);
        }

        private void clusterUnclusterBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (!(DataContext as MainWindowViewModel)?.ServersInitialized ?? true)
            {
                return;
            }

            // Update UI content by inverse value
            clusterUnclusterBtn.Content = clusterUnclusterBtn?.Content?.ToString() == "Cluster Servers" ? "Uncluster Servers" : "Cluster Servers";
        }
    }
}