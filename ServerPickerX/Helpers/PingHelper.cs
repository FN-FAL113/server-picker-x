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
            if (server == null)
            {
                return;
            }

            ServerModel serverModel = server;

            Ping ping = new();

            foreach (RelayModel relay in serverModel.RelayModels)
            {
                serverModel.Ping = "Pinging server";

                try
                {
                    var res = await ping.SendPingAsync(relay.IPv4, timeout: 500);


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

            // if pinging status remains after pinging all server relay addresses then its blocked or unreachable
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
