using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ServerPickerX;

public partial class TitleBarButtons : UserControl
{
    public TitleBarButtons()
    {
        InitializeComponent();
    }

    private void MinimizeBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window parentWindow)
        {
            parentWindow.WindowState = WindowState.Minimized;
        }
    }

    private void CloseBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window parentWindow)
        {
            parentWindow.Close();
        }
    }
}