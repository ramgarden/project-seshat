using ProjectSeshat.App.ViewModels;
using ProjectSeshat.Atlas;
using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Core.Domain;
using Xunit;

namespace ProjectSeshat.Tests.Atlas;

public sealed class AtlasServiceTests
{
    private static InMemorySystemRepository NewRepo(params GalacticCoordinates[] positions)
    {
        var repo = new InMemorySystemRepository();
        var id = 1L;
        foreach (var position in positions)
        {
            repo.Add(new StarSystem(new StarSystemId(id++), $"SYSTEM {id - 1}", position));
        }

        return repo;
    }

    [Fact]
    public async Task Cluster_YieldsFrontierRegions()
    {
        // A tight cluster of visited systems centred near the origin in one cell.
        var repo = NewRepo(
            new GalacticCoordinates(10, 0, 0),
            new GalacticCoordinates(-10, 0, 0),
            new GalacticCoordinates(0, 10, 0),
            new GalacticCoordinates(0, -10, 0),
            new GalacticCoordinates(0, 0, 10),
            new GalacticCoordinates(0, 0, -10));

        var atlas = new AtlasService();

        var snapshot = await atlas.GetSurveySnapshotAsync(repo);
        Assert.Equal(6, snapshot.SurveyedSystems);

        var regions = await atlas.RankUndiscoveredRegionsAsync(repo, maxRegions: 10);
        Assert.NotEmpty(regions);
        // Regions must be near the surveyed cluster, and ranked by proximity first.
        Assert.All(regions, r => Assert.True(r.DistanceFromReferenceLy < 4000));
    }

    [Fact]
    public async Task NoVisitedSystems_ReturnsEmpty()
    {
        var atlas = new AtlasService();

        var snapshot = await atlas.GetSurveySnapshotAsync(new InMemorySystemRepository());
        var regions = await atlas.RankUndiscoveredRegionsAsync(new InMemorySystemRepository());

        Assert.Equal(0, snapshot.SurveyedSystems);
        Assert.Empty(regions);
    }

    [Fact]
    public async Task SearchGuide_TiersSystemsBySurveyState()
    {
        var systems = new InMemorySystemRepository();
        systems.Add(new StarSystem(new StarSystemId(1), "HONK ME", new GalacticCoordinates(0, 0, 0), SystemSurveyState.Unexplored));
        systems.Add(new StarSystem(new StarSystemId(2), "FSS ME", new GalacticCoordinates(100, 0, 0), SystemSurveyState.Honked, NonBodySignals: 4));
        systems.Add(new StarSystem(new StarSystemId(3), "DONE", new GalacticCoordinates(200, 0, 0), SystemSurveyState.FssScanned));

        var bodies = new InMemoryBodyRepository();
        bodies.Add(new CelestialBody(
            new CelestialBodyId(Guid.NewGuid()),
            new StarSystemId(3),
            "DONE 1",
            BodyKind.Planet,
            null,
            "Earthlike body",
            null,
            500,
            ScanStatus.FssScanned,
            WorthDss: true));

        var atlas = new AtlasService();
        var guide = await atlas.BuildSearchGuideAsync(systems, bodies);

        var honk = Assert.Single(guide.NeedHonk);
        Assert.Equal("HONK ME", honk.SystemName);

        var fss = Assert.Single(guide.NeedFss);
        Assert.Equal("FSS ME", fss.SystemName);
        Assert.Equal(4, fss.Signals);

        var dss = Assert.Single(guide.NeedDss);
        Assert.Equal("DONE 1", dss.BodyName);
        Assert.Equal("DONE", dss.SystemName);
        Assert.Contains("Earthlike", dss.Reason);
    }

