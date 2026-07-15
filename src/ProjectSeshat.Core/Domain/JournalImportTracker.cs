namespace ProjectSeshat.Core.Domain;

public sealed class JournalImportTracker
{
    public JournalImportTracker(string filePath, string fingerprint)
    {
        FilePath = filePath;
        Fingerprint = fingerprint;
    }

    public Guid Id { get; init; } = Guid.NewGuid();

    public string FilePath { get; set; }

    public string Fingerprint { get; set; }
}
