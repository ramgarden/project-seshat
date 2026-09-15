using Microsoft.EntityFrameworkCore;
using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Data.Repositories;

public sealed class BeaconRepository : IBeaconRepository
{
    private readonly ProjectSeshatDbContext _context;

    public BeaconRepository(ProjectSeshatDbContext context)
    {
        _context = context;
    }

    public async ValueTask<BeaconScan?> FindByIdAsync(
        BeaconScanId id,
        CancellationToken cancellationToken = default)
        => await _context.Beacons.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async ValueTask SaveAsync(BeaconScan beacon, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Beacons.FirstOrDefaultAsync(
            x => x.Fingerprint == beacon.Fingerprint,
            cancellationToken);

        if (existing is null)
        {
            _context.Beacons.Add(beacon);
        }
        else
        {
            _context.Entry(existing).State = EntityState.Detached;
            _context.Beacons.Update(beacon with { Id = existing.Id });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
        => await _context.Beacons.CountAsync(cancellationToken);

    public async Task<bool> ExistsByFingerprintAsync(
        string fingerprint,
        CancellationToken cancellationToken = default)
        => await _context.Beacons.AnyAsync(x => x.Fingerprint == fingerprint, cancellationToken);

    public async Task<IReadOnlyList<BeaconScan>> ListAsync(
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        var beacons = await _context.Beacons
            .OrderByDescending(x => x.ObservedAt)
            .Take(maxCount)
            .ToListAsync(cancellationToken);

        return beacons
            .GroupBy(x => x.Fingerprint, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    public async Task<IReadOnlyList<BeaconScan>> FindBySystemNameAsync(
        string systemName,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        var beacons = await _context.Beacons
            .Where(x => x.SystemName == systemName)
            .OrderByDescending(x => x.ObservedAt)
            .Take(maxCount)
            .ToListAsync(cancellationToken);

        return beacons
            .GroupBy(x => x.Fingerprint, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }
}
