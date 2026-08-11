namespace ProjectSeshat.Core.Domain;

/// <summary>
/// A community-reported star-system sighting captured from the EDDN stream. Rows are keyed by
/// system name (deduplicated) and only carry the fields the platform needs, so the table stays
/// small even at full EDDN volume. A repository prunes oldest rows past a configured bound.
/// </summary>
public sealed record CommunityDiscovery(
    CommunityDiscoveryId Id,
    string SystemName,
    GalacticCoordinates? Position,
    DateTimeOffset FirstReportedAt,
    DateTimeOffset LastReportedAt,
    long ReportCount);

/// <summary>
/// An unpersisted, deduplication-free sighting batch item. The repository collapses multiple
/// sightings of the same system and applies a bounded prune, so the stored summary stays small.
/// </summary>
public sealed record CommunitySighting(string SystemName, GalacticCoordinates? Position, DateTimeOffset ReportedAt);