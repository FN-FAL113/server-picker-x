using System;

namespace ServerPickerX.Services.Loggers
{
    public class ConsoleLoggerService : ILoggerService
    {
        public void LogError(string message, string? details = null)
        {
            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {message}";
            
            if (!string.IsNullOrEmpty(details))
            {
                logMessage += $" | Details: {details}";
            }

            System.Diagnostics.Debug.WriteLine(logMessage);
        }

        public void LogInfo(string message)
        {
            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] INFO: {message}";

            System.Diagnostics.Debug.WriteLine(logMessage);
        }

        public void LogWarning(string message)
        {
            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] WARNING: {message}";

            System.Diagnostics.Debug.WriteLine(logMessage);
        }

    }
}