using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ProjectSeshat.App.Views;

public partial class SearchGuideView : UserControl
{
    public SearchGuideView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
