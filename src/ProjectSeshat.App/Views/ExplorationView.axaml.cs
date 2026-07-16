using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ProjectSeshat.App.Views;

public partial class ExplorationView : UserControl
{
    public ExplorationView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
