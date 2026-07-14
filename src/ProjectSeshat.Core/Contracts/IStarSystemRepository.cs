using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Core.Contracts;

/// <summary>Defines persistence operations for star systems without choosing a storage technology.</summary>
public interface IStarSystemRepository
{
    ValueTask<StarSystem?> FindByIdAsync(StarSystemId id, CancellationToken cancellationToken = default);

    ValueTask SaveAsync(StarSystem system, CancellationToken cancellationToken = default);
}
