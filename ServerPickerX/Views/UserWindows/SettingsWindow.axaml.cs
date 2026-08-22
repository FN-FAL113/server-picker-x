using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using ServerPickerX.Helpers;
using ServerPickerX.Services.DependencyInjection;
using ServerPickerX.Services.Localizations;
using ServerPickerX.Services.Themes;
using ServerPickerX.Settings;
using ServerPickerX.ViewModels;
using System;
using System.Reflection;
using System.Threading;

namespace ServerPickerX;

public partial class SettingsWindow : Window
{
    private readonly JsonSetting _jsonSetting;
    private readonly ILocalizationService _localizationService;

    // Parameterless constructor, allows design previewer to create its own instance since it doesn't support DI
    public SettingsWindow()
    {
        InitializeComponent();

        _jsonSetting = ServiceLocator.GetRequiredService<JsonSetting>();
        _localizationService = ServiceLocator.GetRequiredService<ILocalizationService>();
    }

    // DI constructor, allows inversion of control and unit tests mocking
    public SettingsWindow(
        JsonSetting jsonSetting,
        ILocalizationService localizationService
        )
    {
        InitializeComponent();

        _jsonSetting = jsonSetting;
        _localizationService = localizationService;
    }

    private async void Window_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Set data context and configure UI controls
        await _jsonSetting.LoadSettingsAsync();

        DataContext = ServiceLocator.GetRequiredService<SettingsWindowViewModel>();

        VersionTextBlock.Text = "Version: " + Assembly.GetEntryAssembly()!.GetName().Version!.ToString(3);

        LanguageComboBox.SelectionChanged -= LanguageComboBox_SelectionChanged;
        LanguageComboBox.SelectedValue = _jsonSetting.language;
        LanguageComboBox.SelectionChanged += LanguageComboBox_SelectionChanged;

        ThemeComboBox.SelectionChanged -= ThemeComboBox_SelectionChanged;
        ThemeComboBox.SelectedIndex = GetThemeIndex(_jsonSetting.theme);
        ThemeComboBox.SelectionChanged += ThemeComboBox_SelectionChanged;
    }

    private async void ThemeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ThemeComboBox is null) return;

        // Items are ordered System, Light, Dark so the labels stay translatable
        string selectedTheme = ThemeComboBox.SelectedIndex switch
        {
            1 => ThemeService.LightTheme,
            2 => ThemeService.DarkTheme,
            _ => ThemeService.SystemTheme,
        };

        ThemeService.Apply(selectedTheme);

        FooterButtons.Instance?.RefreshThemeButton(selectedTheme);

        await _jsonSetting.SetThemeAsync(selectedTheme);
    }

    private static int GetThemeIndex(string? theme)
    {
        if (ThemeService.LightTheme.Equals(theme, StringComparison.OrdinalIgnoreCase)) return 1;

        if (ThemeService.DarkTheme.Equals(theme, StringComparison.OrdinalIgnoreCase)) return 2;

        return 0;
    }

    private void TitleBar_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        // Prevent other mouse event listeners from being triggered
        e.Handled = true;

        var parentWindow = TopLevel.GetTopLevel(this) as Window;

        parentWindow?.BeginMoveDrag(e);
    }

    private async void LanguageComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (LanguageComboBox is null || LanguageComboBox.SelectedItem is null) return;

        // Set language using combo box selection, this will trigger UI updates immediately
        var selectedLanguage = (string)LanguageComboBox.SelectedItem;
        var language = selectedLanguage.Replace(" ", "").Split("|")[1];

        await _jsonSetting.SetLanguageAsync(selectedLanguage);

        await _localizationService.SetLanguage(language);
    }
}