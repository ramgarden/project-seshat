using Microsoft.EntityFrameworkCore;
using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Data.Repositories;

public sealed class ResearchThreadRepository : IResearchThreadRepository
{
    private readonly ProjectSeshatDbContext _context;

    public ResearchThreadRepository(ProjectSeshatDbContext context)
    {
        _context = context;
    }

    public async ValueTask<ResearchThread?> FindByIdAsync(ResearchThreadId id, CancellationToken cancellationToken = default)
        => await _context.ResearchThreads.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async ValueTask SaveAsync(ResearchThread thread, CancellationToken cancellationToken = default)
    {
        var existing = await _context.ResearchThreads.AnyAsync(x => x.Id == thread.Id, cancellationToken);
        if (!existing)
        {
            _context.ResearchThreads.Add(thread);
        }
        else
        {
            _context.ResearchThreads.Update(thread);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
        => await _context.ResearchThreads.CountAsync(cancellationToken);

    public async Task<IReadOnlyList<ResearchThread>> ListAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        var results = await _context.ResearchThreads.Take(maxCount).ToListAsync(cancellationToken);
        return results.OrderByDescending(t => t.CreatedAt).ToList();
    }

    public async Task<IReadOnlyList<ResearchThread>> FindBySubjectAsync(
        StarSystemId? systemId,
        CelestialBodyId? bodyId,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ResearchThreads.AsQueryable();

        if (systemId.HasValue)
        {
            query = query.Where(t => t.SystemId == systemId.Value);
        }

        if (bodyId.HasValue)
        {
            query = query.Where(t => t.BodyId == bodyId.Value);
        }

        var results = await query.ToListAsync(cancellationToken);
        return results.OrderByDescending(t => t.CreatedAt).ToList();
    }
}
