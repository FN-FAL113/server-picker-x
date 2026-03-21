using ServerPickerX.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace ServerPickerX.Services.SystemFirewalls
{
    public interface ISystemFirewallService
    {
        Task BlockServersAsync(ObservableCollection<ServerModel> serverModels);
        Task UnblockServersAsync(ObservableCollection<ServerModel> serverModels);
        Task ResetFirewallAsync();
        Task SyncBlockedStateAsync(IReadOnlyList<ServerModel> servers);
    }
}