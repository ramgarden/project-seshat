using System;
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
    private readonly ICodexEntryRepository? _codexEntryRepository;
    private readonly IObservationRepository? _observationRepository;
    private readonly JournalPathResolver _journalPathResolver;

    private string _statusMessage = "Watching for journal changes";
    private string _journalPathStatus = "Searching for journal files";
    private string _loadingProgress = "0/0 files";
    private string _activeFileName = "No file selected";
    private int _systemsIndexedCount;
    private int _commanderRecordsCount;
    private int _evidenceRecordsCount;
    private int _bodiesIndexedCount;
    private int _codexEntriesCount;
    private int _observationsCount;

    public DashboardViewModel(
        IStarSystemRepository starSystemRepository,
        ICommanderRepository commanderRepository,
        IEvidenceRepository evidenceRepository,
        JournalPathResolver? journalPathResolver = null,
        ICelestialBodyRepository? celestialBodyRepository = null,
        ICodexEntryRepository? codexEntryRepository = null,
        IObservationRepository? observationRepository = null)
    {
        _starSystemRepository = starSystemRepository;
        _commanderRepository = commanderRepository;
        _evidenceRepository = evidenceRepository;
        _celestialBodyRepository = celestialBodyRepository;
        _codexEntryRepository = codexEntryRepository;
        _observationRepository = observationRepository;
        _journalPathResolver = journalPathResolver ?? new JournalPathResolver();

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
        CodexEntriesCount = _codexEntryRepository?.CountAsync().GetAwaiter().GetResult() ?? 0;
        ObservationsCount = _observationRepository?.CountAsync().GetAwaiter().GetResult() ?? 0;
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
}
