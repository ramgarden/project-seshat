using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProjectSeshat.Data;
using ProjectSeshat.Data.Repositories;
using ProjectSeshat.Journals;
using Xunit;

namespace ProjectSeshat.Tests.Journals;

public sealed class JournalDirectoryImportTests
{
    [Fact]
    public async Task ImportDirectoryAsync_ImportsRecentFilesAndExplorationEvents()
    {
        using var tempDirectory = new TemporaryDirectory();
        var journalDirectory = Path.Combine(tempDirectory.Path, "Journals");
        Directory.CreateDirectory(journalDirectory);

        var newestFile = Path.Combine(journalDirectory, "Journal.20240101T123000.01.log");
        var middleFile = Path.Combine(journalDirectory, "Journal.20240101T110000.01.log");
        var oldestFile = Path.Combine(journalDirectory, "Journal.20240101T100000.01.log");

        await WriteJournalAsync(newestFile, new[]
        {
            "{\"timestamp\":\"2024-01-01T12:30:00Z\",\"event\":\"LoadGame\",\"Commander\":\"Juno Quill\"}",
            "{\"timestamp\":\"2024-01-01T12:31:00Z\",\"event\":\"FSDJump\",\"StarSystem\":\"LHS 3447\"}",
            "{\"timestamp\":\"2024-01-01T12:32:00Z\",\"event\":\"Scan\",\"BodyName\":\"Achenar B 1\"}",
            "{\"timestamp\":\"2024-01-01T12:33:00Z\",\"event\":\"Location\",\"StarSystem\":\"LHS 3447\",\"StationName\":\"Jameson Memorial\"}"
        });

        await WriteJournalAsync(middleFile, new[]
        {
            "{\"timestamp\":\"2024-01-01T11:30:00Z\",\"event\":\"SAASignalsFound\",\"BodyName\":\"Achenar B 1\",\"SignalName\":\"Unstable signal\"}",
            "{\"timestamp\":\"2024-01-01T11:31:00Z\",\"event\":\"ApproachBody\",\"BodyName\":\"Achenar B 1\"}"
        });

        await WriteJournalAsync(oldestFile, new[]
        {
            "{\"timestamp\":\"2024-01-01T10:00:00Z\",\"event\":\"StartJump\",\"JumpType\":\"Hyperspace\"}"
        });

        File.SetLastWriteTimeUtc(newestFile, new DateTimeOffset(2024, 1, 1, 12, 30, 0, TimeSpan.Zero).UtcDateTime);
        File.SetLastWriteTimeUtc(middleFile, new DateTimeOffset(2024, 1, 1, 11, 30, 0, TimeSpan.Zero).UtcDateTime);
        File.SetLastWriteTimeUtc(oldestFile, new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero).UtcDateTime);

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        var starSystemRepository = new StarSystemRepository(context);
        var commanderRepository = new CommanderRepository(context);
        var evidenceRepository = new EvidenceRepository(context);
        var reader = new JournalReader();

        await reader.ImportDirectoryAsync(journalDirectory, starSystemRepository, commanderRepository, evidenceRepository);

        Assert.Equal(1, await commanderRepository.CountAsync());
        Assert.Equal(1, await starSystemRepository.CountAsync());
        Assert.Equal(4, await evidenceRepository.CountAsync());

        var evidence = await context.Evidence.ToListAsync();
        Assert.Contains(evidence, item => item.Summary.Contains("Achenar B 1", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(evidence, item => item.Summary.Contains("signal", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(evidence, item => item.Summary.Contains("LHS 3447", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task WriteJournalAsync(string path, IReadOnlyList<string> lines)
    {
        await File.WriteAllLinesAsync(path, lines);
    }

    private static ProjectSeshatDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ProjectSeshatDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new ProjectSeshatDbContext(options);
        context.Database.EnsureCreated();
        return context;
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
