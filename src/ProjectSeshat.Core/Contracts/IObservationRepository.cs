using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Core.Contracts;

/// <summary>Defines persistence operations for observations recorded in the observatory.</summary>
public interface IObservationRepository
{
    ValueTask<Observation?> FindByIdAsync(ObservationGuid id, CancellationToken cancellationToken = default);

    ValueTask SaveAsync(Observation observation, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Observation>> FindByBodyIdAsync(CelestialBodyId bodyId, CancellationToken cancellationToken = default);
}
