using ProjectSeshat.Atlas;

namespace ProjectSeshat.App.ViewModels;

/// <summary>
/// Turns the crawl's current step into the copy shown on the on-screen overlay and spoken aloud.
/// Kept as pure text logic so it is fully unit-testable offline; only the window/TTS is device-bound.
/// </summary>
public static class GuidanceFormatter
{
    /// <summary>The short one-line caption for the overlay, e.g. "JUMP TO SOL".</summary>
    public static string OverlayTitle(CrawlStep? step)
        => step is null ? "NO NEXT MOVE" : $"{step.Action?.ToUpperInvariant()} → {step.Target?.ToUpperInvariant()}";

    /// <summary>The supporting line: the reason (and DSS detail) for the current step.</summary>
    public static string OverlayDetail(CrawlStep? step)
        => step is null
            ? "Every known system is fully surveyed. Import deeper jumps to resume the outward crawl."
            : string.IsNullOrWhiteSpace(step.Detail)
                ? step.Reason ?? ""
                : $"{step.Reason} \u2014 {step.Detail}";

    /// <summary>
    /// A natural-language transcript for the voice pinger, e.g. "Jump to Sol, one hundred and two
    /// light years". Returns null when there is nothing worth saying.
    /// </summary>
    public static string? Transcript(CrawlStep? step)
    {
        if (step is null)
        {
            return null;
        }

        return step.Action switch
        {
            "Honk" => $"Arrived at {step.Target}. Run the discovery scan, then check for signals.",
            "FSS" => $"Run the full spectrum scanner at {step.Target} to resolve the signals.{SignalSuffix(step)}",
            "DSS" => $"Map the body {step.Target} with the detailed surface scanner.{DssSuffix(step)}",
            "Back-track" => $"All nearby stars are searched. Back-track to {step.Target}.",
            "Jump" => $"Nothing interesting here. Jump to {step.Target}.{DistanceSuffix(step)}",
            _ => $"{step.Action} at {step.Target}."
        };
    }

    private static string SignalSuffix(CrawlStep step)
        => string.IsNullOrWhiteSpace(step.Detail) ? "" : $" Signals detected: {step.Detail}.";

    private static string DssSuffix(CrawlStep step)
        => string.IsNullOrWhiteSpace(step.Detail) ? "" : $" Reason: {step.Detail}.";

    private static string DistanceSuffix(CrawlStep step)
        => string.IsNullOrWhiteSpace(step.Detail) ? "" : $" {step.Detail}.";
}