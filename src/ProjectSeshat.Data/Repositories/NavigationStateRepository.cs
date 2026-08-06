using Microsoft.EntityFrameworkCore;
using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Data.Repositories;

public sealed class NavigationStateRepository : INavigationStateRepository
{
    private readonly ProjectSeshatDbContext _context;

    public NavigationStateRepository(ProjectSeshatDbContext context)
    {
        _context = context;
    }

    public async Task<NavigationState?> GetAsync(CancellationToken cancellationToken = default)
        => await _context.NavigationStates.FirstOrDefaultAsync(cancellationToken);

    public async ValueTask SaveAsync(NavigationState state, CancellationToken cancellationToken = default)
    {
        var existing = await _context.NavigationStates.AnyAsync(s => s.Id == state.Id, cancellationToken);
        if (!existing)
        {
            _context.NavigationStates.Add(state);
        }
        else
        {
            _context.NavigationStates.Update(state);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
