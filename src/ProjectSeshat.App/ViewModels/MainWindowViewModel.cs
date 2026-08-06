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
    private bool _isExplorationActive;
    private bool _isThreadsActive;
    private bool _isAtlasActive;
    private readonly JournalWatcher? _journalWatcher;

    public MainWindowViewModel(
        IStarSystemRepository starSystemRepository,
        ICommanderRepository commanderRepository,
        IEvidenceRepository evidenceRepository,
        JournalPathResolver? journalPathResolver = null,
        ICelestialBodyRepository? celestialBodyRepository = null,
        ICodexEntryRepository? codexEntryRepository = null,
        IObservationRepository? observationRepository = null,
        ResearchThreadEngine? researchThreadEngine = null,
        InvestigationService? investigationService = null,
        AtlasService? atlasService = null,
        JournalWatcher? journalWatcher = null)
    {
        Dashboard = new DashboardViewModel(
            starSystemRepository,
            commanderRepository,
            evidenceRepository,
            journalPathResolver,
            celestialBodyRepository,
            codexEntryRepository,
            observationRepository);

        Exploration = new ExplorationViewModel(
            starSystemRepository,
            evidenceRepository,
            celestialBodyRepository);

        Threads = new ThreadsViewModel(researchThreadEngine, investigationService);

        Atlas = new AtlasViewModel(atlasService, starSystemRepository, celestialBodyRepository);

        _journalWatcher = journalWatcher;
        if (_journalWatcher is not null)
        {
            _journalWatcher.Imported += (s, e) =>
            {
                Dashboard.ReportLiveActivity(e);
                Exploration.RefreshExploreView();
                Atlas.Refresh();
            };
        }

        NavigateToDashboardCommand = new RelayCommand(() => CurrentPage = Dashboard);
        NavigateToExplorationCommand = new RelayCommand(() => CurrentPage = Exploration);
        NavigateToThreadsCommand = new RelayCommand(() => CurrentPage = Threads);
        NavigateToAtlasCommand = new RelayCommand(() => CurrentPage = Atlas);

        // Start on Dashboard
        _currentPage = Dashboard;
        UpdateActiveStates();
    }

    public string WindowTitle => "Project Seshat - Galactic Research Platform";

    public string ProjectName => "PROJECT SESHAT";

    public string PlatformName => "Galactic Research Platform";

    public DashboardViewModel Dashboard { get; }

    public ExplorationViewModel Exploration { get; }

    public ThreadsViewModel Threads { get; }

    public AtlasViewModel Atlas { get; }

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

    public bool IsAtlasActive
    {
        get => _isAtlasActive;
        private set => SetProperty(ref _isAtlasActive, value);
    }

    public ICommand NavigateToDashboardCommand { get; }

    public ICommand NavigateToExplorationCommand { get; }

    public ICommand NavigateToThreadsCommand { get; }

    public ICommand NavigateToAtlasCommand { get; }

    /// <summary>Starts live journal watching so the guide and stats update as the game writes new events.</summary>
    public void StartJournalWatcher() => _journalWatcher?.Start();

    private void UpdateActiveStates()
    {
        IsDashboardActive = CurrentPage == Dashboard;
        IsExplorationActive = CurrentPage == Exploration;
        IsThreadsActive = CurrentPage == Threads;
        IsAtlasActive = CurrentPage == Atlas;
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
