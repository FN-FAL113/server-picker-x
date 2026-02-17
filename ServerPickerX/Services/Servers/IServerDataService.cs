using ServerPickerX.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ServerPickerX.Services.Servers
{
    public interface IServerDataService
    {
        Task LoadServersAsync();
        string GetCurrentRevision();
        ServerData GetServerData();
        List<string> GetClusterKeywords();
    } 
}