using System;
using System.Linq;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using ServerPickerX.Constants;
using ServerPickerX.Services.Localizations;
using ServerPickerX.Services.Loggers;
using ServerPickerX.Services.MessageBoxes;
using ServerPickerX.Services.Processes;
using ServerPickerX.Services.Servers;
using ServerPickerX.Services.SystemFirewalls;
using ServerPickerX.Services.Versions;
using ServerPickerX.Services.DependencyInjection;
using ServerPickerX.Settings;
using ServerPickerX.ViewModels;
using ServerPickerX.Views;

namespace ServerPickerX
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

#pragma warning disable IL2026
        // Reflection is partially used here and might not be trim-compatible unless JsonSerializerIsReflectionEnabledByDefault is set to true in .csproj
        public override void OnFrameworkInitializationCompleted()
        {

            var serviceCollection = new ServiceCollection();

            // Register singleton services
            serviceCollection.AddSingleton<ILoggerService, FileLoggerService>();
            serviceCollection.AddSingleton<IMessageBoxService, MessageBoxService>();
            serviceCollection.AddSingleton<IProcessService, ProcessService>();
            serviceCollection.AddSingleton<ILocalizationService, LocalizationService>();
            serviceCollection.AddSingleton<IVersionService, VersionService>();
            serviceCollection.AddSingleton<JsonSetting>();
            serviceCollection.AddSingleton<HttpClient>();

            // Register concrete services and contionally provide these services through parent interface service resolver
            serviceCollection.AddTransient<CS2ServerDataService>();
            serviceCollection.AddTransient<CS2PerfectWorldServerDataService>();
            serviceCollection.AddTransient<DeadLockServerDataService>();
            serviceCollection.AddTransient<MarathonServerDataService>();
            serviceCollection.AddTransient<IServerDataService>(serviceProvider =>
            {
                JsonSetting jsonSetting = serviceProvider.GetRequiredService<JsonSetting>();
                ILoggerService loggerService = serviceProvider.GetRequiredService<ILoggerService>();

                try
                {
                    // Factory method may be suitable if more entries are added in the future
                    return jsonSetting.game_mode switch
                    {
                        GameModes.CounterStrike2 => serviceProvider.GetRequiredService<CS2ServerDataService>(),
                        GameModes.CounterStrike2PerfectWorld => serviceProvider.GetRequiredService<CS2PerfectWorldServerDataService>(),
                        GameModes.Deadlock => serviceProvider.GetRequiredService<DeadLockServerDataService>(),
                        GameModes.Marathon => serviceProvider.GetRequiredService<MarathonServerDataService>(),
                        _ => throw new NotSupportedException($"Unsupported game mode: {jsonSetting.game_mode}")
                    };
                } catch (NotSupportedException ex)
                {
                    loggerService.LogErrorAsync(ex.Message);

                    throw;
                }
                
            });
            serviceCollection.AddTransient<WindowsFirewallService>();
            serviceCollection.AddTransient<LinuxFirewallService>();
            serviceCollection.AddTransient<ISystemFirewallService>(serviceProvider =>
            {
                ILoggerService loggerService = serviceProvider.GetRequiredService<ILoggerService>();

                try
                {
                    if (OperatingSystem.IsWindows())
                    {
                        return serviceProvider.GetRequiredService<WindowsFirewallService>();
                    }
                    else if (OperatingSystem.IsLinux())
                    {
                        return serviceProvider.GetRequiredService<LinuxFirewallService>();
                    }

                    throw new PlatformNotSupportedException("Firewall services are only available for Windows and Linux");
                } catch (PlatformNotSupportedException ex)
                {
                    loggerService.LogErrorAsync(ex.Message);

                    throw;
                }
            });

            // Register view model services
            serviceCollection.AddTransient<MainWindowViewModel>();
            serviceCollection.AddTransient<SettingsWindowViewModel>();

            ServiceLocator.Initialize(serviceCollection.BuildServiceProvider());

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
                // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
                DisableAvaloniaDataAnnotationValidation();
                desktop.MainWindow = new MainWindow();
            }

            base.OnFrameworkInitializationCompleted();
        }

        // Reflection is partially used here and might not be trim-compatible unless JsonSerializerIsReflectionEnabledByDefault is set to true in .csproj
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