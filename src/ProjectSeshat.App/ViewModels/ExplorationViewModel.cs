using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ProjectSeshat.Core.Contracts;

namespace ProjectSeshat.App.ViewModels;

/// <summary>Presentation data for the system exploration page.</summary>
public sealed class ExplorationViewModel : ViewModelBase
{
    private readonly IStarSystemRepository _starSystemRepository;
    private readonly IEvidenceRepository _evidenceRepository;
    private string _selectedExploreItemTitle = "No system selected";
    private string _selectedExploreItemDetail = "Select a system to inspect what has already been searched and where deeper investigation may be useful.";
    private ExploreItem? _selectedExploreItem;

    public ExplorationViewModel(IStarSystemRepository starSystemRepository, IEvidenceRepository evidenceRepository)
    {
        _starSystemRepository = starSystemRepository;
        _evidenceRepository = evidenceRepository;

        SelectExploreItemCommand = new RelayCommand<ExploreItem>(SelectExploreItem);
        RefreshExploreView();
    }

    public ObservableCollection<ExploreItem> ExploreItems { get; } = new();

    public string ExplorationSummary => $"{ExploreItems.Count} systems currently available for exploration and {GetEvidenceRecordsCount()} evidence records to review.";

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

    public ICommand SelectExploreItemCommand { get; }

    public void RefreshExploreView()
    {
        ExploreItems.Clear();

        var systems = _starSystemRepository.CountAsync().GetAwaiter().GetResult();
        var evidence = GetEvidenceRecordsCount();

        if (systems == 0)
        {
            ExploreItems.Add(new ExploreItem("No systems yet", "Import journal files to start building a searchable list of systems and evidence."));
            OnPropertyChanged(nameof(ExplorationSummary));
            return;
        }

        var sampleSystems = new List<string> { "LHS 3447", "Sol", "Achenar" };
        foreach (var systemName in sampleSystems.Take(Math.Min(3, systems)))
        {
            var reasoning = systemName switch
            {
                "Sol" => "Cradle of humanity and Federation capital. High security presence, historical interest, and local planetary orbits make it a primary target for faction observation.",
                "Achenar" => "Seat of the Empire. Known for extreme-gravity worlds, major political hubs, and security alerts. A prime candidate for investigative scan evidence.",
                "LHS 3447" => "A high-density stellar neighborhood containing multiple stars and outposts. Ideal for baseline astronomical scans and traffic observation.",
                _ => $"Imported star system containing {evidence} total evidence reports in local archives. Available for observation."
            };
            ExploreItems.Add(new ExploreItem(systemName, reasoning));
        }

        if (systems > 3)
        {
            ExploreItems.Add(new ExploreItem("More systems discovered", $"You have {systems} systems imported. Use this list as a guide to decide where to search next and where to investigate further."));
        }

        OnPropertyChanged(nameof(ExplorationSummary));
    }

    private int GetEvidenceRecordsCount()
    {
        return _evidenceRepository.CountAsync().GetAwaiter().GetResult();
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
