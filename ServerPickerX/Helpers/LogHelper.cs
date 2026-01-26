using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ServerPickerX.Helpers
{
    public class LogHelper
    {
       public static void LogErrorToFile(string exception, string sourceOfError, string? fileName = "")
        {
            File.AppendAllText(AppDomain.CurrentDomain.BaseDirectory +
                          (String.IsNullOrEmpty(fileName) ? DateTimeOffset.Now.ToUnixTimeSeconds().ToString() : fileName) + ".txt",
                          sourceOfError + Environment.NewLine + exception);
        }
    }
}
