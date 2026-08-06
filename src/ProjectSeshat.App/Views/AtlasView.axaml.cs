using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ProjectSeshat.App.Views;

public partial class AtlasView : UserControl
{
    public AtlasView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
