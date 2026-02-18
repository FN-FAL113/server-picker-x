using System;
using System.Linq;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using ServerPickerX.Services.Loggers;
using ServerPickerX.Services.MessageBoxes;
using ServerPickerX.Services.Processes;
using ServerPickerX.Services.Servers;
using ServerPickerX.Services.SystemFirewalls;
using ServerPickerX.Services.Versions;
using ServerPickerX.Settings;
using ServerPickerX.Settings;
using ServerPickerX.Settings;
using ServerPickerX.ViewModels;
using ServerPickerX.Views;

namespace ServerPickerX
{
    public partial class App : Application
    {
        // Singleton service container, access services across the app on execution lifetime
        public static IServiceProvider ServiceProvider { get; private set; }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddSingleton<ILoggerService, FileLoggerService>();
            serviceCollection.AddSingleton<IMessageBoxService, MessageBoxService>();
            serviceCollection.AddSingleton<IProcessService, ProcessService>();
            serviceCollection.AddSingleton<VersionService>();
            serviceCollection.AddSingleton<JsonSetting>();
            serviceCollection.AddSingleton<HttpClient>();

            serviceCollection.AddTransient<CS2ServerDataService>();
            serviceCollection.AddTransient<DeadLockServerDataService>();
            serviceCollection.AddTransient<IServerDataService>(serviceProvider =>
            {
                JsonSetting jsonSetting = serviceProvider.GetRequiredService<JsonSetting>();

                if (jsonSetting.game_mode == GameModes.CounterStrike2)
                {
                    return serviceProvider.GetRequiredService<CS2ServerDataService>();
                }
                else if (jsonSetting.game_mode == GameModes.Deadlock)
                {
                    return serviceProvider.GetRequiredService<DeadLockServerDataService>();
                }
                else
                {
                    throw new NotSupportedException("Server data services are only available for CS2 and Deadlock");
                }
            });
            serviceCollection.AddTransient<WindowsFirewallService>();
            serviceCollection.AddTransient<LinuxFirewallService>();
            serviceCollection.AddTransient<ISystemFirewallService>(serviceProvider =>
            {
                if (OperatingSystem.IsWindows())
                {
                    return serviceProvider.GetRequiredService<WindowsFirewallService>();
                }
                else if (OperatingSystem.IsLinux())
                {
                    return serviceProvider.GetRequiredService<LinuxFirewallService>();
                }
                else
                {
                    throw new PlatformNotSupportedException("Firewall services are only available for Windows and Linux");
                }
            });
            serviceCollection.AddTransient<MainWindowViewModel>();
            serviceCollection.AddTransient<SettingsWindowViewModel>();

            ServiceProvider = serviceCollection.BuildServiceProvider();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
                // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
                DisableAvaloniaDataAnnotationValidation();
                desktop.MainWindow = new MainWindow();
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            // Get an array of plugins to remove
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            // remove each entry found
            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }
    }
}