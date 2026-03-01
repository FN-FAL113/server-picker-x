
using Microsoft.Extensions.DependencyInjection;
using ServerPickerX.Helpers;
using ServerPickerX.Services.Loggers;
using ServerPickerX.Services.MessageBoxes;
using ServerPickerX.Services.Settings;
using System;
using System.Diagnostics.CodeAnalysis;
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

    public class JsonSetting : ISetting
    {
        // Properties are virtual for unit test mocking 
        public virtual string warning { get; private set; } = "Do not modify settings here! only do it from the app!";

        public virtual string game_mode { set; get; } = "Counter Strike 2";

        public virtual string language { set; get; } = "English | en-us";

        public virtual string deadlock_server_revision { get; set; } = "-1";

        public virtual string cs2_server_revision { get; set; } = "-1";

        public virtual bool is_clustered { get; set; } = false;

        public virtual bool version_check_on_startup { get; set; } = true;

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

        public JsonSetting()
        {
            _messageBoxService = App.ServiceProvider.GetRequiredService<IMessageBoxService>();
            _logger = App.ServiceProvider.GetRequiredService<ILoggerService>();
        }

        public JsonSetting(
            IMessageBoxService messageBoxService,
            ILoggerService logger
            )
        {
            _messageBoxService = messageBoxService;
            _logger = logger;
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
                is_clustered = localSettings.is_clustered;
                version_check_on_startup = localSettings.version_check_on_startup;
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync("An error has occured while loading json settings", ex.Message);

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
                await _logger.LogErrorAsync("An error has occured while saving json settings", ex.Message);

                await _messageBoxService.ShowMessageBoxAsync("Error", "An error has occured while saving json settings");

                return false;
            }
        }
    }
}
