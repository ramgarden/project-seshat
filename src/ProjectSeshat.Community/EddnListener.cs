namespace ProjectSeshat.Community;

/// <summary>
/// Subscribes to EDDN and raises a normalized <see cref="EddnEvent"/> for each handled message.
/// </summary>
public sealed class EddnListener : IDisposable
{
    private readonly IEddnTransport _transport;

    private bool _started;

    public EddnListener(IEddnTransport transport)
    {
        _transport = transport;
    }

    /// <summary>Raised for each normalized, handled EDDN event.</summary>
    public event EventHandler<EddnEvent>? EventReceived;

    /// <summary>Raised when the underlying transport fails; consumers should surface it rather than crash.</summary>
    public event Action<Exception>? TransportErrorReceived;

    /// <summary>Raised when the underlying connection state changes (true = connected).</summary>
    public event Action<bool>? ConnectionChanged;

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _transport.MessageReceived += OnRawMessage;
        _transport.TransportError += OnTransportError;
        _transport.ConnectionChanged += OnConnectionChanged;
        _transport.Start();
    }

    public void Stop()
    {
        if (!_started)
        {
            return;
        }

        _started = false;
        _transport.Stop();
        _transport.MessageReceived -= OnRawMessage;
        _transport.TransportError -= OnTransportError;
        _transport.ConnectionChanged -= OnConnectionChanged;
    }

    private void OnRawMessage(string raw)
    {
        var parsed = EddnMessageParser.Parse(raw);
        if (parsed is not null)
        {
            EventReceived?.Invoke(this, parsed);
        }
    }

    private void OnTransportError(Exception exception) => TransportErrorReceived?.Invoke(exception);

    private void OnConnectionChanged(bool connected) => ConnectionChanged?.Invoke(connected);

    public void Dispose()
    {
        Stop();
        _transport.Dispose();
    }
}