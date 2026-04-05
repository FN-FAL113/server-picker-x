using CommunityToolkit.Mvvm.ComponentModel;
using MsBox.Avalonia.Enums;
using ServerPickerX.Comparers;
using ServerPickerX.Extensions;
using ServerPickerX.Models;
using ServerPickerX.Services.DependencyInjection;
using ServerPickerX.Services.Localizations;
using ServerPickerX.Services.MessageBoxes;
using ServerPickerX.Settings;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace ServerPickerX.ViewModels
{
    public partial class PresetManagerWindowViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ObservableCollectionExtended<PresetModel> presets = [];

        public ObservableCollectionExtended<PresetServerModel> PresetServers { get; } = [];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanDelete))]
        [NotifyPropertyChangedFor(nameof(CanApply))]
        [NotifyPropertyChangedFor(nameof(CanEditPreset))]
        [NotifyPropertyChangedFor(nameof(CanToggleClusterMode))]
        private PresetModel? selectedPresetItem;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanDelete))]
        private bool isDeletingPreset;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ClusterToggleText))]
        private bool editorIsClustered;

        private readonly MainWindowViewModel _mainVm;
        private readonly JsonSetting _jsonSetting;
        private readonly IMessageBoxService _messageBoxService;
        private readonly ILocalizationService _localizationService;

        public string WindowTitle =>
            $"{_localizationService.GetLocaleValue("PresetsWindowTitle")} - {_mainVm.GetCurrentGameMode()}";

        public bool CanDelete => SelectedPresetItem != null && !IsDeletingPreset;

        public bool CanApply => SelectedPresetItem != null;

        public bool CanEditPreset => SelectedPresetItem != null;

        public bool CanToggleClusterMode => SelectedPresetItem != null;

        public string ClusterToggleText => _localizationService.GetLocaleValue(
            EditorIsClustered ? "UnclusterServers" : "ClusterServers");

        public PresetManagerWindowViewModel(
            MainWindowViewModel mainVm,
            JsonSetting jsonSetting,
            IMessageBoxService messageBoxService,
            ILocalizationService localizationService
            )
        {
            _mainVm = mainVm;
            _jsonSetting = jsonSetting;
            _messageBoxService = messageBoxService;
            _localizationService = localizationService;
            EditorIsClustered = false;

            ReloadPresets(_mainVm.SelectedPreset?.Name);
        }

        partial void OnSelectedPresetItemChanged(PresetModel? oldValue, PresetModel? newValue)
        {
            if (newValue != null)
            {
                EditorIsClustered = newValue.IsClustered;
            }
            else
            {
                EditorIsClustered = false;
            }

            LoadServerItemsForSelectedPreset();
        }

        public async Task AddPresetAsync()
        {
            string presetName = GetNextPresetName();
            PresetModel newPreset = new()
            {
                Name = presetName,
                GameMode = _mainVm.GetCurrentGameMode(),
                IsClustered = false,
                BlockedServerKeys = [],
            };

            await _jsonSetting.AddOrUpdatePresetAsync(ClonePreset(newPreset));

            ReloadPresets(presetName);
        }

        public async Task<bool> DeletePresetsAsync(IReadOnlyList<PresetModel> presetsToDelete)
        {
            if (presetsToDelete == null || presetsToDelete.Count == 0 || IsDeletingPreset)
            {
                return false;
            }

            List<PresetModel> normalizedPresets = presetsToDelete
                .Where(preset => preset != null)
                .Distinct()
                .ToList();

            if (normalizedPresets.Count == 0)
            {
                return false;
            }

            IsDeletingPreset = true;

            try
            {
                string confirmationText = normalizedPresets.Count == 1
                    ? string.Format(
                        _localizationService.GetLocaleValue("PresetDeleteConfirmDialogue"),
                        normalizedPresets[0].Name
                        )
                    : string.Format(
                        _localizationService.GetLocaleValue("PresetDeleteSelectedConfirmDialogue"),
                        normalizedPresets.Count
                        );

                bool shouldDelete = await _messageBoxService.ShowMessageBoxConfirmationAsync(
                    _localizationService.GetLocaleValue("MessageBoxInfoTitle"),
                    confirmationText,
                    Icon.Setting
                    );

                if (!shouldDelete)
                {
                    return false;
                }

                List<PresetModel> currentPresets = GetCurrentGamePresets();
                HashSet<string> deletedPresetNames = normalizedPresets
                    .Select(preset => preset.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                string? preferredPresetName = currentPresets
                    .FirstOrDefault(preset => !deletedPresetNames.Contains(preset.Name))
                    ?.Name;

                foreach (PresetModel preset in normalizedPresets)
                {
                    await _mainVm.DeletePresetAsync(preset);
                }

                ReloadPresets(preferredPresetName);

                return true;
            }
            finally
            {
                IsDeletingPreset = false;
            }
        }

        public async Task<bool> ApplySelectedPresetAsync()
        {
            if (SelectedPresetItem == null)
            {
                return false;
            }

            // The modal can add/delete presets without touching the main dropdown state.
            // Refresh the main preset list before applying so the selected preset can be reselected there too.
            _mainVm.LoadPresetPickerItems();

            return await _mainVm.ApplyPresetAsync(ClonePreset(SelectedPresetItem));
        }

        public async Task<bool> RenamePresetAsync(PresetModel preset, string originalPresetName)
        {
            if (preset == null)
            {
                return false;
            }

            string newPresetName = (preset.Name ?? string.Empty).Trim();
            string currentPresetName = originalPresetName.Trim();

            if (string.IsNullOrWhiteSpace(newPresetName))
            {
                await _messageBoxService.ShowMessageBoxAsync(
                    _localizationService.GetLocaleValue("MessageBoxInfoTitle"),
                    _localizationService.GetLocaleValue("PresetNameRequiredDialogue")
                    );

                ReloadPresets(currentPresetName);
                return false;
            }

            if (newPresetName.Equals(currentPresetName, StringComparison.OrdinalIgnoreCase))
            {
                ReloadPresets(currentPresetName);
                return true;
            }

            bool hasDuplicatePresetName = _jsonSetting.HasDuplicatePresetNameByCurrentGameMode(newPresetName);
            bool overwroteDifferentPreset = false;

            if (hasDuplicatePresetName)
            {
                overwroteDifferentPreset = await _messageBoxService.ShowMessageBoxConfirmationAsync(
                        _localizationService.GetLocaleValue("MessageBoxInfoTitle"),
                        string.Format(
                        _localizationService.GetLocaleValue("PresetOverwriteConfirmDialogue"),
                        newPresetName
                        ),
                    Icon.Setting
                    );

                if (!overwroteDifferentPreset)
                {
                    // Revert back prename and reload presets
                    preset.Name = currentPresetName;
                    ReloadPresets(currentPresetName);
                    return false;
                }
            }

            await _jsonSetting.RemovePresetAsync(_mainVm.GetCurrentGameMode(), newPresetName);
            await _jsonSetting.AddOrUpdatePresetAsync(preset);
            await SyncPresetReferenceAfterRenameAsync(currentPresetName, newPresetName, overwroteDifferentPreset);

            ReloadPresets(newPresetName);

            return true;
        }

        public async Task PersistSelectedPresetServerKeysAsync()
        {
            if (SelectedPresetItem == null)
            {
                return;
            }

            SelectedPresetItem.BlockedServerKeys = PresetServers
                .Where(serverItem => serverItem.IsBlocked)
                .Select(serverItem => serverItem.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(serverKey => serverKey, StringComparer.OrdinalIgnoreCase)
                .ToList();

            await _jsonSetting.AddOrUpdatePresetAsync(ClonePreset(SelectedPresetItem));
            await ClearAppliedPresetReferenceIfNeededAsync(SelectedPresetItem.Name);
        }

        public async Task ToggleSelectedPresetClusterModeAsync()
        {
            if (SelectedPresetItem == null)
            {
                return;
            }

            bool hasBlockedEntries = (SelectedPresetItem.BlockedServerKeys?.Count ?? 0) > 0;

            if (hasBlockedEntries)
            {
                bool shouldChangeMode = await _messageBoxService.ShowMessageBoxConfirmationAsync(
                    _localizationService.GetLocaleValue("MessageBoxInfoTitle"),
                    _localizationService.GetLocaleValue("PresetChangeViewModeConfirmDialogue"),
                    Icon.Setting
                    );

                if (!shouldChangeMode)
                {
                    return;
                }
            }

            SelectedPresetItem.IsClustered = !SelectedPresetItem.IsClustered;
            SelectedPresetItem.BlockedServerKeys = [];
            EditorIsClustered = SelectedPresetItem.IsClustered;

            await _jsonSetting.AddOrUpdatePresetAsync(ClonePreset(SelectedPresetItem));
            await ClearAppliedPresetReferenceIfNeededAsync(SelectedPresetItem.Name);

            LoadServerItemsForSelectedPreset();
        }

        // Use NaturalStringComparer to sort preset names.
        // This is to avoid issues with numbers in preset names being treated as part of the number sequence.
        // For example, "Preset 1" should come before "Preset 10" in ascending order, not after.
        public void SortPresets(ListSortDirection direction)
        {
            string? selectedPresetName = SelectedPresetItem?.Name;
            List<PresetModel> sortedPresets = (direction == ListSortDirection.Ascending
                ? Presets.OrderBy(preset => preset.Name, NaturalStringComparer.OrdinalIgnoreCase)
                : Presets.OrderByDescending(preset => preset.Name, NaturalStringComparer.OrdinalIgnoreCase))
                .ToList();

            Presets.Clear();
            Presets.AddRange(sortedPresets);

            if (!string.IsNullOrWhiteSpace(selectedPresetName))
            {
                SelectedPresetItem = Presets.FirstOrDefault(preset =>
                    preset.Name.Equals(selectedPresetName, StringComparison.OrdinalIgnoreCase));
            }
        }

        public void SortServerItems(string sortKey, ListSortDirection direction)
        {
            List<PresetServerModel> sortedItems = sortKey switch
            {
                "Blocked" => (direction == ListSortDirection.Ascending
                    ? PresetServers.OrderBy(serverItem => serverItem.IsBlocked)
                    : PresetServers.OrderByDescending(serverItem => serverItem.IsBlocked)).ToList(),
                "Flag" => (direction == ListSortDirection.Ascending
                    ? PresetServers.OrderBy(serverItem => serverItem.FlagSortKey, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(serverItem => serverItem.Description, StringComparer.OrdinalIgnoreCase)
                    : PresetServers.OrderByDescending(serverItem => serverItem.FlagSortKey, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(serverItem => serverItem.Description, StringComparer.OrdinalIgnoreCase)).ToList(),
                "ServerId" => (direction == ListSortDirection.Ascending
                    ? PresetServers.OrderBy(serverItem => serverItem.Name, StringComparer.OrdinalIgnoreCase)
                    : PresetServers.OrderByDescending(serverItem => serverItem.Name, StringComparer.OrdinalIgnoreCase)).ToList(),
                _ => (direction == ListSortDirection.Ascending
                    ? PresetServers.OrderBy(serverItem => serverItem.Description, StringComparer.OrdinalIgnoreCase)
                    : PresetServers.OrderByDescending(serverItem => serverItem.Description, StringComparer.OrdinalIgnoreCase)).ToList(),
            };

            PresetServers.Clear();
            PresetServers.AddRange(sortedItems);
        }

        private void ReloadPresets(string? selectedPresetName = null)
        {
            string? presetNameToSelect = selectedPresetName ?? SelectedPresetItem?.Name;
            List<PresetModel> currentPresets = GetCurrentGamePresets();

            if (currentPresets.Count == 0)
            {
                Presets = [];
                SelectedPresetItem = null;
                PresetServers.Clear();
                return;
            }

            Presets = new ObservableCollectionExtended<PresetModel>(currentPresets);

            SelectedPresetItem = !string.IsNullOrWhiteSpace(presetNameToSelect)
                ? Presets.FirstOrDefault(preset =>
                    preset.Name.Equals(presetNameToSelect, StringComparison.OrdinalIgnoreCase))
                : null;

            SelectedPresetItem ??= Presets[0];
        }

        private void LoadServerItemsForSelectedPreset()
        {
            PresetServers.Clear();

            if (SelectedPresetItem == null)
            {
                return;
            }

            HashSet<string> blockedServerKeys = (SelectedPresetItem.BlockedServerKeys ?? [])
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (ServerModel serverModel in _mainVm.GetCurrentGameServerModels(SelectedPresetItem.IsClustered))
            {
                string serverKey = _mainVm.GetServerKey(serverModel, SelectedPresetItem.IsClustered);

                PresetServers.Add(new PresetServerModel(serverModel, serverKey, blockedServerKeys.Contains(serverKey)));
            }
        }

        private List<PresetModel> GetCurrentGamePresets()
        {
            return _jsonSetting.GetPresetsByGameMode(_mainVm.GetCurrentGameMode());
        }

        private async Task ClearAppliedPresetReferenceIfNeededAsync(string presetName)
        {
            bool matchesAppliedPreset = (_mainVm.SelectedPreset?.Name ?? string.Empty)
                .Equals(presetName, StringComparison.OrdinalIgnoreCase);
            bool matchesLastSelected = (_jsonSetting.GetLastSelectedPresetNameByGameMode() ?? string.Empty)
                .Equals(presetName, StringComparison.OrdinalIgnoreCase);

            if (!matchesAppliedPreset && !matchesLastSelected)
            {
                return;
            }

            if (matchesLastSelected)
            {
                await _jsonSetting.ClearLastSelectedPresetNameByGameModeAsync();
            }

            if (matchesAppliedPreset)
            {
                _mainVm.SelectPresetByName(string.Empty);
            }
        }

        private async Task SyncPresetReferenceAfterRenameAsync(
            string originalPresetName,
            string renamedPresetName,
            bool overwroteDifferentPreset
            )
        {
            if (overwroteDifferentPreset)
            {
                await ClearAppliedPresetReferenceIfNeededAsync(renamedPresetName);

                return;
            }

            bool renamedLastSelected = (_jsonSetting.GetLastSelectedPresetNameByGameMode() ?? string.Empty)
                .Equals(originalPresetName, StringComparison.OrdinalIgnoreCase);
            bool renamedAppliedPreset = (_mainVm.SelectedPreset?.Name ?? string.Empty)
                .Equals(originalPresetName, StringComparison.OrdinalIgnoreCase);
            

            if (renamedLastSelected)
            {
                await _jsonSetting.SetLastSelectedPresetNameByGameModeAsync(renamedPresetName);
            }

            if (renamedAppliedPreset)
            {
                _mainVm.LoadPresetPickerItems();
                _mainVm.SelectPresetByName(renamedPresetName);
            }
        }

        private string GetNextPresetName()
        {
            string basePresetName = _localizationService.GetLocaleValue("NewPresetName");
            HashSet<string> existingPresetNames = GetCurrentGamePresets()
                .Select(preset => preset.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!existingPresetNames.Contains(basePresetName))
            {
                return basePresetName;
            }

            for (int suffix = 2; ; suffix++)
            {
                string candidateName = $"{basePresetName} ({suffix})";

                if (!existingPresetNames.Contains(candidateName))
                {
                    return candidateName;
                }
            }
        }

        private static PresetModel ClonePreset(PresetModel preset)
        {
            return new PresetModel
            {
                Name = preset.Name,
                GameMode = preset.GameMode,
                IsClustered = preset.IsClustered,
                BlockedServerKeys = (preset.BlockedServerKeys ?? [])
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(serverKey => serverKey, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                };
        }
    }
}
