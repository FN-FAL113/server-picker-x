using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using ServerPickerX.Constants;
using ServerPickerX.Helpers;
using ServerPickerX.Services.DependencyInjection;
using ServerPickerX.Services.Localizations;
using ServerPickerX.Services.MessageBoxes;
using ServerPickerX.Settings;
using ServerPickerX.ViewModels;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace ServerPickerX;

public partial class SettingsWindow : Window
{
    private readonly JsonSetting _jsonSetting;
    private readonly ILocalizationService _localizationService;
    private readonly IMessageBoxService _messageBoxService;

    // Parameterless constructor, allows design previewer to create its own instance since it doesn't support DI
    public SettingsWindow()
    {
        InitializeComponent();

        _jsonSetting = ServiceLocator.GetRequiredService<JsonSetting>();
        _localizationService = ServiceLocator.GetRequiredService<ILocalizationService>();
        _messageBoxService = ServiceLocator.GetRequiredService<IMessageBoxService>();
    }

    // DI constructor, allows inversion of control and unit tests mocking
    public SettingsWindow(
        JsonSetting jsonSetting,
        ILocalizationService localizationService,
        IMessageBoxService messageBoxService
        )
    {
        InitializeComponent();

        _jsonSetting = jsonSetting;
        _localizationService = localizationService;
        _messageBoxService = messageBoxService;
    }

    private async void Window_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Set data context and configure UI controls
        await _jsonSetting.LoadSettingsAsync();

        DataContext = ServiceLocator.GetRequiredService<SettingsWindowViewModel>();

        VersionTextBlock.Text = "Version: " + Assembly.GetEntryAssembly()!.GetName().Version!.ToString(3);

        RenderModeComboBox.ItemsSource = this.ResolveRenderModeComboBoxValues();

        LanguageComboBox.SelectionChanged -= LanguageComboBox_SelectionChanged;
        LanguageComboBox.SelectedValue = _jsonSetting.language;
        LanguageComboBox.SelectionChanged += LanguageComboBox_SelectionChanged;

        RenderModeComboBox.SelectionChanged -= RenderModeComboBox_SelectionChanged;
        RenderModeComboBox.SelectedValue = _jsonSetting.render_mode;
        RenderModeComboBox.SelectionChanged += RenderModeComboBox_SelectionChanged;
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

    private async void RenderModeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (RenderModeComboBox is null || RenderModeComboBox.SelectedItem is null) return;

        // Set render mode using combo box selection, this will require restarting the app
        var selectedRenderMode = (string)RenderModeComboBox.SelectedItem;

        await _jsonSetting.SetRenderModeAsync(selectedRenderMode);

        await _messageBoxService.ShowMessageBoxAsync(
                _localizationService.GetLocaleValue("MessageBoxInfoTitle"),
                _localizationService.GetLocaleValue("RenderModeDialogue"),
                MsBox.Avalonia.Enums.Icon.Setting
                );
    }

    private IReadOnlyList<string> ResolveRenderModeComboBoxValues()
    {
        if (OperatingSystem.IsWindows())
        {
            return RenderModes.AllWindows;
        } else
        {
            return RenderModes.AllLinux;
        }
    }
}