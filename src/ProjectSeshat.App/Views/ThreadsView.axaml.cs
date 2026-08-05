using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ProjectSeshat.App.Views;

public partial class ThreadsView : UserControl
{
    public ThreadsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
