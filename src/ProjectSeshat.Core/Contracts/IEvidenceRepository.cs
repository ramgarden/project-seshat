using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Core.Contracts;

/// <summary>Defines persistence operations for research evidence without choosing a storage technology.</summary>
public interface IEvidenceRepository
{
    ValueTask<EvidenceRecord?> FindByIdAsync(EvidenceId id, CancellationToken cancellationToken = default);

    ValueTask SaveAsync(EvidenceRecord evidence, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
