using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
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
    private readonly IBeaconRepository? _beaconRepository;
    private readonly INavigationStateRepository? _navigationRepository;
    private string _summaryText = "No surveyed systems yet. Import journal files to build a search guide.";
    private string _nextJumpTitle = "No next jump";
    private string _nextJumpDetail = "Every reachable system is already searched. Import more journals or plot deeper.";
    private GuideTarget? _selectedTarget;
    private string _outwardNext = "No search in progress";
    private string _outwardNextReason = "Build an outward survey from the gate below to start hunting Raxxla systematically.";
    private string _recommendedGate = "No gate found";
    private string _recommendedGateReasoning = "Import more journals first — a search gate needs some surveyed ground to recommend.";
    private string _nearestRecommendedGate = "No gate found";
    private string _nearestRecommendedGateReasoning = "Import more journals first — a search gate needs some surveyed ground to recommend.";
    private NextAction? _currentAction;
    private StarSystemId? _selectedGateId;
    private CancellationTokenSource? _refreshCancellation;
    private int _refreshVersion;

    public SearchGuideViewModel(
        AtlasService? atlas = null,
        IStarSystemRepository? systemRepository = null,
        ICelestialBodyRepository? bodyRepository = null,
        INavigationStateRepository? navigationRepository = null,
        IBeaconRepository? beaconRepository = null)
    {
        _atlas = atlas;
        _systemRepository = systemRepository;
        _bodyRepository = bodyRepository;
        _navigationRepository = navigationRepository;
        _beaconRepository = beaconRepository;

        SelectRecommendedGateCommand = new RelayCommand(async () => { await SelectRecommendedGate(); });
        SelectNearestGateCommand = new RelayCommand(async () => { await SelectNearestGate(); });
        ClearGateCommand = new RelayCommand(async () => { await ClearGate(); });

        Refresh();
    }

    public void Refresh()
        => _ = RefreshAsync();

    public ObservableCollection<GuideTarget> HonkItems { get; } = new();

    public ObservableCollection<GuideTarget> FssItems { get; } = new();

    public ObservableCollection<GuideTarget> DssItems { get; } = new();

    public ObservableCollection<GuideTarget> RaxxlaIntelItems { get; } = new();

    private string _raxxlaIntelStatus = "Scan journals to start flagging Raxxla-hunt points of interest.";
    public string RaxxlaIntelStatus
    {
        get => _raxxlaIntelStatus;
        private set => SetProperty(ref _raxxlaIntelStatus, value);
    }

    public string RaxxlaMoveTitle
    {
        get => _raxxlaMoveTitle;
        private set => SetProperty(ref _raxxlaMoveTitle, value);
    }

    public string RaxxlaMoveSubtitle
    {
        get => _raxxlaMoveSubtitle;
        private set => SetProperty(ref _raxxlaMoveSubtitle, value);
    }

    public string RaxxlaMoveDetail
    {
        get => _raxxlaMoveDetail;
        private set => SetProperty(ref _raxxlaMoveDetail, value);
    }

    public bool HasRaxxlaMove
    {
        get => _hasRaxxlaMove;
        private set => SetProperty(ref _hasRaxxlaMove, value);
    }

    private string _raxxlaMoveTitle = "No Raxxla move yet";
    private string _raxxlaMoveSubtitle = "Import journals to flag community-derived search priorities.";
    private string _raxxlaMoveDetail = "Raxxla hints are investigation priorities, not claimed locations.";
    private bool _hasRaxxlaMove;

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

    public string NearestRecommendedGate
    {
        get => _nearestRecommendedGate;
        private set => SetProperty(ref _nearestRecommendedGate, value);
    }

    public string NearestRecommendedGateReasoning
    {
        get => _nearestRecommendedGateReasoning;
        private set => SetProperty(ref _nearestRecommendedGateReasoning, value);
    }

    public bool HasRecommendedGate => !string.IsNullOrEmpty(RecommendedGate) && RecommendedGate != "No gate found";

    public bool HasNearestRecommendedGate => !string.IsNullOrEmpty(NearestRecommendedGate) && NearestRecommendedGate != "No gate found";

    public bool HasSelectedGate => _selectedGateId is not null;

    public string GateActionText => _selectedGateId is null ? "Use as gate" : $"Gate: {RecommendedGate} · Clear";

    /// <summary>Explains where the outward search starts: the current system unless a gate is chosen.</summary>
    public string SearchOriginText
    {
        get
        {
            if (_selectedGateId is not null && HasRecommendedGate)
            {
                return $"Search bubble starts at {RecommendedGate} — created from the recommended gate.";
            }

            return "Search starts from your current system and radiates outward, unless you pick a better gate below.";
        }
    }

    public ICommand SelectRecommendedGateCommand { get; }

    public ICommand SelectNearestGateCommand { get; }

    public ICommand ClearGateCommand { get; }

    /// <summary>Raised whenever the canonical next action changes so overlays/voice can follow.</summary>
    public event Action<NextAction?>? NextActionUpdated;

    /// <summary>Compatibility event for callers still using the legacy crawl-step model.</summary>
    public event Action<CrawlStep?>? CrawlUpdated;

    /// <summary>The canonical next action, or null when there is no immediate instruction.</summary>
    public NextAction? CurrentAction => _currentAction;

    public string NextActionTitle
    {
        get => GuidanceFormatter.OverlayTitle(_currentAction);
        private set => SetProperty(ref _nextActionTitle, value);
    }

    public string NextActionReason
    {
        get => _currentAction?.Reason ?? "No immediate action is available.";
        private set => SetProperty(ref _nextActionReason, value);
    }

    public string NextActionDetail
    {
        get => GuidanceFormatter.OverlayDetail(_currentAction);
        private set => SetProperty(ref _nextActionDetail, value);
    }

    public bool HasNextAction => _currentAction is not null;

    private string _nextActionTitle = "NO NEXT MOVE";
    private string _nextActionReason = "No immediate action is available.";
    private string _nextActionDetail = "Every known system is fully surveyed. Import deeper jumps to resume the outward crawl.";

    private async Task SelectRecommendedGate()
    {
        if (_atlas is null || _systemRepository is null || string.IsNullOrEmpty(RecommendedGate))
        {
            return;
        }

        var gate = await _systemRepository.FindByNameAsync(RecommendedGate, CancellationToken.None);
        await SelectGate(gate);
    }

    private async Task SelectNearestGate()
    {
        if (_atlas is null || _systemRepository is null || string.IsNullOrEmpty(NearestRecommendedGate)
            || string.Equals(NearestRecommendedGate, RecommendedGate, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var gate = await _systemRepository.FindByNameAsync(NearestRecommendedGate, CancellationToken.None);
        await SelectGate(gate);
    }

    private async Task SelectGate(StarSystem? gate)
    {
        if (gate is null)
        {
            return;
        }

        _selectedGateId = gate.Id;
        OnPropertyChanged(nameof(HasSelectedGate));
        OnPropertyChanged(nameof(GateActionText));
        OnPropertyChanged(nameof(SearchOriginText));
        await RefreshAsync();
    }

    private async Task ClearGate()
    {
        if (_selectedGateId is null)
        {
            await SelectRecommendedGate();
            return;
        }

        _selectedGateId = null;
        OnPropertyChanged(nameof(HasSelectedGate));
        OnPropertyChanged(nameof(GateActionText));
        OnPropertyChanged(nameof(SearchOriginText));
        await RefreshAsync();
    }

    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_refreshCancellation is not null)
        {
            try
            {
                _refreshCancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            _refreshCancellation.Dispose();
        }
        var refreshCancellation = new CancellationTokenSource();
        _refreshCancellation = refreshCancellation;
        CancellationTokenSource? linkedRefreshCancellation = null;
        if (cancellationToken.CanBeCanceled)
        {
            linkedRefreshCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, refreshCancellation.Token);
        }

        var refreshToken = linkedRefreshCancellation?.Token ?? refreshCancellation.Token;
        var refreshVersion = ++_refreshVersion;
        return RefreshCoreAsync(refreshToken, refreshVersion, refreshCancellation, linkedRefreshCancellation);
    }

    private bool IsCurrentRefresh(int version, CancellationToken cancellationToken)
        => !cancellationToken.IsCancellationRequested && version == _refreshVersion;

    private async Task RefreshCoreAsync(
        CancellationToken cancellationToken,
        int refreshVersion,
        CancellationTokenSource refreshCancellation,
        CancellationTokenSource? linkedRefreshCancellation)
    {
        try
        {
            if (!IsCurrentRefresh(refreshVersion, cancellationToken))
            {
                return;
            }

            HonkItems.Clear();
            FssItems.Clear();
            DssItems.Clear();

            if (_atlas is null || _systemRepository is null || _bodyRepository is null)
            {
                SummaryText = "Search guide is unavailable in this context.";
                NextJumpTitle = "No next jump";
                NextJumpDetail = "The search guide is unavailable in this context.";
                NextActionTitle = GuidanceFormatter.OverlayTitle(null);
                NextActionReason = "No immediate action is available.";
                NextActionDetail = "Every known system is fully surveyed. Import deeper jumps to resume the outward crawl.";
                _currentAction = null;
                NextActionUpdated?.Invoke(null);
                CrawlUpdated?.Invoke(null);
                return;
            }

            var guide = await _atlas.BuildSearchGuideAsync(
                _systemRepository,
                _bodyRepository,
                _navigationRepository,
                cancellationToken);

            if (!IsCurrentRefresh(refreshVersion, cancellationToken))
            {
                return;
            }

            var crawl = await _atlas.BuildOutwardCrawlAsync(
                _systemRepository,
                navigationRepository: _navigationRepository,
                bodyRepository: _bodyRepository,
                gateSystemId: _selectedGateId,
                cancellationToken: cancellationToken);

            if (!IsCurrentRefresh(refreshVersion, cancellationToken))
            {
                return;
            }

            var previousAction = _currentAction;
            _currentAction = crawl.NextAction;

            if (!EqualityComparer<NextAction?>.Default.Equals(previousAction, _currentAction))
            {
                NextActionUpdated?.Invoke(_currentAction);
                CrawlUpdated?.Invoke(ToLegacyStep(_currentAction));
            }

            if (_currentAction is null)
            {
                OutwardNext = "No search in progress";
                OutwardNextReason = "Every known system is fully surveyed. Import deeper jumps to resume the outward crawl.";
            }
            else
            {
                OutwardNext = NextActionTitle;
                OutwardNextReason = string.IsNullOrWhiteSpace(NextActionDetail)
                    ? NextActionReason
                    : NextActionDetail;
            }

            var suggestions = await _atlas.RecommendSearchGatesAsync(
                _systemRepository,
                _navigationRepository,
                cancellationToken: cancellationToken);

            if (!IsCurrentRefresh(refreshVersion, cancellationToken))
            {
                return;
            }

            if (suggestions.Best is { } gate)
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

            if (suggestions.BestNearest is { } nearestGate)
            {
                NearestRecommendedGate = nearestGate.SystemName;
                NearestRecommendedGateReasoning = nearestGate.Reasoning;
                OnPropertyChanged(nameof(HasNearestRecommendedGate));
            }
            else
            {
                NearestRecommendedGate = "No gate found";
                NearestRecommendedGateReasoning = "Import more journals first — a search gate needs some surveyed ground to recommend.";
                OnPropertyChanged(nameof(HasNearestRecommendedGate));
            }

            OnPropertyChanged(nameof(SearchOriginText));

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

            RaxxlaIntelItems.Clear();
            var intelHits = new List<RaxxlaIntelHit>(await _atlas.FindRaxxlaIntelAsync(
                _systemRepository,
                _bodyRepository,
                maxHits: 50,
                cancellationToken: cancellationToken));

            if (!IsCurrentRefresh(refreshVersion, cancellationToken))
            {
                return;
            }

            // Also check beacons for Raxxla-relevant lore terms
            if (_beaconRepository is not null)
            {
                var beacons = await _beaconRepository.ListAsync(100, cancellationToken);
                foreach (var beacon in beacons)
                {
                    if (RaxxlaSearchIntel.ReasonForBeacon(beacon) is { } beaconReason)
                    {
                        intelHits.Add(new RaxxlaIntelHit("Beacon", beacon.SystemName, beacon.BeaconName, beaconReason, 4));
                    }
                }
            }

            foreach (var hit in intelHits)
            {
                RaxxlaIntelItems.Add(GuideTarget.Intel(hit));
            }

            UpdateRaxxlaMove();

            RaxxlaIntelStatus = RaxxlaIntelItems.Count == 0
                ? "No Raxxla-hunt points of interest flagged yet. Consume signals and map notable bodies to build this list."
                : $"{RaxxlaIntelItems.Count} point{(RaxxlaIntelItems.Count == 1 ? "" : "s")} worth investigating \u2014 community-derived hints, not a claimed location.";

            if (_currentAction is { } nextAction)
            {
                NextJumpTitle = nextAction.Action is NextActionKind.Jump or NextActionKind.BackTrack
                    ? nextAction.Target
                    : "No next jump";
                var routeDetail = nextAction.Detail;
                if (string.IsNullOrWhiteSpace(routeDetail) && nextAction.DistanceLy is { } distance)
                {
                    routeDetail = $"{distance:N0} Ly away";
                }

                NextJumpDetail = nextAction.Action is NextActionKind.Jump or NextActionKind.BackTrack
                    ? $"{nextAction.Reason}{(string.IsNullOrWhiteSpace(routeDetail) ? "" : $" — {routeDetail}")}"
                    : "Complete the current in-system action before opening the jump plot.";
            }
            else
            {
                NextJumpTitle = "No next jump";
                NextJumpDetail = "Every known system is fully surveyed. Import deeper jumps to resume the outward crawl.";
            }

            SummaryText = _currentAction is null
                ? "No immediate action is available."
                : $"Next: {NextActionTitle} — {NextActionReason}";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            linkedRefreshCancellation?.Dispose();
            refreshCancellation.Dispose();
        }
    }

    private static CrawlStep? ToLegacyStep(NextAction? action)
        => action is null
            ? null
            : new CrawlStep(
                action.Action == NextActionKind.BackTrack ? "Back-track" : action.Action.ToString(),
                action.Target,
                action.Reason,
                action.Detail);

    private void UpdateRaxxlaMove()
    {
        var move = RaxxlaIntelItems.FirstOrDefault();
        RaxxlaMoveTitle = move?.Title ?? "No Raxxla move yet";
        RaxxlaMoveSubtitle = move?.Subtitle ?? "Import journals to flag community-derived search priorities.";
        RaxxlaMoveDetail = move?.Detail ?? "Raxxla hints are investigation priorities, not claimed locations.";
        HasRaxxlaMove = move is not null;
    }

    private sealed class RelayCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
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

    public static GuideTarget Intel(RaxxlaIntelHit hit)
    {
        var tag = hit.Type switch
        {
            "Body" => "MAP / DSS",
            "Signal" => "FSS SIGNAL",
            "Name" => "LORE NAME",
            _ => "IN HUNT BUBBLE"
        };

        return new GuideTarget(hit.Target, $"{tag} · {hit.SystemName}", hit.Reason);
    }
}
