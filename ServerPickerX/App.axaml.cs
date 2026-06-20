using System;
using System.Linq;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
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

            // Register a provider that loads definitions once and expose it as a singleton
            serviceCollection.AddSingleton<ServerDefinitionProvider>();

            // Register a factory for IServerDataService using JSON server definitions
            serviceCollection.AddTransient<IServerDataService>(serviceProvider =>
            {
                ILoggerService loggerService = serviceProvider.GetRequiredService<ILoggerService>();
                JsonSetting jsonSetting = serviceProvider.GetRequiredService<JsonSetting>();

                try
                {
                    // Get server definition by current game mode that contains app related metadata
                    var serverDefinitionProvider = serviceProvider.GetRequiredService<ServerDefinitionProvider>();
                    var serverDefinition = serverDefinitionProvider.GetServerDefinitionByGameMode(jsonSetting.game_mode);

                    if (serverDefinition != null)
                    {
                        // ActivatorUtilities will instantiate a given type and injects dependencies from existing DI container
                        // while missing dependencies are supplied as manual argument (serverDefinition)
                        IServerDataService? obj = ActivatorUtilities.CreateInstance<ConfiguredServerDataService>(serviceProvider, serverDefinition);

                        if (obj != null) return obj;
                    }

                    throw new InvalidOperationException("Failed to register service [IServerDataService]");
                }
                catch (InvalidOperationException ex) 
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
                // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugin
                desktop.MainWindow = new MainWindow();
            }

            base.OnFrameworkInitializationCompleted();
        }

    }
}
