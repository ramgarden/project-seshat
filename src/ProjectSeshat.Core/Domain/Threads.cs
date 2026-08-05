namespace ProjectSeshat.Core.Domain;

/// <summary>Classifies the lifecycle stage of a research thread.</summary>
public enum ThreadStatus
{
    Open,
    Investigating,
    Concluded,
    Archived
}

/// <summary>
/// Represents a research thread: a focused line of enquiry about a subject
/// (a system and/or body) that advances through workflow stages.
/// </summary>
public sealed record ResearchThread(
    ResearchThreadId Id,
    string Subject,
    string? Notes,
    StarSystemId? SystemId,
    CelestialBodyId? BodyId,
    ThreadStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
