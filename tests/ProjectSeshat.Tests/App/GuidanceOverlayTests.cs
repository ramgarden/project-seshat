using ProjectSeshat.App.ViewModels;
using ProjectSeshat.Atlas;
using Xunit;

namespace ProjectSeshat.Tests.App;

public sealed class GuidanceFormatterTests
{
    [Fact]
    public void OverlayTitle_UppercasesActionAndTarget()
    {
        var step = new CrawlStep("Jump", "Sol", "Nearest unsearched star within reach", "210 Ly away");

        var title = GuidanceFormatter.OverlayTitle(step);

        Assert.Equal("JUMP → SOL", title);
    }

    [Fact]
    public void OverlayTitle_NullStep_ShowsNothingToDo()
        => Assert.Equal("NO NEXT MOVE", GuidanceFormatter.OverlayTitle(null));

    [Fact]
    public void OverlayDetail_CombinesReasonAndDetail()
    {
        var step = new CrawlStep("FSS", "Sag A*", "Resolve the signals", "Biological,Geological");

        var detail = GuidanceFormatter.OverlayDetail(step);

        Assert.Equal("Resolve the signals \u2014 Biological,Geological", detail);
    }

    [Fact]
    public void OverlayDetail_NullStep_ShowsResumePrompt()
        => Assert.Contains("fully surveyed", GuidanceFormatter.OverlayDetail(null));

    [Fact]
    public void Transcript_InterpretsEachAction()
    {
        Assert.Equal("Arrived at Sol. Run the discovery scan, then check for signals.",
            GuidanceFormatter.Transcript(new CrawlStep("Honk", "Sol", "Arrived")));
        Assert.Equal("Run the full spectrum scanner at Sol to resolve the signals. Signals detected: Biological.",
            GuidanceFormatter.Transcript(new CrawlStep("FSS", "Sol", "Honk detected signals", "Biological")));
        Assert.Equal("Map the body Sol 1 with the detailed surface scanner. Reason: Terraformable world.",
            GuidanceFormatter.Transcript(new CrawlStep("DSS", "Sol 1", "DSS in Sol", "Terraformable world")));
        Assert.Equal("All nearby stars are searched. Back-track to Sol.",
            GuidanceFormatter.Transcript(new CrawlStep("Back-track", "Sol", "All nearby stars searched")));
        Assert.Equal("Nothing interesting here. Jump to Sol. 210 Ly away.",
            GuidanceFormatter.Transcript(new CrawlStep("Jump", "Sol", "Nearest unsearched", "210 Ly away")));
    }

    [Fact]
    public void Transcript_NullStep_ReturnsNothingToSay()
        => Assert.Null(GuidanceFormatter.Transcript(null));
}

public sealed class GuidanceOverlayViewModelTests
{
    [Fact]
    public void SetStep_UpdatesTitleAndDetail()
    {
        var viewModel = new GuidanceOverlayViewModel();
        string? lastTranscript = null;
        viewModel.Updated += step => lastTranscript = step.Transcript;

        viewModel.SetStep(new CrawlStep("Jump", "Sol", "Nearest unsearched star within reach"));

        Assert.Equal("JUMP → SOL", viewModel.TitleText);
        Assert.Equal("Nearest unsearched star within reach", viewModel.DetailText);
        Assert.Contains("Jump to Sol", lastTranscript);
    }

    [Fact]
    public void SetStep_NullStep_ShowsNothingToDo()
    {
        var viewModel = new GuidanceOverlayViewModel();

        viewModel.SetStep(null);

        Assert.Equal("NO NEXT MOVE", viewModel.TitleText);
        Assert.Null(viewModel.Transcript);
    }
}