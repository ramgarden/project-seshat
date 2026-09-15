using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using ProjectSeshat.Community;
using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Journals;

namespace ProjectSeshat.App.ViewModels;

/// <summary>Presentation data for the research dashboard page.</summary>
public sealed class DashboardViewModel : ViewModelBase
{
    private readonly IStarSystemRepository _starSystemRepository;
    private readonly ICommanderRepository _commanderRepository;
    private readonly IEvidenceRepository _evidenceRepository;
    private readonly ICelestialBodyRepository? _celestialBodyRepository;
    private readonly IBeaconRepository? _beaconRepository;
    private readonly ICodexEntryRepository? _codexEntryRepository;
    private readonly IObservationRepository? _observationRepository;
    private readonly JournalPathResolver _journalPathResolver;
    private readonly CommunityService? _communityService;

    private string _statusMessage = "Watching for journal changes";
    private string _journalPathStatus = "Searching for journal files";
    private string _loadingProgress = "0/0 files";
    private string _activeFileName = "No file selected";
    private int _systemsIndexedCount;
    private int _commanderRecordsCount;
    private int _evidenceRecordsCount;
    private int _bodiesIndexedCount;
    private int _beaconCount;
    private int _codexEntriesCount;
    private int _observationsCount;

    public DashboardViewModel(
        IStarSystemRepository starSystemRepository,
        ICommanderRepository commanderRepository,
        IEvidenceRepository evidenceRepository,
        JournalPathResolver? journalPathResolver = null,
        ICelestialBodyRepository? celestialBodyRepository = null,
        IBeaconRepository? beaconRepository = null,
        ICodexEntryRepository? codexEntryRepository = null,
        IObservationRepository? observationRepository = null,
        CommunityService? communityService = null)
    {
        _starSystemRepository = starSystemRepository;
        _commanderRepository = commanderRepository;
        _evidenceRepository = evidenceRepository;
        _celestialBodyRepository = celestialBodyRepository;
        _beaconRepository = beaconRepository;
        _codexEntryRepository = codexEntryRepository;
        _observationRepository = observationRepository;
        _journalPathResolver = journalPathResolver ?? new JournalPathResolver();

        _communityService = communityService;
        if (_communityService is not null)
        {
            _communityService.Changed += (_, _) =>
            {
                OnPropertyChanged(nameof(IsCommunityConnected));
                OnPropertyChanged(nameof(CommunityEventCount));
                OnPropertyChanged(nameof(CommunityDiscoveryCount));
                OnPropertyChanged(nameof(CommunityRecentDiscoveries));
                OnPropertyChanged(nameof(CommunityStatusText));
                OnPropertyChanged(nameof(CommunityToggleText));
                OnPropertyChanged(nameof(CommunityErrorText));
                OnPropertyChanged(nameof(HasCommunityError));
            };
        }

        StartCommunityCommand = new RelayCommand(() => _communityService?.Start());
        StopCommunityCommand = new RelayCommand(() => _communityService?.Stop());
        ToggleCommunityCommand = new RelayCommand(ToggleCommunity);

        RefreshCommunityStatus();

        var resolvedPath = _journalPathResolver.ResolvePath();
        if (resolvedPath is not null)
        {
            JournalPathStatus = $"Using journal folder: {resolvedPath}";
        }

        RefreshStats();
    }

    public string StatisticsHeading => "STATISTICS";
    public string SystemsIndexedLabel => "SYSTEMS INDEXED";
    public string CommanderRecordsLabel => "COMMANDER RECORDS";
    public string EvidenceRecordsLabel => "EVIDENCE RECORDS";
    public string BodiesIndexedLabel => "BODIES CATALOGUED";
    public string BeaconsIndexedLabel => "BEACONS INDEXED";
    public string CodexEntriesLabel => "CODEX ENTRIES";
    public string ObservationsLabel => "OBSERVATIONS";

    public int SystemsIndexedCount
    {
        get => _systemsIndexedCount;
        private set => SetProperty(ref _systemsIndexedCount, value);
    }

    public int CommanderRecordsCount
    {
        get => _commanderRecordsCount;
        private set => SetProperty(ref _commanderRecordsCount, value);
    }

    public int EvidenceRecordsCount
    {
        get => _evidenceRecordsCount;
        private set => SetProperty(ref _evidenceRecordsCount, value);
    }

    public int BodiesIndexedCount
    {
        get => _bodiesIndexedCount;
        private set => SetProperty(ref _bodiesIndexedCount, value);
    }

    public int CodexEntriesCount
    {
        get => _codexEntriesCount;
        private set => SetProperty(ref _codexEntriesCount, value);
    }

    public int ObservationsCount
    {
        get => _observationsCount;
        private set => SetProperty(ref _observationsCount, value);
    }

