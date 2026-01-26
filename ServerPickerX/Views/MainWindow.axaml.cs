using Avalonia.Collections;
using Avalonia.Controls;
using HarfBuzzSharp;
using ServerPickerX.Comparer;
using ServerPickerX.Models;
using ServerPickerX.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace ServerPickerX.Views
{
    public partial class MainWindow : Window
    {

        private ListSortDirection pingSortDirection = ListSortDirection.Ascending;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            DataContext = await new MainWindowViewModel().LoadServersAsync();

            await ((MainWindowViewModel)DataContext).PingServers();
        }

        private void Window_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            // prevent other mouse event listeners from being triggered
            e.Handled = true;

            BeginMoveDrag(e);
        }

        private void MinimizeBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close();
        }

        private async void DataGrid_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
        {
            // a cell is double clicked, ping the selected server
            if (e.Source is Border || e.Source is TextBlock || e.Source is Image)
            {
                await ((MainWindowViewModel)DataContext).PingSelectedServer();
            }
        }

        private void DataGridTextColumn_HeaderPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            pingSortDirection = pingSortDirection == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;

            serverList.Columns[3].CustomSortComparer = new PingComparer(pingSortDirection);
        }
    }
}