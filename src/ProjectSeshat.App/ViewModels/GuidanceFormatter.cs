using ProjectSeshat.Atlas;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.App.ViewModels;

/// <summary>
/// Turns the crawl's current step into the copy shown on the on-screen overlay and spoken aloud.
/// Kept as pure text logic so it is fully unit-testable offline; only the window/TTS is device-bound.
/// </summary>
public static class GuidanceFormatter
{
    /// <summary>The short one-line caption for the overlay.</summary>
    public static string OverlayTitle(NextAction? action)
    {
        if (action is null)
        {
            return "NO NEXT MOVE";
        }

        return action.Action switch
        {
            NextActionKind.Honk => $"HONK {action.Target?.ToUpperInvariant()}",
            NextActionKind.Fss => "INSPECT THE TARGETED SIGNAL",
            NextActionKind.Dss => $"DSS TARGETED BODY",
            NextActionKind.BackTrack => $"BACK-TRACK TO {action.Target?.ToUpperInvariant()}",
            NextActionKind.Jump => $"JUMP TO {action.Target?.ToUpperInvariant()}",
            _ => $"{action.Action} → {action.Target?.ToUpperInvariant()}"
        };
    }

    /// <summary>
    /// The supporting line: names the target (body or system), then the reason/why it matters.
    /// </summary>
    public static string OverlayDetail(NextAction? action)
    {
        if (action is null)
        {
            return "Every known system is fully surveyed. Import deeper jumps to resume the outward crawl.";
        }

        var targetLabel = action.Action switch
        {
            NextActionKind.Fss => $"signal in {action.Target}",
            NextActionKind.Dss => $"body {action.Target}",
            _ => $"{action.Target}"
        };

        var reason = string.IsNullOrWhiteSpace(action.Detail) ? action.Reason ?? "" : $"{action.Reason} — {action.Detail}";
        return $"{targetLabel}. {reason}";
    }

    /// <summary>
    /// A natural-language transcript for the voice pinger, e.g. "Jump to Sol, one hundred and two
    /// light years". Returns null when there is nothing worth saying.
    /// </summary>
    public static string? Transcript(NextAction? action)
    {
        if (action is null)
        {
            return null;
        }

        return action.Action switch
        {
            NextActionKind.Honk => $"Arrived at {action.Target}. Run the discovery scan, then check for signals.",
            NextActionKind.Fss => $"Target the signal, then run the full spectrum scanner at {action.Target} to resolve it.{SignalSuffix(action)}",
            NextActionKind.Dss => $"Target the body {action.Target}, then map it with the detailed surface scanner.{DssSuffix(action)}",
            NextActionKind.BackTrack => $"All nearby stars are searched. Back-track to {action.Target}.",
            NextActionKind.Jump => $"Nothing interesting here. Jump to {action.Target}.{DistanceSuffix(action)}",
            _ => $"{action.Action} at {action.Target}."
        };
    }

    private static string SignalSuffix(NextAction action)
        => string.IsNullOrWhiteSpace(action.Detail) ? "" : $" Signals detected: {action.Detail}.";

    private static string DssSuffix(NextAction action)
        => string.IsNullOrWhiteSpace(action.Detail) ? "" : $" Reason: {action.Detail}.";

    private static string DistanceSuffix(NextAction action)
        => string.IsNullOrWhiteSpace(action.Detail) ? "" : $" {action.Detail}.";
}