    [Fact]
    public async Task OutwardCrawl_OrdersNearestUnsearchedFirst()
    {
        var systems = new InMemorySystemRepository();
        var here = new StarSystem(new StarSystemId(1), "HERE", new GalacticCoordinates(0, 0, 0), SystemSurveyState.FssScanned);
        systems.Add(here);
        systems.Add(new StarSystem(new StarSystemId(2), "NEAR", new GalacticCoordinates(0, 0, 100), SystemSurveyState.Unexplored));
        systems.Add(new StarSystem(new StarSystemId(3), "FAR", new GalacticCoordinates(0, 0, 1000), SystemSurveyState.Unexplored));
        var nav = new InMemoryNavigationStateRepository(
            new NavigationState(new NavigationStateId(Guid.NewGuid()), here.Id, DateTimeOffset.UtcNow));

        var atlas = new AtlasService();
        var crawl = await atlas.BuildOutwardCrawlAsync(systems, navigationRepository: nav);

        Assert.Equal(2, crawl.Route.Count);
        Assert.Equal("NEAR", crawl.Route[0].SystemName);
        Assert.Equal("FAR", crawl.Route[1].SystemName);
        Assert.Equal("Jump", crawl.NextStep?.Action);
        Assert.Equal("NEAR", crawl.NextStep?.Target);
        Assert.Equal("HERE", crawl.CurrentSystemName);
        Assert.NotNull(crawl.RecommendedGate);
    }

    [Fact]
    public async Task OutwardCrawl_AutoAdvances_WhenCurrentSystemHasNoWork()
    {
        var systems = new InMemorySystemRepository();
        var here = new StarSystem(new StarSystemId(1), "HERE", new GalacticCoordinates(0, 0, 0), SystemSurveyState.FssScanned);
        systems.Add(here);
        systems.Add(new StarSystem(new StarSystemId(2), "NEXT", new GalacticCoordinates(0, 0, 50), SystemSurveyState.Unexplored));
        var nav = new InMemoryNavigationStateRepository(
            new NavigationState(new NavigationStateId(Guid.NewGuid()), here.Id, DateTimeOffset.UtcNow));

        var atlas = new AtlasService();
        var crawl = await atlas.BuildOutwardCrawlAsync(systems, navigationRepository: nav);

        // Nothing left to FSS/DSS here → the guide should tell us to jump on.
        Assert.Equal("Jump", crawl.NextStep?.Action);
        Assert.Equal("NEXT", crawl.NextStep?.Target);
    }

    [Fact]
    public async Task OutwardCrawl_PrefersInSystemHonkBeforeLeaving()
    {
        var systems = new InMemorySystemRepository();
        var here = new StarSystem(new StarSystemId(1), "HERE", new GalacticCoordinates(0, 0, 0), SystemSurveyState.Unexplored);
        systems.Add(here);
        systems.Add(new StarSystem(new StarSystemId(2), "NEXT", new GalacticCoordinates(0, 0, 50), SystemSurveyState.Unexplored));
        var nav = new InMemoryNavigationStateRepository(
            new NavigationState(new NavigationStateId(Guid.NewGuid()), here.Id, DateTimeOffset.UtcNow));

        var atlas = new AtlasService();
        var crawl = await atlas.BuildOutwardCrawlAsync(systems, navigationRepository: nav);

        Assert.Equal("Honk", crawl.NextStep?.Action);
        Assert.Equal("HERE", crawl.NextStep?.Target);
    }

    [Fact]
    public async Task OutwardCrawl_PrefersFssWorkInCurrentSystem()
    {
        var systems = new InMemorySystemRepository();
        var here = new StarSystem(new StarSystemId(1), "HERE", new GalacticCoordinates(0, 0, 0), SystemSurveyState.Honked, NonBodySignals: 3, SignalTypes: "Biological");
        systems.Add(here);
        systems.Add(new StarSystem(new StarSystemId(2), "NEXT", new GalacticCoordinates(0, 0, 50), SystemSurveyState.Unexplored));
        var nav = new InMemoryNavigationStateRepository(
            new NavigationState(new NavigationStateId(Guid.NewGuid()), here.Id, DateTimeOffset.UtcNow));

        var atlas = new AtlasService();
        var crawl = await atlas.BuildOutwardCrawlAsync(systems, navigationRepository: nav);

        Assert.Equal("FSS", crawl.NextStep?.Action);
        Assert.Equal("HERE", crawl.NextStep?.Target);
        Assert.Equal("Biological", crawl.NextStep?.Detail);
    }

