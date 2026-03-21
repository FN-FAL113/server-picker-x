using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;

namespace ServerPickerX.Models
{
    // ObservableObject base class requires a partial class type to  
    // generate boiler plate code for common MVVM implementations
    public partial class ServerModel : ObservableObject
    {
        public string Flag { get; set; } = "";

        public string Name { get; set; } = "";

        public string Description { get; set; } = "";

        [ObservableProperty]
        public string? ping;

        [ObservableProperty]
        public string? status;

        [ObservableProperty]
        public string? blockedStatus;

        public List<RelayModel> RelayModels { get; set; } = [];

        private CancellationTokenSource? _cancelTokenSource;

        public async void PingServer()
        {
            // If there's an ongoing ping operation then cancel it through token signals
            // Linux ICMP behaves differently. Executing too many ping operations may result in a timeout
            if (this._cancelTokenSource != null)
            {
                this._cancelTokenSource.Cancel();
                
            }

            this._cancelTokenSource = new CancellationTokenSource();
            var cancelToken = this._cancelTokenSource.Token;

            using var ping = new Ping();

            Ping = "Pinging server";

            foreach (RelayModel relay in RelayModels)
            {
                try
                {
                    // Cancellable async operation
                    var res = await ping.SendPingAsync(
                        address: IPAddress.Parse(relay.IPv4), 
                        timeout: TimeSpan.FromMilliseconds(800), 
                        options: new PingOptions(), 
                        cancellationToken: cancelToken
                        );

                    if (res.Status == IPStatus.Success && res.RoundtripTime >= 0)
                    {
                        Ping = res.RoundtripTime + "ms";
                        Status = "✅";

                        break;
                    }
                }
                catch (Exception ex) when (ex is PingException or OperationCanceledException)
                {
                    break;
                }
            }

            // if pinging status remains depite ping all relays then its blocked or unreachable
            if (Ping == "Pinging server")
            {
                Ping = "";
                Status = "❌";
            }
        }
    }
}
