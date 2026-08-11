using System.Net;
using ProjectSeshat.Community;
using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Core.Domain;
using Xunit;

namespace ProjectSeshat.Tests.Community;

public sealed class EddnMessageParserTests
{
    [Fact]

    public void Parse_FsdJump_ExtractsSystemAndPosition()
    {
        const string json = """
        {"$schemaRef":"https://eddn.edcd.io/schemas/journal/1","header":{"uploaderID":"x","gatewayTimestamp":"2024-01-01T00:00:00Z"},"message":{"timestamp":"2024-01-01T00:00:00Z","event":"FSDJump","StarSystem":"LHS 3447","StarPos":[-23.4,-72.4,-35.3],"StarClass":"K"}}
        """;

        var parsed = EddnMessageParser.Parse(json);

        Assert.NotNull(parsed);
        Assert.Equal("FSDJump", parsed!.Event);
        Assert.Equal("LHS 3447", parsed.StarSystem);
        Assert.NotNull(parsed.Position);
        Assert.Equal(-23.4, parsed.Position!.X);
        Assert.Equal(-72.4, parsed.Position.Y);
    }

    [Fact]
    public void Parse_Scan_ExtractsBodyDetails()
    {
        const string json = """
        {"$schemaRef":"https://eddn.edcd.io/schemas/journal/1","message":{"event":"Scan","BodyName":"LHS 3447 1","PlanetClass":"Earthlike body","TerraformState":"Terraformable","DistanceFromArrivalLS":1200}}
        """;

        var parsed = EddnMessageParser.Parse(json);

        Assert.NotNull(parsed);
        Assert.Equal("Scan", parsed!.Event);
        Assert.Equal("LHS 3447 1", parsed.BodyName);
        Assert.Equal("Earthlike body", parsed.PlanetClass);
        Assert.True(parsed.IsTerraformable);
        Assert.Equal(1200, parsed.DistanceFromArrivalLs);
    }

    [Fact]
    public void Parse_UnhandledEvent_ReturnsNull()
    {
        const string json = """
        {"$schemaRef":"https://eddn.edcd.io/schemas/journal/1","message":{"event":"Docked","StarSystem":"LHS 3447"}}
        """;

        Assert.Null(EddnMessageParser.Parse(json));
    }

    [Fact]
    public void Parse_HandledSignalThroughListener()
    {
        using var transport = new FakeTransport("""
        {"$schemaRef":"https://eddn.edcd.io/schemas/journal/1","message":{"event":"FSSDiscoveryScan","StarSystem":"LHS 3447","BodyCount":5,"NonBodyCount":2}}
        """);
        using var listener = new EddnListener(transport);

        EddnEvent? received = null;
        listener.EventReceived += (_, e) => received = e;
        listener.Start();

        Assert.NotNull(received);
        Assert.Equal("FSSDiscoveryScan", received!.Event);
        Assert.Equal("LHS 3447", received.StarSystem);
    }

    [Fact]
    public void Parse_MalformedJson_ReturnsNullWithoutThrowing()
    {
        Assert.Null(EddnMessageParser.Parse("this is not json {"));
        Assert.Null(EddnMessageParser.Parse(""));
        Assert.Null(EddnMessageParser.Parse("   "));
    }

    [Fact]
    public void CommunityService_SurfacesTransportFailure_WithoutThrowing()
    {
        using var transport = new FailingTransport("boom");
        using var listener = new EddnListener(transport);
        using var service = new CommunityService(listener);

        service.Start();

        Assert.False(service.IsConnected);
        Assert.Equal("boom", service.LastError);
    }

    [Fact]
    public void CommunityService_ReflectsRealConnectionStateFromTransport()
    {
        using var transport = new ScriptedTransport();
        using var listener = new EddnListener(transport);
        using var service = new CommunityService(listener);

        service.Start();

        transport.ReportConnected();
        Assert.True(service.IsConnected);

        transport.ReportDisconnected();
        Assert.False(service.IsConnected);
    }

    [Fact]
    public void CommunityService_BuffersAndPersistsSightingsOnStop()
    {
        var now = DateTimeOffset.UtcNow;
        var message = $"{{\"$schemaRef\":\"https://eddn.edcd.io/schemas/journal/1\",\"message\":{{\"timestamp\":\"{now:O}\",\"event\":\"FSDJump\",\"StarSystem\":\"LHS 3447\",\"StarPos\":[-23.4,-72.4,-35.3]}}}}";
        using var transport = new FakeTransport(message);
        using var listener = new EddnListener(transport);
        using var repository = new InMemoryDiscoveryRepository();
        using var service = new CommunityService(listener, repository);

        service.Start();
        Assert.Equal(1, service.ReceivedCount);
        Assert.Empty(repository.Discoveries);

        service.Stop();

        Assert.Single(repository.Discoveries);
        Assert.Equal("LHS 3447", repository.Discoveries[0].SystemName);
    }

