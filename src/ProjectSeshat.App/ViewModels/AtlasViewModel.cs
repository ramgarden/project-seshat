using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using ProjectSeshat.Atlas;
using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.App.ViewModels;

/// <summary>Presentation data for the guided search page (honk - FSS - DSS).</summary>
public sealed class AtlasViewModel : ViewModelBase
{
    private readonly AtlasService? _atlas;
    private readonly IStarSystemRepository? _systemRepository;
    private readonly ICelestialBodyRepository? _bodyRepository;
    private readonly INavigationStateRepository? _navigationRepository;
    private string _summaryText = "No surveyed systems yet. Import journal files to build a search guide.";
    private string _currentSystemText = "Current position unknown";
    private string _nextJumpTitle = "No next jump";
    private string _nextJumpDetail = "Every reachable system is already searched. Import more journals or plot deeper.";
    private GuideTarget? _selectedTarget;

    public AtlasViewModel(
        AtlasService? atlas = null,
        IStarSystemRepository? systemRepository = null,
        ICelestialBodyRepository? bodyRepository = null,
        INavigationStateRepository? navigationRepository = null)
    {
        _atlas = atlas;
        _systemRepository = systemRepository;
        _bodyRepository = bodyRepository;
        _navigationRepository = navigationRepository;
        Refresh();
    }

    public ObservableCollection<GuideTarget> HonkItems { get; } = new();

    public ObservableCollection<GuideTarget> FssItems { get; } = new();

    public ObservableCollection<GuideTarget> DssItems { get; } = new();

    public string SummaryText
    {
        get => _summaryText;
        private set => SetProperty(ref _summaryText, value);
    }

    public string CurrentSystemText
    {
        get => _currentSystemText;
        private set => SetProperty(ref _currentSystemText, value);
    }

    public string NextJumpTitle
    {
        get => _nextJumpTitle;
        private set => SetProperty(ref _nextJumpTitle, value);
    }

    public string NextJumpDetail
    {
        get => _nextJumpDetail;
        private set => SetProperty(ref _nextJumpDetail, value);
    }

    public GuideTarget? SelectedTarget
    {
        get => _selectedTarget;
        set
        {
            if (SetProperty(ref _selectedTarget, value))
            {
                OnPropertyChanged(nameof(SelectedTargetTitle));
                OnPropertyChanged(nameof(SelectedTargetDetail));
            }
        }
    }

    public string SelectedTargetTitle => SelectedTarget?.Title ?? "Nothing selected";

    public string SelectedTargetDetail => SelectedTarget?.Detail ?? "Select an entry above to see where to search next.";

    public void Refresh()
    {
        HonkItems.Clear();
        FssItems.Clear();
        DssItems.Clear();

        if (_atlas is null || _systemRepository is null || _bodyRepository is null)
        {
            SummaryText = "Search guide is unavailable in this context.";
            return;
        }

        var guide = _atlas.BuildSearchGuideAsync(_systemRepository, _bodyRepository, _navigationRepository).GetAwaiter().GetResult();

        CurrentSystemText = guide.CurrentSystemName is not null
            ? $"You are at {guide.CurrentSystemName}"
            : "Current position unknown";

        var step = 0;
        foreach (var target in guide.NeedHonk)
        {
            HonkItems.Add(GuideTarget.Honk(target) with { Title = $"{++step}. {target.SystemName}" });
        }

        foreach (var target in guide.NeedFss)
        {
            FssItems.Add(GuideTarget.Fss(target));
        }

        foreach (var target in guide.NeedDss)
        {
            DssItems.Add(GuideTarget.Dss(target));
        }

        if (guide.NeedHonk.Count > 0)
        {
            var first = guide.NeedHonk[0];
            var distance = first.DistanceLy is null ? "position unknown" : $"{first.DistanceLy.Value:N0} LY away";
            NextJumpTitle = first.SystemName;
            NextJumpDetail = $"Next jump: discovery-scan {first.SystemName}. {distance} from your current position. After you reach it, the next-nearest unexplored system on the route follows.";
        }
        else
        {
            NextJumpTitle = "No next jump";
            NextJumpDetail = "Every reachable system has already been discovery-scanned. When you import deeper jumps, a new route appears here.";
        }

        SummaryText = $"Next: {guide.HonkCount} system{(guide.HonkCount == 1 ? "" : "s")} to honk, then {guide.FssCount} to FSS, and {guide.DssCount} bod{(guide.DssCount == 1 ? "y" : "ies")} worth a DSS scan.";
    }
}

public sealed record GuideTarget(string Title, string Subtitle, string Detail)
{
    public static GuideTarget Honk(HonkTarget t)
    {
        var distance = t.DistanceLy is null ? "position unknown" : $"{(int)t.DistanceLy.Value:N0} LY away";
        return new GuideTarget(t.SystemName, $"Needs honk \u00b7 {distance}", $"Discovery-scan: {t.SystemName}. {distance}.");
    }

    public static GuideTarget Fss(FssTarget t)
    {
        var distance = t.DistanceLy is null ? "position unknown" : $"{(int)t.DistanceLy.Value:N0} LY away";
        var signals = $"{t.Signals} signal{(t.Signals == 1 ? "" : "s")} hinting at something worth resolving";
        return new GuideTarget(t.SystemName, $"Needs FSS \u00b7 {signals}", $"Full Spectrum Scanner: {t.SystemName}. {signals} ({distance}).");
    }

    public static GuideTarget Dss(DssTarget t)
    {
        return new GuideTarget(
            t.BodyName,
            $"{t.SystemName} \u00b7 {(int)t.DistanceLs:N0} ls \u00b7 {t.Reason}",
            $"Detailed Surface Scanner: {t.BodyName} in {t.SystemName}. {(int)t.DistanceLs:N0} ls from arrival. Mapping worth it \u2014 {t.Reason}.");
    }
}
