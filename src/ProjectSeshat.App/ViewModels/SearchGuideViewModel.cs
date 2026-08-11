using System;
using System.Collections.ObjectModel;
using ProjectSeshat.Atlas;
using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.App.ViewModels;

/// <summary>
/// The landing page: shows exactly where to jump next and what is needed (honk, FSS, DSS), with
/// an explanation of why each target is worth the scan.
/// </summary>
public sealed class SearchGuideViewModel : ViewModelBase
{
    private readonly AtlasService? _atlas;
    private readonly IStarSystemRepository? _systemRepository;
    private readonly ICelestialBodyRepository? _bodyRepository;
    private readonly INavigationStateRepository? _navigationRepository;
    private string _summaryText = "No surveyed systems yet. Import journal files to build a search guide.";
    private string _nextJumpTitle = "No next jump";
    private string _nextJumpDetail = "Every reachable system is already searched. Import more journals or plot deeper.";
    private GuideTarget? _selectedTarget;
    private string _outwardNext = "No search in progress";
    private string _outwardNextReason = "Build an outward survey from the gate below to start hunting Raxxla systematically.";
    private string _recommendedGate = "No gate found";
    private string _recommendedGateReasoning = "Import more journals first — a search gate needs some surveyed ground to recommend.";

    public SearchGuideViewModel(
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

    public string CurrentPositionText { get; private set; } = "Current position unknown";

    public string SummaryText
    {
        get => _summaryText;
        private set => SetProperty(ref _summaryText, value);
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

    public string SelectedTargetDetail => SelectedTarget?.Detail ?? "Select an entry above to see details and why it is a priority.";

    public string OutwardNext
    {
        get => _outwardNext;
        private set => SetProperty(ref _outwardNext, value);
    }

    public string OutwardNextReason
    {
        get => _outwardNextReason;
        private set => SetProperty(ref _outwardNextReason, value);
    }

    public string RecommendedGate
    {
        get => _recommendedGate;
        private set => SetProperty(ref _recommendedGate, value);
    }

    public string RecommendedGateReasoning
    {
        get => _recommendedGateReasoning;
        private set => SetProperty(ref _recommendedGateReasoning, value);
    }

    public bool HasRecommendedGate => !string.IsNullOrEmpty(RecommendedGate) && RecommendedGate != "No gate found";

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

        var crawl = _atlas.BuildOutwardCrawlAsync(
            _systemRepository,
            navigationRepository: _navigationRepository,
            bodyRepository: _bodyRepository).GetAwaiter().GetResult();

        if (crawl.NextStep is { } nextStep)
        {
            OutwardNext = $"{nextStep.Action} → {nextStep.Target}";
            OutwardNextReason = nextStep.Reason + (string.IsNullOrWhiteSpace(nextStep.Detail) ? "" : $" ({nextStep.Detail})");
        }
        else
        {
            var hops = crawl.Route.FirstOrDefault();
            if (hops is not null)
            {
                OutwardNext = $"{hops.Kind} → {hops.SystemName}";
                OutwardNextReason = hops.Reason;
            }
            else
            {
                OutwardNext = "No search in progress";
                OutwardNextReason = "Every known system is fully surveyed. Import deeper jumps to resume the outward crawl.";
            }
        }

        if (crawl.RecommendedGate is { } gate)
        {
            RecommendedGate = gate.SystemName;
            RecommendedGateReasoning = gate.Reasoning;
            OnPropertyChanged(nameof(HasRecommendedGate));
        }
        else
        {
            RecommendedGate = "No gate found";
            RecommendedGateReasoning = "Import more journals first — a search gate needs some surveyed ground to recommend.";
            OnPropertyChanged(nameof(HasRecommendedGate));
        }

        CurrentPositionText = guide.CurrentSystemName is not null
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
        return new GuideTarget(t.SystemName, $"Needs honk · {distance}", $"Discovery-scan: {t.SystemName}. {distance}.");
    }

    public static GuideTarget Fss(FssTarget t)
    {
        var distance = t.DistanceLy is null ? "position unknown" : $"{(int)t.DistanceLy.Value:N0} LY away";

        string why;
        if (!string.IsNullOrWhiteSpace(t.SignalTypes))
        {
            why = $"Interesting signal{(t.SignalTypes.Contains(',') ? "s" : "")}: {t.SignalTypes}. Resolve with the Full Spectrum Scanner to identify what is out there.";
        }
        else if (t.Signals > 0)
        {
            var plural = t.Signals == 1 ? "signal" : "signals";
            why = $"{t.Signals} non-body {plural} detected — likely life, geological, or anomalous phenomena worth resolving. Run the FSS here.";
        }
        else
        {
            why = "Worth a quick FSS to confirm nothing unusual is hiding near this system.";
        }

        return new GuideTarget(t.SystemName, $"Needs FSS · {t.Signals} signal{(t.Signals == 1 ? "" : "s")} · {distance}", why);
    }

    public static GuideTarget Dss(DssTarget t)
    {
        var why = t.Reason switch
        {
            "Terraformable world" => "Terraformable world — a high-value, colonisation-ready body. Map it to lock in the discovery and the payout.",
            "Earthlike body" => "Earth-like world — extremely rare and scientifically significant. Surface-map to record it.",
            "Water world" => "Water world — a prime life-hunting target. Surface-map it.",
            "Ammonia world" => "Ammonia world — exotic, ammonia-based chemistry. A notable mapping target.",
            _ => $"Notable {t.Reason} worth a Detailed Surface Scan."
        };

        return new GuideTarget(
            t.BodyName,
            $"{t.SystemName} · {(int)t.DistanceLs:N0} ls · {t.Reason}",
            $"Detailed Surface Scanner: {t.BodyName} in {t.SystemName}. {(int)t.DistanceLs:N0} ls from arrival. {why}");
    }
}
