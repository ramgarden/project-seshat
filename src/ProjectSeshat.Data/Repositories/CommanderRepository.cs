using Microsoft.EntityFrameworkCore;
using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Data.Repositories;

public sealed class CommanderRepository : ICommanderRepository
{
    private readonly ProjectSeshatDbContext _context;

    public CommanderRepository(ProjectSeshatDbContext context)
    {
        _context = context;
    }

    public async ValueTask<Commander?> FindByIdAsync(CommanderId id, CancellationToken cancellationToken = default)
        => await _context.Commanders.FirstOrDefaultAsync(commander => commander.Id == id, cancellationToken);

    public async ValueTask SaveAsync(Commander commander, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Commanders.AnyAsync(x => x.Id == commander.Id, cancellationToken);
        if (!existing)
        {
            _context.Commanders.Add(commander);
        }
        else
        {
            _context.Commanders.Update(commander);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
        => await _context.Commanders.CountAsync(cancellationToken);
}
