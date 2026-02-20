using Moq;
using ServerPickerX.Services.Loggers;
using ServerPickerX.Services.MessageBoxes;
using ServerPickerX.Services.Servers;
using ServerPickerX.Services.SystemFirewalls;
using ServerPickerX.Settings;
using ServerPickerX.ViewModels;
using ServerPickerX.Factories.Models;
using MsBox.Avalonia.Enums;
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
        public async Task Test_LoadServers_CollectionIsNotEmptyAndIsInitialized()
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
        public async Task Test_ClusterUnclusterServers_UpdatesServerModelsBasedOnJsonSetting()
        {
            // Arrange
            // Test servers are unclustered by default
            JsonSetting setting = (JsonSetting)reflectGetField(_vm, "_jsonSetting");
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
            // Test servers are clustered
            _jsonSetting.SetupGet(i => i.is_clustered).Returns(true);

            // Act
            await _vm.ClusterUnclusterServersAsync();

            // Assert
            Assert.NotEmpty(_vm.ServerModels);
            Assert.True(setting.is_clustered);
            Assert.Equal(serverData.ClusteredServers, _vm.ServerModels);
        }

        [Fact]
        public async Task Test_PingServers_UpdatesModelPingAndStatus()
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

            Thread.Sleep(70); // Pinging is done in parallel operation and is not awaited

            // Assert
            Assert.NotEmpty(_vm.ServerModels);
            Assert.True(_vm.ServerModels[0].Ping?.Contains("ms"));
            Assert.Equal("✅", _vm.ServerModels[0].Status);
        }

        [Fact]
        public async Task Test_PingSelectedServer_UpdatesModelPingAndStatus()
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

            Thread.Sleep(70); // Pinging is done in parallel operation and is not awaited

            // Assert
            Assert.NotEmpty(_vm.ServerModels);
            Assert.True(_vm.SelectedDataGridItem.Ping?.Contains("ms"));
            Assert.Equal("✅", _vm.ServerModels[0].Status);
        }

        [Fact]
        public async Task Test_BlockAllAsync_WithServers()
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
                })
                .Returns(Task.CompletedTask);

            var result = await _vm.BlockAllAsync();

            // Assert
            Assert.NotEmpty(_vm.ServerModels);
            // Verify method is invoked
            _systemFirewallService.Verify(i => i.BlockServersAsync(_vm.ServerModels), Times.Once); 
            Assert.True(result);
            foreach (var server in _vm.ServerModels)
            {
                Assert.Empty(server.Ping);
                Assert.Equal("❌", server.Status);
            }
        }

        [Fact]
        public async Task Test_BlockAllAsync_NoServers()
        {
            // Arrange - Server Models empty by default

            // Act
            var result = await _vm.BlockAllAsync();

            // Assert
            // Verify method is not invoked
            _systemFirewallService.Verify(i => i.BlockServersAsync(It.IsAny<ObservableCollection<ServerModel>>()), Times.Never); 
            Assert.False(result);
        }

        [Fact]
        public async Task Test_BlockSelectedAsync_WithSelection()
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

            ObservableCollection<ServerModel> selectedServers = [_vm.ServerModels[0], _vm.ServerModels[2]];

            _systemFirewallService.Setup(i => i.BlockServersAsync(selectedServers))
                .Callback((ObservableCollection<ServerModel> serverModels) =>
                {
                    foreach (var server in serverModels)
                    {
                        // In order for PingServers method to update Ping to null and Status to ❌ 
                        server.RelayModels.Clear();
                    }
                })
                .Returns(Task.CompletedTask);

            var result = await _vm.BlockSelectedAsync(selectedServers);

            // Assert
            Assert.NotEmpty(_vm.ServerModels);
            _systemFirewallService.Verify(i => i.BlockServersAsync(selectedServers)); // Verify method is invoked
            Assert.True(result);
            foreach (var item in _vm.ServerModels.Select((value, index) => new { value, index }))
            {
                if(item.index is 0 or 2)
                {
                    Assert.Empty(item.value.Ping);
                    Assert.Equal("❌", item.value.Status);
                } else
                {
                    Assert.True(item.value.Ping.Contains("ms"));
                    Assert.Equal("✅", item.value.Status);
                }
            }
        }

        [Fact]
        public async Task Test_BlockSelectedAsync_EmptySelection()
        {
            // Arrange - Server Models empty by default

            // Act
            var result = await _vm.BlockSelectedAsync(new List<ServerModel>());

            // Assert
            // Verify method is not invoked
            _systemFirewallService.Verify(i => i.BlockServersAsync(It.IsAny<ObservableCollection<ServerModel>>()), Times.Never);
            Assert.False(result);
        }

        [Fact]
        public async Task Test_UnblockAllAsync_WithServers()
        {
            // Arrange
            ServerData serverData = new()
            {
                ClusteredServers = ServerModelFactory.Create(3),
                UnclusteredServers = ServerModelFactory.CreateWithCluster(3),
            };
            _serverDataService.Setup(i => i.LoadServersAsync()).Returns(Task.CompletedTask);
            _serverDataService.Setup(i => i.GetServerData()).Returns(serverData);

            await _vm.LoadServersAsync();

            _systemFirewallService.Setup(i => i.UnblockServersAsync(_vm.ServerModels))
                .Callback((ObservableCollection<ServerModel> serverModels) =>
                {
                    foreach (var server in serverModels)
                    {
                        // clear relay models to force ping failure
                        server.RelayModels.Clear();
                    }
                })
                .Returns(Task.CompletedTask);

            var result = await _vm.UnblockAllAsync();

            Assert.True(result);
            foreach (var srv in _vm.ServerModels)
            {
                Assert.Empty(srv.Ping);
                Assert.Equal("❌", srv.Status);
            }
        }

        [Fact]
        public async Task Test_UnblockAllAsync_NoServers()
        {
            // Arrange – ServerModels is empty by default

            // Act
            var result = await _vm.UnblockAllAsync();

            // Assert
            Assert.False(result);
            // Verify method is not invoked
            _systemFirewallService.Verify(i => i.UnblockServersAsync(It.IsAny<ObservableCollection<ServerModel>>()), Times.Never());
        }

        [Fact]
        public async Task Test_UnblockSelectedAsync_WithSelection()
        {
            // Arrange
            ServerData serverData = new()
            {
                ClusteredServers = ServerModelFactory.Create(3),
                UnclusteredServers = ServerModelFactory.CreateWithCluster(3),
            };

            _serverDataService.Setup(i => i.LoadServersAsync()).Returns(Task.CompletedTask);
            _serverDataService.Setup(i => i.GetServerData()).Returns(serverData);

            // Act
            await _vm.LoadServersAsync();

            List<ServerModel> selected = [_vm.ServerModels[0], _vm.ServerModels[2]];

            _systemFirewallService.Setup(i => i.UnblockServersAsync(It.IsAny<ObservableCollection<ServerModel>>()))
                .Returns(Task.CompletedTask);

            var result = await _vm.UnblockSelectedAsync(selected);

            Assert.True(result);
            foreach (var srv in selected)
            {
                Assert.Contains("ms", srv.Ping);
                Assert.Equal("✅", srv.Status);
            }
        }

        [Fact]
        public async Task Test_UnblockSelectedAsync_EmptySelection()
        {
            var emptyList = new List<ServerModel>();

            // Act
            var result = await _vm.UnblockSelectedAsync(emptyList);

            // Assert
            Assert.False(result);
            _messageBoxService.Verify(i => i.ShowMessageBoxAsync("Info", "Hey! Please select at least one server to unblock"), Times.Once());
        }

        [Fact]
        public async Task Test_PerformOperationAsync_PendingOperation()
        {
            // Arrange
            _vm.PendingOperation = true;

            var serverModels = new ObservableCollection<ServerModel>(new List<ServerModel> { new ServerModel() });

            // Act
            var result = await _vm.PerformOperationAsync(true, serverModels);

            // Assert
            Assert.False(result);
            _messageBoxService.Verify(i => i.ShowMessageBoxAsync("Info", "Whoa! There's already a pending operation. Please wait...", Icon.Setting), Times.Once());
        }

        [Fact]
        public async Task Test_PerformOperationAsync_Blocking_Success()
        {
            // Arrange
            ObservableCollection<ServerModel> serverModels = [new ServerModel(), new ServerModel()];

            _systemFirewallService.Setup(i => i.BlockServersAsync(serverModels)).Returns(Task.CompletedTask);

            // Act
            var result = await _vm.PerformOperationAsync(true, serverModels);

            Assert.True(result);
            foreach (var srv in serverModels)
            {
                Assert.Empty(srv.Ping);
                Assert.Equal("❌", srv.Status);
            }
        }

        [Fact]
        public async Task Test_PerformOperationAsync_Unblocking_Success()
        {
            // Arrange
            ServerData serverData = new()
            {
                ClusteredServers = ServerModelFactory.Create(3),
                UnclusteredServers = ServerModelFactory.CreateWithCluster(3),
            };
            ObservableCollection<ServerModel> serverModels = new (serverData.ClusteredServers);

            _systemFirewallService.Setup(i => i.UnblockServersAsync(serverModels)).Returns(Task.CompletedTask);

            // Act
            var result = await _vm.PerformOperationAsync(false, serverModels);

            // Assert
            Assert.True(result);
            foreach (var srv in serverModels)
            {
                Assert.Contains("ms", srv.Ping);
                Assert.Equal("✅", srv.Status);
            }
        }

        [Fact]
        public void Test_GetServerDataService()
        {
            var service = _vm.GetServerDataService();
            Assert.Same(_serverDataService.Object, service);
        }

        [Fact]
        public async Task Test_SearchText_FilteredServerModels()
        {
            // Arrange – add servers with known names/descriptions
            ServerModel s1 = new() { Name = "Alpha", Description = "Desc Alpha" };
            ServerModel s2 = new() { Name = "Beta", Description = "Desc Beta" };
            ServerModel s3 = new() { Name = "Gamma", Description = "Gamma Zone" };
            _vm.ServerModels.Clear();
            _vm.ServerModels.AddRange(new List<ServerModel> { s1, s2, s3 });

            // Act – set search text to match 'alpha'
            _vm.SearchText = "alpha"; // triggers OnSearchTextChanged and raises property changed for FilteredServerModels
            var filtered = _vm.FilteredServerModels;

            Assert.Single(filtered);
            Assert.Contains(s1, filtered);
        }

        public object reflectGetField(object obj, string propertyName)
        {
            FieldInfo prop = obj.GetType().GetField(
                propertyName,
                BindingFlags.Instance | BindingFlags.NonPublic
                );

            return prop?.GetValue(obj);
        }
    }
}

