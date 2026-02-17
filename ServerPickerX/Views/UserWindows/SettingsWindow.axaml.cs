using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ServerPickerX.Settings;
using ServerPickerX.Services;
using ServerPickerX.ViewModels;
using System.Reflection;
using System.Threading.Tasks;
using ServerPickerX.Services.Loggers;
using ServerPickerX.Services.MessageBoxes;
using ServerPickerX.Services.Processes;
using ServerPickerX.Views;
using Microsoft.Extensions.DependencyInjection;
using ServerPickerX.Services.SystemFirewalls;

namespace ServerPickerX;

public partial class SettingsWindow : Window
{
    private readonly JsonSetting _jsonSetting;
    
    public SettingsWindow()
    {
        InitializeComponent();

        _jsonSetting = App.ServiceProvider.GetRequiredService<JsonSetting>();
    }

    private async void Window_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await _jsonSetting.LoadSettingsAsync();

        DataContext = App.ServiceProvider.GetRequiredService<SettingsWindowViewModel>();

        VersionTextBlock.Text = "Version: " + Assembly.GetEntryAssembly().GetName().Version.ToString(3);
    }

    private void TitleBar_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        // Prevent other mouse event listeners from being triggered
        e.Handled = true;

        var parentWindow = TopLevel.GetTopLevel(this) as Window;

        parentWindow?.BeginMoveDrag(e);
    }
}