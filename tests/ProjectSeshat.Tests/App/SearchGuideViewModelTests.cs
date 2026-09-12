using System.Threading;
using ProjectSeshat.App.ViewModels;
using ProjectSeshat.Atlas;
using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Core.Domain;
using Xunit;

namespace ProjectSeshat.Tests.App;

public sealed class SearchGuideViewModelTests
{
    [Fact]
    public async Task RefreshAsync_UsesItsOwnCancellationToken()
    {
        var systems = new TestSystemRepository(
            new StarSystem(new StarSystemId(1), "HOME BASE", new GalacticCoordinates(0, 0, 0), SystemSurveyState.FssScanned),
            new StarSystem(new StarSystemId(2), "NEXT", new GalacticCoordinates(0, 0, 50), SystemSurveyState.Unexplored));
        var viewModel = new SearchGuideViewModel(new AtlasService(), systems, new TestBodyRepository(), null);

        await viewModel.RefreshAsync();

        Assert.Equal("HOME BASE", viewModel.RecommendedGate);
        Assert.True(viewModel.HasRecommendedGate);
    }

    [Fact]
    public async Task RefreshAsync_CancelsStaleRefreshAndAppliesLatestRefresh()
    {
        var systems = new TestSystemRepository(
            new StarSystem(new StarSystemId(1), "HOME BASE", new GalacticCoordinates(0, 0, 0), SystemSurveyState.FssScanned),
            new StarSystem(new StarSystemId(2), "NEXT", new GalacticCoordinates(0, 0, 50), SystemSurveyState.Unexplored));
        systems.DelayNextList = true;
        var viewModel = new SearchGuideViewModel(new AtlasService(), systems, new TestBodyRepository(), null);
        using var cancellation = new CancellationTokenSource();
        var firstRefresh = viewModel.RefreshAsync(cancellation.Token);
        await systems.ListStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cancellation.Cancel();
        await firstRefresh;

        var latestRefresh = viewModel.RefreshAsync();
        await latestRefresh;

        Assert.Equal("HOME BASE", viewModel.RecommendedGate);
        Assert.True(viewModel.HasRecommendedGate);
    }

    [Fact]
    public async Task RefreshAsync_UpdatesRaxxlaMoveFromHighestPriorityIntel()
    {
        var systems = new TestSystemRepository(
            new StarSystem(
                new StarSystemId(1),
                "HOME BASE",
                new GalacticCoordinates(0, 0, 0),
                SystemSurveyState.Honked,
                NonBodySignals: 1,
                SignalTypes: "Thargoid"));
        var bodies = new TestBodyRepository(
            new CelestialBody(
                new CelestialBodyId(Guid.NewGuid()),
                new StarSystemId(1),
                "HOME BASE 8 A",
                BodyKind.Moon,
                null,
                null,
                null,
                100,
                ScanStatus.FssScanned,
                WorthDss: true));
        var navigation = new TestNavigationStateRepository();
        var viewModel = new SearchGuideViewModel(new AtlasService(), systems, bodies, navigation);
        NextAction? updatedAction = null;
        viewModel.NextActionUpdated += action => updatedAction = action;

        await viewModel.RefreshAsync();

        navigation.State = new NavigationState(
            new NavigationStateId(Guid.NewGuid()),
            new StarSystemId(1),
            DateTimeOffset.UtcNow);

        await viewModel.RefreshAsync();

        Assert.True(viewModel.HasRaxxlaMove);
        Assert.Equal("HOME BASE 8 A", viewModel.RaxxlaMoveTitle);
        Assert.Equal("MAP / DSS · HOME BASE", viewModel.RaxxlaMoveSubtitle);
        Assert.Contains("8th moon", viewModel.RaxxlaMoveDetail);
        Assert.Equal(NextActionKind.Fss, updatedAction?.Action);
        Assert.Equal("HOME BASE", updatedAction?.TargetSystemName);
    }

    private sealed class TestSystemRepository(params StarSystem[] systems) : IStarSystemRepository
    {
        private readonly List<StarSystem> _systems = systems.ToList();
        private int _delayNextList;

        public TaskCompletionSource ListStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool DelayNextList
        {
            set => Volatile.Write(ref _delayNextList, value ? 1 : 0);
        }

