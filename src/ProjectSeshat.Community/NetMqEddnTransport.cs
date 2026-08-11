using NetMQ;
using NetMQ.Monitoring;
using NetMQ.Sockets;

namespace ProjectSeshat.Community;

/// <summary>
/// Real EDDN transport: subscribes to the EDDN ZeroMQ relay and raises one message per frame.
/// Uses a NetMQ monitor socket so connection state reflects reality (not merely "start requested").
/// Requires network access; the stream is live crowdsourced data.
/// </summary>
public sealed class NetMqEddnTransport : IEddnTransport
{
    private readonly string _endpoint;
    private readonly string _monitorEndpoint;
    private SubscriberSocket? _socket;
    private NetMQPoller? _poller;
    private NetMQMonitor? _monitor;
    private int _started;

    public static string DefaultEndpoint => "tcp://eddn.edcd.io:9500";

    public NetMqEddnTransport(string? endpoint = null)
    {
        _endpoint = endpoint ?? DefaultEndpoint;
        _monitorEndpoint = $"inproc://seshat-eddn-monitor-{Guid.NewGuid():N}";
    }

    public event Action<string>? MessageReceived;

    public event Action<Exception>? TransportError;

    /// <summary>Raised when the transport's live connection state changes (true = connected to the relay).</summary>
    public event Action<bool>? ConnectionChanged;

    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return;
        }

        SubscriberSocket? socket = null;
        try
        {
            socket = new SubscriberSocket();
            socket.Connect(_endpoint);
            socket.Subscribe("");
            socket.ReceiveReady += OnReceiveReady;

            var monitor = new NetMQMonitor(socket, _monitorEndpoint, SocketEvents.All);
            monitor.Connected += (_, _) => ConnectionChanged?.Invoke(true);
            monitor.Disconnected += (_, _) => ConnectionChanged?.Invoke(false);
            monitor.BindFailed += (_, _) => ConnectionChanged?.Invoke(false);
            monitor.ConnectDelayed += (_, _) => ConnectionChanged?.Invoke(false);

            _monitor = monitor;
            _socket = socket;
            var poller = new NetMQPoller { socket };
            _poller = poller;
            poller.RunAsync();
            monitor.StartAsync();
        }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref _started, 0);
            _socket = null;
            _poller = null;
            TryDisposeMonitor();
            socket?.Dispose();
            TransportError?.Invoke(ex);
        }
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _started, 0) == 0)
        {
            return;
        }

        TryDisposeMonitor();
        StopPoller();

        if (_socket is not null)
        {
            try
            {
                _socket.Dispose();
            }
            catch
            {
                // Never let shutdown take the process down.
            }

            _socket = null;
        }
    }

    public void Dispose() => Stop();

    private void TryDisposeMonitor()
    {
        var monitor = _monitor;
        _monitor = null;
        if (monitor is null)
        {
            return;
        }

        try
        {
            monitor.Dispose();
        }
        catch
        {
            // Monitor cleanup can race with the poller thread; never propagate.
        }
    }

    private void StopPoller()
    {
        var poller = _poller;
        _poller = null;
        if (poller is null)
        {
            return;
        }

        try
        {
            poller.Stop();
            poller.Dispose();
        }
        catch
        {
            // Best-effort teardown across threads.
        }
    }

    private void OnReceiveReady(object? sender, NetMQSocketEventArgs e)
    {
        string frame;
        try
        {
            frame = e.Socket.ReceiveFrameString(System.Text.Encoding.UTF8);
        }
        catch (Exception ex)
        {
            TransportError?.Invoke(ex);
            return;
        }

        if (string.IsNullOrEmpty(frame))
        {
            // First frame of a message from a "" subscription is the (empty) topic;
            // the actual EDDN payload arrives in the following frame(s).
            try
            {
                while (e.Socket.TryReceiveFrameString(System.Text.Encoding.UTF8, out var payload))
                {
                    if (!string.IsNullOrEmpty(payload))
                    {
                        MessageReceived?.Invoke(payload);
                    }
                }
            }
            catch (Exception ex)
            {
                TransportError?.Invoke(ex);
            }
        }
        else
        {
            MessageReceived?.Invoke(frame);
        }
    }
}