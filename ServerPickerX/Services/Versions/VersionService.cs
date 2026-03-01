using ServerPickerX.Settings;
using ServerPickerX.Helpers;
using ServerPickerX.Views;
using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ServerPickerX.Services.MessageBoxes;
using ServerPickerX.Services.Loggers;

namespace ServerPickerX.Services.Versions
{
    public class VersionService : IVersionService
    {
        private readonly ILoggerService _logger;
        private readonly IMessageBoxService _messageBoxService;
        private readonly HttpClient _httpClient;
        private readonly JsonSetting _jsonSettings;

        public VersionService(
            ILoggerService logger,
            IMessageBoxService messageBoxService,
            HttpClient httpClient,
            JsonSetting jsonSettings
            )
        {
            _logger = logger;
            _messageBoxService = messageBoxService;
            _httpClient = httpClient;
            _jsonSettings = jsonSettings;
        }

        public async Task CheckVersionAsync()
        {
            if (MainWindow.IsDebugBuild || !_jsonSettings.version_check_on_startup)
            {
                return;
            }

            _httpClient.DefaultRequestHeaders.Add("User-Agent", "server-picker-x");

            try
            {
                var res = await _httpClient.GetStreamAsync("https://api.github.com/repositories/1141835010/releases");

                if (res == null)
                {
                    throw new Exception(
                        "Failed to check for newer app version!" + Environment.NewLine + Environment.NewLine +
                        "- Verify your internet connection or firewall are working and enabled" + Environment.NewLine +
                        "- Make sure to run the app as admin or with sudo level execution"
                    );
                }

                var jsonArray = (JsonArray?)await JsonArray.ParseAsync(res);

                if (jsonArray?[0]?["tag_name"] == null)
                {
                    return;
                }

                string assemblyVersion = Assembly.GetEntryAssembly()!.GetName()!.Version!.ToString(3);

                // version is up to date
                if (assemblyVersion == jsonArray[0]!["tag_name"]!.ToString().Split("v")[1])
                {
                    return;
                }

                // prompt user to visit gh releases page for newer version
                await _messageBoxService.ShowMessageBoxWithLinkAsync(
                        "Version Check",
                        "New version available! Go to releases?",
                        "https://github.com/FN-FAL113/server-picker-x/releases"
                    );
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync("Failed to check version", ex.Message);

                await _messageBoxService.ShowMessageBoxAsync("Error", ex.Message);
            }
        }
    }
}