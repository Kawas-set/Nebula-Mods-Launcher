using Avalonia.Controls;
using Avalonia.Interactivity;
using ModLauncher.Services;

namespace ModLauncher.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        DataContext = AppLocalizer.Instance;
    }

    private void CloseClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
