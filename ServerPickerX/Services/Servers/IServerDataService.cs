using ServerPickerX.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ServerPickerX.Services.Servers
{
    public interface IServerDataService
    {
        Task<bool> LoadServersAsync();
        string GetFetchedRevision();
        ServerData GetServerData();
        List<string> GetClusterKeywords();
    }
}