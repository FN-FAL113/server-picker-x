using Avalonia;
using ServerPickerX.Models;
using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace ServerPickerX.Helpers
{
    public class PingHelper
    {
        public static async Task PingServer(ServerModel server)
        {
            if (server == null) return;

            ServerModel serverModel = server;

            Ping ping = new Ping();

            foreach (RelayModel relay in serverModel.RelayModels)
            {
                serverModel.Ping = "Pinging server";

                try
                {
                    var res = await ping.SendPingAsync(relay.IPv4, timeout: 1000);


                    if (res.RoundtripTime > 0)
                    {
                        serverModel.Ping = res.RoundtripTime + "ms";
                        break;
                    }
                }
                catch
                {
                    continue;
                }
            }

            // Update server status by checking if pingable or not
            if (serverModel.Ping == "Pinging server")
            {
                serverModel.Status = "❌";
                serverModel.Ping = "";
            }
            else
            {
                serverModel.Status = "✅";
            }

            ping.Dispose();
        }

        public static async Task CancelAllPings(List<Ping> pings)
        {
            foreach (Ping ping in pings)
            {
                ping.SendAsyncCancel();
                ping.Dispose();
            }
        }
    }
}
