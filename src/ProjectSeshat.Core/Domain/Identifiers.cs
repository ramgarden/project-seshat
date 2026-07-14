namespace ProjectSeshat.Core.Domain;

/// <summary>Identifies a star system in the research catalog.</summary>
public readonly record struct StarSystemId(long Value);

/// <summary>Identifies a commander record.</summary>
public readonly record struct CommanderId(Guid Value);

/// <summary>Identifies a recorded item of research evidence.</summary>
public readonly record struct EvidenceId(Guid Value);
