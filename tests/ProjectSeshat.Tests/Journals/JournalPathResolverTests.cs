using ProjectSeshat.Journals;
using Xunit;

namespace ProjectSeshat.Tests.Journals;

public sealed class JournalPathResolverTests
{
    [Fact]
    public void ResolvePath_ReturnsDirectoryContainingJournalFiles()
    {
        using var tempDirectory = new TemporaryDirectory();
        var journalDirectory = Path.Combine(tempDirectory.Path, "Journal");
        Directory.CreateDirectory(journalDirectory);
        File.WriteAllText(Path.Combine(journalDirectory, "Journal.20240101T000000.01.log"), "{\"event\":\"Scan\"}\n");

        var resolver = new JournalPathResolver(new[] { journalDirectory });

        Assert.Equal(journalDirectory, resolver.ResolvePath());
    }

    [Fact]
    public void ResolvePath_ReturnsNullWhenNoJournalFilesExist()
    {
        using var tempDirectory = new TemporaryDirectory();
        var resolver = new JournalPathResolver(new[] { tempDirectory.Path });

        Assert.Null(resolver.ResolvePath());
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
