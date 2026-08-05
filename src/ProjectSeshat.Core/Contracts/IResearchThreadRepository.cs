using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Core.Contracts;

/// <summary>Defines persistence operations for research threads.</summary>
public interface IResearchThreadRepository
{
    ValueTask<ResearchThread?> FindByIdAsync(ResearchThreadId id, CancellationToken cancellationToken = default);

    ValueTask SaveAsync(ResearchThread thread, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ResearchThread>> ListAsync(int maxCount, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ResearchThread>> FindBySubjectAsync(
        StarSystemId? systemId,
        CelestialBodyId? bodyId,
        CancellationToken cancellationToken = default);
}
