using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using ProjectSeshat.Core.Domain;
using ProjectSeshat.ThreadEngine;

namespace ProjectSeshat.App.ViewModels;

/// <summary>Presentation data for the research-threads workflow page.</summary>
public sealed class ThreadsViewModel : ViewModelBase
{
    private readonly ResearchThreadEngine? _engine;
    private string _newThreadSubject = string.Empty;
    private string _newThreadNotes = string.Empty;
    private string _statusMessage = "No threads yet. Create one to begin a line of enquiry.";
    private ThreadItem? _selectedThread;

    public ThreadsViewModel(ResearchThreadEngine? engine)
    {
        _engine = engine;
        CreateThreadCommand = new RelayCommand(CreateThread);
        AdvanceCommand = new RelayCommand(AdvanceThread);
        ReopenCommand = new RelayCommand(ReopenThread);
        ConcludeCommand = new RelayCommand(ConcludeThread);
        RefreshThreads();
    }

    public ObservableCollection<ThreadItem> Threads { get; } = new();

    public string NewThreadSubject
    {
        get => _newThreadSubject;
        set => SetProperty(ref _newThreadSubject, value);
    }

    public string NewThreadNotes
    {
        get => _newThreadNotes;
        set => SetProperty(ref _newThreadNotes, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ThreadItem? SelectedThread
    {
        get => _selectedThread;
        set
        {
            if (SetProperty(ref _selectedThread, value))
            {
                SelectThread(value);
            }
        }
    }

    public string SelectedThreadTitle => SelectedThread?.Subject ?? "No thread selected";

    public string SelectedThreadDetail => SelectedThread is null
        ? "Select a research thread to review its subject, stage, and notes."
        : $"{SelectedThread.Subject} \u2014 {SelectedThread.StatusLabel}.\n{SelectedThread.DisplayNotes}";

    public ICommand CreateThreadCommand { get; }

    public ICommand AdvanceCommand { get; }

    public ICommand ReopenCommand { get; }

    public ICommand ConcludeCommand { get; }

    public void RefreshThreads()
    {
        Threads.Clear();

        if (_engine is null)
        {
            StatusMessage = "Research threads are unavailable in this context.";
            return;
        }

        var threads = _engine.ListAsync().GetAwaiter().GetResult();
        foreach (var thread in threads)
        {
            Threads.Add(new ThreadItem(thread.Id, thread.Subject, thread.Notes, thread.Status));
        }

        if (Threads.Count == 0)
        {
            StatusMessage = "No threads yet. Create one to begin a line of enquiry.";
        }
        else
        {
            StatusMessage = $"{Threads.Count} research thread{(Threads.Count == 1 ? "" : "s")}";
            OnPropertyChanged(nameof(SelectedThreadTitle));
            OnPropertyChanged(nameof(SelectedThreadDetail));
        }
    }

    private void CreateThread()
    {
        if (_engine is null)
        {
            StatusMessage = "Research threads are unavailable in this context.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewThreadSubject))
        {
            StatusMessage = "Provide a subject before creating a research thread.";
            return;
        }

        try
        {
            _engine
                .CreateThreadAsync(NewThreadSubject, notes: string.IsNullOrWhiteSpace(NewThreadNotes) ? null : NewThreadNotes)
                .GetAwaiter()
                .GetResult();
            NewThreadSubject = string.Empty;
            NewThreadNotes = string.Empty;
            StatusMessage = "Research thread created.";
            RefreshThreads();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not create thread: {ex.Message}";
        }
    }

    private void AdvanceThread()
    {
        if (_engine is null || SelectedThread is null)
        {
            return;
        }

        try
        {
            _engine.AdvanceAsync(SelectedThread.Id).GetAwaiter().GetResult();
            StatusMessage = $"Advanced '{SelectedThread.Subject}'.";
            RefreshThreads();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not advance thread: {ex.Message}";
        }
    }

    private void ReopenThread()
    {
        if (_engine is null || SelectedThread is null)
        {
            return;
        }

        try
        {
            _engine.ReopenAsync(SelectedThread.Id).GetAwaiter().GetResult();
            StatusMessage = $"Reopened '{SelectedThread.Subject}'.";
            RefreshThreads();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not reopen thread: {ex.Message}";
        }
    }

    private void ConcludeThread()
    {
        if (_engine is null || SelectedThread is null)
        {
            return;
        }

        try
        {
            _engine.ConcludeAsync(SelectedThread.Id, SelectedThread.Notes).GetAwaiter().GetResult();
            StatusMessage = $"Concluded '{SelectedThread.Subject}'.";
            RefreshThreads();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not conclude thread: {ex.Message}";
        }
    }

    private void SelectThread(ThreadItem? item)
    {
        OnPropertyChanged(nameof(SelectedThreadTitle));
        OnPropertyChanged(nameof(SelectedThreadDetail));
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

public sealed record ThreadItem(ResearchThreadId Id, string Subject, string? Notes, ThreadStatus Status)
{
    public string StatusLabel => Status switch
    {
        ThreadStatus.Open => "Open",
        ThreadStatus.Investigating => "Investigating",
        ThreadStatus.Concluded => "Concluded",
        ThreadStatus.Archived => "Archived",
        _ => Status.ToString()
    };

    public string DisplayNotes => string.IsNullOrWhiteSpace(Notes) ? "No notes recorded." : Notes;
}
