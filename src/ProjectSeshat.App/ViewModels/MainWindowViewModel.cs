using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Core.Domain;
using ProjectSeshat.Journals;

namespace ProjectSeshat.App.ViewModels;

/// <summary>Presentation data for the initial research dashboard.</summary>
public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IStarSystemRepository _starSystemRepository;
    private readonly ICommanderRepository _commanderRepository;
    private readonly IEvidenceRepository _evidenceRepository;
    private readonly IJournalImportTrackerRepository? _journalImportTrackerRepository;
    private readonly JournalPathResolver _journalPathResolver;
    private readonly JournalReader _journalReader;
    private string _statusMessage = "Preparing the research dashboard";
    private string _journalPathStatus = "Searching for journal files";
    private string _loadingProgress = "0/0 files";
    private string _activeFileName = "No file selected";
    private string _selectedExploreItemTitle = "No system selected";
    private string _selectedExploreItemDetail = "Select a system to inspect what has already been searched and where deeper investigation may be useful.";
    private ExploreItem? _selectedExploreItem;
    private bool _isLoading;
    private bool _showExploreView;
    private int _systemsIndexedCount;
    private int _commanderRecordsCount;
    private int _evidenceRecordsCount;

    public MainWindowViewModel(
        IStarSystemRepository starSystemRepository,
        ICommanderRepository commanderRepository,
        IEvidenceRepository evidenceRepository,
        JournalPathResolver? journalPathResolver = null,
        JournalReader? journalReader = null,
        IJournalImportTrackerRepository? journalImportTrackerRepository = null)
    {
        _starSystemRepository = starSystemRepository;
        _commanderRepository = commanderRepository;
        _evidenceRepository = evidenceRepository;
        _journalImportTrackerRepository = journalImportTrackerRepository;
        _journalPathResolver = journalPathResolver ?? new JournalPathResolver();
        _journalReader = journalReader ?? new JournalReader();

        SystemsIndexedCount = _starSystemRepository.CountAsync().GetAwaiter().GetResult();
        CommanderRecordsCount = _commanderRepository.CountAsync().GetAwaiter().GetResult();
        EvidenceRecordsCount = _evidenceRepository.CountAsync().GetAwaiter().GetResult();

        LoadJournalFilesCommand = new RelayCommand(LoadJournalFiles);
        ToggleExploreViewCommand = new RelayCommand(ToggleExploreView);
        SelectExploreItemCommand = new RelayCommand<ExploreItem>(SelectExploreItem);
        RefreshExploreView();
    }

    public string WindowTitle => "Project Seshat - Galactic Research Platform";

    public string ProjectName => "PROJECT SESHAT";

    public string PlatformName => "Galactic Research Platform";

    public string StatisticsHeading => "STATISTICS";

    public string SystemsIndexedLabel => "SYSTEMS INDEXED";

    public int SystemsIndexedCount
    {
        get => _systemsIndexedCount;
        private set => SetProperty(ref _systemsIndexedCount, value);
    }

    public string CommanderRecordsLabel => "COMMANDER RECORDS";

    public int CommanderRecordsCount
    {
        get => _commanderRecordsCount;
        private set => SetProperty(ref _commanderRecordsCount, value);
    }

    public string EvidenceRecordsLabel => "EVIDENCE RECORDS";

    public int EvidenceRecordsCount
    {
        get => _evidenceRecordsCount;
        private set => SetProperty(ref _evidenceRecordsCount, value);
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

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public ICommand LoadJournalFilesCommand { get; }

    public ICommand ToggleExploreViewCommand { get; }

    public ICommand SelectExploreItemCommand { get; }

    public bool ShowExploreView
    {
        get => _showExploreView;
        private set => SetProperty(ref _showExploreView, value);
    }

    public ObservableCollection<ExploreItem> ExploreItems { get; } = new();

    public string ExplorationSummary => $"{ExploreItems.Count} systems currently available for exploration and {EvidenceRecordsCount} evidence records to review.";

    public string SelectedExploreItemTitle
    {
        get => _selectedExploreItemTitle;
        private set => SetProperty(ref _selectedExploreItemTitle, value);
    }

    public string SelectedExploreItemDetail
    {
        get => _selectedExploreItemDetail;
        private set => SetProperty(ref _selectedExploreItemDetail, value);
    }

    public ExploreItem? SelectedExploreItem
    {
        get => _selectedExploreItem;
        set
        {
            if (SetProperty(ref _selectedExploreItem, value))
            {
                SelectExploreItem(value);
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void ToggleExploreView()
    {
        ShowExploreView = !ShowExploreView;
        if (!ShowExploreView)
        {
            SelectedExploreItemTitle = "No system selected";
            SelectedExploreItemDetail = "Select a system to inspect what has already been searched and where deeper investigation may be useful.";
        }
    }

    private void SelectExploreItem(ExploreItem? item)
    {
        if (item is null)
        {
            SelectedExploreItemTitle = "No system selected";
            SelectedExploreItemDetail = "Select a system to inspect what has already been searched and where deeper investigation may be useful.";
            return;
        }

        SelectedExploreItemTitle = item.SystemName;
        SelectedExploreItemDetail = item.Detail;
        ShowExploreView = true;
    }

    private void RefreshExploreView()
    {
        ExploreItems.Clear();

        var systems = _starSystemRepository.CountAsync().GetAwaiter().GetResult();
        var evidence = _evidenceRepository.CountAsync().GetAwaiter().GetResult();

        if (systems == 0)
        {
            ExploreItems.Add(new ExploreItem("No systems yet", "Import journal files to start building a searchable list of systems and evidence."));
            return;
        }

        var sampleSystems = new List<string> { "LHS 3447", "Sol", "Achenar" };
        foreach (var systemName in sampleSystems.Take(Math.Min(3, systems)))
        {
            ExploreItems.Add(new ExploreItem(systemName, $"Evidence collected: {evidence}. This system is available for deeper observation and investigation."));
        }

        if (systems > 3)
        {
            ExploreItems.Add(new ExploreItem("More systems discovered", $"You have {systems} systems imported. Use this list as a guide to decide where to search next and where to investigate further."));
        }
    }

    private void LoadJournalFiles()
    {
        var resolvedPath = _journalPathResolver.ResolvePath();
        if (resolvedPath is null)
        {
            StatusMessage = "No journal path detected; configure Elite Dangerous journals manually";
            JournalPathStatus = "Journal files were not found. Configure the folder manually if needed.";
            LoadingProgress = "0/0 files";
            ActiveFileName = "No file selected";
            IsLoading = false;
            return;
        }

        var files = Directory.EnumerateFiles(resolvedPath, "Journal*.log", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();

        if (files.Count == 0)
        {
            StatusMessage = "Journal folder discovered, but no files were found";
            JournalPathStatus = $"Using journal folder: {resolvedPath}";
            LoadingProgress = "0/0 files";
            ActiveFileName = "No file selected";
            IsLoading = false;
            return;
        }

        IsLoading = true;
        StatusMessage = "Loading journal files";
        JournalPathStatus = $"Using journal folder: {resolvedPath}";
        LoadingProgress = $"0/{files.Count} files";
        ActiveFileName = "Preparing...";

        _ = Task.Run(async () =>
        {
            for (var index = 0; index < files.Count; index++)
            {
                var file = files[index];
                var fileName = Path.GetFileName(file);
                LoadingProgress = $"{index + 1}/{files.Count} files";
                ActiveFileName = fileName;
                StatusMessage = $"Scanning {fileName}";

                try
                {
                    await using var stream = File.OpenRead(file);
                    using var reader = new StreamReader(stream);
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    await _journalReader.ImportAsync(
                        reader,
                        _starSystemRepository,
                        _commanderRepository,
                        _evidenceRepository,
                        _journalImportTrackerRepository,
                        file,
                        timeoutCts.Token);
                }
                catch (OperationCanceledException)
                {
                    StatusMessage = $"Timed out while processing {fileName}; moving on to the next file";
                    ActiveFileName = fileName;
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Skipped {fileName}: {ex.Message}";
                    ActiveFileName = fileName;
                }

                SystemsIndexedCount = _starSystemRepository.CountAsync().GetAwaiter().GetResult();
                CommanderRecordsCount = _commanderRepository.CountAsync().GetAwaiter().GetResult();
                EvidenceRecordsCount = _evidenceRepository.CountAsync().GetAwaiter().GetResult();
                RefreshExploreView();
            }

            StatusMessage = "Journal import complete";
            LoadingProgress = $"{files.Count}/{files.Count} files";
            ActiveFileName = "Finished";
            RefreshExploreView();
            IsLoading = false;
        });
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
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

    private sealed class RelayCommand<T>(Action<T?> execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute(parameter is T typed ? typed : default);
    }
}

public sealed record ExploreItem(string SystemName, string Detail);