    [Fact]
    public async Task OutwardCrawl_PrefersDssWorkInCurrentSystem()
    {
        var systems = new InMemorySystemRepository();
        var here = new StarSystem(new StarSystemId(1), "HERE", new GalacticCoordinates(0, 0, 0), SystemSurveyState.FssScanned);
        systems.Add(here);
        var bodies = new InMemoryBodyRepository();
        bodies.Add(new CelestialBody(
            new CelestialBodyId(Guid.NewGuid()),
            here.Id,
            "HERE 1",
            BodyKind.Planet,
            null,
            "Earthlike body",
            null,
            500,
            ScanStatus.FssScanned,
            WorthDss: true));
        var nav = new InMemoryNavigationStateRepository(
            new NavigationState(new NavigationStateId(Guid.NewGuid()), here.Id, DateTimeOffset.UtcNow));

        var atlas = new AtlasService();
        var crawl = await atlas.BuildOutwardCrawlAsync(systems, bodyRepository: bodies, navigationRepository: nav);

        Assert.Equal("DSS", crawl.NextStep?.Action);
        Assert.Equal("HERE 1", crawl.NextStep?.Target);
    }

    [Fact]
    public async Task OutwardCrawl_BackTracksThroughChartedStar_WhenNeighbourhoodExhausted()
    {
        var systems = new InMemorySystemRepository();
        var here = new StarSystem(new StarSystemId(1), "HERE", new GalacticCoordinates(0, 0, 0), SystemSurveyState.FssScanned);
        systems.Add(here);
        // Charted stepping stones between here and the distant frontier.
        systems.Add(new StarSystem(new StarSystemId(2), "ONWAY", new GalacticCoordinates(0, 0, 50), SystemSurveyState.FssScanned));
        // The only unsearched system is far beyond the local neighbourhood.
        systems.Add(new StarSystem(new StarSystemId(3), "FRONTIER", new GalacticCoordinates(0, 0, 1000), SystemSurveyState.Unexplored));
        var nav = new InMemoryNavigationStateRepository(
            new NavigationState(new NavigationStateId(Guid.NewGuid()), here.Id, DateTimeOffset.UtcNow));

        var atlas = new AtlasService();
        var crawl = await atlas.BuildOutwardCrawlAsync(systems, navigationRepository: nav, neighbourhoodRadiusLy: 60);

        Assert.NotEmpty(crawl.Route);
        Assert.Equal(CrawlHopKind.BackTrack, crawl.Route[0].Kind);
        Assert.Equal("ONWAY", crawl.Route[0].SystemName);
        Assert.Equal("Back-track", crawl.NextStep?.Action);
        Assert.Equal("ONWAY", crawl.NextStep?.Target);
    }

    [Fact]
    public async Task OutwardCrawl_LongJumps_WhenNoChartedWaypointExists()
    {
        var systems = new InMemorySystemRepository();
        var here = new StarSystem(new StarSystemId(1), "HERE", new GalacticCoordinates(0, 0, 0), SystemSurveyState.FssScanned);
        systems.Add(here);
        // No charted stepping stone: must stretch-jump straight to the frontier.
        systems.Add(new StarSystem(new StarSystemId(3), "FRONTIER", new GalacticCoordinates(0, 0, 1000), SystemSurveyState.Unexplored));
        var nav = new InMemoryNavigationStateRepository(
            new NavigationState(new NavigationStateId(Guid.NewGuid()), here.Id, DateTimeOffset.UtcNow));

        var atlas = new AtlasService();
        var crawl = await atlas.BuildOutwardCrawlAsync(systems, navigationRepository: nav, neighbourhoodRadiusLy: 60);

        Assert.Equal(CrawlHopKind.Jump, crawl.Route[0].Kind);
        Assert.Equal("FRONTIER", crawl.Route[0].SystemName);
        Assert.Equal("Jump", crawl.NextStep?.Action);
    }

    [Fact]
    public async Task OutwardCrawl_NoNextStep_WhenEverythingIsSearched()
    {
        var systems = new InMemorySystemRepository();
        var here = new StarSystem(new StarSystemId(1), "HERE", new GalacticCoordinates(0, 0, 0), SystemSurveyState.FssScanned);
        systems.Add(here);
        systems.Add(new StarSystem(new StarSystemId(2), "SEARCHED", new GalacticCoordinates(0, 0, 100), SystemSurveyState.FssScanned));
        var nav = new InMemoryNavigationStateRepository(
            new NavigationState(new NavigationStateId(Guid.NewGuid()), here.Id, DateTimeOffset.UtcNow));

        var atlas = new AtlasService();
        var crawl = await atlas.BuildOutwardCrawlAsync(systems, navigationRepository: nav);

        Assert.Empty(crawl.Route);
        Assert.Null(crawl.NextStep);
    }