    [Fact]
    public void CommunityService_StopsPersistingWithoutARepository()
    {
        var now = DateTimeOffset.UtcNow;
        var message = $"{{\"$schemaRef\":\"https://eddn.edcd.io/schemas/journal/1\",\"message\":{{\"timestamp\":\"{now:O}\",\"event\":\"FSDJump\",\"StarSystem\":\"LHS 3447\"}}}}";
        using var transport = new FakeTransport(message);
        using var listener = new EddnListener(transport);
        using var service = new CommunityService(listener);

        service.Start();
        service.Stop();

        Assert.Equal(1, service.ReceivedCount);
    }

    private sealed class InMemoryDiscoveryRepository : ICommunityDiscoveryRepository, IDisposable
    {
        public IReadOnlyList<CommunityDiscovery> Discoveries => _discoveries;

        private readonly List<CommunityDiscovery> _discoveries = new();

        public ValueTask RecordSightingsAsync(IEnumerable<CommunitySighting> sightings, CancellationToken cancellationToken = default)
        {
            foreach (var sighting in sightings)
            {
                var existing = _discoveries.FirstOrDefault(d => d.SystemName == sighting.SystemName);
                if (existing is null)
                {
                    _discoveries.Add(new CommunityDiscovery(
                        new CommunityDiscoveryId(Guid.NewGuid()),
                        sighting.SystemName,
                        sighting.Position,
                        sighting.ReportedAt,
                        sighting.ReportedAt,
                        1));
                }
                else
                {
                    var index = _discoveries.IndexOf(existing);
                    _discoveries[index] = existing with
                    {
                        Position = sighting.Position ?? existing.Position,
                        LastReportedAt = sighting.ReportedAt > existing.LastReportedAt ? sighting.ReportedAt : existing.LastReportedAt,
                        ReportCount = existing.ReportCount + 1
                    };
                }
            }

            return ValueTask.CompletedTask;
        }

        public Task<IReadOnlyList<CommunityDiscovery>> ListRecentAsync(int maxCount, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CommunityDiscovery>>(
                _discoveries.OrderByDescending(d => d.LastReportedAt).Take(maxCount).ToList());

        public Task<int> CountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_discoveries.Count);

        public ValueTask PruneAsync(int maxRows, TimeSpan maxAge, CancellationToken cancellationToken = default)
        {
            var cutoff = DateTimeOffset.UtcNow - maxAge;
            _discoveries.RemoveAll(d => d.LastReportedAt < cutoff);
            while (_discoveries.Count > maxRows)
            {
                _discoveries.RemoveAt(_discoveries.Count - 1);
            }

            return ValueTask.CompletedTask;
        }

        public void Dispose()
        {
        }
    }

    private sealed class ScriptedTransport : IEddnTransport
    {
#pragma warning disable CS0067
        public event Action<string>? MessageReceived;

        public event Action<Exception>? TransportError;
#pragma warning restore CS0067

        public event Action<bool>? ConnectionChanged;

        public void ReportConnected() => ConnectionChanged?.Invoke(true);

        public void ReportDisconnected() => ConnectionChanged?.Invoke(false);

        public void Start()
        {
        }

        public void Stop()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class FailingTransport : IEddnTransport
    {
        private readonly string _error;

        public FailingTransport(string error) => _error = error;

#pragma warning disable CS0067
        public event Action<string>? MessageReceived;

        public event Action<Exception>? TransportError;

        public event Action<bool>? ConnectionChanged;
#pragma warning restore CS0067

        public void Start() => TransportError?.Invoke(new InvalidOperationException(_error));

        public void Stop()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeTransport : IEddnTransport
    {
        private readonly string _line;

        public FakeTransport(string line) => _line = line;

#pragma warning disable CS0067
        public event Action<string>? MessageReceived;

        public event Action<Exception>? TransportError;

        public event Action<bool>? ConnectionChanged;
#pragma warning restore CS0067

        public void Start() => MessageReceived?.Invoke(_line);

        public void Stop()
        {
        }

        public void Dispose()
        {
        }
    }
}

public sealed class SpanshRouteServiceTests
{
    [Fact]
    public async Task PlotRouteAsync_ParsesJumps()
    {
        const string json = """
        {"range":60,"jumps":[{"system":"Sol","distance":0,"coords":[0,0,0]},{"system":"Sirius","distance":45.2,"coords":[12,-30,-45]}]}
        """;
        using var http = new HttpClient(new StubHandler(json));
        var service = new SpanshRouteService(http);

        var route = await service.PlotRouteAsync("Sol", "Colonia", 60);

        Assert.Equal(2, route.Count);
        Assert.Equal("Sol", route[0].SystemName);
        Assert.Equal(0, route[0].DistanceLy);
        Assert.Equal(45.2, route[1].DistanceLy);
        Assert.Equal(-45, route[1].Z);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _json;

        public StubHandler(string json) => _json = json;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, System.Text.Encoding.UTF8, "application/json")
            });
    }
}
