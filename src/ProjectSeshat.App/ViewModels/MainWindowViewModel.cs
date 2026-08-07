using System;
using System.Windows.Input;
using ProjectSeshat.Atlas;
using ProjectSeshat.Core.Contracts;
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
    private readonly JournalWatcher? _journalWatcher;

    public MainWindowViewModel(
        IStarSystemRepository starSystemRepository,
        ICommanderRepository commanderRepository,
        IEvidenceRepository evidenceRepository,
        JournalPathResolver? journalPathResolver = null,
        ICelestialBodyRepository? celestialBodyRepository = null,
        ICodexEntryRepository? codexEntryRepository = null,
        IObservationRepository? observationRepository = null,
        INavigationStateRepository? navigationRepository = null,
        ResearchThreadEngine? researchThreadEngine = null,
        InvestigationService? investigationService = null,
        AtlasService? atlasService = null,
        JournalWatcher? journalWatcher = null,
        ISurveyRegionRepository? surveyRegionRepository = null)
    {
        Dashboard = new DashboardViewModel(
            starSystemRepository,
            commanderRepository,
            evidenceRepository,
            journalPathResolver,
            celestialBodyRepository,
            codexEntryRepository,
            observationRepository);

        SearchGuide = new SearchGuideViewModel(atlasService, starSystemRepository, celestialBodyRepository, navigationRepository);
        GalaxyMap = new GalaxyMapViewModel(atlasService, starSystemRepository, navigationRepository, surveyRegionRepository);
        Survey = new SurveyViewModel(atlasService, starSystemRepository, surveyRegionRepository);

        Exploration = new ExplorationViewModel(
            starSystemRepository,
            evidenceRepository,
            celestialBodyRepository);

        Threads = new ThreadsViewModel(researchThreadEngine, investigationService);

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

    public ICommand NavigateToDashboardCommand { get; }

    public ICommand NavigateToSearchGuideCommand { get; }

    public ICommand NavigateToSurveyCommand { get; }

    public ICommand NavigateToExplorationCommand { get; }

    public ICommand NavigateToThreadsCommand { get; }

    public ICommand NavigateToGalaxyMapCommand { get; }

    /// <summary>Starts live journal watching so the guide and stats update as the game writes new events.</summary>
    public void StartJournalWatcher() => _journalWatcher?.Start();

    private void UpdateActiveStates()
    {
        IsDashboardActive = CurrentPage == Dashboard;
        IsSearchGuideActive = CurrentPage == SearchGuide;
        IsSurveyActive = CurrentPage == Survey;
        IsExplorationActive = CurrentPage == Exploration;
        IsThreadsActive = CurrentPage == Threads;
        IsGalaxyMapActive = CurrentPage == GalaxyMap;
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
