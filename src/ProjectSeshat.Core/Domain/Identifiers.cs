namespace ProjectSeshat.Core.Domain;

/// <summary>Identifies a star system in the research catalog.</summary>
public readonly record struct StarSystemId(long Value);

/// <summary>Identifies a commander record.</summary>
public readonly record struct CommanderId(Guid Value);

/// <summary>Identifies a recorded item of research evidence.</summary>
public readonly record struct EvidenceId(Guid Value);

/// <summary>Identifies a celestial body catalogued in the atlas.</summary>
public readonly record struct CelestialBodyId(Guid Value);

/// <summary>Identifies an entry in the discovery codex.</summary>
public readonly record struct CodexEntryId(Guid Value);

/// <summary>Identifies an observation recorded in the observatory.</summary>
public readonly record struct ObservationGuid(Guid Value);

/// <summary>Identifies a research thread in the ThreadEngine.</summary>
public readonly record struct ResearchThreadId(Guid Value);

/// <summary>Identifies the commander's persisted navigation state.</summary>
public readonly record struct NavigationStateId(Guid Value);

/// <summary>Identifies a persisted survey region in the atlas.</summary>
public readonly record struct SurveyRegionId(Guid Value);

/// <summary>Identifies a community-reported star-system discovery.</summary>
public readonly record struct CommunityDiscoveryId(Guid Value);
