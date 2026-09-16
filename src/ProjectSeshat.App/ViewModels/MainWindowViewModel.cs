using System;
using System.Threading.Tasks;
using System.Windows.Input;
using ProjectSeshat.App.Elite;
using ProjectSeshat.Atlas;
using ProjectSeshat.Community;
using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Core.Domain;
using ProjectSeshat.Investigations;
using ProjectSeshat.Journals;
using ProjectSeshat.ThreadEngine;

namespace ProjectSeshat.App.ViewModels;

/// <summary>Application shell and navigation coordinator.</summary>
public sealed class MainWindowViewModel : ViewModelBase
{
    private ViewModelBase _currentPage;
    private bool _isDashboardActive;
    private bool _isSearchGuideActive;
    private bool _isSurveyActive;
    private bool _isExplorationActive;
    private bool _isThreadsActive;
    private bool _isGalaxyMapActive;
    private bool _isKeybindSetupActive;
    private readonly JournalWatcher? _journalWatcher;
    private readonly IJournalImportTrackerRepository? _importTrackerRepository;
    private readonly IVoicePinger? _voicePinger;
    private readonly KeyAutomationService? _keyAutomation;
    private bool _overlayVisible;
    private string _overlayVisibilityText = "Show overlay";
    private bool _autoTargetEnabled;
    private string _autoTargetStatusText = "Auto-target unavailable";

    public MainWindowViewModel(
        IStarSystemRepository starSystemRepository,
        ICommanderRepository commanderRepository,
        IEvidenceRepository evidenceRepository,
        JournalPathResolver? journalPathResolver = null,
        ICelestialBodyRepository? celestialBodyRepository = null,
        IBeaconRepository? beaconRepository = null,
        ICodexEntryRepository? codexEntryRepository = null,
        IObservationRepository? observationRepository = null,
        INavigationStateRepository? navigationRepository = null,
        ResearchThreadEngine? researchThreadEngine = null,
        InvestigationService? investigationService = null,
        AtlasService? atlasService = null,
        JournalWatcher? journalWatcher = null,
        ISurveyRegionRepository? surveyRegionRepository = null,
        CommunityService? communityService = null,
        IVoicePinger? voicePinger = null,
        KeyAutomationService? keyAutomation = null,
        string? databasePath = null,
        IJournalImportTrackerRepository? importTrackerRepository = null)
    {
        _journalWatcher = journalWatcher;
        _importTrackerRepository = importTrackerRepository;

        Dashboard = new DashboardViewModel(
            starSystemRepository,
            commanderRepository,
            evidenceRepository,
            journalPathResolver,
            celestialBodyRepository,
            beaconRepository,
            codexEntryRepository,
            observationRepository,
            communityService,
            databasePath,
            ResetAndReimportAsync);

        SearchGuide = new SearchGuideViewModel(
            atlasService,
            starSystemRepository,
            celestialBodyRepository,
            navigationRepository,
            beaconRepository);
        GalaxyMap = new GalaxyMapViewModel(atlasService, starSystemRepository, navigationRepository, surveyRegionRepository);
        Survey = new SurveyViewModel(atlasService, starSystemRepository, surveyRegionRepository);

        Exploration = new ExplorationViewModel(
            starSystemRepository,
            evidenceRepository,
            celestialBodyRepository);

        Threads = new ThreadsViewModel(researchThreadEngine, investigationService);

        _keyAutomation = keyAutomation ?? new KeyAutomationService();
        _keyAutomation.TryEnable();
        RefreshAutomationStatus();

        _voicePinger = voicePinger ?? new SilentVoicePinger();
        Guidance = new GuidanceOverlayViewModel();
        SearchGuide.NextActionUpdated += OnCrawlUpdated;
        // The SearchGuide already refreshed during construction (before this subscription), so
        // re-raise so the overlay + auto-target get the very first step without waiting for an import.
        SearchGuide.Refresh();
        // Overlay is opt-in, not default-on: an always-on-top transparent window floating over the
        // game adds compositor/GPU overhead that can make the game laggy. The sidebar toggle shows it.
        OverlayVisible = false;

        KeybindSetup = new KeybindSetupViewModel(_keyAutomation);

        ToggleAutoTargetCommand = new RelayCommand(ToggleAutoTarget);
        OpenKeybindSetupCommand = new RelayCommand(() => CurrentPage = KeybindSetup);

        _journalWatcher = journalWatcher;
        if (_journalWatcher is not null)
        {
            _journalWatcher.Imported += (s, e) =>
            {
                Dashboard.ReportLiveActivity(e);
                SearchGuide.Refresh();
                GalaxyMap.Refresh();
                Survey.Refresh();
                Exploration.RefreshExploreView();
            };
        }

        NavigateToDashboardCommand = new RelayCommand(() => CurrentPage = Dashboard);
        NavigateToSearchGuideCommand = new RelayCommand(() => CurrentPage = SearchGuide);
        NavigateToSurveyCommand = new RelayCommand(() => CurrentPage = Survey);
        NavigateToExplorationCommand = new RelayCommand(() => CurrentPage = Exploration);
        NavigateToThreadsCommand = new RelayCommand(() => CurrentPage = Threads);
        NavigateToGalaxyMapCommand = new RelayCommand(() => CurrentPage = GalaxyMap);

        ToggleOverlayCommand = new RelayCommand(ToggleOverlay);
        SpeakNowCommand = new RelayCommand(SpeakNow);
        TestGuidanceCommand = new RelayCommand(TestGuidance);

        // Landing page is the search guide.
        _currentPage = SearchGuide;
        UpdateActiveStates();
    }

