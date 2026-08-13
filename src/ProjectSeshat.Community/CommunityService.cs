using System.Collections.Concurrent;
using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Community;

/// <summary>
/// High-level coordinator for community data. Owns the EDDN listener and exposes connection
/// state, error surfaces, and running counts for the UI; Start/Stop the live stream.
///
/// Incoming sightings are buffered in memory and flushed to an
/// <see cref="ICommunityDiscoveryRepository"/> on a timer (and on stop), then pruned to a
/// bounded row count, so the live relay can never outgrow the local database.
/// </summary>
public sealed class CommunityService : IDisposable
{
    private const int DiscoveryCap = 250_000;
    private static readonly TimeSpan DiscoveryAge = TimeSpan.FromDays(180);
    internal static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(10);

    private readonly EddnListener? _listener;
    private readonly ICommunityDiscoveryRepository? _discoveryRepository;
    private readonly ConcurrentDictionary<string, Sight> _sightings = new();

    private long _receivedCount;
    private int _discoveryCount;
    private IReadOnlyList<string> _recentDiscoveries = Array.Empty<string>();
    private bool _disposed;
    private Timer? _flushTimer;
    private long _lastChangedTick; // coalesces per-event ChChanged to keep UI/DB work bounded

    public CommunityService(EddnListener? listener = null, ICommunityDiscoveryRepository? discoveryRepository = null)
    {
        _listener = listener;
        _discoveryRepository = discoveryRepository;
        if (_listener is not null)
        {
            _listener.EventReceived += OnEventReceived;
            _listener.TransportErrorReceived += failure =>
            {
                LastError = failure.Message;
                IsConnected = false;
                Changed?.Invoke(this, EventArgs.Empty);
            };
            _listener.ConnectionChanged += connected =>
            {
                IsConnected = connected;
                Changed?.Invoke(this, EventArgs.Empty);
            };
        }
    }

    /// <summary>Raised when connection state, counts, or the last error change.</summary>
    public event EventHandler? Changed;

    /// <summary>True when a live transport is available (regardless of connection state).</summary>
    public bool IsAvailable => _listener is not null;

    /// <summary>True only when the transport reports an established connection to the relay.</summary>
    public bool IsConnected { get; private set; }

    public long ReceivedCount => Volatile.Read(ref _receivedCount);

    /// <summary>Total distinct systems with a community sighting in the local summary store.</summary>
    public int DiscoveryCount => Volatile.Read(ref _discoveryCount);

    /// <summary>Most recently reported systems (names only), newest first.</summary>
    public IReadOnlyList<string> RecentDiscoveries => _recentDiscoveries;

    /// <summary>A human-readable description of the most recent transport failure, if any.</summary>
    public string? LastError { get; private set; }

    public void Start()
    {
        if (_listener is null)
        {
            return;
        }

        LastError = null;
        try
        {
            _listener.Start();
            StartFlushTimer();
        }
        catch (Exception failure)
        {
            IsConnected = false;
            LastError = failure.Message;
        }

        RefreshDiscoveryCount();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        if (_listener is null)
        {
            return;
        }

        StopFlushTimer();
        try
        {
            _listener.Stop();
        }
        catch (Exception failure)
        {
            LastError = failure.Message;
        }

        IsConnected = false;
        FlushSightings();
        RefreshDiscoveryCount();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopFlushTimer();
        if (_listener is not null)
        {
            _listener.EventReceived -= OnEventReceived;
            _listener.Dispose();
        }

        FlushSightings();
    }

    private void OnEventReceived(object? sender, EddnEvent eddnEvent)
    {
        Interlocked.Increment(ref _receivedCount);

        // Only system-level events give us the coordinates/first-seen signal we want.
        if (!string.IsNullOrEmpty(eddnEvent.StarSystem))
        {
            _sightings[eddnEvent.StarSystem] = new Sight(eddnEvent.Position, eddnEvent.ReportedAt);
        }

        // The relay can burst thousands of frames/second; raise Changed at most ~4x/sec so the
        // UI binding layer and any consumers never get drowned while playing.
        var now = Environment.TickCount64;
        var last = Volatile.Read(ref _lastChangedTick);
        if (now - last >= ChangedIntervalMs && Interlocked.CompareExchange(ref _lastChangedTick, now, last) == last)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private const long ChangedIntervalMs = 250;

    private void StartFlushTimer()
    {
        StopFlushTimer();
        _flushTimer = new Timer(_ => FlushSightings(), null, FlushInterval, FlushInterval);
    }

    private void StopFlushTimer()
    {
        _flushTimer?.Dispose();
        _flushTimer = null;
    }

    private void FlushSightings()
    {
        if (_discoveryRepository is null || _sightings.IsEmpty)
        {
            return;
        }

        var batch = new List<CommunitySighting>(_sightings.Count);
        foreach (var (system, sight) in _sightings)
        {
            batch.Add(new CommunitySighting(system, sight.Position, sight.ReportedAt));
        }

        _sightings.Clear();

        try
        {
            _discoveryRepository.RecordSightingsAsync(batch).GetAwaiter().GetResult();
            _discoveryRepository.PruneAsync(DiscoveryCap, DiscoveryAge).GetAwaiter().GetResult();
            RefreshDiscoveryCount();
            RefreshRecentDiscoveries();
        }
        catch (Exception failure)
        {
            LastError = failure.Message;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshDiscoveryCount()
    {
        if (_discoveryRepository is null)
        {
            return;
        }

        try
        {
            _discoveryCount = _discoveryRepository.CountAsync().GetAwaiter().GetResult();
        }
        catch (Exception failure)
        {
            LastError = failure.Message;
        }
    }

    private void RefreshRecentDiscoveries()
    {
        if (_discoveryRepository is null)
        {
            return;
        }

        try
        {
            _recentDiscoveries = _discoveryRepository
                .ListRecentAsync(200)
                .GetAwaiter()
                .GetResult()
                .Select(d => d.SystemName)
                .ToArray();
        }
        catch (Exception failure)
        {
            LastError = failure.Message;
        }
    }

    private sealed record Sight(GalacticCoordinates? Position, DateTimeOffset ReportedAt);
}