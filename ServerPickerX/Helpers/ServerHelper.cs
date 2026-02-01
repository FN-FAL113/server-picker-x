using ServerPickerX.ConfigSections;
using ServerPickerX.Models;
using ServerPickerX.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace ServerPickerX.Helpers
{
    public class ServerHelper
    {
        public static string current_server_revision = string.Empty;

        public async static Task<ObservableCollection<ServerModel>> LoadServers()
        {
            ObservableCollection<ServerModel> serverModels = [];

            using HttpClient httpClient = new();

            try
            {
                Stream res = await httpClient.GetStreamAsync("https://api.steampowered.com/ISteamApps/GetSDRConfig/v1/?appid=730");

                if (res == null)
                {
                    throw new Exception(
                        "Failed to load servers!" + Environment.NewLine + Environment.NewLine +
                        "- Verify your internet connection or firewall are working and enabled" + Environment.NewLine +
                        "- Make sure to run the app as admin or with sudo level execution"
                    );
                }

                JsonObject? mainJson = await JsonObject.ParseAsync(res) as JsonObject;

                if (mainJson?["revision"] == null || mainJson?["pops"] == null)
                {
                    throw new Exception("Server relay data unavailable. Please try again later.");
                }

                current_server_revision = mainJson["revision"].ToString();
                
                // update json setting server revision value if initialized for the first time
                JsonSetting jsonSettings = MainWindow.jsonSettings;

                if (jsonSettings.server_revision == "-1")
                {
                    jsonSettings.server_revision = current_server_revision;

                    await jsonSettings.SaveSettings();
                }

                foreach (KeyValuePair<string, JsonNode> server in mainJson["pops"] as JsonObject)
                {
                    if (server.Value?["relays"] == null)
                    {
                        continue;
                    }

                    var serverModel = new ServerModel
                    {
                        Flag = "/Assets/flags/"
                            + server.Value["desc"]?.ToString() + $" ({server.Key}).png",
                        Name = server.Key,
                        Description = server.Value["desc"]?.ToString()
                    };

                    foreach (JsonObject relay in server.Value["relays"] as JsonArray)
                    {
                        serverModel.RelayModels.Add(new RelayModel
                        {
                            IPv4 = relay["ipv4"]?.ToString()
                        });
                    }

                    serverModels.Add(serverModel);
                }
            }
            catch (Exception ex) {
                await MessageBoxHelper.ShowMessageBox("Error", ex.Message);
            }

            return serverModels;
        }

        public static async Task BlockUnblockServersWindows(bool shouldBlock, ObservableCollection<ServerModel> serverModels)
        {
            Process process = ProcessHelper.CreateProcess("cmd.exe");

            foreach (ServerModel serverModel in serverModels)
            {
                string ipAddresses = String.Join(",", serverModel.RelayModels.Select(s => s.IPv4).ToList());
       
                process.StartInfo.Arguments = $"/c {Path.Combine(Environment.SystemDirectory, "netsh.exe")} " +
                        "advfirewall firewall " +
                        (shouldBlock ? "add" : "delete") + " rule " +
                        "name=server_picker_x_" + serverModel.Description.Replace(" ", "") +
                        (shouldBlock ? " dir=out action=block protocol=ANY " + "remoteip=" + ipAddresses : "");

                process.Start();
                process.WaitForExit();

                string stdOut = process.StandardOutput.ReadToEnd();
                string stdErr = process.StandardError.ReadToEnd();

                // skip throwing an exception if user tries to unblock a non-blocked server
                if ((process.ExitCode == 1 || process.ExitCode < 0) &&
                    !$"{stdOut} {stdErr}".Contains("No rules match"))
                {
                    throw new Exception("StdOut: " + stdOut + Environment.NewLine + "StdErr: " + stdErr);
                }

                await PingHelper.PingServer(serverModel);
            }

            process.Dispose();
        }

        public static async Task BlockUnblockServersLinux(bool shouldBlock, ObservableCollection<ServerModel> serverModels)
        {
            Process process = ProcessHelper.CreateProcess("sudo");

            foreach (ServerModel serverModel in serverModels)
            {
                string ipAddresses = String.Join(",", serverModel.RelayModels.Select(s => s.IPv4).ToList());

                // append or delete rules in the iptables input chain
                process.StartInfo.Arguments = "iptables " +
                        (shouldBlock ? "-A" : "-D") + " INPUT -s " + ipAddresses + " -j DROP";

                process.Start();
                process.WaitForExit();

                string stdOut = process.StandardOutput.ReadToEnd();
                string stdErr = process.StandardError.ReadToEnd();

                if ((process.ExitCode == 1 || process.ExitCode < 0) && 
                    !$"{stdOut} {stdErr}".Contains("Bad rule (does a matching"))
                {
                    throw new Exception("StdOut: " + stdOut + Environment.NewLine + "StdErr: " + stdErr);
                }

                await PingHelper.PingServer(serverModel);
            }

            process.Dispose();
        }
    }
}