    public string WindowTitle => "Project Seshat - Galactic Research Platform";

    public string ProjectName => "PROJECT SESHAT";

    public string PlatformName => "Galactic Research Platform";

    public DashboardViewModel Dashboard { get; }

    public SearchGuideViewModel SearchGuide { get; }

    public SurveyViewModel Survey { get; }

    public ExplorationViewModel Exploration { get; }

    public ThreadsViewModel Threads { get; }

    public GalaxyMapViewModel GalaxyMap { get; }

    public KeybindSetupViewModel KeybindSetup { get; }

    public GuidanceOverlayViewModel Guidance { get; }

    public bool OverlayVisible
    {
        get => _overlayVisible;
        private set
        {
            if (SetProperty(ref _overlayVisible, value))
            {
                OverlayVisibilityText = value ? "Hide overlay" : "Show overlay";
            }
        }
    }

    public string OverlayVisibilityText
    {
        get => _overlayVisibilityText;
        private set => SetProperty(ref _overlayVisibilityText, value);
    }

    public bool VoiceAvailable => _voicePinger is { IsAvailable: true };

    public ViewModelBase CurrentPage
    {
        get => _currentPage;
        set
        {
            if (SetProperty(ref _currentPage, value))
            {
                UpdateActiveStates();
            }
        }
    }

    public bool IsDashboardActive
    {
        get => _isDashboardActive;
        private set => SetProperty(ref _isDashboardActive, value);
    }

    public bool IsSearchGuideActive
    {
        get => _isSearchGuideActive;
        private set => SetProperty(ref _isSearchGuideActive, value);
    }

    public bool IsSurveyActive
    {
        get => _isSurveyActive;
        private set => SetProperty(ref _isSurveyActive, value);
    }

    public bool IsExplorationActive
    {
        get => _isExplorationActive;
        private set => SetProperty(ref _isExplorationActive, value);
    }

    public bool IsThreadsActive
    {
        get => _isThreadsActive;
        private set => SetProperty(ref _isThreadsActive, value);
    }

    public bool IsGalaxyMapActive
    {
        get => _isGalaxyMapActive;
        private set => SetProperty(ref _isGalaxyMapActive, value);
    }

    public bool IsKeybindSetupActive
    {
        get => _isKeybindSetupActive;
        private set => SetProperty(ref _isKeybindSetupActive, value);
    }

    public ICommand NavigateToDashboardCommand { get; }

    public ICommand NavigateToSearchGuideCommand { get; }

    public ICommand NavigateToSurveyCommand { get; }

