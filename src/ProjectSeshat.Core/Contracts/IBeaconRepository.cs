using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Core.Contracts;

/// <summary>Defines persistence operations for beacon scans captured from journals.</summary>
public interface IBeaconRepository
{
    ValueTask<BeaconScan?> FindByIdAsync(BeaconScanId id, CancellationToken cancellationToken = default);

    ValueTask SaveAsync(BeaconScan beacon, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsByFingerprintAsync(string fingerprint, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BeaconScan>> ListAsync(int maxCount, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BeaconScan>> FindBySystemNameAsync(
        string systemName,
        int maxCount,
        CancellationToken cancellationToken = default);
}
