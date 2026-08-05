using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Core.Contracts;

/// <summary>Defines persistence operations for celestial bodies catalogued in the atlas.</summary>
public interface ICelestialBodyRepository
{
    ValueTask<CelestialBody?> FindByIdAsync(CelestialBodyId id, CancellationToken cancellationToken = default);

    ValueTask SaveAsync(CelestialBody body, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CelestialBody>> FindBySystemIdAsync(StarSystemId systemId, CancellationToken cancellationToken = default);

    Task<int> CountForSystemAsync(StarSystemId systemId, CancellationToken cancellationToken = default);

    Task<int> CountNeedingSurfaceScanAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CelestialBody>> ListNeedingSurfaceScanAsync(int maxCount, CancellationToken cancellationToken = default);

    ValueTask UpdateScanStatusAsync(CelestialBodyId id, ScanStatus status, CancellationToken cancellationToken = default);
}
