using System;
using System.IO;

namespace ServerPickerX.Services.Loggers
{
    public class FileLoggerService : ILoggerService
    {
        private readonly string _logFilePath;

        public FileLoggerService()
        {
            _logFilePath = AppDomain.CurrentDomain.BaseDirectory + "server_picker_x_log.txt";
        }

        public void LogError(string message, string? details = null)
        {
            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {message}";
            
            if (!string.IsNullOrEmpty(details))
            {
                logMessage += $" | Details: {details}";
            }

            File.AppendAllTextAsync(_logFilePath, logMessage + Environment.NewLine);
        }

        public void LogInfo(string message)
        {
            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] INFO: {message}";

            File.AppendAllTextAsync(_logFilePath, logMessage + Environment.NewLine);
        }

        public void LogWarning(string message)
        {
            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] WARNING: {message}";

            File.AppendAllTextAsync(_logFilePath, logMessage + Environment.NewLine);
        }
    }
}