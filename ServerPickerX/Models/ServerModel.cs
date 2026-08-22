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
        // State token rendered as a coloured dot, not display text
        public const string StatusUp = "up";

        public const string StatusDown = "down";

        // Placeholder shown while a probe is in flight
        public const string PendingReading = "\u2026";

        public string Flag { get; set; } = "";

        public string Name { get; set; } = "";

        public string Description { get; set; } = "";

        [ObservableProperty]
        public string? ping;

        [ObservableProperty]
        public string? status;
         
        [ObservableProperty]
        public string? packetLoss;

        // True while a firewall rule exists for this server
        [ObservableProperty]
        public bool isBlocked;

        public List<RelayModel> RelayModels { get; set; } = [];

        private CancellationTokenSource? _cancelTokenSource;

        public async void PingServer()
        {
            if (this._cancelTokenSource != null)
            {
                this._cancelTokenSource.Cancel();
            }

            this._cancelTokenSource = new CancellationTokenSource();
            var cancelToken = this._cancelTokenSource.Token;

            using var ping = new Ping();

            Ping = PendingReading;

            RelayModel? bestRelay = null;
            long bestRtt = long.MaxValue;

            // Phase 1, Find the best relay (lowest RTT)
            foreach (RelayModel relay in RelayModels)
            {
                try
                {
                    var res = await ping.SendPingAsync(
                        address: IPAddress.Parse(relay.IPv4), 
                        timeout: TimeSpan.FromMilliseconds(800), 
                        options: new PingOptions(), 
                        cancellationToken: cancelToken
                        );

                    if (res.Status == IPStatus.Success && res.RoundtripTime >= 0 && res.RoundtripTime < bestRtt)
                    {
                        bestRtt = res.RoundtripTime;
                        bestRelay = relay;
                    }
                }
                catch (Exception ex) when(ex is OperationCanceledException) { }
            }

            if (bestRelay != null)
            {
                PacketLoss = PendingReading;

                // Phase 2, Probe the best relay 4 times
                int successCount = 0;
                long finalBestRtt = long.MaxValue;
                const int probeCount = 4;

                for (int i = 0; i < probeCount; i++)
                {
                    try
                    {
                        var res = await ping.SendPingAsync(
                            address: IPAddress.Parse(bestRelay.IPv4), 
                            timeout: TimeSpan.FromMilliseconds(2000), 
                            options: new PingOptions(), 
                            cancellationToken: cancelToken
                            );

                        if (res.Status == IPStatus.Success && res.RoundtripTime >= 0)
                        {
                            successCount++;
                            finalBestRtt = Math.Min(finalBestRtt, res.RoundtripTime);
                        }
                    }
                    catch (Exception ex) when (ex is OperationCanceledException) { }
                }

                // Cast before dividing, integer division here only yields 0 or 1
                double lossPercent = (1 - ((double)successCount / probeCount)) * 100;
                Ping = successCount > 0 ? finalBestRtt + "ms" : "";
                Status = successCount > 0 ? StatusUp : StatusDown;
                PacketLoss = $"{lossPercent:F0}%";
            } else if (Ping == PendingReading)
            {
                Ping = "";
                PacketLoss = "";
                Status = StatusDown;
            }
        }
    }
}
