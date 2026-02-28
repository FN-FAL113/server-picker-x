using System;
using System.Threading.Tasks;

namespace ServerPickerX.Services.Loggers
{
    public class ConsoleLoggerService : ILoggerService
    {
        public Task LogErrorAsync(string message, string? details = null)
        {
            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {message}";

            if (!string.IsNullOrEmpty(details))
            {
                logMessage += $" | Details: {details}";
            }

            System.Diagnostics.Debug.WriteLine(logMessage);

            return Task.CompletedTask;
        }

        public Task LogInfoAsync(string message)
        {
            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] INFO: {message}";

            System.Diagnostics.Debug.WriteLine(logMessage);

            return Task.CompletedTask;
        }

        public Task LogWarningAsync(string message)
        {
            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] WARNING: {message}";

            System.Diagnostics.Debug.WriteLine(logMessage);

            return Task.CompletedTask;
        }

    }
}