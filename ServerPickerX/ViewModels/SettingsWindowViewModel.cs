using CommunityToolkit.Mvvm.Input;
using ServerPickerX.Services.DependencyInjection;
using ServerPickerX.Services.Loggers;
using ServerPickerX.Services.MessageBoxes;
using ServerPickerX.Services.Servers;
using ServerPickerX.Services.SystemFirewalls;
using ServerPickerX.Settings;
using System;
using System.Threading.Tasks;

namespace ServerPickerX.ViewModels
{
    public partial class SettingsWindowViewModel : ViewModelBase
    {
        public bool VersionCheckOnStartup { get; set; }

        private readonly ILoggerService _loggerService;
        private readonly IMessageBoxService _messageBoxService;
        private readonly ISystemFirewallService _systemFirewallService;
        private readonly JsonSetting _jsonSetting;

        // Parameterless constructor, allows design previewer to instantiate this class since it doesn't support DI
        public SettingsWindowViewModel()
        {
            _loggerService = ServiceLocator.GetRequiredService<ILoggerService>();
            _messageBoxService = ServiceLocator.GetRequiredService<IMessageBoxService>();
            _systemFirewallService = ServiceLocator.GetRequiredService<ISystemFirewallService>();
            _jsonSetting = ServiceLocator.GetRequiredService<JsonSetting>();

            VersionCheckOnStartup = _jsonSetting.version_check_on_startup;
        }

        // DI constructor, allows inversion of control and unit tests mocking
        public SettingsWindowViewModel(
            ILoggerService loggerService,
            IMessageBoxService messageBoxService,
            ISystemFirewallService systemFirewallService,
            JsonSetting jsonSetting
            )
        {
            _loggerService = loggerService;
            _messageBoxService = messageBoxService;
            _systemFirewallService = systemFirewallService;
            _jsonSetting = jsonSetting;

            VersionCheckOnStartup = _jsonSetting.version_check_on_startup;
        }

        [RelayCommand]
        public async Task VersionCheckerToggleCommand()
        {
            _jsonSetting.version_check_on_startup = VersionCheckOnStartup;

            await _jsonSetting.SaveSettingsAsync();
        }

        public async Task ResetFirewallCommand()
        {
            try
            {
                await _systemFirewallService.ResetFirewallAsync();
            }
            catch (Exception ex)
            {
                await _loggerService.LogErrorAsync("An error has occurred while resetting firewall.", ex.Message);

                await _messageBoxService.ShowMessageBoxAsync(
                    "Error",
                    "Oops! Something went wrong. Please upload the log file to GitHub."
                    );

            }
        }
    }
}
