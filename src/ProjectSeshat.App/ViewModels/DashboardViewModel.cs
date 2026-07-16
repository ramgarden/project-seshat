using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Journals;

namespace ProjectSeshat.App.ViewModels;

/// <summary>Presentation data for the research dashboard page.</summary>
public sealed class DashboardViewModel : ViewModelBase
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
    private int _systemsIndexedCount;
    private int _commanderRecordsCount;
    private int _evidenceRecordsCount;
    private bool _isLoading;

    public DashboardViewModel(
        IStarSystemRepository starSystemRepository,
        ICommanderRepository commanderRepository,
        IEvidenceRepository evidenceRepository,
        IJournalImportTrackerRepository? journalImportTrackerRepository = null,
        JournalPathResolver? journalPathResolver = null,
        JournalReader? journalReader = null)
    {
        _starSystemRepository = starSystemRepository;
        _commanderRepository = commanderRepository;
        _evidenceRepository = evidenceRepository;
        _journalImportTrackerRepository = journalImportTrackerRepository;
        _journalPathResolver = journalPathResolver ?? new JournalPathResolver();
        _journalReader = journalReader ?? new JournalReader();

        LoadJournalFilesCommand = new RelayCommand(LoadJournalFiles);
        RefreshStats();
    }

    public string StatisticsHeading => "STATISTICS";
    public string SystemsIndexedLabel => "SYSTEMS INDEXED";
    public string CommanderRecordsLabel => "COMMANDER RECORDS";
    public string EvidenceRecordsLabel => "EVIDENCE RECORDS";

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

    public event EventHandler? DataImported;

    public void RefreshStats()
    {
        SystemsIndexedCount = _starSystemRepository.CountAsync().GetAwaiter().GetResult();
        CommanderRecordsCount = _commanderRepository.CountAsync().GetAwaiter().GetResult();
        EvidenceRecordsCount = _evidenceRepository.CountAsync().GetAwaiter().GetResult();
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

                RefreshStats();
                DataImported?.Invoke(this, EventArgs.Empty);
            }

            StatusMessage = "Journal import complete";
            LoadingProgress = $"{files.Count}/{files.Count} files";
            ActiveFileName = "Finished";
            RefreshStats();
            DataImported?.Invoke(this, EventArgs.Empty);
            IsLoading = false;
        });
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
