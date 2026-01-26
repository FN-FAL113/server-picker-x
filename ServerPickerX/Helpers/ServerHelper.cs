using MsBox.Avalonia.Enums;
using ServerPickerX.Models;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerPickerX.Helpers
{
    public class ServerHelper
    {
        public static async Task BlockUnblockServersWindows(bool shouldBlock, ObservableCollection<ServerModel> serverModels)
        {
            Process process = ProcessHelper.createProcess("cmd.exe");

            foreach (ServerModel serverModel in serverModels)
            {
                string ipAddresses = String.Join(",", serverModel.RelayModels.Select(s => s.IPv4).ToList());

                try
                {
                    process.StartInfo.Arguments = $"/c {Path.Combine(Environment.SystemDirectory, "netsh.exe")} " + 
                            "advfirewall firewall " + 
                            (shouldBlock ? "add" : "delete") + " rule " +
                            "name=server_picker_x_" + serverModel.Description.Replace(" ", "") + 
                            (shouldBlock ? " dir=out action=block protocol=ANY " + "remoteip=" + ipAddresses : "");

                    process.Start();
                    process.WaitForExit();

                    if (process.ExitCode == 1 || process.ExitCode < 0)
                    {
                        throw new Exception("StdOut: " + process.StandardOutput.ReadToEnd() + Environment.NewLine 
                            + "StdErr: " + process.StandardError.ReadToEnd());
                    }

                    await PingHelper.PingServer(serverModel);
                }
                catch (Exception ex)
                {
                    if(ex.Message.Contains("No rules match the specified criteria"))
                    {
                        continue;
                    }

                    LogHelper.LogErrorToFile(
                        ex.Message, 
                        "An error has occured while" + (shouldBlock ? "blocking" : "unblocking") + " servers."
                    );

                    await MessageBoxHelper.ShowMessageBox(
                        "Error",
                        "An error has occured while" + (shouldBlock ? "blocking" : "unblocking") +  " servers." + 
                        Environment.NewLine + 
                        "Please upload the generated error file on github issue tracker.",
                        ButtonEnum.Ok
                    );
                }
            }

            process.Dispose();
        }

       
    }
}
