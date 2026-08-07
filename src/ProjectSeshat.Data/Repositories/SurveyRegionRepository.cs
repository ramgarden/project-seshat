using Microsoft.EntityFrameworkCore;
using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Data.Repositories;

public sealed class SurveyRegionRepository : ISurveyRegionRepository
{
    private readonly ProjectSeshatDbContext _context;

    public SurveyRegionRepository(ProjectSeshatDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SurveyRegion>> ListAsync(int maxCount, CancellationToken cancellationToken = default)
        => await _context.SurveyRegions
            .OrderBy(r => r.Surveyed)
            .ThenByDescending(r => r.Score)
            .Take(maxCount)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SurveyRegion>> ListUnsurveyedAsync(int maxCount, CancellationToken cancellationToken = default)
        => await _context.SurveyRegions
            .Where(r => !r.Surveyed)
            .OrderByDescending(r => r.Score)
            .Take(maxCount)
            .ToListAsync(cancellationToken);

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
        => await _context.SurveyRegions.CountAsync(cancellationToken);

    public async ValueTask SaveAllAsync(IEnumerable<SurveyRegion> regions, CancellationToken cancellationToken = default)
    {
        var materialized = regions as IReadOnlyList<SurveyRegion> ?? regions.ToList();
        if (materialized.Count == 0)
        {
            return;
        }

        foreach (var region in materialized)
        {
            var exists = await _context.SurveyRegions
                .AnyAsync(r => r.CellX == region.CellX && r.CellY == region.CellY && r.CellZ == region.CellZ, cancellationToken);
            if (!exists)
            {
                _context.SurveyRegions.Add(region);
            }
            else
            {
                _context.SurveyRegions.Update(region);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
