using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ServerPickerX.Helpers
{
    public class LogHelper
    {
       public static async Task LogErrorToFile(string exception, string sourceOfError, string? fileName = "")
        {
            await File.AppendAllTextAsync(AppDomain.CurrentDomain.BaseDirectory +
                          (String.IsNullOrEmpty(fileName) ? DateTimeOffset.Now.ToUnixTimeSeconds().ToString() : fileName) + ".txt",
                          sourceOfError + Environment.NewLine + exception);
        }
    }
}
