using ServerPickerX.Models;
using ServerPickerX.Services.Loggers;
using ServerPickerX.Services.MessageBoxes;
using ServerPickerX.Services.Processes;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace ServerPickerX.Services.SystemFirewalls
{
    public class LinuxFirewallService(
        ILoggerService _loggerService,
        IMessageBoxService _messageBoxService,
        IProcessService _processService
        ) : ISystemFirewallService
    {
        public async Task BlockServersAsync(ObservableCollection<ServerModel> serverModels)
        {
            foreach (var serverModel in serverModels)
            {
                string ipAddresses = string.Join(",", serverModel.RelayModels.Select(s => s.IPv4).ToList());

                using var process = _processService.CreateProcess("sudo");

                try
                {
                    process.StartInfo.Arguments = "iptables " +
                        "-A INPUT -s " + ipAddresses + " -j DROP";

                    process.Start();
                    await process.WaitForExitAsync();

                    string stdOut = process.StandardOutput.ReadToEnd();
                    string stdErr = process.StandardError.ReadToEnd();

                    if ((process.ExitCode == 1 || process.ExitCode < 0) &&
                        !($"{stdOut} {stdErr}".Contains("Bad rule (does a matching")))
                    {
                        throw new Exception("StdOut: " + stdOut + Environment.NewLine + "StdErr: " + stdErr);
                    }
                }
                catch (Exception ex)
                {
                    await _loggerService.LogErrorAsync($"Failed to block server {serverModel.Name}", ex.Message);

                    throw;
                }
            }
        }

        public async Task UnblockServersAsync(ObservableCollection<ServerModel> serverModels)
        {
            foreach (var serverModel in serverModels)
            {
                string ipAddresses = string.Join(",", serverModel.RelayModels.Select(s => s.IPv4).ToList());

                using var process = _processService.CreateProcess("sudo");

                try
                {
                    process.StartInfo.Arguments = "iptables " +
                       "-D INPUT -s " + ipAddresses + " -j DROP";

                    process.Start();
                    await process.WaitForExitAsync();

                    string stdOut = process.StandardOutput.ReadToEnd();
                    string stdErr = process.StandardError.ReadToEnd();

                    if ((process.ExitCode == 1 || process.ExitCode < 0) &&
                        !($"{stdOut} {stdErr}".Contains("Bad rule (does a matching")))
                    {
                        throw new Exception("StdOut: " + stdOut + Environment.NewLine + "StdErr: " + stdErr);
                    }
                }
                catch (Exception ex)
                {
                    await _loggerService.LogErrorAsync($"Failed to unblock server {serverModel.Name}", ex.Message);

                    throw;
                }
            }
        }

        public async Task ResetFirewallAsync()
        {
            var result = await _messageBoxService.ShowMessageBoxConfirmationAsync(
                "Warning",
                "This will attempt to reset firewall to its default state. Confirm action?",
                MsBox.Avalonia.Enums.Icon.Warning
                );

            if (!result)
            {
                return;
            }

            using Process process = _processService.CreateProcess("sudo");

            try
            {
                process.StartInfo.Arguments = $"iptables -F";

                process.Start();
                process.WaitForExit();

                if (process.ExitCode == 1 || process.ExitCode < 0)
                {
                    throw new Exception("StdOut: " + process.StandardOutput.ReadToEnd() +
                        Environment.NewLine + "StdErr: " + process.StandardError.ReadToEnd());
                }

                await _messageBoxService.ShowMessageBoxAsync(
                    "Info",
                    "Successfully reset iptables!",
                    MsBox.Avalonia.Enums.Icon.Success
                    );
            }
            catch (Exception ex)
            {
                await _loggerService.LogErrorAsync("An error has occured while resetting iptables.", ex.Message);

                await _messageBoxService.ShowMessageBoxAsync(
                    "Error",
                    "An error has occured while resetting firewall! Please upload log file to github."
                    );
            }
        }
    }
}