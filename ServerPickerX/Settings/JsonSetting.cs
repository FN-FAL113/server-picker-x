
using ServerPickerX.Helpers;
using ServerPickerX.Services.Loggers;
using ServerPickerX.Services.MessageBoxes;
using ServerPickerX.Settings;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ServerPickerX.Settings
{
    // publishing an app with trimmed assemblies or using Ahead-of-Time compilation for 
    // reduced build size can limit the functionality of serialization since it requires reflection 
    // to determine dynamic types on runtime which is not possible with trimmed or AOT applications.
    // JsonSerializerContext preserves the types and provides serialization metadata on compile-time.
    [JsonSerializable(typeof(JsonSetting))]
    internal partial class SourceGenerationContext : JsonSerializerContext { }

    public class JsonSetting : Setting
    {
        public string warning { get; private set; } = "Do not modify settings here! only do it from the app!";

        public string game_mode { set; get; } = "Counter Strike 2";

        public string deadlock_server_revision { get; set; } = "-1";

        public string cs2_server_revision { get; set; } = "-1";

        public bool is_clustered { get; set; } = false;

        public bool version_check_on_startup { get; set; } = true;

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
        private ILoggerService _logger { get; set; }

        public JsonSetting() { }

        public JsonSetting(
            IMessageBoxService messageBoxService,
            ILoggerService logger
            ) 
        {
            _messageBoxService = messageBoxService;
            _logger = logger;
        }

        public override async Task<Setting> LoadSettingsAsync()
        {
            try
            {
                // create local json settings if not exists with serialized object properties
                if (!File.Exists(jsonFilePath))
                {
                    using FileStream newSettingsFile = File.Create(jsonFilePath);

                    await JsonSerializer.SerializeAsync(newSettingsFile, this);

                    return this;
                }

                using FileStream settingsFile = File.OpenRead(jsonFilePath);

                JsonSetting localSettings = await JsonSerializer.DeserializeAsync<JsonSetting>(settingsFile, serializerOptions) ?? this;

                game_mode = localSettings.game_mode;
                cs2_server_revision = localSettings.cs2_server_revision;
                deadlock_server_revision = localSettings.deadlock_server_revision;
                is_clustered = localSettings.is_clustered;
                version_check_on_startup = localSettings.version_check_on_startup;
            }
            catch (Exception ex) {
                _logger.LogError("An error has occured while loading json settings", ex.Message);

                await _messageBoxService.ShowMessageBoxAsync("Error", "An error has occured while loading json settings");
            }

            return this;
        }

        public override async Task<bool> SaveSettingsAsync()
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
            catch (Exception ex) {
                _logger.LogError("An error has occured while saving json settings", ex.Message);

                await _messageBoxService.ShowMessageBoxAsync("Error", "An error has occured while saving json settings");

                return false;
            }
        }
    }
}
