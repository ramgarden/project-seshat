using Microsoft.EntityFrameworkCore;
using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Data.Repositories;

public sealed class CommunityDiscoveryRepository : ICommunityDiscoveryRepository
{
    private readonly ProjectSeshatDbContext _context;

    public CommunityDiscoveryRepository(ProjectSeshatDbContext context)
    {
        _context = context;
    }

    public async ValueTask RecordSightingsAsync(IEnumerable<CommunitySighting> sightings, CancellationToken cancellationToken = default)
    {
        var materialized = sightings as IReadOnlyList<CommunitySighting> ?? sightings.ToList();
        if (materialized.Count == 0)
        {
            return;
        }

        var bySystem = materialized
            .GroupBy(s => s.SystemName)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    Sighting = new CommunitySighting(
                        g.Key,
                        g.Select(s => s.Position).Where(p => p is not null).LastOrDefault(),
                        g.Max(s => s.ReportedAt)),
                    Count = g.LongCount()
                });

        var existing = await _context.CommunityDiscoveries
            .Where(d => bySystem.Keys.Contains(d.SystemName))
            .ToDictionaryAsync(d => d.SystemName, cancellationToken);

        foreach (var (systemName, sighting) in bySystem)
        {
            var item = sighting.Sighting;
            if (existing.TryGetValue(systemName, out var row))
            {
                var updated = row with
                {
                    Position = item.Position ?? row.Position,
                    LastReportedAt = item.ReportedAt > row.LastReportedAt ? item.ReportedAt : row.LastReportedAt,
                    ReportCount = row.ReportCount + sighting.Count
                };
                _context.Entry(row).CurrentValues.SetValues(updated);
            }
            else
            {
                _context.CommunityDiscoveries.Add(new CommunityDiscovery(
                    new CommunityDiscoveryId(Guid.NewGuid()),
                    systemName,
                    item.Position,
                    item.ReportedAt,
                    item.ReportedAt,
                    sighting.Count));
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CommunityDiscovery>> ListRecentAsync(int maxCount, CancellationToken cancellationToken = default)
        => await _context.CommunityDiscoveries
            .OrderByDescending(d => d.LastReportedAt)
            .Take(maxCount)
            .ToListAsync(cancellationToken);

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
        => await _context.CommunityDiscoveries.CountAsync(cancellationToken);

    public async ValueTask PruneAsync(int maxRows, TimeSpan maxAge, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow - maxAge;
        var stale = await _context.CommunityDiscoveries
            .Where(d => d.LastReportedAt < cutoff)
            .ToListAsync(cancellationToken);
        _context.CommunityDiscoveries.RemoveRange(stale);

        var count = await _context.CommunityDiscoveries.CountAsync(cancellationToken);
        var overflow = count - maxRows;
        if (overflow > 0)
        {
            var oldest = await _context.CommunityDiscoveries
                .OrderBy(d => d.LastReportedAt)
                .Take(overflow)
                .ToListAsync(cancellationToken);
            _context.CommunityDiscoveries.RemoveRange(oldest);
        }

        if (stale.Count > 0 || overflow > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}