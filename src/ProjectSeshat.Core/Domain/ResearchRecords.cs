namespace ProjectSeshat.Core.Domain;

/// <summary>Represents a known star system.</summary>
public sealed record StarSystem(
    StarSystemId Id,
    string Name,
    GalacticCoordinates? Position = null,
    SystemSurveyState SurveyState = SystemSurveyState.Unexplored,
    int NonBodySignals = 0,
    string? SignalTypes = null);

/// <summary>Classifies the immediate action recommended by the guided search.</summary>
public enum NextActionKind
{
    /// <summary>Discovery-scan the current system.</summary>
    Honk,

    /// <summary>Resolve non-body signals in the current system.</summary>
    Fss,

    /// <summary>Surface-map a specific body in the current system.</summary>
    Dss,

    /// <summary>Jump to the next unsearched system.</summary>
    Jump,

    /// <summary>Return through a charted system toward the next unsearched system.</summary>
    BackTrack
}

/// <summary>A single, canonical instruction shared by the guide, overlay, voice, and automation.</summary>
public sealed record NextAction(
    NextActionKind Action,
    string Target,
    string Reason,
    string? Detail = null,
    string? TargetSystem = null,
    string? TargetBody = null,
    double? DistanceLy = null)
{
    /// <summary>The target system when the action is system-scoped.</summary>
    public string? TargetSystemName => TargetSystem ?? (Action is NextActionKind.Dss ? null : Target);

    /// <summary>The target body when the action is body-scoped.</summary>
    public string? TargetBodyName => TargetBody;
}

/// <summary>Classifies how far a star system's survey has progressed.</summary>
public enum SystemSurveyState
{
    /// <summary>Arrived but not yet discovery-scanned (honk) — needs a honk.</summary>
    Unexplored,

    /// <summary>Discovery-scan (honk) done; census and signals known — may need FSS.</summary>
    Honked,

    /// <summary>Full Spectrum Scanner resolved the bodies and signals.</summary>
    FssScanned
}

/// <summary>Represents a commander known to the research platform.</summary>
public sealed record Commander(CommanderId Id, string Name);

/// <summary>Represents a normalized identity for a journal event so imports can be deduplicated.</summary>
public sealed record JournalImportKey(string Value);

/// <summary>Classifies the kind of information captured as evidence.</summary>
public enum EvidenceKind
{
    Observation,
    Discovery,
    Investigation
}

/// <summary>Represents an immutable item of evidence captured for research.</summary>
public sealed record EvidenceRecord(
    EvidenceId Id,
    EvidenceKind Kind,
    string Summary,
    DateTimeOffset RecordedAt,
    ResearchThreadId? ThreadId = null);

// ── Atlas ────────────────────────────────────────────────────────────────────

/// <summary>Galactic position in light-years on the galaxy map.</summary>
public sealed record GalacticCoordinates(double X, double Y, double Z);

/// <summary>Classifies the broad type of a celestial body.</summary>
public enum BodyKind
{
    Star,
    Planet,
    Moon,
    AsteroidBelt,
    Unknown
}

/// <summary>Classifies the depth of scanning a celestial body has received.</summary>
public enum ScanStatus
{
    /// <summary>Detected (FSS honk/point) but not otherwise characterized.</summary>
    Discovered,

    /// <summary>Full Spectrum Scanner spectral details captured.</summary>
    FssScanned,

    /// <summary>Detailed Surface Scanner mapping complete.</summary>
    Mapped
}

/// <summary>Represents a celestial body catalogued from scanner data.</summary>
public sealed record CelestialBody(
    CelestialBodyId Id,
    StarSystemId SystemId,
    string Name,
    BodyKind Kind,
    string? StarClass,
    string? PlanetClass,
    bool? IsTerraformable,
    double? DistanceFromArrivalLs,
    ScanStatus ScanStatus = ScanStatus.Discovered,
    bool WorthDss = false);

/// <summary>Represents a beacon scan captured from the journal.</summary>
public sealed record BeaconScan(
    BeaconScanId Id,
    string BeaconName,
    string? BeaconType,
    string? BeaconOwner,
    string SystemName,
    long? SystemAddress,
    DateTimeOffset ObservedAt,
    string Fingerprint);

// ── Codex ────────────────────────────────────────────────────────────────────

/// <summary>Groups a codex discovery by its broad research category.</summary>
public enum CodexCategory
{
    Biology,
    Geology,
    Phenomena,
    Astronomy,
    Other
}

/// <summary>Represents a discovery entry in the exploration codex.</summary>
public sealed record CodexEntry(
    CodexEntryId Id,
    string Name,
    CodexCategory Category,
    string? Description,
    DateTimeOffset DiscoveredAt);

// ── Observatory ───────────────────────────────────────────────────────────────

/// <summary>Represents a timestamped observation linked to a body and commander.</summary>
public sealed record ObservationRecord(
    ObservationGuid Id,
    CelestialBodyId? BodyId,
    CommanderId? CommanderId,
    string Notes,
    DateTimeOffset ObservedAt);
