using Avalonia.Logging;
using ServerPickerX.Models;
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

                    string stdOut = process.StandardOutput.ReadToEnd();
                    string stdErr = process.StandardError.ReadToEnd();

                    if ((process.ExitCode == 1 || process.ExitCode < 0) &&
                        !($"{stdOut} {stdErr}".Contains("No rules match")))
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

                using var process = _processService.CreateProcess("cmd.exe");

                process.StartInfo.Arguments = $"/c {Path.Combine(Environment.SystemDirectory, "netsh.exe")} " +
                        "advfirewall firewall " +
                        "delete rule " +
                        "name=server_picker_x_" + serverModel.Description.Replace(" ", "");

                try
                {
                    process.Start();
                    await process.WaitForExitAsync();

                    string stdOut = process.StandardOutput.ReadToEnd();
                    string stdErr = process.StandardError.ReadToEnd();

                    if ((process.ExitCode == 1 || process.ExitCode < 0) &&
                        !($"{stdOut} {stdErr}".Contains("No rules match")))
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

            using Process process = _processService.CreateProcess("cmd.exe");

            try
            {
                process.StartInfo.Arguments = $"/c {Path.Combine(Environment.SystemDirectory, "netsh.exe")} advfirewall reset";

                process.Start();
                process.WaitForExit();

                if (process.ExitCode == 1 || process.ExitCode < 0)
                {
                    throw new Exception("StdOut: " + process.StandardOutput.ReadToEnd() +
                        Environment.NewLine + "StdErr: " + process.StandardError.ReadToEnd());
                }

                await _messageBoxService.ShowMessageBoxAsync(
                    "Info",
                    "Successfully reset windows firewall!",
                    MsBox.Avalonia.Enums.Icon.Success
                    );
            }
            catch (Exception ex)
            {
                await _loggerService.LogErrorAsync("An error has occured while resetting firewall.", ex.Message);

                await _messageBoxService.ShowMessageBoxAsync(
                    "Error",
                    "An error has occured while resetting windows firewall! Please upload log file to github."
                    );
            }
        }
    }
}