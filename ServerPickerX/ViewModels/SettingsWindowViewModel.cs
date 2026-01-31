using CommunityToolkit.Mvvm.Input;
using ServerPickerX.ConfigSections;
using ServerPickerX.Views;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ServerPickerX.ViewModels
{
    public partial class SettingsWindowViewModel: ViewModelBase
    {
        public bool VersionCheckOnStartup { get; set; }

        public SettingsWindowViewModel() { 
            VersionCheckOnStartup = MainWindow.jsonSettings.version_check_on_startup;
        }

        [RelayCommand]
        public async Task VersionCheckerCommand()
        {
            JsonSetting jsonSetting = MainWindow.jsonSettings;

            jsonSetting.version_check_on_startup = VersionCheckOnStartup;

            await jsonSetting.SaveSettings();
        }
    }
}