    public ICommand NavigateToExplorationCommand { get; }

    public ICommand NavigateToThreadsCommand { get; }

    public ICommand NavigateToGalaxyMapCommand { get; }

    public ICommand OpenKeybindSetupCommand { get; }

    public ICommand ToggleOverlayCommand { get; }

    public ICommand SpeakNowCommand { get; }

    public ICommand TestGuidanceCommand { get; }

    /// <summary>Starts live journal watching so the guide and stats update as the game writes new events.</summary>
    public void StartJournalWatcher() => _journalWatcher?.Start();

    private void OnCrawlUpdated(NextAction? action)
    {
        Guidance.SetStep(action);
        if (action is not null && OverlayVisible)
        {
            _voicePinger?.Speak(Guidance.Transcript ?? "");
        }

        // When auto-targeting is enabled, drive the game with the commander's real keys.
        if (action is not null && _autoTargetEnabled)
        {
            _keyAutomation?.AutoTargetNextStar(action);
        }
    }

    private void ToggleOverlay() => OverlayVisible = !OverlayVisible;

    private void SpeakNow() => _voicePinger?.Speak(Guidance.Transcript ?? "");

    public ICommand ToggleAutoTargetCommand { get; }

    public bool AutoTargetEnabled
    {
        get => _autoTargetEnabled;
        private set => SetProperty(ref _autoTargetEnabled, value);
    }

    public string AutoTargetStatusText
    {
        get => _autoTargetStatusText;
        private set => SetProperty(ref _autoTargetStatusText, value);
    }

    public bool AutoTargetAvailable => _keyAutomation?.CanAutoTarget == true;

    public string AutoTargetToggleText => AutoTargetEnabled ? "Disable auto-target" : "Enable auto-target";

    private void ToggleAutoTarget()
    {
        AutoTargetEnabled = !AutoTargetEnabled;
        OnPropertyChanged(nameof(AutoTargetToggleText));
    }

    private void RefreshAutomationStatus() => AutoTargetStatusText = _keyAutomation?.StatusText ?? "Auto-target unavailable";

    /// <summary>
    /// Pushes a fixed demo step through the same overlay + voice pipeline the crawl uses, so the
    /// on-screen caption and speech can be checked without any journal data.
    /// </summary>
    private void TestGuidance()
    {
        var action = new NextAction(
            NextActionKind.Jump,
            "Sol",
            "Nearest unsearched star within reach",
            DistanceLy: 90);
        OnCrawlUpdated(action);
        OverlayVisible = true;
        _voicePinger?.Speak(Guidance.Transcript ?? "");
    }

    private void UpdateActiveStates()
    {
        IsDashboardActive = CurrentPage == Dashboard;
        IsSearchGuideActive = CurrentPage == SearchGuide;
        IsSurveyActive = CurrentPage == Survey;
        IsExplorationActive = CurrentPage == Exploration;
        IsThreadsActive = CurrentPage == Threads;
        IsGalaxyMapActive = CurrentPage == GalaxyMap;
        IsKeybindSetupActive = CurrentPage == KeybindSetup;
    }

    /// <summary>
    /// Clears the import tracker and triggers a full re-scan of all journal files.
    /// This rebuilds the database from scratch without requiring an app restart.
    /// </summary>
    public async Task<JournalScanResult> ResetAndReimportAsync()
    {
        try
        {
            // Clear the import tracker so all files are re-processed
            if (_importTrackerRepository is not null)
            {
                await _importTrackerRepository.ClearAllAsync();
            }

            // Reset journal watcher state and do full re-scan
            if (_journalWatcher is not null)
            {
                var result = await _journalWatcher.ResetAndRescanAsync();
                
                // Refresh all views with the new data
                Dashboard.RefreshStats();
                SearchGuide.Refresh();
                GalaxyMap.Refresh();
                Survey.Refresh();
                Exploration.RefreshExploreView();
                
                return result;
            }
        }
        catch (Exception ex)
        {
            SeshatLog.LogError(ex, "Reset and reimport failed");
        }
        return new JournalScanResult(0, 0);
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
