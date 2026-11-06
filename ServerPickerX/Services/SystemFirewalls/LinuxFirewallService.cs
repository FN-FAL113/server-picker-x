using ServerPickerX.Models;
using ServerPickerX.Services.Localizations;
using ServerPickerX.Services.Loggers;
using ServerPickerX.Services.MessageBoxes;
using ServerPickerX.Services.Processes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
        private const string _shellPath = "/bin/sh";

        // Each script runs under a single privilege elevation so the user authenticates at most once per operation,
        // relay addresses are passed as script arguments ("$@") instead of being interpolated into the script
        // Rules go to the top of INPUT, firewall managers like ufw accept replies to outgoing traffic in their own rules
        // near the top so a rule appended below them never matches. Existing copies are removed first so rules
        // appended by older versions move up instead of being kept in place
        private const string _blockScript = """
            for ip do
                while iptables -D INPUT -s "$ip" -j DROP 2>/dev/null; do :; done
                iptables -I INPUT -s "$ip" -j DROP || exit
            done
            """;

        // Removes every copy of a rule and skips addresses that are not blocked
        private const string _unblockScript = """
            for ip do
                while iptables -C INPUT -s "$ip" -j DROP 2>/dev/null; do
                    iptables -D INPUT -s "$ip" -j DROP || exit
                done
            done
            """;

        // Only removes rules with the exact shape this app creates, including ones left by older versions.
        // Flushing would also wipe rules from ufw, Docker or the user, and with ufw's DROP input policy
        // an empty INPUT chain drops all incoming traffic
        private const string _resetScript = """
            rules=$(iptables -S INPUT) || exit
            for ip in $(printf '%s\n' "$rules" | sed -n 's|^-A INPUT -s \([0-9.]*\)/32 -j DROP$|\1|p'); do
                iptables -D INPUT -s "$ip" -j DROP || exit
            done
            """;

        public Task BlockServersAsync(ObservableCollection<ServerModel> serverModels)
        {
            return RunPrivilegedScriptAsync(_blockScript, GetRelayAddresses(serverModels));
        }

        public Task UnblockServersAsync(ObservableCollection<ServerModel> serverModels)
        {
            return RunPrivilegedScriptAsync(_unblockScript, GetRelayAddresses(serverModels));
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

            await RunPrivilegedScriptAsync(_resetScript, []);

            await _messageBoxService.ShowMessageBoxAsync(
                _localizationService.GetLocaleValue("MessageBoxInfoTitle"),
                _localizationService.GetLocaleValue("FirewallResetSuccessDialogue"),
                MsBox.Avalonia.Enums.Icon.Success
                );
        }

        private static IEnumerable<string> GetRelayAddresses(IEnumerable<ServerModel> serverModels)
        {
            return serverModels
                .SelectMany(s => s.RelayModels)
                .Select(r => r.IPv4)
                .Where(ip => !string.IsNullOrWhiteSpace(ip));
        }

        private async Task RunPrivilegedScriptAsync(string script, IEnumerable<string> scriptArguments)
        {
            using Process process = await CreatePrivilegedShellProcessAsync();

            process.StartInfo.ArgumentList.Add("-c");
            process.StartInfo.ArgumentList.Add(script);
            // Becomes $0 inside the script, the remaining arguments become "$@"
            process.StartInfo.ArgumentList.Add("server_picker_x");

            foreach (string argument in scriptArguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            process.Start();

            // Drain both streams while waiting, a full pipe buffer would otherwise block the process forever
            Task<string> stdOutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stdErrTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            string stdOut = (await stdOutTask).Trim();
            string stdErr = (await stdErrTask).Trim();

            if (process.ExitCode != 0)
            {
                await _loggerService.LogWarningAsync("ExitCode: " + process.ExitCode + " StdOut: " + stdOut + " StdErr: " + stdErr);

                throw new InvalidOperationException($"Firewall command failed with exit code {process.ExitCode}: {stdErr}");
            }
        }

        // The app has no terminal when launched from a desktop shortcut, so a plain "sudo" can never ask for a password.
        // Use root directly if already elevated, else sudo when it works without a prompt (cached credentials from
        // RunServerPickerX.sh or a NOPASSWD rule), else pkexec which shows a graphical polkit password prompt
        private async Task<Process> CreatePrivilegedShellProcessAsync()
        {
            if (Environment.IsPrivilegedProcess)
            {
                return _processService.CreateProcess(_shellPath);
            }

            Process process;

            if (await CanRunSudoNonInteractivelyAsync())
            {
                process = _processService.CreateProcess("sudo");
                process.StartInfo.ArgumentList.Add("-n");
            }
            else
            {
                process = _processService.CreateProcess("pkexec");
            }

            process.StartInfo.ArgumentList.Add(_shellPath);

            return process;
        }

        private async Task<bool> CanRunSudoNonInteractivelyAsync()
        {
            using Process process = _processService.CreateProcess("sudo");

            process.StartInfo.ArgumentList.Add("-n");
            process.StartInfo.ArgumentList.Add("true");

            try
            {
                process.Start();
            }
            catch (Win32Exception)
            {
                // sudo is not installed
                return false;
            }

            await process.WaitForExitAsync();

            return process.ExitCode == 0;
        }
    }
}
