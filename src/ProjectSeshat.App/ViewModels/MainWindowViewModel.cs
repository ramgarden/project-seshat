using System;
using System.Windows.Input;
using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Journals;

namespace ProjectSeshat.App.ViewModels;

/// <summary>Application shell and navigation coordinator.</summary>
public sealed class MainWindowViewModel : ViewModelBase
{
    private ViewModelBase _currentPage;
    private bool _isDashboardActive;
    private bool _isExplorationActive;

    public MainWindowViewModel(
        IStarSystemRepository starSystemRepository,
        ICommanderRepository commanderRepository,
        IEvidenceRepository evidenceRepository,
        JournalPathResolver? journalPathResolver = null,
        JournalReader? journalReader = null,
        IJournalImportTrackerRepository? journalImportTrackerRepository = null)
    {
        Dashboard = new DashboardViewModel(
            starSystemRepository,
            commanderRepository,
            evidenceRepository,
            journalImportTrackerRepository,
            journalPathResolver,
            journalReader);

        Exploration = new ExplorationViewModel(
            starSystemRepository,
            evidenceRepository);

        // Coordinate data updates
        Dashboard.DataImported += (s, e) =>
        {
            Exploration.RefreshExploreView();
        };

        NavigateToDashboardCommand = new RelayCommand(() => CurrentPage = Dashboard);
        NavigateToExplorationCommand = new RelayCommand(() => CurrentPage = Exploration);

        // Start on Dashboard
        _currentPage = Dashboard;
        UpdateActiveStates();
    }

    public string WindowTitle => "Project Seshat - Galactic Research Platform";

    public string ProjectName => "PROJECT SESHAT";

    public string PlatformName => "Galactic Research Platform";

    public DashboardViewModel Dashboard { get; }

    public ExplorationViewModel Exploration { get; }

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

    public ICommand NavigateToDashboardCommand { get; }

    public ICommand NavigateToExplorationCommand { get; }

    private void UpdateActiveStates()
    {
        IsDashboardActive = CurrentPage == Dashboard;
        IsExplorationActive = CurrentPage == Exploration;
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
