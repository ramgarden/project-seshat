using Microsoft.EntityFrameworkCore;
using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Data.Repositories;

public sealed class CelestialBodyRepository : ICelestialBodyRepository
{
    private readonly ProjectSeshatDbContext _context;

    public CelestialBodyRepository(ProjectSeshatDbContext context)
    {
        _context = context;
    }

    public async ValueTask<CelestialBody?> FindByIdAsync(CelestialBodyId id, CancellationToken cancellationToken = default)
        => await _context.CelestialBodies.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public async ValueTask SaveAsync(CelestialBody body, CancellationToken cancellationToken = default)
    {
        var existing = await _context.CelestialBodies.AnyAsync(x => x.Id == body.Id, cancellationToken);
        if (!existing)
        {
            _context.CelestialBodies.Add(body);
        }
        else
        {
            _context.CelestialBodies.Update(body);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
        => await _context.CelestialBodies.CountAsync(cancellationToken);

    public async Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default)
        => await _context.CelestialBodies.AnyAsync(b => b.Name == name, cancellationToken);

    public async Task<IReadOnlyList<CelestialBody>> FindBySystemIdAsync(StarSystemId systemId, CancellationToken cancellationToken = default)
        => await _context.CelestialBodies
            .Where(b => b.SystemId == systemId)
            .OrderBy(b => b.DistanceFromArrivalLs ?? double.MaxValue)
            .ToListAsync(cancellationToken);

    public async Task<int> CountForSystemAsync(StarSystemId systemId, CancellationToken cancellationToken = default)
        => await _context.CelestialBodies.CountAsync(b => b.SystemId == systemId, cancellationToken);
}
