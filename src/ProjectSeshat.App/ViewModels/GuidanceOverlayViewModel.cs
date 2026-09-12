using ProjectSeshat.Atlas;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.App.ViewModels;

/// <summary>The rendered overlay state: title, detail, and the optional TTS transcript.</summary>
public sealed record GuidanceStep(string Title, string Detail, string? Transcript);

/// <summary>Holds the live copy shown on the in-game guidance overlay.</summary>
public sealed class GuidanceOverlayViewModel : ViewModelBase
{
    private string _titleText = "NO NEXT MOVE";
    private string _detailText = "Every known system is fully surveyed. Import deeper jumps to resume the outward crawl.";
    private string? _transcript;

    public string TitleText
    {
        get => _titleText;
        private set => SetProperty(ref _titleText, value);
    }

    public string DetailText
    {
        get => _detailText;
        private set => SetProperty(ref _detailText, value);
    }

    public string? Transcript => _transcript;

    /// <summary>Raised when the on-screen copy changes; the overlay window binds to it.</summary>
    public event Action<GuidanceStep>? Updated;

    public void SetStep(NextAction? action)
    {
        var next = new GuidanceStep(
            GuidanceFormatter.OverlayTitle(action),
            GuidanceFormatter.OverlayDetail(action),
            GuidanceFormatter.Transcript(action));

        _titleText = next.Title;
        _detailText = next.Detail;
        _transcript = next.Transcript;

        OnPropertyChanged(nameof(TitleText));
        OnPropertyChanged(nameof(DetailText));
        Updated?.Invoke(next);
    }
}