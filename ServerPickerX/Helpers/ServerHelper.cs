using MsBox.Avalonia.Enums;
using ServerPickerX.Models;
using System;
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
        public async static Task<ObservableCollection<ServerModel>> LoadServers()
        {
            ObservableCollection<ServerModel> serverModels = [];

            HttpClient httpClient = new();

            try
            {
                string res = await httpClient.GetStringAsync("https://api.steampowered.com/ISteamApps/GetSDRConfig/v1/?appid=730");

                if (string.IsNullOrWhiteSpace(res))
                {
                    throw new Exception(
                        "Failed to load servers..." + Environment.NewLine + Environment.NewLine +
                        "- Verify your internet connection or firewall are working and enabled" + Environment.NewLine +
                        "- Make sure to run the app as admin or with sudo level execution"
                    );
                }

                JsonObject? mainJson = JsonObject.Parse(res) as JsonObject;

                if (mainJson?["revision"] == null || mainJson?["pops"] == null)
                {
                    throw new Exception("Server data not available yet. Please check again later.");
                }

                Debug.WriteLine("Server Revision: " + mainJson["revision"]);

                foreach (var server in mainJson["pops"] as JsonObject)
                {
                    if (server.Value?["relays"] == null || server.Value?["desc"] == null)
                    {
                        throw new Exception($"Invalid server data for {server.Key}. API data structure error.");
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

                return serverModels;
            }
            catch (Exception ex) {
                await MessageBoxHelper.ShowMessageBox("Error", ex.Message);

                return serverModels;
            }
        }

        public static async Task BlockUnblockServersWindows(bool shouldBlock, ObservableCollection<ServerModel> serverModels)
        {
            Process process = ProcessHelper.createProcess("cmd.exe");

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
            Process process = ProcessHelper.createProcess("sudo");

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