    [Fact]
    public async Task SearchGate_PrefersDenseUnsearchedNeighbourhood()
    {
        var systems = new InMemorySystemRepository();
        var here = new StarSystem(new StarSystemId(1), "HOME BASE", new GalacticCoordinates(0, 0, 0), SystemSurveyState.FssScanned);
        systems.Add(here);
        // A cluster of unsearched systems right next to the base.
        systems.Add(new StarSystem(new StarSystemId(2), "UNSEARCHED A", new GalacticCoordinates(0, 0, 60), SystemSurveyState.Unexplored));
        systems.Add(new StarSystem(new StarSystemId(3), "UNSEARCHED B", new GalacticCoordinates(0, 0, 70), SystemSurveyState.Unexplored));
        systems.Add(new StarSystem(new StarSystemId(4), "UNSEARCHED C", new GalacticCoordinates(0, 0, 80), SystemSurveyState.Unexplored));
        // A far, isolated charted base with no local unsearched systems.
        systems.Add(new StarSystem(new StarSystemId(5), "LONELY BASE", new GalacticCoordinates(5000, 0, 0), SystemSurveyState.FssScanned));
        var nav = new InMemoryNavigationStateRepository(
            new NavigationState(new NavigationStateId(Guid.NewGuid()), here.Id, DateTimeOffset.UtcNow));

        var atlas = new AtlasService();
        var gate = await atlas.RecommendSearchGateAsync(systems, navigationRepository: nav);

        Assert.NotNull(gate);
        Assert.Equal("HOME BASE", gate!.SystemName);
        Assert.Contains("unsearched", gate.Reasoning);
    }

    [Fact]
    public async Task SearchGate_ExcludesUnsearchedSystemsAsCandidates()
    {
        var systems = new InMemorySystemRepository();
        systems.Add(new StarSystem(new StarSystemId(1), "BASE", new GalacticCoordinates(0, 0, 0), SystemSurveyState.FssScanned));
        systems.Add(new StarSystem(new StarSystemId(2), "VIRGIN", new GalacticCoordinates(0, 0, 30), SystemSurveyState.Unexplored));
        var nav = new InMemoryNavigationStateRepository(
            new NavigationState(new NavigationStateId(Guid.NewGuid()), new StarSystemId(1), DateTimeOffset.UtcNow));

        var atlas = new AtlasService();
        var gate = await atlas.RecommendSearchGateAsync(systems, navigationRepository: nav);

        Assert.NotNull(gate);
        Assert.Equal("BASE", gate!.SystemName);
    }

    [Fact]
    public async Task SearchGuide_PlotsHonkRouteFromCurrentPosition()
    {
        var here = new StarSystem(new StarSystemId(1), "HERE", new GalacticCoordinates(0, 0, 0), SystemSurveyState.FssScanned);
        var near = new StarSystem(new StarSystemId(2), "NEAR", new GalacticCoordinates(0, 0, 100), SystemSurveyState.Unexplored);
        var far = new StarSystem(new StarSystemId(3), "FAR", new GalacticCoordinates(0, 0, 1000), SystemSurveyState.Unexplored);
        var systems = new InMemorySystemRepository();
        systems.Add(here);
        systems.Add(near);
        systems.Add(far);

        var nav = new InMemoryNavigationStateRepository(
            new NavigationState(new NavigationStateId(Guid.NewGuid()), here.Id, DateTimeOffset.UtcNow));

        var atlas = new AtlasService();
        var guide = await atlas.BuildSearchGuideAsync(systems, new InMemoryBodyRepository(), nav);

        Assert.Equal("HERE", guide.CurrentSystemName);
        Assert.Equal(2, guide.NeedHonk.Count);
        // Nearest unexplored system to the commander is plotted first.
        Assert.Equal("NEAR", guide.NeedHonk[0].SystemName);
        Assert.Equal("FAR", guide.NeedHonk[1].SystemName);
        Assert.True(guide.NeedHonk[0].DistanceLy < guide.NeedHonk[1].DistanceLy);
    }

