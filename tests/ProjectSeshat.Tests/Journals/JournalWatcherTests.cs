using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProjectSeshat.Core.Domain;
using ProjectSeshat.Data;
using ProjectSeshat.Data.Repositories;
using ProjectSeshat.Journals;
using Xunit;

namespace ProjectSeshat.Tests.Journals;

public sealed class JournalWatcherTests
{
    [Fact]
    public async Task ScanDirectoryAsync_ImportsExistingJournals()
    {
        using var temp = new TemporaryDirectory();
        var journalPath = Path.Combine(temp.Path, "Journal.20240101T000000.01.log");
        await File.WriteAllLinesAsync(journalPath, new[]
        {
            "{\"timestamp\":\"2024-01-01T00:00:00Z\",\"event\":\"LoadGame\",\"Commander\":\"Juno Quill\"}",
            "{\"timestamp\":\"2024-01-01T00:00:01Z\",\"event\":\"FSDJump\",\"StarSystem\":\"LHS 3447\",\"StarPos\":[-23.4,-72.4,-35.3]}"
        });

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);

        var watcher = CreateWatcher(temp.Path, context);
        var result = await watcher.ScanDirectoryAsync();

        Assert.Equal(1, result.FilesChanged);
        Assert.Equal(2, result.LinesImported);
        Assert.Equal(1, await context.StarSystems.CountAsync());
        Assert.Equal(1, await context.Commanders.CountAsync());
    }

    [Fact]
    public async Task ScanDirectoryAsync_TailImportsOnlyAppendedLines()
    {
        using var temp = new TemporaryDirectory();
        var journalPath = Path.Combine(temp.Path, "Journal.20240101T000000.01.log");
        await File.WriteAllLinesAsync(journalPath, new[]
        {
            "{\"timestamp\":\"2024-01-01T00:00:00Z\",\"event\":\"LoadGame\",\"Commander\":\"Juno Quill\"}",
            "{\"timestamp\":\"2024-01-01T00:00:01Z\",\"event\":\"FSDJump\",\"StarSystem\":\"LHS 3447\"}"
        });

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);

        var watcher = CreateWatcher(temp.Path, context);

        var first = await watcher.ScanDirectoryAsync();
        Assert.Equal(2, first.LinesImported);

        await File.AppendAllLinesAsync(journalPath, new[]
        {
            "{\"timestamp\":\"2024-01-01T00:00:02Z\",\"event\":\"FSSDiscoveryScan\",\"StarSystem\":\"LHS 3447\",\"BodyCount\":5,\"NonBodyCount\":2}",
            "{\"timestamp\":\"2024-01-01T00:00:03Z\",\"event\":\"Scan\",\"BodyName\":\"LHS 3447 1\",\"PlanetClass\":\"Earthlike body\",\"ScanType\":\"FSS\"}"
        });

        var second = await watcher.ScanDirectoryAsync();

        // Only the two appended, previously unseen lines should be imported.
        Assert.Equal(1, second.FilesChanged);
        Assert.Equal(2, second.LinesImported);

        var system = await context.StarSystems.SingleAsync(s => s.Name == "LHS 3447");
        Assert.Equal(SystemSurveyState.FssScanned, system.SurveyState);
        Assert.Equal(2, system.NonBodySignals);
        Assert.Equal(1, await context.CelestialBodies.CountAsync());
    }

    [Fact]
    public async Task ScanDirectoryAsync_UnchangedFilesAreSkipped()
    {
        using var temp = new TemporaryDirectory();
        var journalPath = Path.Combine(temp.Path, "Journal.20240101T000000.01.log");
        await File.WriteAllLinesAsync(journalPath, new[]
        {
            "{\"timestamp\":\"2024-01-01T00:00:00Z\",\"event\":\"LoadGame\",\"Commander\":\"Juno Quill\"}"
        });

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);

        var watcher = CreateWatcher(temp.Path, context);

        var first = await watcher.ScanDirectoryAsync();
        Assert.Equal(1, first.LinesImported);

        var second = await watcher.ScanDirectoryAsync();
        Assert.Equal(0, second.LinesImported);
        Assert.Equal(0, second.FilesChanged);
    }

    [Fact]
    public async Task ScanDirectoryAsync_SkipsAlreadyImportedContentAcrossRestart()
    {
        using var temp = new TemporaryDirectory();
        var journalPath = Path.Combine(temp.Path, "Journal.20240101T000000.01.log");
        await File.WriteAllLinesAsync(journalPath, new[]
        {
            "{\"timestamp\":\"2024-01-01T00:00:00Z\",\"event\":\"LoadGame\",\"Commander\":\"Juno Quill\"}",
            "{\"timestamp\":\"2024-01-01T00:00:01Z\",\"event\":\"FSDJump\",\"StarSystem\":\"LHS 3447\"}"
        });

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);

        // First "session": full import records the content fingerprint in the import tracker.
        var firstWatcher = CreateWatcher(temp.Path, context);
        Assert.Equal(2, (await firstWatcher.ScanDirectoryAsync()).LinesImported);

        // Second "session": a fresh watcher (in-memory state reset) must not re-import the
        // unchanged file because its fingerprint was already recorded.
        var secondWatcher = CreateWatcher(temp.Path, context);
        var result = await secondWatcher.ScanDirectoryAsync();

        Assert.Equal(0, result.FilesChanged);
        Assert.Equal(0, result.LinesImported);
        Assert.Equal(1, await context.StarSystems.CountAsync());
        Assert.Equal(1, await context.Commanders.CountAsync());
    }

    private static JournalWatcher CreateWatcher(string directory, ProjectSeshatDbContext context)
    {
        return new JournalWatcher(
            directory,
            new JournalReader(),
            new StarSystemRepository(context),
            new CommanderRepository(context),
            new EvidenceRepository(context),
            new JournalImportTrackerRepository(context),
            new CelestialBodyRepository(context));
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