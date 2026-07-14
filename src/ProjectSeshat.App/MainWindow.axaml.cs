using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ProjectSeshat.App.ViewModels;

namespace ProjectSeshat.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        DataContext = new MainWindowViewModel();
    }
}
