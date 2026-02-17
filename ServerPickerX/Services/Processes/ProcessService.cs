using Avalonia.Controls;
using ServerPickerX.Views;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ServerPickerX.Services.Processes
{
    public class ProcessService : IProcessService
    {
        public Process CreateProcess(string filename = "")
        {
            Process process = new();

            process.StartInfo.FileName = filename;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            return process;
        }

        public async Task OpenUrl(string url)
        {
            var topLevel = TopLevel.GetTopLevel(MainWindow.Instance);

            if (topLevel == null)
            {
                return;
            }

            await topLevel.Launcher.LaunchUriAsync(new Uri(url));
        }
    }
}