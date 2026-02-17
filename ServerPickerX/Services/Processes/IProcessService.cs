using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ServerPickerX.Services.Processes
{
    public interface IProcessService
    {
        Process CreateProcess(string filename = "");
        Task OpenUrl(string url);
    }
}