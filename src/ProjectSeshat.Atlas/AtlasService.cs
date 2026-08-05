using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Atlas;

/// <summary>Provides spatial and astronomical research operations for the atlas boundary.</summary>
public sealed class AtlasService
{
    /// <summary>
    /// Returns all celestial bodies catalogued for the given star system,
    /// ordered by distance from the arrival point ascending.
    /// </summary>
    public async Task<IReadOnlyList<CelestialBody>> GetBodiesForSystemAsync(
        StarSystemId systemId,
        ICelestialBodyRepository repository,
        CancellationToken cancellationToken = default)
    {
        var bodies = await repository.FindBySystemIdAsync(systemId, cancellationToken);
        return bodies
            .OrderBy(b => b.DistanceFromArrivalLs ?? double.MaxValue)
            .ToList();
    }
}
