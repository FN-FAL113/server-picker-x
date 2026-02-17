namespace ServerPickerX.Services.Loggers
{
    public interface ILoggerService
    {
        void LogError(string message, string? details = null);
        void LogInfo(string message);
        void LogWarning(string message);
    }
}