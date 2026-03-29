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
        private const string FirewallPolicyProgId = "HNetCfg.FwPolicy2";
        private const string FirewallRuleProgId = "HNetCfg.FWRule";
        private const string FirewallRulePrefix = "server_picker_x_";
        private const int FirewallRuleDirectionOutbound = 2;
        private const int FirewallRuleActionBlock = 0;
        private const int FirewallRuleProtocolAny = 256;
        private const int FirewallRuleProfilesAll = int.MaxValue;

        public Task BlockServersAsync(ObservableCollection<ServerModel> serverModels)
        {
            dynamic firewallRules = GetFirewallRules();

            try
            {
                foreach (var serverModel in serverModels)
                {
                    string ruleName = GetFirewallRuleName(serverModel);
                    string ipAddresses = string.Join(",", serverModel.RelayModels.Select(s => s.IPv4).ToList());

                    RemoveFirewallRuleByName(firewallRules, ruleName);

                    dynamic firewallRule = CreateFirewallRule();
                    firewallRule.Name = ruleName;
                    firewallRule.Description = serverModel.Description;
                    firewallRule.Direction = FirewallRuleDirectionOutbound;
                    firewallRule.Action = FirewallRuleActionBlock;
                    firewallRule.Protocol = FirewallRuleProtocolAny;
                    firewallRule.RemoteAddresses = ipAddresses;
                    firewallRule.Enabled = true;
                    firewallRule.Profiles = FirewallRuleProfilesAll;

                    firewallRules.Add(firewallRule);
                }
            }
            catch (Exception)
            {
                // Perform debugging here if necessary (log error or through debugger breakpoints)
                throw;
            }

            return Task.CompletedTask;
        }

        public Task UnblockServersAsync(ObservableCollection<ServerModel> serverModels)
        {
            dynamic firewallRules = GetFirewallRules();

            try
            {
                foreach (var serverModel in serverModels)
                {
                    string ruleName = GetFirewallRuleName(serverModel);
                    RemoveFirewallRuleByName(firewallRules, ruleName);
                }
            }
            catch (Exception)
            {
                // Perform debugging here if necessary (log error or through debugger breakpoints)
                throw;
            }

            return Task.CompletedTask;
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
            catch (Exception)
            {
                // Perform debugging here if necessary (log error or through debugger breakpoints)
                throw;
            }
        }

        private static dynamic CreateComObject(string progId)
        {
            Type? comType = Type.GetTypeFromProgID(progId);

            if (comType == null)
            {
                throw new InvalidOperationException($"Unable to resolve COM ProgID '{progId}'.");
            }

            return Activator.CreateInstance(comType)
                ?? throw new InvalidOperationException($"Unable to create COM instance for '{progId}'.");
        }

        private static dynamic GetFirewallRules()
        {
            dynamic firewallPolicy = CreateComObject(FirewallPolicyProgId);
            return firewallPolicy.Rules;
        }

        private static dynamic CreateFirewallRule()
        {
            return CreateComObject(FirewallRuleProgId);
        }

        private static string GetFirewallRuleName(ServerModel serverModel)
        {
            return FirewallRulePrefix + serverModel.Description.Replace(" ", "");
        }

        private static void RemoveFirewallRuleByName(dynamic firewallRules, string ruleName)
        {
            while (TryGetFirewallRule(firewallRules, ruleName) != null)
            {
                firewallRules.Remove(ruleName);
            }
        }

        private static dynamic? TryGetFirewallRule(dynamic firewallRules, string ruleName)
        {
            try
            {
                return firewallRules.Item(ruleName);
            }
            catch
            {
                return null;
            }
        }
    }
}
