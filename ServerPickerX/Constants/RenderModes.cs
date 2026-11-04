using Avalonia;
using System;
using System.Collections.Generic;

namespace ServerPickerX.Constants
{
    public static class RenderModes
    {
        public const string Software = "Software (CPU)";
        public const string Vulkan = "Vulkan (GPU)";
        public const string AngleEgl = "AngleEgl (GPU)";
        public const string Wgl = "Wgl (GPU)";
        public const string Egl = "Egl (GPU)";
        public const string Glx = "Glx (GPU)";

        // Read‑only list used as ItemsSource for the Render Modes ComboBox
        public static readonly IReadOnlyList<string> AllWindows = [
            Software,
            Vulkan,
            AngleEgl,
            Wgl
        ];

        public static readonly IReadOnlyList<string> AllLinux = [
            Software,
            Vulkan,
            Egl,
            Glx
        ];

        public static AppBuilder ApplyRenderMode(AppBuilder appBuilder, string? renderMode)
        {
            // With<T> registers options under the compile-time type T, so each branch must pass
            // its concrete options type. Passing them as `object` makes Avalonia silently ignore them.
            return OperatingSystem.IsWindows()
                ? appBuilder.With(CreateWindowsOptions(renderMode))
                : appBuilder.With(CreateLinuxOptions(renderMode));
        }

        public static Win32PlatformOptions CreateWindowsOptions(string? renderMode)
        {
            return new Win32PlatformOptions
            {
                // Software is the fallback if the GPU mode is unsupported, else Avalonia throws on startup
                RenderingMode = [ResolveWindowsRenderMode(renderMode), Win32RenderingMode.Software],
                // Excludes the default WinUIComposition: its render thread parks while holding
                // a lock that closing a window needs, which can freeze the app
                CompositionMode = [Win32CompositionMode.DirectComposition, Win32CompositionMode.RedirectionSurface],
            };
        }

        public static X11PlatformOptions CreateLinuxOptions(string? renderMode)
        {
            return new X11PlatformOptions
            {
                // Software is the fallback if the GPU mode is unsupported, else Avalonia throws on startup
                RenderingMode = [ResolveLinuxRenderMode(renderMode), X11RenderingMode.Software],
            };
        }

        public static Win32RenderingMode ResolveWindowsRenderMode(string? renderMode)
        {
            switch(renderMode)
            {
                case Vulkan:
                    return Win32RenderingMode.Vulkan;
                case AngleEgl:
                    return Win32RenderingMode.AngleEgl;
                case Wgl:
                    return Win32RenderingMode.Wgl;
                default:
                    return Win32RenderingMode.Software;
            };
        }

        public static X11RenderingMode ResolveLinuxRenderMode(string? renderMode)
        {
            switch (renderMode)
            {
                case Vulkan:
                    return X11RenderingMode.Vulkan;
                case Egl:
                    return X11RenderingMode.Egl;
                case Glx:
                    return X11RenderingMode.Glx;
                default:
                    return X11RenderingMode.Software;
            }
            ;
        }
    }
}
