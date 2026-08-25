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

            object platformOptions = OperatingSystem.IsWindows() ?
                new Win32PlatformOptions
                {
                    // Prioritize Software
                    RenderingMode = ResolveWindowsRenderMode()
                } :
                new X11PlatformOptions
                {
                    // Prioritize Software
                    RenderingMode = ResolveLinuxRenderMode()
                };

            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .With(platformOptions)
                #if DEBUG
                .WithDeveloperTools()
                #endif
                .WithInterFont()
                .LogToTrace();
        }

        [RequiresUnreferencedCode("ResolveWindowsRenderMode method: Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(Stream, JsonSerializerOptions)")]
        private static Win32RenderingMode[] ResolveWindowsRenderMode()
        {
            Win32RenderingMode[] defaultRenderMode = [Win32RenderingMode.Software];

            try
            {
                string jsonFilePath = (new JsonSetting()).jsonFilePath;

                // create local json settings if not exists with serialized object properties
                if (!File.Exists(jsonFilePath))
                {
                    return defaultRenderMode;
                }

                using FileStream settingsFile = File.OpenRead(jsonFilePath);

                JsonSetting? localSettings = JsonSerializer.Deserialize<JsonSetting>(settingsFile);

                if (localSettings is null)
                {
                    return defaultRenderMode;
                }

                return [RenderModes.ResolveWindowsRenderMode(localSettings.render_mode)];
            } catch
            {
                return defaultRenderMode;
            }
        }

        [RequiresUnreferencedCode("ResolveLinuxRenderMode method: Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(Stream, JsonSerializerOptions)")]
        private static X11RenderingMode[] ResolveLinuxRenderMode()
        {
            X11RenderingMode[] defaultRenderMode = [X11RenderingMode.Software];

            try
            {
                string jsonFilePath = (new JsonSetting()).jsonFilePath;

                // create local json settings if not exists with serialized object properties
                if (!File.Exists(jsonFilePath))
                {
                    return defaultRenderMode;
                }

                using FileStream settingsFile = File.OpenRead(jsonFilePath);

                JsonSetting? localSettings = JsonSerializer.Deserialize<JsonSetting>(settingsFile);

                if (localSettings is null)
                {
                    return defaultRenderMode;
                }

                return [RenderModes.ResolveLinuxRenderMode(localSettings.render_mode)];
            }
            catch
            {
                return defaultRenderMode;
            }
        }
    }
}
