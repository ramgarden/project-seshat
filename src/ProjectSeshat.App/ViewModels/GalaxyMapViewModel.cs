using System;
using System.Collections.Generic;
using System.Linq;
using ProjectSeshat.Atlas;
using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.App.ViewModels;

/// <summary>Presents the interactive 3D galactic sky-map of surveyed systems and regions.</summary>
public sealed class GalaxyMapViewModel : ViewModelBase
{
    private readonly AtlasService? _atlas;
    private readonly IStarSystemRepository? _systemRepository;
    private readonly INavigationStateRepository? _navigationRepository;
    private readonly ISurveyRegionRepository? _regionRepository;
    private string _currentSystemText = "Current position unknown";
    private IReadOnlyList<SkyPoint> _skyMapPoints = Array.Empty<SkyPoint>();

    public GalaxyMapViewModel(
        AtlasService? atlas = null,
        IStarSystemRepository? systemRepository = null,
        INavigationStateRepository? navigationRepository = null,
        ISurveyRegionRepository? regionRepository = null)
    {
        _atlas = atlas;
        _systemRepository = systemRepository;
        _navigationRepository = navigationRepository;
        _regionRepository = regionRepository;
        Refresh();
    }

    public string CurrentSystemText
    {
        get => _currentSystemText;
        private set => SetProperty(ref _currentSystemText, value);
    }

    public IReadOnlyList<SkyPoint> SkyMapPoints
    {
        get => _skyMapPoints;
        private set => SetProperty(ref _skyMapPoints, value);
    }

    public void Refresh()
    {
        if (_systemRepository is null)
        {
            SkyMapPoints = Array.Empty<SkyPoint>();
            CurrentSystemText = "Current position unknown";
            return;
        }

        var guide = _atlas?.BuildSearchGuideAsync(_systemRepository, new NullBodyRepository(), _navigationRepository).GetAwaiter().GetResult();

        CurrentSystemText = guide?.CurrentSystemName is not null
            ? $"You are at {guide.CurrentSystemName}"
            : "Current position unknown";

        var points = new List<SkyPoint>();

        var systems = _systemRepository.ListWithPositionAsync(1000).GetAwaiter().GetResult();
        foreach (var system in systems)
        {
            if (system.Position is not null)
            {
                points.Add(new SkyPoint(system.Position, system.Name, SkyPointKind.System));
            }
        }

        if (_regionRepository is not null)
        {
            // Source of truth: draw the persisted frontier (unsurveyed) regions on the map.
            var regions = _regionRepository.ListUnsurveyedAsync(200).GetAwaiter().GetResult();
            foreach (var region in regions)
            {
                points.Add(new SkyPoint(region.Center, "Undiscovered region", SkyPointKind.Region));
            }
        }
        else if (_atlas is not null)
        {
            var regions = _atlas.RankUndiscoveredRegionsAsync(_systemRepository, maxRegions: 12).GetAwaiter().GetResult();
            foreach (var region in regions)
            {
                points.Add(new SkyPoint(region.Center, "Undiscovered region", SkyPointKind.Region));
            }
        }

        if (guide?.CurrentPosition is not null)
        {
            points.Add(new SkyPoint(guide.CurrentPosition, guide.CurrentSystemName ?? "You", SkyPointKind.Current));
        }

        if (guide is not null && guide.NeedHonk.Count > 0)
        {
            var next = _systemRepository.FindByNameAsync(guide.NeedHonk[0].SystemName).GetAwaiter().GetResult();
            if (next?.Position is not null)
            {
                points.Add(new SkyPoint(next.Position, next.Name, SkyPointKind.Next));
            }
        }

        SkyMapPoints = points;
    }

    /// <summary>Minimal empty body repository so the sky-map can reuse the search-guide builder.</summary>
    private sealed class NullBodyRepository : ICelestialBodyRepository
    {
        public ValueTask<CelestialBody?> FindByIdAsync(CelestialBodyId id, CancellationToken cancellationToken = default) => ValueTask.FromResult<CelestialBody?>(null);
        public ValueTask SaveAsync(CelestialBody body, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<CelestialBody?> FindByNameAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult<CelestialBody?>(null);
        public Task<IReadOnlyList<CelestialBody>> FindBySystemIdAsync(StarSystemId systemId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CelestialBody>>(Array.Empty<CelestialBody>());
        public Task<int> CountForSystemAsync(StarSystemId systemId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> CountNeedingSurfaceScanAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<CelestialBody>> ListNeedingSurfaceScanAsync(int maxCount, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CelestialBody>>(Array.Empty<CelestialBody>());
        public Task<IReadOnlyList<CelestialBody>> ListDssCandidatesAsync(int maxCount, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CelestialBody>>(Array.Empty<CelestialBody>());
        public ValueTask UpdateScanStatusAsync(CelestialBodyId id, ScanStatus status, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
