using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Reflection;

namespace ServerPickerX.Helpers
{
    public class ImageHelper
    {
        public static Bitmap LoadFromResource(string path)
        {
            Uri resourceUri;

            if (!path.StartsWith("avares://"))
            {
                var assemblyName = Assembly.GetEntryAssembly()?.GetName().Name;
                resourceUri = new Uri($"avares://{assemblyName}/{path.TrimStart('/')}");
            }
            else
            {
                resourceUri = new Uri(path);
            }

            return new Bitmap(AssetLoader.Open(resourceUri));
        }
    }
}
