using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ServerPickerX.ViewModels;
using System.Threading.Tasks;

namespace ServerPickerX;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();

        DataContext = new SettingsWindowViewModel();
    }

    private void TitleBar_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        // prevent other mouse event listeners from being triggered
        e.Handled = true;

        var parentWindow = TopLevel.GetTopLevel(this) as Window;

        parentWindow.BeginMoveDrag(e);
    }
}