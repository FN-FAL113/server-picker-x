using CommunityToolkit.Mvvm.ComponentModel;
using MsBox.Avalonia.Enums;
using Avalonia.Platform;
using ServerPickerX.Comparers;
using ServerPickerX.Extensions;
using ServerPickerX.Models;
using ServerPickerX.Services.Localizations;
using ServerPickerX.Services.MessageBoxes;
using ServerPickerX.Settings;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace ServerPickerX.ViewModels
{
    public partial class PresetManagerWindowViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ObservableCollectionExtended<PresetListItemViewModel> presets = [];

        public ObservableCollectionExtended<PresetServerSelectionItem> ServerItems { get; } = [];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanDelete))]
        [NotifyPropertyChangedFor(nameof(CanApply))]
        [NotifyPropertyChangedFor(nameof(CanEditPreset))]
        [NotifyPropertyChangedFor(nameof(CanToggleClusterMode))]
        private PresetListItemViewModel? selectedPresetItem;

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

        partial void OnSelectedPresetItemChanged(PresetListItemViewModel? oldValue, PresetListItemViewModel? newValue)
        {
            if (oldValue != null && !ReferenceEquals(oldValue, newValue))
            {
                oldValue.IsEditing = false;
            }

            if (newValue != null)
            {
                EditorIsClustered = newValue.Preset.IsClustered;
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
            ServerPresetModel newPreset = new()
            {
                Name = presetName,
                GameMode = _mainVm.GetCurrentGameMode(),
                IsClustered = false,
                BlockedServerKeys = [],
            };

            await _jsonSetting.AddOrUpdatePresetAsync(ClonePreset(newPreset));

            ReloadPresets(presetName);
        }

        public async Task<bool> DeletePresetsAsync(IReadOnlyList<ServerPresetModel> presetsToDelete)
        {
            if (presetsToDelete == null || presetsToDelete.Count == 0 || IsDeletingPreset)
            {
                return false;
            }

            List<ServerPresetModel> normalizedPresets = presetsToDelete
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

                List<ServerPresetModel> currentPresets = GetCurrentGamePresets();
                HashSet<string> deletedPresetNames = normalizedPresets
                    .Select(preset => preset.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                string? preferredPresetName = currentPresets
                    .FirstOrDefault(preset => !deletedPresetNames.Contains(preset.Name))
                    ?.Name;

                foreach (ServerPresetModel preset in normalizedPresets)
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

            return await _mainVm.ApplyPresetAsync(ClonePreset(SelectedPresetItem.Preset));
        }

        public async Task<bool> RenamePresetAsync(PresetListItemViewModel presetItem, string originalPresetName)
        {
            if (presetItem == null)
            {
                return false;
            }

            string newPresetName = (presetItem.Name ?? string.Empty).Trim();
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

            ServerPresetModel? existingPreset = _mainVm.GetCurrentGamePreset(newPresetName);
            bool overwritingDifferentPreset = existingPreset != null &&
                !existingPreset.Name.Equals(currentPresetName, StringComparison.OrdinalIgnoreCase);

            if (overwritingDifferentPreset)
            {
                bool shouldOverwrite = await _messageBoxService.ShowMessageBoxConfirmationAsync(
                        _localizationService.GetLocaleValue("MessageBoxInfoTitle"),
                        string.Format(
                        _localizationService.GetLocaleValue("PresetOverwriteConfirmDialogue"),
                        newPresetName
                        ),
                    Icon.Setting
                    );

                if (!shouldOverwrite)
                {
                    ReloadPresets(currentPresetName);
                    return false;
                }
            }

            ServerPresetModel renamedPreset = ClonePreset(presetItem.Preset);
            renamedPreset.Name = newPresetName;

            await _jsonSetting.AddOrUpdatePresetAsync(renamedPreset);

            await _jsonSetting.RemovePresetAsync(_mainVm.GetCurrentGameMode(), currentPresetName);
            await SyncPresetReferenceAfterRenameAsync(currentPresetName, newPresetName, overwritingDifferentPreset);

            ReloadPresets(newPresetName);

            return true;
        }

        public async Task PersistSelectedPresetServerKeysAsync()
        {
            if (SelectedPresetItem == null)
            {
                return;
            }

            SelectedPresetItem.Preset.BlockedServerKeys = ServerItems
                .Where(serverItem => serverItem.IsBlocked)
                .Select(serverItem => serverItem.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(serverKey => serverKey, StringComparer.OrdinalIgnoreCase)
                .ToList();

            await _jsonSetting.AddOrUpdatePresetAsync(ClonePreset(SelectedPresetItem.Preset));
            await ClearAppliedPresetReferenceIfNeededAsync(SelectedPresetItem.Preset.Name);
        }

        public async Task ToggleSelectedPresetClusterModeAsync()
        {
            if (SelectedPresetItem == null)
            {
                return;
            }

            bool hasBlockedEntries = (SelectedPresetItem.Preset.BlockedServerKeys?.Count ?? 0) > 0;

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

            SelectedPresetItem.Preset.IsClustered = !SelectedPresetItem.Preset.IsClustered;
            SelectedPresetItem.Preset.BlockedServerKeys = [];
            EditorIsClustered = SelectedPresetItem.Preset.IsClustered;

            await _jsonSetting.AddOrUpdatePresetAsync(ClonePreset(SelectedPresetItem.Preset));
            await ClearAppliedPresetReferenceIfNeededAsync(SelectedPresetItem.Preset.Name);

            LoadServerItemsForSelectedPreset();
        }

        // Use NaturalStringComparer to sort preset names.
        // This is to avoid issues with numbers in preset names being treated as part of the number sequence.
        // For example, "Preset 1" should come before "Preset 10" in ascending order, not after.
        public void SortPresets(ListSortDirection direction)
        {
            string? selectedPresetName = SelectedPresetItem?.Name;
            List<PresetListItemViewModel> sortedPresets = (direction == ListSortDirection.Ascending
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
            List<PresetServerSelectionItem> sortedItems = sortKey switch
            {
                "Blocked" => (direction == ListSortDirection.Ascending
                    ? ServerItems.OrderBy(serverItem => serverItem.IsBlocked)
                    : ServerItems.OrderByDescending(serverItem => serverItem.IsBlocked)).ToList(),
                "Flag" => (direction == ListSortDirection.Ascending
                    ? ServerItems.OrderBy(serverItem => serverItem.FlagSortKey, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(serverItem => serverItem.Description, StringComparer.OrdinalIgnoreCase)
                    : ServerItems.OrderByDescending(serverItem => serverItem.FlagSortKey, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(serverItem => serverItem.Description, StringComparer.OrdinalIgnoreCase)).ToList(),
                "ServerId" => (direction == ListSortDirection.Ascending
                    ? ServerItems.OrderBy(serverItem => serverItem.Name, StringComparer.OrdinalIgnoreCase)
                    : ServerItems.OrderByDescending(serverItem => serverItem.Name, StringComparer.OrdinalIgnoreCase)).ToList(),
                _ => (direction == ListSortDirection.Ascending
                    ? ServerItems.OrderBy(serverItem => serverItem.Description, StringComparer.OrdinalIgnoreCase)
                    : ServerItems.OrderByDescending(serverItem => serverItem.Description, StringComparer.OrdinalIgnoreCase)).ToList(),
            };

            ServerItems.Clear();
            ServerItems.AddRange(sortedItems);
        }

        private void ReloadPresets(string? preferredPresetName = null)
        {
            string? presetNameToSelect = preferredPresetName ?? SelectedPresetItem?.Name;
            List<ServerPresetModel> currentPresets = GetCurrentGamePresets();

            if (currentPresets.Count == 0)
            {
                Presets = [];
                SelectedPresetItem = null;
                ServerItems.Clear();
                return;
            }

            Presets = new ObservableCollectionExtended<PresetListItemViewModel>(
                currentPresets.Select(preset => new PresetListItemViewModel(ClonePreset(preset))).ToList()
                );

            SelectedPresetItem = !string.IsNullOrWhiteSpace(presetNameToSelect)
                ? Presets.FirstOrDefault(preset =>
                    preset.Name.Equals(presetNameToSelect, StringComparison.OrdinalIgnoreCase))
                : null;

            SelectedPresetItem ??= Presets[0];
        }

        private void LoadServerItemsForSelectedPreset()
        {
            ServerItems.Clear();

            if (SelectedPresetItem == null)
            {
                return;
            }

            HashSet<string> blockedServerKeys = (SelectedPresetItem.Preset.BlockedServerKeys ?? [])
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (ServerModel serverModel in _mainVm.GetCurrentGameServerModels(SelectedPresetItem.Preset.IsClustered))
            {
                string serverKey = _mainVm.GetServerKey(serverModel, SelectedPresetItem.Preset.IsClustered);

                ServerItems.Add(new PresetServerSelectionItem(serverModel, serverKey, blockedServerKeys.Contains(serverKey)));
            }
        }

        public void StopEditingPresets()
        {
            foreach (PresetListItemViewModel preset in Presets)
            {
                preset.IsEditing = false;
            }
        }

        private List<ServerPresetModel> GetCurrentGamePresets()
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
                bool targetWasAppliedOrRemembered =
                    (_mainVm.SelectedPreset?.Name ?? string.Empty).Equals(renamedPresetName, StringComparison.OrdinalIgnoreCase) ||
                    (_jsonSetting.GetLastSelectedPresetNameByGameMode() ?? string.Empty).Equals(renamedPresetName, StringComparison.OrdinalIgnoreCase);

                if (targetWasAppliedOrRemembered)
                {
                    await ClearAppliedPresetReferenceIfNeededAsync(renamedPresetName);
                }

                if ((_mainVm.SelectedPreset?.Name ?? string.Empty).Equals(originalPresetName, StringComparison.OrdinalIgnoreCase) ||
                    (_jsonSetting.GetLastSelectedPresetNameByGameMode() ?? string.Empty).Equals(originalPresetName, StringComparison.OrdinalIgnoreCase))
                {
                    await ClearAppliedPresetReferenceIfNeededAsync(originalPresetName);
                }

                return;
            }

            bool renamedAppliedPreset = (_mainVm.SelectedPreset?.Name ?? string.Empty)
                .Equals(originalPresetName, StringComparison.OrdinalIgnoreCase);
            bool renamedLastSelected = (_jsonSetting.GetLastSelectedPresetNameByGameMode() ?? string.Empty)
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

        private static ServerPresetModel ClonePreset(ServerPresetModel preset)
        {
            return new ServerPresetModel
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

    public partial class PresetListItemViewModel : ObservableObject
    {
        public ServerPresetModel Preset { get; }

        public string Name
        {
            get => Preset.Name;
            set
            {
                if (Preset.Name == value)
                {
                    return;
                }

                Preset.Name = value;
                OnPropertyChanged();
            }
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDisplayVisible))]
        [NotifyPropertyChangedFor(nameof(IsEditorVisible))]
        private bool isEditing;

        public bool IsDisplayVisible => !IsEditing;

        public bool IsEditorVisible => IsEditing;

        public PresetListItemViewModel(ServerPresetModel preset)
        {
            Preset = preset;
        }
    }

    public partial class PresetServerSelectionItem : ObservableObject
    {
        private static readonly Dictionary<string, string> FlagSortKeyCache = new(StringComparer.OrdinalIgnoreCase);

        public ServerModel ServerModel { get; }

        public string Key { get; }

        public string Flag => ServerModel.Flag;

        public string FlagSortKey { get; }

        public string Name => ServerModel.Name;

        public string Description => ServerModel.Description;

        [ObservableProperty]
        private bool isBlocked;

        public PresetServerSelectionItem(ServerModel serverModel, string key, bool isBlocked)
        {
            ServerModel = serverModel;
            Key = key;
            IsBlocked = isBlocked;
            FlagSortKey = GetFlagSortKey(serverModel.Flag);
        }

        private static string GetFlagSortKey(string flagPath)
        {
            if (string.IsNullOrWhiteSpace(flagPath))
            {
                return string.Empty;
            }

            if (FlagSortKeyCache.TryGetValue(flagPath, out string? cachedValue))
            {
                return cachedValue;
            }

            string assetUri = $"avares://ServerPickerX{flagPath}";

            try
            {
                using var stream = AssetLoader.Open(new Uri(assetUri));
                byte[] hash = SHA256.HashData(stream);
                string flagSortKey = Convert.ToHexString(hash);
                FlagSortKeyCache[flagPath] = flagSortKey;

                return flagSortKey;
            }
            catch
            {
                FlagSortKeyCache[flagPath] = flagPath;

                return flagPath;
            }
        }
    }
}
