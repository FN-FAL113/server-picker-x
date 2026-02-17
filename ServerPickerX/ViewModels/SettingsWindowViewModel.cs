using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using ServerPickerX.Services.SystemFirewalls;
using ServerPickerX.Settings;
using System.Threading.Tasks;

namespace ServerPickerX.ViewModels
{
    public partial class SettingsWindowViewModel: ViewModelBase
    {
        public bool VersionCheckOnStartup { get; set; }

        private readonly JsonSetting _jsonSetting;
        private readonly ISystemFirewallService _systemFirewallService;

        // Parameterless constructors for windows and viewmodels, access services instead through the container
        // DI through constructors doesn't work with design previewer since it has no clue on providing parameters
        public SettingsWindowViewModel()
        {
            _jsonSetting = App.ServiceProvider.GetRequiredService<JsonSetting>();
            _systemFirewallService = App.ServiceProvider.GetRequiredService<ISystemFirewallService>();

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
            await _systemFirewallService.ResetFirewallAsync();
        }
    }
}
