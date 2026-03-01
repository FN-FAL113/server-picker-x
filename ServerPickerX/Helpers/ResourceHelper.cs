using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Reflection;

namespace ServerPickerX.Helpers
{
    public class ResourceHelper
    {
        public static Bitmap LoadImageFromResource(string path)
        {
            return new Bitmap(AssetLoader.Open(CreateResourceUriFromPath(path)));
        }

        // For avalonia resources, embedded resources may have different URI
        public static Uri CreateResourceUriFromPath(string path)
        {
            var assemblyName = Assembly.GetEntryAssembly()?.GetName().Name;

            // Use actual assembly name for designer previewer since it loads in a different context
            if (assemblyName == "Avalonia.Designer.HostApp")
            {
                assemblyName = "ServerPickerX";
            }

            return new Uri($"avares://{assemblyName}/{path.TrimStart('/')}");
        }
    }
}
