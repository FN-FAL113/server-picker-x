using ServerPickerX.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ServerPickerX.Services.Servers
{
    public interface IServerDataService
    {
        Task<bool> LoadServersAsync();
        string GetCurrentRevision();
        ServerData GetServerData();
        List<string> GetClusterKeywords();
    }
}