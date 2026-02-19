using Moq;
using ServerPickerX.Services.Loggers;
using ServerPickerX.Services.MessageBoxes;
using ServerPickerX.Services.Servers;
using ServerPickerX.Services.SystemFirewalls;
using ServerPickerX.Settings;
using ServerPickerX.ViewModels;
using ServerPickerX.Factories.Models;
using System.Reflection;
using System.Collections.ObjectModel;
using ServerPickerX.Models;

namespace ServerPickerX.Tests.ViewModels
{
    public class MainWindowViewModelTest
    {
        private readonly Mock<ILoggerService> _loggerService;
        private readonly Mock<IMessageBoxService> _messageBoxService;
        private readonly Mock<IServerDataService> _serverDataService;
        private readonly Mock<ISystemFirewallService> _systemFirewallService;
        private readonly Mock<JsonSetting> _jsonSetting;
        private readonly MainWindowViewModel _vm;

        // This constructor acts as the setup function
        public MainWindowViewModelTest()
        {
            _loggerService = new Mock<ILoggerService>();
            _messageBoxService = new Mock<IMessageBoxService>();
            _serverDataService = new Mock<IServerDataService>();
            _systemFirewallService = new Mock<ISystemFirewallService>();
            _jsonSetting = new Mock<JsonSetting>();

            _vm = new MainWindowViewModel(
                _loggerService.Object,
                _messageBoxService.Object,
                _serverDataService.Object,
                _systemFirewallService.Object,
                _jsonSetting.Object
                );
        }

        [Fact]
        public async Task Test_LoadServers()
        {
            // Arrange
            ServerData serverData = new() {
                ClusteredServers = ServerModelFactory.Create(3),
                UnclusteredServers = ServerModelFactory.CreateWithCluster(3),
            };

            // mock methods with fake results
            _serverDataService.Setup(i => i.LoadServersAsync()).Returns(Task.CompletedTask);
            _serverDataService.Setup(i => i.GetServerData()).Returns(serverData);

            // Act
            await _vm.LoadServersAsync();

            // Assert
            Assert.NotEmpty(_vm.ServerModels);
            Assert.True(_vm.ServersInitialized);
        }

        [Fact]
        public async Task Test_ClusterUnclusterServers()
        {
            // Arrange
            // test servers are unclustered by default
            JsonSetting setting = (JsonSetting)_vm.GetType().GetField("_jsonSetting", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_vm);
            ServerData serverData = new()
            {
                ClusteredServers = ServerModelFactory.Create(3),
                UnclusteredServers = ServerModelFactory.CreateWithCluster(3),
            };

            // mock method with fake data
            _serverDataService.Setup(i => i.GetServerData()).Returns(serverData);

            // Act
            await _vm.ClusterUnclusterServersAsync();

            // Assert
            Assert.NotEmpty(_vm.ServerModels);
            Assert.False(setting.is_clustered);
            Assert.Equal(serverData.UnclusteredServers, _vm.ServerModels);

            // Arrange
            // test servers are clustered
            _jsonSetting.SetupGet(i => i.is_clustered).Returns(true);

            // Act
            await _vm.ClusterUnclusterServersAsync();

            // Assert
            Assert.NotEmpty(_vm.ServerModels);
            Assert.True(setting.is_clustered);
            Assert.Equal(serverData.ClusteredServers, _vm.ServerModels);
        }

        [Fact]
        public async Task Test_PingServers()
        {
            // Arrange
            ServerData serverData = new()
            {
                ClusteredServers = ServerModelFactory.Create(3),
                UnclusteredServers = ServerModelFactory.CreateWithCluster(3),
            };

            // mock methods with fake results
            _serverDataService.Setup(i => i.LoadServersAsync()).Returns(Task.CompletedTask);
            _serverDataService.Setup(i => i.GetServerData()).Returns(serverData);

            // Act
            await _vm.LoadServersAsync();
            _vm.PingServers(_vm.ServerModels);

            // Assert
            Assert.NotEmpty(_vm.ServerModels);
            Assert.True(_vm.ServerModels[0].Ping?.Contains("ms"));
            Assert.Equal("✅", _vm.ServerModels[0].Status);
        }

        [Fact]
        public async Task Test_PingSelectedServer()
        {
            // Arrange
            ServerData serverData = new()
            {
                ClusteredServers = ServerModelFactory.Create(3),
                UnclusteredServers = ServerModelFactory.CreateWithCluster(3),
            };

            // mock methods with fake results
            _serverDataService.Setup(i => i.LoadServersAsync()).Returns(Task.CompletedTask);
            _serverDataService.Setup(i => i.GetServerData()).Returns(serverData);

            // Act
            await _vm.LoadServersAsync();
            _vm.SelectedDataGridItem = _vm.ServerModels[0];
            _vm.PingSelectedServer();

            // Assert
            Assert.NotEmpty(_vm.ServerModels);
            Assert.True(_vm.SelectedDataGridItem.Ping?.Contains("ms"));
            Assert.Equal("✅", _vm.ServerModels[0].Status);
        }

        [Fact]
        public async Task Test_BlockAllAsync()
        {
            // Arrange
            ServerData serverData = new()
            {
                ClusteredServers = ServerModelFactory.Create(3),
                UnclusteredServers = ServerModelFactory.CreateWithCluster(3),
            };

            // mock methods with fake results
            _serverDataService.Setup(i => i.LoadServersAsync()).Returns(Task.CompletedTask);
            _serverDataService.Setup(i => i.GetServerData()).Returns(serverData);

            // Act
            await _vm.LoadServersAsync();

            _systemFirewallService.Setup(i => i.BlockServersAsync(_vm.ServerModels))
                .Callback((ObservableCollection<ServerModel> serverModels) => {
                    foreach (var server in serverModels)
                    {
                        server.RelayModels.Clear();
                    }
                });

            var result = await _vm.BlockAllAsync();

            // Assert
            Assert.NotEmpty(_vm.ServerModels);
            Assert.True(result);
            Assert.Empty(_vm.ServerModels[0].Ping);
            Assert.Equal("❌", _vm.ServerModels[0].Status);
        }

        [Fact]
        public async Task Test_BlockSelectedAsync()
        {
            // Arrange
            ServerData serverData = new()
            {
                ClusteredServers = ServerModelFactory.Create(3),
                UnclusteredServers = ServerModelFactory.CreateWithCluster(3),
            };

            // mock methods with fake results
            _serverDataService.Setup(i => i.LoadServersAsync()).Returns(Task.CompletedTask);
            _serverDataService.Setup(i => i.GetServerData()).Returns(serverData);

            // Act
            await _vm.LoadServersAsync();

            var selectedServers = _vm.ServerModels.Skip(1).ToList();

            _systemFirewallService.Setup(i => i.BlockServersAsync(new ObservableCollection<ServerModel>(selectedServers)))
                .Callback((ObservableCollection<ServerModel> serverModels) => {
                    foreach (var server in serverModels)
                    {
                        // Clear server relays in order for PingServers method to update Ping to null and Status to ❌ 
                        server.RelayModels.Clear();
                    }
                })
                .Returns(Task.CompletedTask);

            var result = await _vm.BlockSelectedAsync(selectedServers);

            // Assert
            Assert.NotEmpty(_vm.ServerModels);
            Assert.True(result);
            Assert.Empty(selectedServers[1].Ping);
            Assert.Equal("❌", selectedServers[1].Status);
        }

    }
}
