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
using NetFwTypeLib;

namespace ServerPickerX.Services.SystemFirewalls
{
    public class WindowsFirewallService(
        ILoggerService _loggerService,
        ILocalizationService _localizationService,
        IMessageBoxService _messageBoxService,
        IProcessService _processService
        ) : ISystemFirewallService
    {
        private const string FirewallRulePrefix = "server_picker_x_";
        private const NET_FW_RULE_DIRECTION_ FirewallRuleDirectionOutbound = NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_OUT;
        private const NET_FW_ACTION_ FirewallRuleActionBlock = NET_FW_ACTION_.NET_FW_ACTION_BLOCK;
        private const int FirewallRuleProtocolAny = 256;
        private const int FirewallRuleProfilesAll = int.MaxValue;

        public async Task BlockServersAsync(ObservableCollection<ServerModel> serverModels)
        {
            try
            {
                INetFwPolicy2 firewallPolicy = GetFirewallPolicyApi();
                INetFwRules firewallRules = firewallPolicy.Rules;

                foreach (var serverModel in serverModels)
                {
                    string ruleName = GetFirewallRuleName(serverModel);
                    string ipAddresses = string.Join(",", serverModel.RelayModels.Select(s => s.IPv4));

                    RemoveFirewallRuleByName(firewallRules, ruleName);

                    INetFwRule firewallRule = CreateFirewallRuleApi();
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
            catch (Exception ex)
            {
                await _loggerService.LogErrorAsync(ex.Message);
                throw;
            }
        }

        public async Task UnblockServersAsync(ObservableCollection<ServerModel> serverModels)
        {
            try
            {
                INetFwPolicy2 firewallPolicy = GetFirewallPolicyApi();
                INetFwRules firewallRules = firewallPolicy.Rules;

                foreach (var serverModel in serverModels)
                {
                    string ruleName = GetFirewallRuleName(serverModel);
                    RemoveFirewallRuleByName(firewallRules, ruleName);
                }
            }
            catch (Exception ex)
            {
                await _loggerService.LogErrorAsync(ex.Message);
                throw;
            }
        }

        public async Task ResetFirewallAsync()
        {
            var result = await _messageBoxService.ShowMessageBoxConfirmationAsync(
                    _localizationService.GetLocaleValue("MessageBoxInfoTitle"),
                    _localizationService.GetLocaleValue("FirewallResetConfirmDialogue"),
                    MsBox.Avalonia.Enums.Icon.Warning
                );

            if (!result) return;

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
                await _loggerService.LogErrorAsync(ex.Message);
                throw;
            }
        }

        private static INetFwPolicy2 GetFirewallPolicyApi()
            => (INetFwPolicy2)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("E2B3C97F-6AE1-41AC-817A-F6F92166D7DD"))!)!;

        private static INetFwRule CreateFirewallRuleApi()
            => (INetFwRule)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("2C5BC43E-3369-4C33-AB0C-BE9469677AF4"))!)!;

        private static string GetFirewallRuleName(ServerModel serverModel)
            => FirewallRulePrefix + serverModel.Description.Replace(" ", "");

        private static void RemoveFirewallRuleByName(INetFwRules firewallRules, string ruleName)
        {
            while (TryGetFirewallRule(firewallRules, ruleName) != null)
            {
                firewallRules.Remove(ruleName);
            }
        }

        private static INetFwRule? TryGetFirewallRule(INetFwRules firewallRules, string ruleName)
        {
            try { return firewallRules.Item(ruleName); }
            catch { return null; }
        }
    }
}
