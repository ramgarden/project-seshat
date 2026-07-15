using Microsoft.EntityFrameworkCore;
using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Data.Repositories;

public sealed class StarSystemRepository : IStarSystemRepository
{
    private readonly ProjectSeshatDbContext _context;

    public StarSystemRepository(ProjectSeshatDbContext context)
    {
        _context = context;
    }

    public async ValueTask<StarSystem?> FindByIdAsync(StarSystemId id, CancellationToken cancellationToken = default)
        => await _context.StarSystems.FirstOrDefaultAsync(system => system.Id == id, cancellationToken);

    public async ValueTask SaveAsync(StarSystem system, CancellationToken cancellationToken = default)
    {
        var existing = await _context.StarSystems.AnyAsync(x => x.Id == system.Id, cancellationToken);
        if (!existing)
        {
            _context.StarSystems.Add(system);
        }
        else
        {
            _context.StarSystems.Update(system);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
        => await _context.StarSystems.CountAsync(cancellationToken);

    public async Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default)
        => await _context.StarSystems.AnyAsync(system => system.Name == name, cancellationToken);
}
