
using ServerPickerX.Helpers;
using ServerPickerX.Settings;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ServerPickerX.ConfigSections
{
    public class JsonSetting : Setting
    {
        public string warning { get; private set; } = "Do not modify server revision! It will unblock your servers on launch";

        public string server_revision { get; set; } = "-1";

        public bool version_check_on_startup { get; set; } = true;

        [JsonIgnore]
        public readonly string jsonFilePath = "./settings.json";

        public override async Task<Setting> LoadSettings()
        {
            // publishing this app with trimmed assemblies for reduced app size limits the
            // functionality of serialization since it requires reflection to determine
            // dynamic types on runtime which is not possible with trimmed applications
            // that pre-determines static types of the app during compilation
            try
            {
                // create local json settings if not exist with serialized object properties
                if (!File.Exists(jsonFilePath))
                {
                    using FileStream newSettingsFile = File.Create(jsonFilePath);

                    await JsonSerializer.SerializeAsync(newSettingsFile, this);

                    return this;
                }

                using FileStream settingsFile = File.OpenRead(jsonFilePath);

                JsonSetting localSettings = await JsonSerializer.DeserializeAsync<JsonSetting>(settingsFile) ?? this;

                server_revision = localSettings.server_revision;
                version_check_on_startup = localSettings.version_check_on_startup;
            }
            catch (Exception ex) {
                await MessageBoxHelper.ShowMessageBox("Error", "An error has occured while loading json settings");

                await LogHelper.LogErrorToFile(ex.Message, "An error has occured while loading json settings");
            }

            return this;
        }

        public override async Task<bool> SaveSettings()
        {
            try
            {
                // open existing local json settings and deserialize it back to its complex form
                using FileStream file = File.OpenWrite(jsonFilePath);

                await JsonSerializer.SerializeAsync(file, this);

                return true;
            }
            catch (Exception ex) {
                await MessageBoxHelper.ShowMessageBox("Error", "An error has occured while saving json settings");

                await LogHelper.LogErrorToFile(ex.Message, "An error has occured while saving json settings");

                return false;
            }
        }
    }
}
