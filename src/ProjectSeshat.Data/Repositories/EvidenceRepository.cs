using Microsoft.EntityFrameworkCore;
using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Data.Repositories;

public sealed class EvidenceRepository : IEvidenceRepository
{
    private readonly ProjectSeshatDbContext _context;

    public EvidenceRepository(ProjectSeshatDbContext context)
    {
        _context = context;
    }

    public async ValueTask<EvidenceRecord?> FindByIdAsync(EvidenceId id, CancellationToken cancellationToken = default)
        => await _context.Evidence.FirstOrDefaultAsync(evidence => evidence.Id == id, cancellationToken);

    public async ValueTask SaveAsync(EvidenceRecord evidence, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Evidence.AnyAsync(x => x.Id == evidence.Id, cancellationToken);
        if (!existing)
        {
            _context.Evidence.Add(evidence);
        }
        else
        {
            _context.Evidence.Update(evidence);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
        => await _context.Evidence.CountAsync(cancellationToken);
}
