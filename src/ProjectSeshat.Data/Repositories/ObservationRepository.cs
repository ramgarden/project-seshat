using Microsoft.EntityFrameworkCore;
using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Data.Repositories;

public sealed class ObservationRepository : IObservationRepository
{
    private readonly ProjectSeshatDbContext _context;

    public ObservationRepository(ProjectSeshatDbContext context)
    {
        _context = context;
    }

    public async ValueTask<Observation?> FindByIdAsync(ObservationGuid id, CancellationToken cancellationToken = default)
        => await _context.Observations.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async ValueTask SaveAsync(Observation observation, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Observations.AnyAsync(x => x.Id == observation.Id, cancellationToken);
        if (!existing)
        {
            _context.Observations.Add(observation);
        }
        else
        {
            _context.Observations.Update(observation);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
        => await _context.Observations.CountAsync(cancellationToken);

    public async Task<IReadOnlyList<Observation>> FindByBodyIdAsync(CelestialBodyId bodyId, CancellationToken cancellationToken = default)
        => await _context.Observations
            .Where(o => o.BodyId == bodyId)
            .OrderByDescending(o => o.ObservedAt)
            .ToListAsync(cancellationToken);
}
