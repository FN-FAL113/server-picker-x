using System;
using System.Diagnostics;
using System.Text;

namespace ServerPickerX.Helpers
{
    public class ProcessHelper
    {
        public static Process createProcess(string filename)
        {
            Process process = new();

            process.StartInfo.FileName = filename;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            return process;
        }
    }
}
