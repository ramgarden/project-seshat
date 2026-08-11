using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ProjectSeshat.App.ViewModels;

namespace ProjectSeshat.App.Views;

public sealed partial class GuidanceOverlayView : UserControl
{
    public GuidanceOverlayView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}