namespace ProjectSeshat.App;

/// <summary>
/// Speaks short guidance prompts. Kept behind an interface so unit tests and non-Windows
/// environments use a silent/no-op pinger while the desktop app uses Windows TTS.
/// </summary>
public interface IVoicePinger
{
    /// <summary>True when this pinger can actually produce audio on this machine.</summary>
    bool IsAvailable { get; }

    void Speak(string phrase);
}

/// <summary>A pinger that produces no audio; used for tests and non-Windows platforms.</summary>
public sealed class SilentVoicePinger : IVoicePinger
{
    public bool IsAvailable => false;

    public void Speak(string phrase)
    {
    }
}

/// <summary>
/// Windows text-to-speech pinger via <c>System.Speech</c> (Windows only).
/// Fails safe: on any platform/API error it becomes unavailable and never throws.
/// </summary>
public sealed class WindowsSpeechVoicePinger : IVoicePinger
{
    private readonly object _sync = new();

    public bool IsAvailable => OperatingSystem.IsWindows();

    public void Speak(string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase) || !OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            lock (_sync)
            {
                using var synthesizer = new System.Speech.Synthesis.SpeechSynthesizer();
                synthesizer.SetOutputToDefaultAudioDevice();
                synthesizer.Speak(phrase);
            }
        }
        catch
        {
            // TTS is best-effort; never let a speech failure take down the app.
        }
    }
}