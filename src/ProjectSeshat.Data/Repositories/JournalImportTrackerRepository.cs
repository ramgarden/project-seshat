using Microsoft.EntityFrameworkCore;
using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Data.Repositories;

public sealed class JournalImportTrackerRepository : IJournalImportTrackerRepository
{
    private readonly ProjectSeshatDbContext _context;

    public JournalImportTrackerRepository(ProjectSeshatDbContext context)
    {
        _context = context;
    }

    public async Task<bool> HasImportedAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var normalizedPath = Normalize(filePath);
        return await _context.JournalImportTrackers.AnyAsync(tracker => tracker.FilePath == normalizedPath, cancellationToken);
    }

    public async Task MarkImportedAsync(string filePath, string fingerprint, CancellationToken cancellationToken = default)
    {
        var normalizedPath = Normalize(filePath);
        var tracker = await _context.JournalImportTrackers.FirstOrDefaultAsync(x => x.FilePath == normalizedPath, cancellationToken);
        if (tracker is null)
        {
            _context.JournalImportTrackers.Add(new JournalImportTracker(normalizedPath, fingerprint));
        }
        else
        {
            tracker.Fingerprint = fingerprint;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static string Normalize(string filePath) => filePath.Trim().ToLowerInvariant();
}
