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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ServerPickerX.Models;
using ServerPickerX.Services.Localizations;

namespace ServerPickerX.Tests.ViewModels
{
    public class MainWindowViewModelTest
    {
        private readonly Mock<ILoggerService> _loggerService;
        private readonly Mock<IMessageBoxService> _messageBoxService;
        private readonly Mock<ILocalizationService> _localizationService;
        private readonly Mock<IServerDataService> _serverDataService;
        private readonly Mock<ISystemFirewallService> _systemFirewallService;
        private readonly Mock<JsonSetting> _jsonSetting;
        private readonly MainWindowViewModel _vm;

        // This constructor acts as the setup function
        public MainWindowViewModelTest()
        {
            _loggerService = new Mock<ILoggerService>();
            _messageBoxService = new Mock<IMessageBoxService>();
            _localizationService = new Mock<ILocalizationService>();
            _serverDataService = new Mock<IServerDataService>();
            _systemFirewallService = new Mock<ISystemFirewallService>();
            _jsonSetting = new Mock<JsonSetting>();

            _systemFirewallService
                .Setup(s => s.SyncBlockedStateAsync(It.IsAny<IReadOnlyList<ServerModel>>()))
                .Returns(Task.CompletedTask);

            _vm = new MainWindowViewModel(
                _loggerService.Object,
                _messageBoxService.Object,
                _localizationService.Object,
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
            _serverDataService.Setup(i => i.LoadServersAsync()).Returns(Task.FromResult(true));
            _serverDataService.Setup(i => i.GetServerData()).Returns(serverData);

            // Act
            await _vm.LoadServersAsync();

            // Assert
            Assert.NotEmpty(_vm.ServerModels);
            Assert.NotEmpty(_vm.FilteredServerModels);
            Assert.True(_vm.ServerModelsInitialized);
        }

        [Fact]
        public async Task Test_LoadServers_CollectionIsEmptyAndNotInitialized()
        {
            // Arrange
            // mock methods with fake results
            _serverDataService.Setup(i => i.LoadServersAsync()).Returns(Task.FromResult(false));

            // Act
            await _vm.LoadServersAsync();

            // Assert
            Assert.Empty(_vm.ServerModels);
            Assert.Empty(_vm.FilteredServerModels);
            Assert.False(_vm.ServerModelsInitialized);
        }

        [Fact]
        public async Task Test_ClusterUnclusterServers_UpdatesServerModelsBasedOnJsonSetting()
        {
            // Arrange
            // Test servers are unclustered by default
            JsonSetting setting = (JsonSetting)ReflectGetField(_vm, "_jsonSetting");
            ServerData serverData = new()
            {
                ClusteredServers = ServerModelFactory.Create(3),
                UnclusteredServers = ServerModelFactory.CreateWithCluster(3),
            };

            _vm.ServersLoaded = true;

            // mock method with fake data
            _serverDataService.Setup(i => i.GetServerData()).Returns(serverData);

            // Act
            await _vm.ClusterUnclusterServersAsync();

            // Assert
            Assert.NotEmpty(_vm.ServerModels);
            Assert.NotEmpty(_vm.FilteredServerModels);
            Assert.False(setting.is_clustered);
            Assert.Equal(serverData.UnclusteredServers, _vm.ServerModels);

            // Arrange
            // Test servers are clustered
            _jsonSetting.SetupGet(i => i.is_clustered).Returns(true);

            // Act
            await _vm.ClusterUnclusterServersAsync();

            // Assert
            Assert.NotEmpty(_vm.ServerModels);
            Assert.NotEmpty(_vm.FilteredServerModels);
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
            _serverDataService.Setup(i => i.LoadServersAsync()).Returns(Task.FromResult(true));
            _serverDataService.Setup(i => i.GetServerData()).Returns(serverData);

            // Act
            await _vm.LoadServersAsync();
            _vm.PingServers(_vm.ServerModels);

            await Task.Delay(1200); // Ping uses up to ~800ms per relay; async void is not awaited

            // Assert
            Assert.NotEmpty(_vm.ServerModels);
            AssertPingSettled(_vm.ServerModels[0]);
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
            _serverDataService.Setup(i => i.LoadServersAsync()).Returns(Task.FromResult(true));
            _serverDataService.Setup(i => i.GetServerData()).Returns(serverData);

            // Act
            await _vm.LoadServersAsync();
            _vm.SelectedDataGridServerModel = _vm.ServerModels[0];
            _vm.PingSelectedServer();

            await Task.Delay(1200); // Ping uses up to ~800ms per relay; async void is not awaited

            // Assert
            Assert.NotEmpty(_vm.ServerModels);
            AssertPingSettled(_vm.SelectedDataGridServerModel!);
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
            _serverDataService.Setup(i => i.LoadServersAsync()).Returns(Task.FromResult(true));
            _serverDataService.Setup(i => i.GetServerData()).Returns(serverData);

            // Act
            await _vm.LoadServersAsync();

            _systemFirewallService.Setup(i => i.BlockServersAsync(_vm.ServerModels))
                .Callback((ObservableCollection<ServerModel> serverModels) => {
                    // Clear model relays to simulate a failed ICMP ping
                    foreach (var server in serverModels)
                    {
                        server.RelayModels.Clear();
                    }
                })
                .Returns(Task.CompletedTask);

            var result = await _vm.BlockAllAsync();

            await Task.Delay(70);

            // Assert
            // Verify method is invoked
            _systemFirewallService.Verify(i => i.BlockServersAsync(_vm.ServerModels), Times.Once);
            Assert.NotEmpty(_vm.ServerModels);
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
            _serverDataService.Setup(i => i.LoadServersAsync()).Returns(Task.FromResult(true));
            _serverDataService.Setup(i => i.GetServerData()).Returns(serverData);

            // Act
            await _vm.LoadServersAsync();

            ObservableCollection<ServerModel> selectedServers = [_vm.ServerModels[0], _vm.ServerModels[2]];

            _systemFirewallService.Setup(i => i.BlockServersAsync(It.IsAny<ObservableCollection<ServerModel>>()))
                .Callback((ObservableCollection<ServerModel> serverModels) => {
                    // Clear model relays to simulate a failed ICMP ping
                    foreach (var server in serverModels)
                    {
                        server.RelayModels.Clear();
                    }
                })
                .Returns(Task.CompletedTask);

            var result = await _vm.BlockSelectedAsync(selectedServers);

            await Task.Delay(1200); // Non-selected rows still run async PingServer after PerformOperationAsync

            // Assert
            Assert.NotEmpty(_vm.ServerModels);
            _systemFirewallService.Verify(i => i.BlockServersAsync(It.IsAny<ObservableCollection<ServerModel>>()), Times.Once);
            Assert.True(result);
            foreach (var item in _vm.ServerModels.Select((value, index) => new { value, index }))
            {
                if (item.index is 0 or 2)
                {
                    Assert.Empty(item.value.Ping);
                    Assert.Equal("❌", item.value.Status);
                } else
                {
                    AssertPingSettled(item.value);
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
            // Verify method is invoked
            _messageBoxService.Verify(i =>
                i.ShowMessageBoxAsync(
                    _localizationService.Object.GetLocaleValue("MessageBoxInfoTitle"),
                    _localizationService.Object.GetLocaleValue("SelectOneServerToBlockDialogue"),
                    Icon.Info
                ),
                Times.Once()
            );
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
            _serverDataService.Setup(i => i.LoadServersAsync()).Returns(Task.FromResult(true));
            _serverDataService.Setup(i => i.GetServerData()).Returns(serverData);

            // Act
            await _vm.LoadServersAsync();

            _systemFirewallService.Setup(i => i.UnblockServersAsync(_vm.ServerModels)).Returns(Task.CompletedTask);

            var result = await _vm.UnblockAllAsync();

            await Task.Delay(1200); // ping operations are fire-and-forget (background tasks)

            // Assert
            Assert.True(result);
            foreach (var srv in _vm.ServerModels)
            {
                AssertPingSettled(srv);
            }
        }

        [Fact]
        public async Task Test_UnblockAllAsync_NoServers()
        {
            // Arrange – ServerModels is empty by default

            // Act
            var result = await _vm.UnblockAllAsync();

            // Assert
            // Verify method is not invoked
            _systemFirewallService.Verify(i => i.UnblockServersAsync(It.IsAny<ObservableCollection<ServerModel>>()), Times.Never());
            Assert.False(result);
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

            // Act
            List<ServerModel> selected = [serverData.ClusteredServers[0], serverData.ClusteredServers[2]];

            _systemFirewallService.Setup(i => i.UnblockServersAsync(It.IsAny<ObservableCollection<ServerModel>>()))
                .Returns(Task.CompletedTask);

            var result = await _vm.UnblockSelectedAsync(selected);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task Test_UnblockSelectedAsync_EmptySelection()
        {
            var emptyList = new List<ServerModel>();

            // Act
            var result = await _vm.UnblockSelectedAsync(emptyList);

            // Assert
            // Verify method is not invoked
            _systemFirewallService.Verify(i => i.UnblockServersAsync(It.IsAny<ObservableCollection<ServerModel>>()), Times.Never);
            _messageBoxService.Verify(i => 
                i.ShowMessageBoxAsync(
                    _localizationService.Object.GetLocaleValue("MessageBoxInfoTitle"),
                    _localizationService.Object.GetLocaleValue("SelectOneServerToUnblockDialogue"),
                    Icon.Info
                ), 
                Times.Once()
            );
            Assert.False(result);
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
            _messageBoxService.Verify(i => 
                i.ShowMessageBoxAsync(
                    _localizationService.Object.GetLocaleValue("MessageBoxInfoTitle"),
                    _localizationService.Object.GetLocaleValue("PendingOperationDialogue"),
                    Icon.Setting
                ), 
                Times.Once()
            );
        }

        [Fact]
        public async Task Test_PerformOperationAsync_Blocking_Success()
        {
            // Arrange
            ObservableCollection<ServerModel> serverModels = [new ServerModel(), new ServerModel()];

            _systemFirewallService.Setup(i => i.BlockServersAsync(serverModels)).Returns(Task.CompletedTask);

            // Act
            var result = await _vm.PerformOperationAsync(true, serverModels);

            await Task.Delay(70); // ping operations are fire-and-forget (background tasks)

            // Assert
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

            await Task.Delay(1200); // ping operations are fire-and-forget (background tasks)

            // Assert
            Assert.True(result);
            foreach (var srv in serverModels)
            {
                AssertPingSettled(srv);
            }
        }

        [Fact]
        public void Test_GetServerDataService()
        {
            // Arrange
            var service = _vm.GetServerDataService();

            // Act and Assert
            Assert.Same(_serverDataService.Object, service);
        }

        [Fact]
        public async Task Test_SearchText_FilteredServerModels()
        {
            // Arrange
            ServerModel s1 = new() { Name = "Alpha", Description = "Desc Alpha" };
            ServerModel s2 = new() { Name = "Beta", Description = "Desc Beta" };
            ServerModel s3 = new() { Name = "Gamma", Description = "Gamma Zone" };
            _vm.ServerModels.Clear();
            _vm.ServerModels.AddRange(new List<ServerModel> { s1, s2, s3 });

            // Act
            _vm.SearchText = "alpha";
            var filtered = _vm.FilteredServerModels;

            Assert.Single(filtered);
            Assert.Contains(s1, filtered);
        }

        private static void AssertPingSettled(ServerModel model)
        {
            Assert.NotEqual("Pinging server", model.Ping);
            if (model.Status == "✅")
            {
                Assert.Contains("ms", model.Ping ?? "");
            }
            else
            {
                Assert.Equal("❌", model.Status);
            }
        }

        public object ReflectGetField(object obj, string propertyName)
        {
            FieldInfo prop = obj.GetType().GetField(
                propertyName,
                BindingFlags.Instance | BindingFlags.NonPublic
                )!;

            return prop?.GetValue(obj);
        }
    }
}