    [Fact]
    public async Task SearchGuide_WithoutNavigation_FallsBackToCentroid()
    {
        var systems = new InMemorySystemRepository();
        systems.Add(new StarSystem(new StarSystemId(1), "LEFT", new GalacticCoordinates(-1000, 0, 0), SystemSurveyState.Unexplored));
        systems.Add(new StarSystem(new StarSystemId(2), "RIGHT", new GalacticCoordinates(1000, 0, 0), SystemSurveyState.Unexplored));

        var atlas = new AtlasService();
        var guide = await atlas.BuildSearchGuideAsync(systems, new InMemoryBodyRepository(), null);

        Assert.Null(guide.CurrentSystemName);
        Assert.Equal(2, guide.NeedHonk.Count);
    }

    [Fact]
    public void SkyMap_ExposesCurrentSystemAndNextTarget()
    {
        var here = new StarSystem(new StarSystemId(1), "HERE", new GalacticCoordinates(0, 0, 0), SystemSurveyState.FssScanned);
        var next = new StarSystem(new StarSystemId(2), "NEXT", new GalacticCoordinates(0, 0, 100), SystemSurveyState.Unexplored);
        var systems = new InMemorySystemRepository();
        systems.Add(here);
        systems.Add(next);

        var nav = new InMemoryNavigationStateRepository(
            new NavigationState(new NavigationStateId(Guid.NewGuid()), here.Id, DateTimeOffset.UtcNow));

        var viewModel = new GalaxyMapViewModel(new AtlasService(), systems, nav);

        Assert.Contains(viewModel.SkyMapPoints, p => p.Kind == SkyPointKind.Current && p.Name == "HERE");
        Assert.Contains(viewModel.SkyMapPoints, p => p.Kind == SkyPointKind.Next && p.Name == "NEXT");
        Assert.Contains(viewModel.SkyMapPoints, p => p.Kind == SkyPointKind.System);
    }

    [Fact]
    public void GuideTarget_Fss_ExplainsWhySignalTypesMatter()
    {
        var target = ProjectSeshat.App.ViewModels.GuideTarget.Fss(
            new FssTarget("SIGNALS HERE", 3, 50, "Biological,Geological"));

        Assert.Contains("Biological", target.Detail);
        Assert.Contains("Geological", target.Detail);
        Assert.Contains("SIGNALS HERE", target.Title);
    }

    [Fact]
    public async Task RefreshSurveyRegions_PersistsThenMarksCharted()
    {
        var systems = new InMemorySystemRepository();
        systems.Add(new StarSystem(new StarSystemId(1), "HOME", new GalacticCoordinates(0, 0, 0), SystemSurveyState.FssScanned));
        var regions = new InMemorySurveyRegionRepository();
        var atlas = new AtlasService();

        await atlas.RefreshSurveyRegionsAsync(systems, regions);
        var before = await regions.ListAsync(1000);
        Assert.NotEmpty(before);

        var frontier = before.First(r => !r.Surveyed);

        // Chart the frontier cell by placing a system inside it, then refresh again.
        systems.Add(new StarSystem(new StarSystemId(99), "EXPEDITION", frontier.Center, SystemSurveyState.FssScanned));
        await atlas.RefreshSurveyRegionsAsync(systems, regions);

        var after = await regions.ListAsync(1000);
        Assert.Contains(after, r => r.CellX == frontier.CellX && r.CellY == frontier.CellY && r.CellZ == frontier.CellZ && r.Surveyed);
    }

    private sealed class InMemorySurveyRegionRepository : ISurveyRegionRepository
    {
        private readonly List<SurveyRegion> _regions = new();

