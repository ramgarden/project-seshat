namespace ProjectSeshat.Core.Domain;

/// <summary>Represents an observation linked to a celestial body and commander.</summary>
public sealed record Observation(
    ObservationGuid Id,
    CelestialBodyId? BodyId,
    CommanderId? CommanderId,
    string Notes,
    DateTimeOffset ObservedAt);

