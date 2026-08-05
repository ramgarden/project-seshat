using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.App.ViewModels;

/// <summary>Presentation data for the system exploration page.</summary>
public sealed class ExplorationViewModel : ViewModelBase
{
    private readonly IStarSystemRepository _starSystemRepository;
    private readonly IEvidenceRepository _evidenceRepository;
    private readonly ICelestialBodyRepository? _celestialBodyRepository;
    private string _selectedExploreItemTitle = "No system selected";
    private string _selectedExploreItemDetail = "Select a system to inspect what has already been searched and where deeper investigation may be useful.";
    private ExploreItem? _selectedExploreItem;

    public ExplorationViewModel(
        IStarSystemRepository starSystemRepository,
        IEvidenceRepository evidenceRepository,
        ICelestialBodyRepository? celestialBodyRepository = null)
    {
        _starSystemRepository = starSystemRepository;
        _evidenceRepository = evidenceRepository;
        _celestialBodyRepository = celestialBodyRepository;

        SelectExploreItemCommand = new RelayCommand<ExploreItem>(SelectExploreItem);
        RefreshExploreView();
    }

    public ObservableCollection<ExploreItem> ExploreItems { get; } = new();

    public ObservableCollection<BodyItem> SelectedSystemBodies { get; } = new();

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
            ExploreItems.Add(new ExploreItem(0, "No systems yet", "Import journal files to start building a searchable list of systems and evidence."));
            OnPropertyChanged(nameof(ExplorationSummary));
            return;
        }

        // Load real system names from the repository (up to 50 for the list)
        var allSystems = _starSystemRepository.ListAsync(50).GetAwaiter().GetResult();
        foreach (var system in allSystems)
        {
            var bodyCount = _celestialBodyRepository?.CountForSystemAsync(system.Id).GetAwaiter().GetResult() ?? 0;
            var detail = bodyCount > 0
                ? $"{bodyCount} bod{(bodyCount == 1 ? "y" : "ies")} catalogued. Select to inspect bodies and observations."
                : "No bodies catalogued yet. Import journals to populate the atlas for this system.";
            ExploreItems.Add(new ExploreItem(system.Id.Value, system.Name, detail));
        }

        OnPropertyChanged(nameof(ExplorationSummary));
    }

    private int GetEvidenceRecordsCount()
    {
        return _evidenceRepository.CountAsync().GetAwaiter().GetResult();
    }

    private void SelectExploreItem(ExploreItem? item)
    {
        SelectedSystemBodies.Clear();

        if (item is null)
        {
            SelectedExploreItemTitle = "No system selected";
            SelectedExploreItemDetail = "Select a system to inspect what has already been searched and where deeper investigation may be useful.";
            return;
        }

        SelectedExploreItemTitle = item.SystemName;
        SelectedExploreItemDetail = item.Detail;

        if (_celestialBodyRepository is not null && item.SystemIdValue != 0)
        {
            var systemId = new StarSystemId(item.SystemIdValue);
            var bodies = _celestialBodyRepository.FindBySystemIdAsync(systemId).GetAwaiter().GetResult();
            foreach (var body in bodies.OrderBy(b => b.DistanceFromArrivalLs ?? double.MaxValue))
            {
                var label = body.Kind switch
                {
                    BodyKind.Star => $"\u2605 {body.Name}{(body.StarClass is not null ? $" [{body.StarClass}]" : string.Empty)}",
                    BodyKind.Planet => $"\u25cb {body.Name}{(body.PlanetClass is not null ? $" \u2014 {body.PlanetClass}" : string.Empty)}{(body.IsTerraformable == true ? " (Terraformable)" : string.Empty)}",
                    BodyKind.Moon => $"\u25cc {body.Name}",
                    BodyKind.AsteroidBelt => $"\u2234 {body.Name}",
                    _ => $"\u25a1 {body.Name}"
                };
                var distance = body.DistanceFromArrivalLs.HasValue
                    ? $"{body.DistanceFromArrivalLs.Value:N0} ls"
                    : "distance unknown";
                SelectedSystemBodies.Add(new BodyItem(label, distance));
            }
        }
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

public sealed record ExploreItem(long SystemIdValue, string SystemName, string Detail);

public sealed record BodyItem(string Label, string Distance);
