using ServerPickerX.Models;

namespace ServerPickerX.Tests.Models
{
    public class ServerModelTests
    {
        // PingServer is async void, an unhandled exception here would terminate the test host
        [Fact]
        public void PingServer_WithInvalidRelayAddress_MarksServerUnreachable()
        {
            var server = new ServerModel
            {
                RelayModels = [new RelayModel { IPv4 = "" }]
            };

            server.PingServer();

            Assert.Equal("❌", server.Status);
            Assert.Equal("", server.Ping);
        }
    }
}
