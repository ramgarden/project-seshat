using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Observatory;

/// <summary>Provides observation analysis and recording operations for the observatory boundary.</summary>
public sealed class ObservatoryService
{
    /// <summary>Records a new observation into the repository.</summary>
    public async Task RecordObservationAsync(
        Observation observation,
        IObservationRepository repository,
        CancellationToken cancellationToken = default)
    {
        await repository.SaveAsync(observation, cancellationToken);
    }

    /// <summary>Returns all observations recorded for a given celestial body.</summary>
    public async Task<IReadOnlyList<Observation>> GetObservationsForBodyAsync(
        CelestialBodyId bodyId,
        IObservationRepository repository,
        CancellationToken cancellationToken = default)
    {
        return await repository.FindByBodyIdAsync(bodyId, cancellationToken);
    }
}
