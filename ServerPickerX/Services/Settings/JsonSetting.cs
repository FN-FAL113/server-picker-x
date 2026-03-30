using ServerPickerX.Constants;
using ServerPickerX.Helpers;
using ServerPickerX.Models;
using ServerPickerX.Services.DependencyInjection;
using ServerPickerX.Services.Loggers;
using ServerPickerX.Services.MessageBoxes;
using ServerPickerX.Services.Settings;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ServerPickerX.Settings
{
    // Publishing an app with trimmed assemblies or using AOT compilation for reduced
    // build size can limit the serialization functionality since it requires reflection 
    // to determine dynamic types on runtime which is not possible with trimmed or AOT apps.
    // JsonSerializerContext preserves the types and provides serialization metadata on compile-time.
    [JsonSerializable(typeof(JsonSetting))]
    internal partial class SourceGenerationContext : JsonSerializerContext { }

    public class JsonSetting : ISetting
    {
        // Properties are virtual for unit test mocking 
        public virtual string warning { get; private set; } = "Do not modify settings here! only do it from the app!";

        public virtual string game_mode { set; get; } = "Counter Strike 2";

        public virtual string language { set; get; } = "English | en-us";

        public virtual string cs2_server_revision { get; set; } = "-1";

        public virtual string deadlock_server_revision { get; set; } = "-1";

        public virtual string marathon_server_revision { get; set; } = "-1";

        public virtual bool is_clustered { get; set; } = false;

        public virtual bool version_check_on_startup { get; set; } = true;

        public virtual List<ServerPresetModel> server_presets { get; set; } = [];

        public virtual Dictionary<string, string> last_selected_preset_names { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        [JsonIgnore]
        public readonly string jsonFilePath = "./settings.json";

        [JsonIgnore]
        public readonly JsonSerializerOptions serializerOptions = new()
        {
            TypeInfoResolver = SourceGenerationContext.Default,
            WriteIndented = true,
            IncludeFields = true,
        };

        [JsonIgnore]
        private IMessageBoxService _messageBoxService { get; set; }
        [JsonIgnore]
        private ILoggerService _loggerService { get; set; }

        public JsonSetting() {}

        public JsonSetting(
            IMessageBoxService messageBoxService,
            ILoggerService logger
            )
        {
            _messageBoxService = messageBoxService;
            _loggerService = logger;
        }

        #pragma warning disable IL2026
        // Reflection is partially used here and might not be trim-compatible
        // unless JsonSerializerIsReflectionEnabledByDefault is set to true in .csproj
        public async Task LoadSettingsAsync()
        {
            try
            {
                // create local json settings if not exists with serialized object properties
                if (!File.Exists(jsonFilePath))
                {
                    using FileStream newSettingsFile = File.Create(jsonFilePath);

                    await JsonSerializer.SerializeAsync(newSettingsFile, this);

                    return;
                }

                using FileStream settingsFile = File.OpenRead(jsonFilePath);

                JsonSetting localSettings = await JsonSerializer.DeserializeAsync<JsonSetting>(settingsFile, serializerOptions) ?? this;

                game_mode = localSettings.game_mode;
                language = localSettings.language;
                cs2_server_revision = localSettings.cs2_server_revision;
                deadlock_server_revision = localSettings.deadlock_server_revision;
                marathon_server_revision = localSettings.marathon_server_revision;
                is_clustered = localSettings.is_clustered;
                version_check_on_startup = localSettings.version_check_on_startup;
                server_presets = localSettings.server_presets ?? [];
                last_selected_preset_names = localSettings.last_selected_preset_names != null
                    ? new Dictionary<string, string>(localSettings.last_selected_preset_names, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                await _loggerService.LogErrorAsync("An error has occured while loading json settings", ex.Message);

                await _messageBoxService.ShowMessageBoxAsync("Error", "An error has occured while loading json settings");
            }
        }

        // Reflection is partially used here and might not be trim-compatible
        // unless JsonSerializerIsReflectionEnabledByDefault is set to true in .csproj
        public async Task<bool> SaveSettingsAsync()
        {
            try
            {
                // an extra curly brace is being added when serializing,
                // remove the contents first then serialize data to file
                await File.WriteAllTextAsync(jsonFilePath, String.Empty);

                // open existing local json settings and deserialize it back to its complex form
                using FileStream file = File.OpenWrite(jsonFilePath);

                await JsonSerializer.SerializeAsync(file, this, serializerOptions);

                return true;
            }
            catch (Exception ex)
            {
                await _loggerService.LogErrorAsync("An error has occured while saving json settings", ex.Message);

                await _messageBoxService.ShowMessageBoxAsync("Error", "An error has occured while saving json settings");

                return false;
            }
        }

        public async Task<string> GetRevisionByGameModeAsync()
        {
            try
            {
                return this.game_mode switch
                {
                    GameModes.CounterStrike2 or GameModes.CounterStrike2PerfectWorld => this.cs2_server_revision,
                    GameModes.Deadlock => this.deadlock_server_revision,
                    GameModes.Marathon => this.marathon_server_revision,
                    _ => throw new NotSupportedException($"Unsupported game mode: {this.game_mode}"),
                };
            } catch (NotSupportedException ex) {
                await _loggerService.LogErrorAsync("An error has occured while getting server revision by current game mode", ex.Message);

                throw;
            }
        }

        public async Task SetRevisionByGameModeAsync(string revision)
        {
            try
            {
                switch (this.game_mode)
                {
                    case GameModes.CounterStrike2 or GameModes.CounterStrike2PerfectWorld:
                        this.cs2_server_revision = revision;
                        break;
                    case GameModes.Deadlock:
                        this.deadlock_server_revision = revision;
                        break;
                    case GameModes.Marathon:
                        this.marathon_server_revision = revision;
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported game mode: {this.game_mode}");
                };

                await this.SaveSettingsAsync();
            }
            catch (NotSupportedException ex)
            {
                await _loggerService.LogErrorAsync("An error has occured while setting server revision by current game mode", ex.Message);

                throw;
            }
        }

        public async Task SetGameModeAsync(string gameMode)
        {
            this.game_mode = gameMode;

            await this.SaveSettingsAsync();
        }

        public async Task SetLanguageAsync(string language)
        {
            this.language = language;

            await this.SaveSettingsAsync();
        }

        public string GetLastSelectedPresetNameByGameMode()
        {
            if (string.IsNullOrWhiteSpace(game_mode))
            {
                return string.Empty;
            }

            return last_selected_preset_names.TryGetValue(game_mode, out string? presetName)
                ? presetName
                : string.Empty;
        }

        public async Task SetLastSelectedPresetNameByGameModeAsync(string presetName)
        {
            if (string.IsNullOrWhiteSpace(game_mode))
            {
                return;
            }

            last_selected_preset_names[game_mode] = presetName;

            await SaveSettingsAsync();
        }

        public async Task ClearLastSelectedPresetNameByGameModeAsync()
        {
            if (string.IsNullOrWhiteSpace(game_mode))
            {
                return;
            }

            last_selected_preset_names.Remove(game_mode);

            await SaveSettingsAsync();
        }

        public List<ServerPresetModel> GetPresetsByGameMode(string gameMode)
        {
            string normalizedGameMode = gameMode ?? string.Empty;

            return (server_presets ?? [])
                .Where(preset => (preset.GameMode ?? string.Empty).Equals(normalizedGameMode, StringComparison.OrdinalIgnoreCase))
                .OrderBy(preset => preset.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public ServerPresetModel? GetPresetByGameMode(string gameMode, string presetName)
        {
            string normalizedGameMode = gameMode ?? string.Empty;
            string normalizedPresetName = presetName ?? string.Empty;

            return (server_presets ?? []).FirstOrDefault(preset =>
                (preset.GameMode ?? string.Empty).Equals(normalizedGameMode, StringComparison.OrdinalIgnoreCase) &&
                (preset.Name ?? string.Empty).Equals(normalizedPresetName, StringComparison.OrdinalIgnoreCase));
        }

        public async Task AddOrUpdatePresetAsync(ServerPresetModel serverPreset)
        {
            server_presets ??= [];
            ServerPresetModel? existingPreset = GetPresetByGameMode(serverPreset.GameMode, serverPreset.Name);
            List<string> blockedServerKeys = serverPreset.BlockedServerKeys
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (existingPreset == null)
            {
                server_presets.Add(new ServerPresetModel
                {
                    Name = serverPreset.Name,
                    GameMode = serverPreset.GameMode,
                    IsClustered = serverPreset.IsClustered,
                    BlockedServerKeys = blockedServerKeys,
                });
            }
            else
            {
                existingPreset.IsClustered = serverPreset.IsClustered;
                existingPreset.BlockedServerKeys = blockedServerKeys;
            }

            await SaveSettingsAsync();
        }

        public async Task RemovePresetAsync(string gameMode, string presetName)
        {
            server_presets ??= [];
            server_presets.RemoveAll(preset =>
                preset.GameMode.Equals(gameMode, StringComparison.OrdinalIgnoreCase) &&
                preset.Name.Equals(presetName, StringComparison.OrdinalIgnoreCase));

            await SaveSettingsAsync();
        }
    }
}
