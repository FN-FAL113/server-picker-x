using System;
using System.IO;
using System.Threading.Tasks;

namespace ServerPickerX.Services.Loggers
{
    public class FileLoggerService : ILoggerService
    {
        private readonly string _logFilePath = AppDomain.CurrentDomain.BaseDirectory + "server_picker_x_log.txt";

        public async Task LogErrorAsync(string message, string? details = null)
        {
            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {message}";

            if (!string.IsNullOrEmpty(details))
            {
                logMessage += $" | Details: {details}";
            }

            await File.AppendAllTextAsync(_logFilePath, logMessage + Environment.NewLine);
        }

        public async Task LogInfoAsync(string message)
        {
            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] INFO: {message}";

            await File.AppendAllTextAsync(_logFilePath, logMessage + Environment.NewLine);
        }

        public async Task LogWarningAsync(string message)
        {
            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] WARNING: {message}";

            await File.AppendAllTextAsync(_logFilePath, logMessage + Environment.NewLine);
        }
    }
}