using ServerPickerX.Constants;
using ServerPickerX.Models;
using ServerPickerX.Services.Localizations;
using ServerPickerX.Services.Loggers;
using ServerPickerX.Services.MessageBoxes;
using ServerPickerX.Services.Processes;
using System;
using System.Collections.Generic;
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
                        "name=" + WindowsFirewallRuleName.ForServer(serverModel) +
                        " dir=out action=block protocol=ANY " + "remoteip=" + ipAddresses;

                try
                {
                    var (stdOut, stdErr) = await RedirectedProcess.RunAsync(process);
                    stdOut = stdOut.Trim();
                    stdErr = stdErr.Trim();

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
                        "name=" + WindowsFirewallRuleName.ForServer(serverModel);

                try
                {
                    var (stdOut, stdErr) = await RedirectedProcess.RunAsync(process);
                    stdOut = stdOut.Trim();
                    stdErr = stdErr.Trim();

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

                var (stdOut, stdErr) = await RedirectedProcess.RunAsync(process);
                stdOut = stdOut.Trim();
                stdErr = stdErr.Trim();

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

        public async Task SyncBlockedStateAsync(IReadOnlyList<ServerModel> servers)
        {
            if (servers.Count == 0)
            {
                return;
            }

            try
            {
                using var process = _processService.CreateProcess("cmd.exe");
                process.StartInfo.Arguments = $"/c {Path.Combine(Environment.SystemDirectory, "netsh.exe")} " +
                    "advfirewall firewall show rule name=all";

                var (stdOut, stdErr) = await RedirectedProcess.RunAsync(process);
                stdErr = stdErr.Trim();

                if (process.ExitCode > 0)
                {
                    await _loggerService.LogWarningAsync(
                        "SyncBlockedState netsh exit " + process.ExitCode + " StdErr: " + stdErr);
                    ClearBlockedStatus(servers);
                    return;
                }

                var ruleNames = NetshAdvFirewallRuleNameParser.ParseRuleNames(stdOut);

                foreach (var server in servers)
                {
                    bool blocked = ruleNames.Contains(WindowsFirewallRuleName.ForServer(server));
                    server.BlockedStatus = blocked
                        ? BlockedServerDisplayGlyphs.Blocked
                        : BlockedServerDisplayGlyphs.NotBlocked;
                }
            }
            catch (Exception ex)
            {
                await _loggerService.LogErrorAsync("SyncBlockedStateAsync (Windows) failed.", ex.Message);
                ClearBlockedStatus(servers);
            }
        }

        private static void ClearBlockedStatus(IReadOnlyList<ServerModel> servers)
        {
            foreach (var server in servers)
            {
                server.BlockedStatus = "";
            }
        }
    }
}