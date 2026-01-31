using Avalonia.Controls;
using ServerPickerX.Comparers;
using ServerPickerX.ConfigSections;
using ServerPickerX.Helpers;
using ServerPickerX.ViewModels;
using System;
using System.ComponentModel;

namespace ServerPickerX.Views
{
    public partial class MainWindow : Window
    {
        // initialize a static singleton object for accessing main window instance
        public static MainWindow Instance { get; private set; }

        private ListSortDirection pingSortDirection = ListSortDirection.Ascending;

        // initialize a static singleton object for handling json settings
        public static JsonSetting jsonSettings = new();

        public MainWindow()
        {
            InitializeComponent();

            Instance = this;
        }

        private async void Window_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await jsonSettings.LoadSettings();

            MainWindowViewModel viewModel = new MainWindowViewModel();

            DataContext = await viewModel.LoadServersAsync();

            // unblock all server to sync new data if steam sdr api has been updated
            if (jsonSettings.server_revision != ServerHelper.current_server_revision)
            {
                await MessageBoxHelper.ShowMessageBox(
                    "Please Standby", 
                    "Server data just got updated by Valve! All blocked servers " + Environment.NewLine +
                    "will be unblocked in order to synchronize new server data",
                    MsBox.Avalonia.Enums.Icon.Setting
                );

                await viewModel.UnblockAll();

                jsonSettings.server_revision = ServerHelper.current_server_revision;

                await jsonSettings.SaveSettings();
            } else
            {
                await viewModel.PingServers();
            }

            await VersionHelper.CheckVersion();
        }

        private async void DataGrid_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
        {
            var source = e.Source;

            // a cell is double clicked, ping the selected server
            if (source is Border || source is TextBlock || source is Image)
            {
                await ((MainWindowViewModel)DataContext).PingSelectedServer();
            }
        }

        private void TitleBar_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            // prevent other mouse event listeners from being triggered
            e.Handled = true;

            var parentWindow = TopLevel.GetTopLevel(this) as Window;

            parentWindow.BeginMoveDrag(e);
        }

        private void DataGridTextColumn_HeaderPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            // custom sort comparer for ping column due to having a suffix "ms"
            pingSortDirection = pingSortDirection == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;

            serverList.Columns[3].CustomSortComparer = new PingComparer(pingSortDirection);
        }
    }
}