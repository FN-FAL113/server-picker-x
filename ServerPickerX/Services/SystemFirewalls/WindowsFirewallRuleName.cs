using ServerPickerX.Models;

namespace ServerPickerX.Services.SystemFirewalls
{
    public static class WindowsFirewallRuleName
    {
        public const string NamePrefix = "server_picker_x_";

        public static string ForDescription(string description) =>
            NamePrefix + description.Replace(" ", "");

        public static string ForServer(ServerModel server) =>
            ForDescription(server.Description);
    }
}
