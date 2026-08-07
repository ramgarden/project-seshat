using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Core.Contracts;

/// <summary>Defines persistence for the atlas survey regions (the source-of-truth survey listing).</summary>
public interface ISurveyRegionRepository
{
    Task<IReadOnlyList<SurveyRegion>> ListAsync(int maxCount, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SurveyRegion>> ListUnsurveyedAsync(int maxCount, CancellationToken cancellationToken = default);

    /// <summary>Inserts or updates a batch of regions keyed by their grid cell.</summary>
    ValueTask SaveAllAsync(IEnumerable<SurveyRegion> regions, CancellationToken cancellationToken = default);
}
