namespace ProjectSeshat.Core.Domain;

/// <summary>Represents a known star system.</summary>
public sealed record StarSystem(StarSystemId Id, string Name);

/// <summary>Represents a commander known to the research platform.</summary>
public sealed record Commander(CommanderId Id, string Name);

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
