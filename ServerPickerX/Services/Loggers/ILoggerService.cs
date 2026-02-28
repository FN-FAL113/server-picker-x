using System.Threading.Tasks;

namespace ServerPickerX.Services.Loggers
{
    public interface ILoggerService
    {
        Task LogErrorAsync(string message, string? details = null);
        Task LogInfoAsync(string message);
        Task LogWarningAsync(string message);
    }
}