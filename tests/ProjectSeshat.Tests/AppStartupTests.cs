using ProjectSeshat.App.ViewModels;
using Xunit;

namespace ProjectSeshat.Tests;

public sealed class AppStartupTests
{
    [Fact]
    public void App_can_build_view_model_for_startup()
    {
        var viewModel = ProjectSeshat.App.App.CreateViewModel();

        Assert.NotNull(viewModel);
        Assert.NotNull(viewModel.WindowTitle);
        Assert.Equal("PROJECT SESHAT", viewModel.ProjectName);
    }
}
