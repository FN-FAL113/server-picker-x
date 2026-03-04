using Avalonia;
using Avalonia.Controls;
using ServerPickerX.Services.DependencyInjection;
using ServerPickerX.Services.Processes;
using ServerPickerX.Views;

namespace ServerPickerX;

public partial class FooterButtons : UserControl
{
    public FooterButtons()
    {
        InitializeComponent();

        // Attach tooltips to the footer buttons
        ToolTip.SetTip(PaypalBtn, "Donate via PayPal");
        ToolTip.SetTip(GithubBtn, "Go to GitHub repository");
        ToolTip.SetTip(SettingsBtn, "Open settings");
    }

    private async void PaypalBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
await ServiceLocator
            .GetRequiredService<IProcessService>()
            .OpenUrl("https://www.paypal.com/paypalme/fnfal113");
    }

    private async void GithubBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
await ServiceLocator
            .GetRequiredService<IProcessService>()
            .OpenUrl("https://github.com/FN-FAL113/server-picker-x");
    }

    private void SettingsBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SettingsWindow settingsWindow = new()
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        settingsWindow.ShowDialog(MainWindow.Instance!);
        settingsWindow.Activate();
    }
}