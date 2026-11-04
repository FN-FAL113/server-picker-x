using Avalonia;
using ServerPickerX.Constants;
using System.Reflection;

namespace ServerPickerX.Tests.Constants
{
    public class RenderModesTests
    {
        [Theory]
        [InlineData(RenderModes.Software, Win32RenderingMode.Software)]
        [InlineData(RenderModes.AngleEgl, Win32RenderingMode.AngleEgl)]
        [InlineData(RenderModes.Vulkan, Win32RenderingMode.Vulkan)]
        [InlineData(RenderModes.Wgl, Win32RenderingMode.Wgl)]
        [InlineData(null, Win32RenderingMode.Software)]
        [InlineData("unknown", Win32RenderingMode.Software)]
        public void CreateWindowsOptions_UsesSavedModeWithSoftwareFallback(string? renderMode, Win32RenderingMode expected)
        {
            Win32PlatformOptions options = RenderModes.CreateWindowsOptions(renderMode);

            Assert.Equal([expected, Win32RenderingMode.Software], options.RenderingMode);
        }

        [Fact]
        public void CreateWindowsOptions_ExcludesWinUIComposition()
        {
            Win32PlatformOptions options = RenderModes.CreateWindowsOptions(RenderModes.AngleEgl);

            Assert.DoesNotContain(Win32CompositionMode.WinUIComposition, options.CompositionMode!);
        }

        [Theory]
        [InlineData(RenderModes.Software, X11RenderingMode.Software)]
        [InlineData(RenderModes.Egl, X11RenderingMode.Egl)]
        [InlineData(RenderModes.Glx, X11RenderingMode.Glx)]
        [InlineData(RenderModes.Vulkan, X11RenderingMode.Vulkan)]
        [InlineData(null, X11RenderingMode.Software)]
        public void CreateLinuxOptions_UsesSavedModeWithSoftwareFallback(string? renderMode, X11RenderingMode expected)
        {
            X11PlatformOptions options = RenderModes.CreateLinuxOptions(renderMode);

            Assert.Equal([expected, X11RenderingMode.Software], options.RenderingMode);
        }

        // Guards against passing the options as `object`, which Avalonia silently ignores
        [Fact]
        public void ApplyRenderMode_RegistersOptionsUnderTheirConcreteType()
        {
            AppBuilder appBuilder = AppBuilder.Configure<Application>()
                .UseRuntimePlatformSubsystem(() => { }, "test")
                .UseTextShapingSubsystem(() => { }, "test")
                .UseWindowingSubsystem(() => { }, "test")
                .UseRenderingSubsystem(() => { }, "test");

            RenderModes.ApplyRenderMode(appBuilder, RenderModes.Software).SetupWithoutStarting();

            // AvaloniaLocator.Current is not public
            object locator = typeof(AvaloniaLocator)
                .GetProperty("Current", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!
                .GetValue(null)!;

            Type optionsType = OperatingSystem.IsWindows() ? typeof(Win32PlatformOptions) : typeof(X11PlatformOptions);

            object? options = typeof(AvaloniaLocator)
                .GetMethod("GetService", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, [typeof(Type)])!
                .Invoke(locator, [optionsType]);

            Assert.NotNull(options);
        }
    }
}
