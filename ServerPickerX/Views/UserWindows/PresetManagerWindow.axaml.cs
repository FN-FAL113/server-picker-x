using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ServerPickerX.Models;
using ServerPickerX.Services.DependencyInjection;
using ServerPickerX.Services.Localizations;
using ServerPickerX.Services.MessageBoxes;
using ServerPickerX.Settings;
using ServerPickerX.ViewModels;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace ServerPickerX.Views
{
    public partial class PresetManagerWindow : Window
    {
        private bool _allowPresetNameEdit;
        private bool _committingPresetName;
        private bool _isEditingPresetName;
        private string? _editingPresetOriginalName;
        private ListSortDirection? _presetSortDirection;
        private string? _serverSortColumn;
        private ListSortDirection _serverSortDirection = ListSortDirection.Ascending;

        public PresetManagerWindow()
        {
            InitializeComponent();
        }

        public PresetManagerWindow(MainWindowViewModel mainVm)
        {
            InitializeComponent();
            DataContext = new PresetManagerWindowViewModel(
                mainVm,
                ServiceLocator.GetRequiredService<JsonSetting>(),
                ServiceLocator.GetRequiredService<IMessageBoxService>(),
                ServiceLocator.GetRequiredService<ILocalizationService>()
                );
        }

        private async void AddBtn_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not PresetManagerWindowViewModel vm)
            {
                return;
            }

            await vm.AddPresetAsync();
            ReapplyPresetSortIfNeeded();
            ReapplyServerSortIfNeeded();
            BeginEditingSelectedPreset();
        }

        private async void DeleteBtn_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not PresetManagerWindowViewModel vm)
            {
                return;
            }

            bool deleted = await vm.DeletePresetsAsync(GetSelectedPresetItems());

            if (deleted)
            {
                ReapplyPresetSortIfNeeded();
                ReapplyServerSortIfNeeded();
                RestorePresetListFocus();
            }
        }

        private async void ApplyBtn_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not PresetManagerWindowViewModel vm)
            {
                return;
            }

            if (await vm.ApplySelectedPresetAsync())
            {
                Close();
            }
        }

        private async void ClusterToggleBtn_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not PresetManagerWindowViewModel vm)
            {
                return;
            }

            await vm.ToggleSelectedPresetClusterModeAsync();
            ReapplyServerSortIfNeeded();
        }

        private void PresetListGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            ReapplyServerSortIfNeeded();
        }

        private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            e.Handled = true;
            (TopLevel.GetTopLevel(this) as Window)?.BeginMoveDrag(e);
        }

        private void PresetListGrid_BeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
        {
            if (e.Row.DataContext is not PresetModel presetItem)
            {
                return;
            }

            _isEditingPresetName = true;
            _editingPresetOriginalName = presetItem.Name;
        }

        private async void PresetListGrid_RowEditEnded(object? sender, DataGridRowEditEndedEventArgs e)
        {
            _isEditingPresetName = false;
            PresetListGrid.IsReadOnly = true;

            if (e.EditAction != DataGridEditAction.Commit || e.Row.DataContext is not PresetModel presetItem)
            {
                return;
            }

            await CommitPresetNameAsync(presetItem, _editingPresetOriginalName ?? presetItem.Name);
        }

        private async Task CommitPresetNameAsync(PresetModel presetItem, string originalPresetName)
        {
            if (_committingPresetName || DataContext is not PresetManagerWindowViewModel vm)
            {
                return;
            }

            _committingPresetName = true;

            try
            {
                await vm.RenamePresetAsync(presetItem, originalPresetName);
                ReapplyPresetSortIfNeeded();
                ReapplyServerSortIfNeeded();
            }
            finally
            {
                _committingPresetName = false;
                _editingPresetOriginalName = null;
            }
        }

        private void PresetListGrid_DoubleTapped(object? sender, TappedEventArgs e)
        {
            if (_isEditingPresetName || DataContext is not PresetManagerWindowViewModel vm || vm.SelectedPresetItem == null)
            {
                return;
            }

            if (e.Source is not Control sourceControl ||
                sourceControl.FindAncestorOfType<DataGridColumnHeader>() != null ||
                sourceControl.FindAncestorOfType<DataGridCell>() == null)
            {
                return;
            }

            BeginEditingSelectedPreset();
        }

        private async void PresetListGrid_KeyDown(object? sender, KeyEventArgs e)
        {
            if (DataContext is not PresetManagerWindowViewModel vm)
            {
                return;
            }

            if (e.Key == Key.Delete)
            {
                List<PresetModel> selectedPresetItems = GetSelectedPresetItems();

                if (selectedPresetItems.Count == 0)
                {
                    return;
                }

                e.Handled = true;
                bool deleted = await vm.DeletePresetsAsync(selectedPresetItems);

                if (deleted)
                {
                    RestorePresetListFocus();
                }

                return;
            }

            if (e.Key == Key.F2)
            {
                e.Handled = true;
                BeginEditingSelectedPreset();
            }
        }

        private void PresetListGrid_Sorting(object? sender, DataGridColumnEventArgs e)
        {
            if (DataContext is not PresetManagerWindowViewModel vm || PresetListGrid.Columns.Count == 0 || !ReferenceEquals(e.Column, PresetListGrid.Columns[0]))
            {
                return;
            }

            _presetSortDirection = _presetSortDirection switch
            {
                ListSortDirection.Ascending => ListSortDirection.Descending,
                _ => ListSortDirection.Ascending,
            };

            vm.SortPresets(_presetSortDirection.Value);
        }

        private async void BlockedCheckBox_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not PresetManagerWindowViewModel vm)
            {
                return;
            }

            await vm.PersistSelectedPresetServerKeysAsync();
            ReapplyServerSortIfNeeded();
        }

        private async void ServerItemsGrid_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Space || DataContext is not PresetManagerWindowViewModel vm)
            {
                return;
            }

            if (ServerItemsGrid.SelectedItems.Count == 0)
            {
                return;
            }

            List<PresetServerModel> selectedItems = ServerItemsGrid.SelectedItems
                .OfType<PresetServerModel>()
                .ToList();

            if (selectedItems.Count == 0)
            {
                return;
            }

            e.Handled = true;

            bool shouldBlock = selectedItems.Any(serverItem => !serverItem.IsBlocked);

            foreach (PresetServerModel serverItem in selectedItems)
            {
                serverItem.IsBlocked = shouldBlock;
            }

            await vm.PersistSelectedPresetServerKeysAsync();
            ReapplyServerSortIfNeeded();
        }

        private void ServerItemsGrid_Sorting(object? sender, DataGridColumnEventArgs e)
        {
            string? sortKey = e.Column.SortMemberPath switch
            {
                "IsBlocked" => "Blocked",
                "FlagSortKey" => "Flag",
                "Name" => "ServerId",
                "Description" => "ServerName",
                _ => null,
            };

            if (sortKey == null)
            {
                return;
            }

            ListSortDirection defaultDirection = sortKey == "Blocked"
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;

            SortServerItems(sortKey, defaultDirection);
        }

        private void SortServerItems(string columnName, ListSortDirection defaultDirection = ListSortDirection.Ascending)
        {
            if (DataContext is not PresetManagerWindowViewModel vm)
            {
                return;
            }

            if (_serverSortColumn == columnName)
            {
                _serverSortDirection = _serverSortDirection == ListSortDirection.Ascending
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;
            }
            else
            {
                _serverSortColumn = columnName;
                _serverSortDirection = defaultDirection;
            }

            vm.SortServerItems(columnName, _serverSortDirection);
        }

        private void ReapplyPresetSortIfNeeded()
        {
            if (_presetSortDirection == null || DataContext is not PresetManagerWindowViewModel vm)
            {
                return;
            }

            vm.SortPresets(_presetSortDirection.Value);
        }

        private void ReapplyServerSortIfNeeded()
        {
            if (string.IsNullOrWhiteSpace(_serverSortColumn) || DataContext is not PresetManagerWindowViewModel vm)
            {
                return;
            }

            vm.SortServerItems(_serverSortColumn, _serverSortDirection);
        }

        private void RestorePresetListFocus()
        {
            Dispatcher.UIThread.Post(() => PresetListGrid.Focus());
        }

        private void BeginEditingSelectedPreset()
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (DataContext is not PresetManagerWindowViewModel vm || vm.SelectedPresetItem == null || GetSelectedPresetItems().Count != 1)
                {
                    return;
                }

                PresetListGrid.Focus();
                PresetListGrid.IsReadOnly = false;
                PresetListGrid.CurrentColumn = PresetListGrid.Columns.FirstOrDefault();
                PresetListGrid.BeginEdit();
            });
        }

        private List<PresetModel> GetSelectedPresetItems()
        {
            IEnumerable selectedItems = PresetListGrid.SelectedItems ?? System.Array.Empty<object>();
            List<PresetModel> selectedPresetItems = selectedItems
                .OfType<PresetModel>()
                .Distinct()
                .ToList();

            if (selectedPresetItems.Count == 0 && DataContext is PresetManagerWindowViewModel vm && vm.SelectedPresetItem != null)
            {
                selectedPresetItems.Add(vm.SelectedPresetItem);
            }

            return selectedPresetItems;
        }
    }
}
