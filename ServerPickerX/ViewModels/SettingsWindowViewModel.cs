using CommunityToolkit.Mvvm.Input;
using ServerPickerX.ConfigSections;
using ServerPickerX.Helpers;
using ServerPickerX.Models;
using ServerPickerX.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
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

        [RelayCommand]
        public async Task ResetFirewallCommand()
        {
            var result = await MessageBoxHelper.ShowMessageBoxConfirmation(
                    "Warning",
                    "This will attempt to reset firewall to its default state. Confirm action?",
                    MsBox.Avalonia.Enums.Icon.Warning
                );

            if (!result)
            {
                return;
            }

            Process process = ProcessHelper.CreateProcess();

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    process.StartInfo.FileName = "cmd.exe";

                    process.StartInfo.Arguments = $"/c {Path.Combine(Environment.SystemDirectory, "netsh.exe")} advfirewall reset";
                }
                else if (OperatingSystem.IsLinux())
                {
                    process.StartInfo.FileName = "sudo";

                    process.StartInfo.Arguments = $"iptables -F";
                }

                process.Start();
                process.WaitForExit();

                if (process.ExitCode == 1 || process.ExitCode < 0)
                {
                    throw new Exception("StdOut: " + process.StandardOutput.ReadToEnd() + 
                        Environment.NewLine + "StdErr: " + process.StandardError.ReadToEnd());
                }
            }
            catch (Exception ex) {
                await MessageBoxHelper.ShowMessageBox(
                        "Error",
                        "An error has occured while resetting firewall! Please upload generated error file to github."
                    );

                await LogHelper.LogErrorToFile(ex.Message, "An error has occured while resetting firewall.");
            }
        }
    }
}