    public int BeaconCount
    {
        get => _beaconCount;
        private set => SetProperty(ref _beaconCount, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string JournalPathStatus
    {
        get => _journalPathStatus;
        private set => SetProperty(ref _journalPathStatus, value);
    }

    public string LoadingProgress
    {
        get => _loadingProgress;
        private set => SetProperty(ref _loadingProgress, value);
    }

    public string ActiveFileName
    {
        get => _activeFileName;
        private set => SetProperty(ref _activeFileName, value);
    }

    public void RefreshStats()
    {
        SystemsIndexedCount = _starSystemRepository.CountAsync().GetAwaiter().GetResult();
        CommanderRecordsCount = _commanderRepository.CountAsync().GetAwaiter().GetResult();
        EvidenceRecordsCount = _evidenceRepository.CountAsync().GetAwaiter().GetResult();
        BodiesIndexedCount = _celestialBodyRepository?.CountAsync().GetAwaiter().GetResult() ?? 0;
        BeaconCount = _beaconRepository?.CountAsync().GetAwaiter().GetResult() ?? 0;
        CodexEntriesCount = _codexEntryRepository?.CountAsync().GetAwaiter().GetResult() ?? 0;
        ObservationsCount = _observationRepository?.CountAsync().GetAwaiter().GetResult() ?? 0;
    }

    public bool CommunityAvailable => _communityService?.IsAvailable ?? false;

    public bool IsCommunityConnected
    {
        get => _communityService?.IsConnected ?? false;
        private set { }
    }

    public long CommunityEventCount => _communityService?.ReceivedCount ?? 0;

    public int CommunityDiscoveryCount => _communityService?.DiscoveryCount ?? 0;

    public IReadOnlyList<string> CommunityRecentDiscoveries => _communityService?.RecentDiscoveries ?? Array.Empty<string>();

    public string CommunityStatusText => _communityService is null
        ? "Community data unavailable"
        : IsCommunityConnected
            ? $"EDDN connected \u2014 {CommunityEventCount} community event{(CommunityEventCount == 1 ? "" : "s")} received"
            : "EDDN disconnected \u2014 start the stream to receive community discoveries";

    public string CommunityStatusColor =>
        _communityService is null ? "#7D8A99"
        : IsCommunityConnected ? "#7FCFB0"
        : "#F2C14E";

    public string CommunityDiscoverySummary =>
        CommunityDiscoveryCount == 0
            ? "No community discoveries recorded yet \u2014 systems seen on the stream are saved here."
            : $"{CommunityDiscoveryCount} distinct system{(CommunityDiscoveryCount == 1 ? "" : "s")} discovered by the community on record \u2014 pruned to 250,000, so disk stays bounded.";

    public bool HasCommunityDiscoveries => CommunityDiscoveryCount > 0;

    public string? CommunityErrorText => _communityService?.LastError;

    public bool HasCommunityError => !string.IsNullOrEmpty(CommunityErrorText);

    public ICommand StartCommunityCommand { get; }

    public ICommand StopCommunityCommand { get; }

    public ICommand ToggleCommunityCommand { get; }

    public string CommunityToggleText => IsCommunityConnected ? "Stop EDDN" : "Start EDDN";

    private void ToggleCommunity()
    {
        if (_communityService is null)
        {
            return;
        }

        try
        {
            if (_communityService.IsConnected)
            {
                _communityService.Stop();
            }
            else
            {
                _communityService.Start();
            }
        }
        catch (Exception failure)
        {
            SeshatLog.LogError(failure, "EDDN toggle failed");
            _communityService.Stop();
        }

        RefreshCommunityStatus();
        OnPropertyChanged(nameof(CommunityToggleText));
        OnPropertyChanged(nameof(CommunityErrorText));
        OnPropertyChanged(nameof(HasCommunityError));
    }

    private void RefreshCommunityStatus()
    {
        OnPropertyChanged(nameof(CommunityAvailable));
        OnPropertyChanged(nameof(IsCommunityConnected));
        OnPropertyChanged(nameof(CommunityEventCount));
        OnPropertyChanged(nameof(CommunityDiscoveryCount));
        OnPropertyChanged(nameof(CommunityRecentDiscoveries));
        OnPropertyChanged(nameof(CommunityStatusText));
        OnPropertyChanged(nameof(CommunityErrorText));
        OnPropertyChanged(nameof(HasCommunityError));
    }

    /// <summary>Reflects a live journal import pass so the status panel stays current automatically.</summary>
    public void ReportLiveActivity(JournalScanResult result)
    {
        ActiveFileName = result.LinesImported > 0 ? "Watching live" : "Up to date";
        LoadingProgress = $"{result.LinesImported} new line{(result.LinesImported == 1 ? "" : "s")} this pass";
        StatusMessage = result.LinesImported > 0
            ? $"Live import complete \u2014 {result.LinesImported} new journal line{(result.LinesImported == 1 ? "" : "s")}"
            : $"Watching for journal changes \u2014 no new events since the last scan";

        RefreshStats();
    }

    private sealed class RelayCommand(Action? execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => execute is not null;

        public void Execute(object? parameter) => execute?.Invoke();
    }
}
