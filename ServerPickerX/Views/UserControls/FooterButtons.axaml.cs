using Avalonia;
using Avalonia.Controls;
using ServerPickerX.Helpers;
using ServerPickerX.Views;

namespace ServerPickerX;

public partial class FooterButtons : UserControl
{
    public FooterButtons()
    {
        InitializeComponent();
    }

    private void PaypalBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ProcessHelper.createProcessFromUrl("https://www.paypal.com/paypalme/fnfal113");
    }

    private void GithubBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ProcessHelper.createProcessFromUrl("https://github.com/FN-FAL113/server-picker-x");
    }

    private void SettingsBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SettingsWindow settingsWindow = new();

        settingsWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;

        settingsWindow.ShowDialog(MainWindow.Instance);
        settingsWindow.Activate();
    }
}