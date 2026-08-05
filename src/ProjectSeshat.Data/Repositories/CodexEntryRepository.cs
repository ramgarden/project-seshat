using Microsoft.EntityFrameworkCore;
using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Data.Repositories;

public sealed class CodexEntryRepository : ICodexEntryRepository
{
    private readonly ProjectSeshatDbContext _context;

    public CodexEntryRepository(ProjectSeshatDbContext context)
    {
        _context = context;
    }

    public async ValueTask<CodexEntry?> FindByIdAsync(CodexEntryId id, CancellationToken cancellationToken = default)
        => await _context.CodexEntries.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async ValueTask SaveAsync(CodexEntry entry, CancellationToken cancellationToken = default)
    {
        var existing = await _context.CodexEntries.AnyAsync(x => x.Id == entry.Id, cancellationToken);
        if (!existing)
        {
            _context.CodexEntries.Add(entry);
        }
        else
        {
            _context.CodexEntries.Update(entry);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
        => await _context.CodexEntries.CountAsync(cancellationToken);

    public async Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default)
        => await _context.CodexEntries.AnyAsync(e => e.Name == name, cancellationToken);

    public async Task<IReadOnlyList<CodexEntry>> FindByCategoryAsync(CodexCategory category, CancellationToken cancellationToken = default)
        => await _context.CodexEntries
            .Where(e => e.Category == category)
            .OrderBy(e => e.Name)
            .ToListAsync(cancellationToken);
}
