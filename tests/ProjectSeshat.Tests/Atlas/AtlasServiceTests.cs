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
