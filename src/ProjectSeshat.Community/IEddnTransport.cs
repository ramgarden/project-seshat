namespace ProjectSeshat.Community;

/// <summary>Abstraction over the EDDN transport so the listener can be tested without a live connection.</summary>
public interface IEddnTransport : IDisposable
{
    /// <summary>Raised for each raw message frame received from the stream.</summary>
    event Action<string>? MessageReceived;

    /// <summary>Raised when the transport fails (connect/read) instead of letting it crash the process.</summary>
    event Action<Exception>? TransportError;

    /// <summary>Raised when the underlying connection state changes (true = connected to the relay).</summary>
    event Action<bool>? ConnectionChanged;

    void Start();

    void Stop();
}