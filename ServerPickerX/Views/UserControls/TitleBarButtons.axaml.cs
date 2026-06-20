using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ServerPickerX.Services.DependencyInjection;
using ServerPickerX.Services.MessageBoxes;

namespace ServerPickerX;

public partial class TitleBarButtons : UserControl
{
    public TitleBarButtons()
    {
        InitializeComponent();
    }

    private void MinimizeBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var parentWindow = TopLevel.GetTopLevel(this) as Window;

        parentWindow?.WindowState = WindowState.Minimized;
    }

    private void CloseBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var parentWindow = TopLevel.GetTopLevel(this) as Window;

        parentWindow?.Close();
    }

    private void MaximizeBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var parentWindow = TopLevel.GetTopLevel(this) as Window;
        
        if (parentWindow?.Name == "Settings")
        {
            return;
        }

        if (parentWindow?.WindowState == WindowState.Maximized) 
        {
            parentWindow?.WindowState = WindowState.Normal;
        } else
        {
            parentWindow?.WindowState = WindowState.Maximized;
        }
    }
}