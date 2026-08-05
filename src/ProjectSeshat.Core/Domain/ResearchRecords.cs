namespace ProjectSeshat.Core.Domain;

/// <summary>Represents a known star system.</summary>
public sealed record StarSystem(StarSystemId Id, string Name);

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
    DateTimeOffset RecordedAt);

// ── Atlas ────────────────────────────────────────────────────────────────────

/// <summary>Classifies the broad type of a celestial body.</summary>
public enum BodyKind
{
    Star,
    Planet,
    Moon,
    AsteroidBelt,
    Unknown
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
    double? DistanceFromArrivalLs);

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
