using Avalonia.Controls;
using Avalonia.Layout;
using ProjectSeshat.App.ViewModels;

namespace ProjectSeshat.App;

public sealed class MainWindow : Window
{
    public MainWindow()
    {
        DataContext = new MainWindowViewModel();
        Title = "Project Seshat";
        Width = 960;
        Height = 640;
        Content = new TextBlock
        {
            Text = ((MainWindowViewModel)DataContext).WelcomeMessage,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
    }
}
