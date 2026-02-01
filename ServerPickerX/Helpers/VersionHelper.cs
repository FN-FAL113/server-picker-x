using ServerPickerX.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace ServerPickerX.Helpers
{
    public class VersionHelper
    {
        public async static Task CheckVersion()
        {
            if (!MainWindow.jsonSettings.version_check_on_startup)
            {
                return;
            }

            using HttpClient httpClient = new();

            httpClient.DefaultRequestHeaders.Add("User-Agent", "server-picker-x");

            try
            {
                Stream res = await httpClient.GetStreamAsync("https://api.github.com/repositories/1141835010/releases");

                if (res == null) {
                    throw new Exception(
                        "Failed to check for newer app version!" + Environment.NewLine + Environment.NewLine +
                        "- Verify your internet connection or firewall are working and enabled" + Environment.NewLine +
                        "- Make sure to run the app as admin or with sudo level execution"
                    );
                }

                JsonArray jsonArray = (JsonArray)await JsonArray.ParseAsync(res);

                if (jsonArray?[0]?["tag_name"] == null)
                {
                    return;
                }

                string assemblyVersion = Assembly.GetEntryAssembly().GetName().Version.ToString(3);

                // version is up to date
                if(assemblyVersion == jsonArray[0]["tag_name"].ToString().Split("v")[1])
                {
                    return;
                }

                // prompt user to visit gh releases page for newer version
                await MessageBoxHelper.ShowMessageBoxWithLink(
                        "Version Check", 
                        "New version available! Go to releases?",
                        "https://github.com/FN-FAL113/cs2-server-picker/releases"
                    );
            }
            catch (Exception ex) {
                await MessageBoxHelper.ShowMessageBox("Error", "An error has occured while checking for newer version");

                await LogHelper.LogErrorToFile(ex.Message, "An error has occured while checking for newer version");
            }
        }
    }
}
