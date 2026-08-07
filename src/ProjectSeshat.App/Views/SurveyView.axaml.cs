using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ProjectSeshat.App.Views;

public partial class SurveyView : UserControl
{
    public SurveyView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
