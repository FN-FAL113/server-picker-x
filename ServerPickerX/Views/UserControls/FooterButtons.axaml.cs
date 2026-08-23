using Avalonia;
using Avalonia.Controls;
using ServerPickerX.Services.DependencyInjection;
using ServerPickerX.Services.Processes;
using ServerPickerX.Services.Themes;
using ServerPickerX.Settings;
using ServerPickerX.Views;

namespace ServerPickerX;

public partial class FooterButtons : UserControl
{
    // Bound by the theme button so its glyph tracks the current setting
    public static readonly StyledProperty<string> ThemeIconProperty =
        AvaloniaProperty.Register<FooterButtons, string>(nameof(ThemeIcon), "fa-desktop");

    public string ThemeIcon
    {
        get => GetValue(ThemeIconProperty);
        set => SetValue(ThemeIconProperty, value);
    }

    // Lets the main window refresh the glyph after settings.json is read
    public static FooterButtons? Instance { get; private set; }

    private readonly JsonSetting _jsonSetting;

    public FooterButtons()
    {
        InitializeComponent();

        Instance = this;

        _jsonSetting = ServiceLocator.GetRequiredService<JsonSetting>();

        RefreshThemeButton();

        // Attach tooltips to the footer buttons
        ToolTip.SetTip(PaypalBtn, "Donate via PayPal");
        ToolTip.SetTip(GithubBtn, "Go to GitHub repository");
        ToolTip.SetTip(SettingsBtn, "Open settings");
    }

    private async void ThemeBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string nextTheme = ThemeService.Next(_jsonSetting.theme);

        ThemeService.Apply(nextTheme);

        RefreshThemeButton(nextTheme);

        await _jsonSetting.SetThemeAsync(nextTheme);
    }

    public void RefreshThemeButton(string? theme = null)
    {
        string currentTheme = theme ?? _jsonSetting.theme;

        ThemeIcon = ThemeService.GetIcon(currentTheme);

        ToolTip.SetTip(ThemeBtn, $"Theme: {currentTheme}");
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