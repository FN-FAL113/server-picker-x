using Avalonia.Logging;
using ServerPickerX.Models;
using ServerPickerX.Services.Localizations;
using ServerPickerX.Services.Loggers;
using ServerPickerX.Services.MessageBoxes;
using ServerPickerX.Services.Processes;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ServerPickerX.Services.SystemFirewalls
{
    public class WindowsFirewallService(
        ILoggerService _loggerService,
        ILocalizationService _localizationService,
        IMessageBoxService _messageBoxService,
        IProcessService _processService
        ) : ISystemFirewallService
    {
        public async Task BlockServersAsync(ObservableCollection<ServerModel> serverModels)
        {
            foreach (var serverModel in serverModels)
            {
                string ipAddresses = string.Join(",", serverModel.RelayModels.Select(s => s.IPv4).ToList());

                using var process = _processService.CreateProcess("cmd.exe");

                process.StartInfo.Arguments = $"/c {Path.Combine(Environment.SystemDirectory, "netsh.exe")} " +
                        "advfirewall firewall " +
                        "add rule " +
                        "name=server_picker_x_" + serverModel.Description.Replace(" ", "") +
                        " dir=out action=block protocol=ANY " + "remoteip=" + ipAddresses;

                try
                {
                    process.Start();
                    await process.WaitForExitAsync();

                    string stdOut = process.StandardOutput.ReadToEnd().Trim();
                    string stdErr = process.StandardError.ReadToEnd().Trim();

                    if (process.ExitCode > 0)
                    {
                        await _loggerService.LogWarningAsync("StdOut: " + stdOut + " StdErr: " + stdErr);
                    }
                }
                catch (Exception ex)
                {
                    // Perform debugging here if necessary (log error or through debugger breakpoints)
                    throw;
                }
            }
        }

        public async Task UnblockServersAsync(ObservableCollection<ServerModel> serverModels)
        {
            foreach (var serverModel in serverModels)
            {
                string ipAddresses = string.Join(",", serverModel.RelayModels.Select(s => s.IPv4).ToList());

                using var process = _processService.CreateProcess("cmd.exe");

                process.StartInfo.Arguments = $"/c {Path.Combine(Environment.SystemDirectory, "netsh.exe")} " +
                        "advfirewall firewall " +
                        "delete rule " +
                        "name=server_picker_x_" + serverModel.Description.Replace(" ", "");

                try
                {
                    process.Start();
                    await process.WaitForExitAsync();

                    string stdOut = process.StandardOutput.ReadToEnd().Trim();
                    string stdErr = process.StandardError.ReadToEnd().Trim();

                    if (process.ExitCode > 0)
                    {
                        await _loggerService.LogWarningAsync("StdOut: " + stdOut + " StdErr: " + stdErr);
                    }
                }
                catch (Exception ex)
                {
                    // Perform debugging here if necessary (log error or through debugger breakpoints)
                    throw;
                }
            }
        }

        public async Task ResetFirewallAsync()
        {
            var result = await _messageBoxService.ShowMessageBoxConfirmationAsync(
                    _localizationService.GetLocaleValue("MessageBoxInfoTitle"),
                    _localizationService.GetLocaleValue("FirewallResetConfirmDialogue"),
                    MsBox.Avalonia.Enums.Icon.Warning
                );

            if (!result)
            {
                return;
            }

            using Process process = _processService.CreateProcess("cmd.exe");

            try
            {
                process.StartInfo.Arguments = $"/c {Path.Combine(Environment.SystemDirectory, "netsh.exe")} advfirewall reset";

                process.Start();
                process.WaitForExit();

                string stdOut = process.StandardOutput.ReadToEnd().Trim();
                string stdErr = process.StandardError.ReadToEnd().Trim();

                if (process.ExitCode > 0)
                {
                    await _loggerService.LogWarningAsync("StdOut: " + stdOut + " StdErr: " + stdErr);
                }

                await _messageBoxService.ShowMessageBoxAsync(
                    _localizationService.GetLocaleValue("MessageBoxInfoTitle"),
                    _localizationService.GetLocaleValue("FirewallResetSuccessDialogue"),
                    MsBox.Avalonia.Enums.Icon.Success
                    );
            }
            catch (Exception ex)
            {
                // Perform debugging here if necessary (log error or through debugger breakpoints)
                throw;
            }
        }
    }
}