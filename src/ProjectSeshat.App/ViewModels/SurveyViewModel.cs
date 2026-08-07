using System;
using System.Collections.ObjectModel;
using System.Linq;
using ProjectSeshat.Atlas;
using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.App.ViewModels;

/// <summary>Presents the persisted, source-of-truth atlas survey listing of frontier regions.</summary>
public sealed class SurveyViewModel : ViewModelBase
{
    private readonly AtlasService? _atlas;
    private readonly IStarSystemRepository? _systemRepository;
    private readonly ISurveyRegionRepository? _regionRepository;
    private string _summaryText = "No survey regions yet. Import journal files to build the survey.";

    public SurveyViewModel(
        AtlasService? atlas = null,
        IStarSystemRepository? systemRepository = null,
        ISurveyRegionRepository? regionRepository = null)
    {
        _atlas = atlas;
        _systemRepository = systemRepository;
        _regionRepository = regionRepository;
        Refresh();
    }

    public ObservableCollection<SurveyRegionItem> Regions { get; } = new();

    public string SummaryText
    {
        get => _summaryText;
        private set => SetProperty(ref _summaryText, value);
    }

    public void Refresh()
    {
        if (_atlas is not null && _systemRepository is not null && _regionRepository is not null)
        {
            _atlas.RefreshSurveyRegionsAsync(_systemRepository, _regionRepository).GetAwaiter().GetResult();
        }

        Regions.Clear();

        if (_regionRepository is null)
        {
            SummaryText = "Survey listing is unavailable in this context.";
            return;
        }

        var regions = _regionRepository.ListAsync(500).GetAwaiter().GetResult();
        var rank = 0;
        foreach (var region in regions)
        {
            Regions.Add(SurveyRegionItem.From(region, ++rank));
        }

        var uncharted = regions.Count(r => !r.Surveyed);
        SummaryText = $"{uncharted} frontier region{(uncharted == 1 ? "" : "s")} to survey · {regions.Count - uncharted} already charted.";
    }
}

public sealed record SurveyRegionItem(
    int Rank,
    string Label,
    string DistanceText,
    string ScoreText,
    string NearbyText,
    string StatusText,
    bool Surveyed)
{
    public static SurveyRegionItem From(SurveyRegion region, int rank)
    {
        var label = $"R {region.CellX},{region.CellY},{region.CellZ}";
        var distanceText = $"{(int)region.DistanceFromReferenceLy:N0} LY out";
        var scoreText = $"score {region.Score:0.0}";
        var nearbyText = $"{region.NearbyVisitedSystems} charted nearby";
        var statusText = region.Surveyed ? "Charted" : "Frontier";
        return new SurveyRegionItem(rank, label, distanceText, scoreText, nearbyText, statusText, region.Surveyed);
    }
}
