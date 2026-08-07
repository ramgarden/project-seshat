namespace ProjectSeshat.Core.Domain;

/// <summary>
/// A persisted survey region in the atlas. Regions are stable across restarts: they are keyed
/// by their galactic grid cell, carry a survey score, and are flagged <see cref="Surveyed"/>
/// once the commander charts systems inside them.
/// </summary>
public sealed record SurveyRegion(
    SurveyRegionId Id,
    int CellX,
    int CellY,
    int CellZ,
    GalacticCoordinates Center,
    double Score,
    int NearbyVisitedSystems,
    double DistanceFromReferenceLy,
    bool Surveyed,
    DateTimeOffset LastUpdatedAt);
