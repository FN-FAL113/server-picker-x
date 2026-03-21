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
using System.Linq;
using System.Threading.Tasks;

namespace ServerPickerX.Services.SystemFirewalls
{
    public class LinuxFirewallService(
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

                using var process = _processService.CreateProcess("sudo");

                try
                {
                    process.StartInfo.Arguments = "iptables " +
                        "-A INPUT -s " + ipAddresses + " -j DROP";

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

                using var process = _processService.CreateProcess("sudo");

                try
                {
                    process.StartInfo.Arguments = "iptables " +
                       "-D INPUT -s " + ipAddresses + " -j DROP";

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

            using Process process = _processService.CreateProcess("sudo");

            try
            {
                process.StartInfo.Arguments = $"iptables -F";

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
                using var process = _processService.CreateProcess("sudo");
                process.StartInfo.Arguments = "iptables -S INPUT";

                var (stdOut, stdErr) = await RedirectedProcess.RunAsync(process);
                stdErr = stdErr.Trim();

                if (process.ExitCode > 0)
                {
                    await _loggerService.LogWarningAsync(
                        "SyncBlockedState iptables exit " + process.ExitCode + " StdErr: " + stdErr);
                    ClearBlockedStatus(servers);
                    return;
                }

                var dropSets = IptablesInputDropParser.ParseDropSourceIpSets(stdOut);

                foreach (var server in servers)
                {
                    var relaySet = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var relay in server.RelayModels)
                    {
                        if (!string.IsNullOrWhiteSpace(relay.IPv4))
                        {
                            relaySet.Add(relay.IPv4.Trim());
                        }
                    }

                    bool blocked = relaySet.Count > 0
                        && dropSets.Exists(ds => ds.SetEquals(relaySet));

                    server.BlockedStatus = blocked
                        ? BlockedServerDisplayGlyphs.Blocked
                        : BlockedServerDisplayGlyphs.NotBlocked;
                }
            }
            catch (Exception ex)
            {
                await _loggerService.LogErrorAsync("SyncBlockedStateAsync (Linux) failed.", ex.Message);
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