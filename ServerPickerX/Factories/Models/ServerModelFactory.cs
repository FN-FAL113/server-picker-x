using ServerPickerX.Models;
using System;
using System.Collections.Generic;

namespace ServerPickerX.Factories.Models
{
    public class ServerModelFactory
    {
        public static List<ServerModel> Create(int count = 1)
        {
            List<ServerModel> servers = [];
            List<string> countries = ["(China)", "(Hong Kong)", "(Sweden)"];

            Random rand = new();

            for (int i = 0; i < count; i++)
            {
                var server = new ServerModel()
                {
                    Name = "server_id " + (i + 1),
                    Description = $"Server{i + 1} {countries[rand.Next(0, 3)]}",
                    RelayModels = [new RelayModel { IPv4 = "127.0.0.1" }]
                };

                servers.Add(server);
            }

            return servers;
        }

        public static List<ServerModel> CreateWithCluster(int count = 1)
        {
            List<ServerModel> servers = [];
            List<string> countries = ["(China)", "(Hong Kong)", "(Sweden)"];

            Random rand = new();

            for (int i = 0; i < count; i++)
            {
                var server = new ServerModel()
                {
                    Name = "cluster",
                    Description = $"Cluster{i + 1}",
                    RelayModels = [new RelayModel { IPv4 = "127.0.0.1" }]
                };

                servers.Add(server);
            }

            // add non clustered servers
            servers.AddRange(Create(count));

            return servers;
        }
    }
}