        public ValueTask<StarSystem?> FindByIdAsync(StarSystemId id, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_systems.FirstOrDefault(system => system.Id == id));

        public ValueTask SaveAsync(StarSystem system, CancellationToken cancellationToken = default)
        {
            var index = _systems.FindIndex(existing => existing.Id == system.Id);
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

        public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(_systems.Count);

        public Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult(_systems.Any(system => system.Name == name));

        public Task<StarSystem?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult(_systems.FirstOrDefault(system => system.Name == name));

        public Task<IReadOnlyList<StarSystem>> ListAsync(int maxCount, CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref _delayNextList) == 1)
            {
                Interlocked.Exchange(ref _delayNextList, 0);
                ListStarted.TrySetResult();
                return Task.Delay(1000, cancellationToken).ContinueWith(
                    _ => (IReadOnlyList<StarSystem>)Array.Empty<StarSystem>(),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }

            return Task.FromResult<IReadOnlyList<StarSystem>>(_systems.Take(maxCount).ToList());
        }

        public Task<IReadOnlyList<StarSystem>> ListWithPositionAsync(int maxCount, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StarSystem>>(_systems.Where(system => system.Position is not null).Take(maxCount).ToList());

        public Task<IReadOnlyList<StarSystem>> ListBySurveyStateAsync(
            SystemSurveyState state,
            int maxCount,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StarSystem>>(_systems.Where(system => system.SurveyState == state).Take(maxCount).ToList());
    }

    private sealed class TestBodyRepository(params CelestialBody[] bodies) : ICelestialBodyRepository
    {
        private readonly List<CelestialBody> _bodies = bodies.ToList();

        public ValueTask<CelestialBody?> FindByIdAsync(CelestialBodyId id, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_bodies.FirstOrDefault(body => body.Id == id));

        public ValueTask SaveAsync(CelestialBody body, CancellationToken cancellationToken = default)
        {
            var index = _bodies.FindIndex(existing => existing.Id == body.Id);
            if (index >= 0)
            {
                _bodies[index] = body;
            }
            else
            {
                _bodies.Add(body);
            }

            return ValueTask.CompletedTask;
        }

        public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(_bodies.Count);

        public Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult(_bodies.Any(body => body.Name == name));

        public Task<CelestialBody?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult(_bodies.FirstOrDefault(body => body.Name == name));

        public Task<IReadOnlyList<CelestialBody>> FindBySystemIdAsync(
            StarSystemId systemId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CelestialBody>>(_bodies.Where(body => body.SystemId == systemId).ToList());

        public Task<int> CountForSystemAsync(StarSystemId systemId, CancellationToken cancellationToken = default)
            => Task.FromResult(_bodies.Count(body => body.SystemId == systemId));

        public Task<int> CountNeedingSurfaceScanAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_bodies.Count(body => body.ScanStatus != ScanStatus.Mapped));

        public Task<IReadOnlyList<CelestialBody>> ListNeedingSurfaceScanAsync(
            int maxCount,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CelestialBody>>(_bodies.Where(body => body.ScanStatus != ScanStatus.Mapped).Take(maxCount).ToList());

        public Task<IReadOnlyList<CelestialBody>> ListDssCandidatesAsync(
            int maxCount,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CelestialBody>>(_bodies.Where(body => body.WorthDss && body.ScanStatus != ScanStatus.Mapped).Take(maxCount).ToList());

        public ValueTask UpdateScanStatusAsync(
            CelestialBodyId id,
            ScanStatus status,
            CancellationToken cancellationToken = default)
        {
            var index = _bodies.FindIndex(body => body.Id == id);
            if (index >= 0)
            {
                _bodies[index] = _bodies[index] with { ScanStatus = status };
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestNavigationStateRepository : INavigationStateRepository
    {
        private NavigationState? _state;

        public NavigationState? State
        {
            get => _state;
            set => _state = value;
        }

        public TestNavigationStateRepository(NavigationState? state = null)
        {
            State = state;
        }

        public Task<NavigationState?> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<NavigationState?>(State);

        public ValueTask SaveAsync(NavigationState state, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }
}
