namespace ProjectSeshat.Core.Contracts;

public interface IJournalImportTrackerRepository
{
    Task<bool> HasImportedAsync(string filePath, CancellationToken cancellationToken = default);

    Task MarkImportedAsync(string filePath, string fingerprint, CancellationToken cancellationToken = default);
}
