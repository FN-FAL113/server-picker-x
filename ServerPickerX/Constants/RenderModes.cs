using Avalonia;
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

        public static Win32RenderingMode ResolveWindowsRenderMode(string renderMode)
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

        public static X11RenderingMode ResolveLinuxRenderMode(string renderMode)
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
