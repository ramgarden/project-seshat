using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Core.Contracts;

/// <summary>
/// Defines persistence for community-reported star-system discoveries. Rows are deduplicated by
/// system name and pruned to an upper bound so the table cannot grow without limit.
/// </summary>
public interface ICommunityDiscoveryRepository
{
    /// <summary>Upserts a batch of sightings, collapsing repeated systems and updating aggregate fields.</summary>
    ValueTask RecordSightingsAsync(IEnumerable<CommunitySighting> sightings, CancellationToken cancellationToken = default);

    /// <summary>Lists the most recently reported systems, newest first.</summary>
    Task<IReadOnlyList<CommunityDiscovery>> ListRecentAsync(int maxCount, CancellationToken cancellationToken = default);

    /// <summary>Returns the total number of distinct systems on record.</summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>Prunes rows past <paramref name="maxRows"/> (oldest first) and older than <paramref name="maxAge"/>.</summary>
    ValueTask PruneAsync(int maxRows, TimeSpan maxAge, CancellationToken cancellationToken = default);
}