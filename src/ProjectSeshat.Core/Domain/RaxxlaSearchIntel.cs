namespace ProjectSeshat.Core.Domain;

/// <summary>
/// An individual "worth investigating" hit flagged by the Raxxla search criteria,
/// with the reason it was flagged (which community hint matched).
/// </summary>
public sealed record RaxxlaIntelHit(
    string Type,       // "Signal" | "Body" | "Name" | "Beacon" | "Proximity"
    string SystemName,
    string Target,     // the body/signal/system name that matched
    string Reason,     // human-readable why-it's-worth-a-look
    int Priority);     // higher = investigate sooner

/// <summary>
/// Curated community-derived criteria for what to look for while hunting Raxxla.
/// Pure data + matching helpers, so it is fully unit-testable; the search guide consumes it.
/// Nothing here is a claimed location — it is a set of "worth investigating" priorities distilled
/// from The Great Raxxla Potato Hunt playbook and the elite-dangerous lore wiki.
/// </summary>
public static class RaxxlaSearchIntel
{
    /// <summary>The radius (ly) of the community's Sol-centred hunt bubble.</summary>
    public const double SolSearchRadiusLy = 200;

    /// <summary>Sol's canonical galactic coordinates (0,0,0).</summary>
    public static readonly GalacticCoordinates SolCoordinates = new(0, 0, 0);

    /// <summary>Body classes the community treats as notable / high-signal.</summary>
    public static readonly IReadOnlyList<string> NotableBodyClasses = new[]
    {
        "Earthlike body",
        "Water world",
        "Ammonia world",
        "High metal content body"
    };

    /// <summary>Signal-type terms that read as "unusual" per the GRPH playbook.</summary>
    public static readonly IReadOnlyList<string> SuspiciousSignalTerms = new[]
    {
        "Other",
        "Non-Human",
        "Thargoid",
        "Guardian",
        "Unregistered",
        "Wakes",
        "Anomaly",
        "Unknown"
    };

    /// <summary>Lore name substrings worth a double-take in system/body names.</summary>
    public static readonly IReadOnlyList<string> LoreNameTerms = new[]
    {
        "Raxxla",
        "Omphalos",
        "Rift",
        "Dark Wheel",
        "Astrophel",
        "Spiralling",
        "Delphi",
        "Witchspace",
        "Jewel",
        "Whisperer",
        "Siren",
        "Mother",
        "Galaxies"
    };

    /// <summary>Returns the reason (or null) for a body being notable by planet class.</summary>
    public static string? ReasonForBodyClass(string? planetClass)
    {
        if (string.IsNullOrWhiteSpace(planetClass))
        {
            return null;
        }

        foreach (var notable in NotableBodyClasses)
        {
            if (planetClass.Contains(notable, StringComparison.OrdinalIgnoreCase))
            {
                return $"{notable}: a body class the hunt community watches closely";
            }
        }

        return null;
    }

    /// <summary>Returns the reason (or null) for a body being an 8th moon (the Dark Wheel clue).</summary>
    public static string? ReasonForEighthMoon(string? bodyName)
    {
        if (string.IsNullOrWhiteSpace(bodyName) || !HasEighthMoonDesignation(bodyName))
        {
            return null;
        }

        return "8th moon — mirrors the Dark Wheel HQ being on the eighth moon of an unnamed gas giant";
    }

    /// <summary>Returns which (if any) suspicious signal term appears in a signal-type list.</summary>
    public static string? ReasonForSignalTypes(string? signalTypes)
    {
        if (string.IsNullOrWhiteSpace(signalTypes))
        {
            return null;
        }

        foreach (var term in SuspiciousSignalTerms)
        {
            if (signalTypes.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                return $"unusual signal type '{term}' — the hunt flags these on the nav panel";
            }
        }

        return null;
    }

    /// <summary>Returns a reason (or null) when a name contains a lore term.</summary>
    public static string? ReasonForLoreName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        foreach (var term in LoreNameTerms)
        {
            if (name.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                return $"name contains '{term}' — matches a community lore hint";
            }
        }

        return null;
    }

    /// <summary>Returns a reason (or null) when a scanned beacon name, owner, or type contains a lore term.</summary>
    public static string? ReasonForBeacon(BeaconScan beacon)
    {
        var reasons = new List<string>();
        if (ReasonForLoreName(beacon.BeaconName) is { } nameReason) reasons.Add(nameReason);
        if (ReasonForLoreName(beacon.BeaconOwner) is { } ownerReason) reasons.Add(ownerReason);
        if (ReasonForLoreName(beacon.BeaconType) is { } typeReason) reasons.Add(typeReason);
        return reasons.Count == 0 ? null : string.Join("; ", reasons) + $" — scanned beacon in {beacon.SystemName}";
    }

    /// <summary>True when the position is inside the Sol-centred hunt bubble.</summary>
    public static bool IsWithinSolSearchArea(GalacticCoordinates? position)
        => position is not null && Distance(position, SolCoordinates) <= SolSearchRadiusLy;

    private static bool HasEighthMoonDesignation(string bodyName)
    {
        // 8th-moon pattern: "... <n> <letter>" with n == 8 (e.g. "LHS 3447 8 A") or a bare "… 8".
        var trimmed = bodyName.TrimEnd();
        var lastSpace = trimmed.LastIndexOf(' ');
        if (lastSpace >= 0)
        {
            var tail = trimmed[(lastSpace + 1)..];
            if (tail.Length == 1 && char.IsLetter(tail[0]))
            {
                // moon of a numbered body: verify the number before the letter is 8
                return EndsWithNumber(trimmed[..lastSpace], 8);
            }

            return EndsWithNumber(trimmed, 8);
        }

        return false;
    }

    private static bool EndsWithNumber(string text, int expected)
    {
        var lastSpace = text.LastIndexOf(' ');
        var tail = lastSpace >= 0 ? text[(lastSpace + 1)..] : text;
        return int.TryParse(tail, out var n) && n == expected;
    }

    private static double Distance(GalacticCoordinates a, GalacticCoordinates b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        var dz = a.Z - b.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}