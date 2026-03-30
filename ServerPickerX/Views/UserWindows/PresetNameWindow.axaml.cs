using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace ServerPickerX;

public partial class PresetNameWindow : Window
{
    private readonly string? _initialPresetName;

    public PresetNameWindow()
    {
        InitializeComponent();
    }

    public PresetNameWindow(string? initialPresetName)
    {
        InitializeComponent();
        _initialPresetName = initialPresetName;
    }

    private void Window_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        PresetNameTextBox.Text = _initialPresetName;
        PresetNameTextBox.Focus();
        PresetNameTextBox.CaretIndex = PresetNameTextBox.Text?.Length ?? 0;
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        var parentWindow = TopLevel.GetTopLevel(this) as Window;
        parentWindow?.BeginMoveDrag(e);
    }

    private void SaveBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(PresetNameTextBox.Text);
    }

    private void CancelBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(null);
    }
}
