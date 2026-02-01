using System;
using System.Diagnostics;
using System.Text;

namespace ServerPickerX.Helpers
{
    public class ProcessHelper
    {
        public static Process CreateProcess(string filename = "")
        {
            Process process = new();

            process.StartInfo.FileName = filename;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            return process;
        }

        public static void CreateProcessFromUrl(string url)
        {
            if (OperatingSystem.IsWindows())
            {
                using var proc = new Process { StartInfo = { UseShellExecute = true, FileName = url } };
                proc.Start();
            }
            else if (OperatingSystem.IsLinux())
            {
                Process.Start("x-www-browser", url);
            }
            else
            {
                Process.Start("open", url);
            }
        }
    }
}
