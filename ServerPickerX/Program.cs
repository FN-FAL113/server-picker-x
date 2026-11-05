using Avalonia;
using Optris.Icons.Avalonia;
using Optris.Icons.Avalonia.FontAwesome;
using Optris.Icons.Avalonia.MaterialDesign;
using ServerPickerX.Constants;
using ServerPickerX.Settings;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;

namespace ServerPickerX
{
    internal sealed class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args) => BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
        {
            IconProvider.Current
            .Register<FontAwesomeIconProvider>()
            .Register<MaterialDesignIconProvider>();

            AppBuilder appBuilder = AppBuilder.Configure<App>().UsePlatformDetect();

            return RenderModes.ApplyRenderMode(appBuilder, ReadSavedRenderMode())
                #if DEBUG
                .WithDeveloperTools()
                #endif
                .WithInterFont()
                .LogToTrace();
        }

        [RequiresUnreferencedCode("ReadSavedRenderMode method: Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(Stream, JsonSerializerOptions)")]
        private static string? ReadSavedRenderMode()
        {
            try
            {
                string jsonFilePath = (new JsonSetting()).jsonFilePath;

                if (!File.Exists(jsonFilePath))
                {
                    return null;
                }

                using FileStream settingsFile = File.OpenRead(jsonFilePath);

                return JsonSerializer.Deserialize<JsonSetting>(settingsFile)?.render_mode;
            }
            catch
            {
                return null;
            }
        }
    }
}