        public Task<IReadOnlyList<SurveyRegion>> ListAsync(int maxCount, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SurveyRegion>>(
                _regions.OrderBy(r => r.Surveyed).ThenByDescending(r => r.Score).Take(maxCount).ToList());

        public Task<IReadOnlyList<SurveyRegion>> ListUnsurveyedAsync(int maxCount, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SurveyRegion>>(
                _regions.Where(r => !r.Surveyed).OrderByDescending(r => r.Score).Take(maxCount).ToList());

        public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(_regions.Count);

        public ValueTask SaveAllAsync(IEnumerable<SurveyRegion> regions, CancellationToken cancellationToken = default)
        {
            foreach (var region in regions)
            {
                var i = _regions.FindIndex(r => r.CellX == region.CellX && r.CellY == region.CellY && r.CellZ == region.CellZ);
                if (i >= 0) _regions[i] = region; else _regions.Add(region);
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class InMemoryNavigationStateRepository : INavigationStateRepository
    {
        private NavigationState? _state;

        public InMemoryNavigationStateRepository(NavigationState? state = null)
        {
            _state = state;
        }

        public Task<NavigationState?> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_state);

        public ValueTask SaveAsync(NavigationState state, CancellationToken cancellationToken = default)
        {
            _state = state;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class InMemoryBodyRepository : ICelestialBodyRepository
    {
        private readonly List<CelestialBody> _bodies = new();

        public void Add(CelestialBody body) => _bodies.Add(body);

        public ValueTask<CelestialBody?> FindByIdAsync(CelestialBodyId id, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_bodies.FirstOrDefault(b => b.Id == id));

        public ValueTask SaveAsync(CelestialBody body, CancellationToken cancellationToken = default)
        {
            var i = _bodies.FindIndex(b => b.Id == body.Id);
            if (i >= 0) _bodies[i] = body; else _bodies.Add(body);
            return ValueTask.CompletedTask;
        }

        public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(_bodies.Count);

        public Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult(_bodies.Any(b => b.Name == name));

        public Task<CelestialBody?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult(_bodies.FirstOrDefault(b => b.Name == name));

        public Task<IReadOnlyList<CelestialBody>> FindBySystemIdAsync(StarSystemId systemId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CelestialBody>>(_bodies.Where(b => b.SystemId == systemId).ToList());

        public Task<int> CountForSystemAsync(StarSystemId systemId, CancellationToken cancellationToken = default)
            => Task.FromResult(_bodies.Count(b => b.SystemId == systemId));

        public Task<int> CountNeedingSurfaceScanAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_bodies.Count(b => b.ScanStatus != ScanStatus.Mapped));

        public Task<IReadOnlyList<CelestialBody>> ListNeedingSurfaceScanAsync(int maxCount, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CelestialBody>>(_bodies.Where(b => b.ScanStatus != ScanStatus.Mapped).Take(maxCount).ToList());

        public Task<IReadOnlyList<CelestialBody>> ListDssCandidatesAsync(int maxCount, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CelestialBody>>(_bodies.Where(b => b.WorthDss && b.ScanStatus != ScanStatus.Mapped).Take(maxCount).ToList());

        public ValueTask UpdateScanStatusAsync(CelestialBodyId id, ScanStatus status, CancellationToken cancellationToken = default)
        {
            var i = _bodies.FindIndex(b => b.Id == id);
            if (i >= 0) _bodies[i] = _bodies[i] with { ScanStatus = status };
            return ValueTask.CompletedTask;
        }
    }

    private sealed class InMemorySystemRepository : IStarSystemRepository
    {
        private readonly List<StarSystem> _systems = new();

        public void Add(StarSystem system) => _systems.Add(system);

        public ValueTask<StarSystem?> FindByIdAsync(StarSystemId id, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_systems.FirstOrDefault(s => s.Id == id));

        public ValueTask SaveAsync(StarSystem system, CancellationToken cancellationToken = default)
        {
            var index = _systems.FindIndex(s => s.Id == system.Id);
            if (index >= 0)
            {
                _systems[index] = system;
            }
            else
            {
                _systems.Add(system);
            }

            return ValueTask.CompletedTask;
        }

        public Task<int> CountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_systems.Count);

        public Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult(_systems.Any(s => s.Name == name));

        public Task<StarSystem?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult(_systems.FirstOrDefault(s => s.Name == name));

        public Task<IReadOnlyList<StarSystem>> ListAsync(int maxCount, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StarSystem>>(_systems.Take(maxCount).ToList());

        public Task<IReadOnlyList<StarSystem>> ListWithPositionAsync(int maxCount, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StarSystem>>(
                _systems.Where(s => s.Position is not null).Take(maxCount).ToList());

        public Task<IReadOnlyList<StarSystem>> ListBySurveyStateAsync(SystemSurveyState state, int maxCount, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StarSystem>>(
                _systems.Where(s => s.SurveyState == state).Take(maxCount).ToList());
    }
}
