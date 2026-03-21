using System.Diagnostics;
using System.Threading.Tasks;

namespace ServerPickerX.Services.Processes
{
    /// <summary>
    /// Avoids deadlock when stdout/stderr are redirected: the child blocks if the pipe fills
    /// while the parent waits for exit before reading. Reads must start before awaiting exit.
    /// </summary>
    internal static class RedirectedProcess
    {
        public static async Task<(string StandardOutput, string StandardError)> RunAsync(Process process)
        {
            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return (await stdoutTask, await stderrTask);
        }
    }
}
