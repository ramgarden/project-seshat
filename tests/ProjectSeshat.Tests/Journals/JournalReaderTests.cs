using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProjectSeshat.Data;
using ProjectSeshat.Data.Repositories;
using ProjectSeshat.Journals;
using Xunit;

namespace ProjectSeshat.Tests.Journals;

public sealed class JournalReaderTests
{
    [Fact]
    public async Task ImportAsync_PersistsCommanderSystemAndEvidence()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        var starSystemRepository = new StarSystemRepository(context);
        var commanderRepository = new CommanderRepository(context);
        var evidenceRepository = new EvidenceRepository(context);
        var reader = new JournalReader();

        using var journal = new StringReader("""
{"timestamp":"2024-01-01T00:00:00Z","event":"LoadGame","Commander":"Juno Quill"}
{"timestamp":"2024-01-01T00:00:01Z","event":"FSDJump","StarSystem":"LHS 3447"}
{"timestamp":"2024-01-01T00:00:02Z","event":"Scan","BodyName":"Achenar B 1"}
""");

        await reader.ImportAsync(journal, starSystemRepository, commanderRepository, evidenceRepository);

        Assert.Equal(1, await starSystemRepository.CountAsync());
        Assert.Equal(1, await commanderRepository.CountAsync());
        Assert.Equal(1, await evidenceRepository.CountAsync());

        var persistedSystem = await context.StarSystems.SingleAsync();
        Assert.Equal("LHS 3447", persistedSystem.Name);
    }

    [Fact]
    public async Task ImportAutoDetectedPathAsync_ImportsFromResolvedDirectory()
    {
        using var tempDirectory = new TemporaryDirectory();
        var journalDirectory = Path.Combine(tempDirectory.Path, "Journals");
        Directory.CreateDirectory(journalDirectory);
        await File.WriteAllLinesAsync(Path.Combine(journalDirectory, "Journal.20240101T000000.01.log"), new[]
        {
            "{\"timestamp\":\"2024-01-01T00:00:00Z\",\"event\":\"LoadGame\",\"Commander\":\"Juno Quill\"}",
            "{\"timestamp\":\"2024-01-01T00:00:01Z\",\"event\":\"FSDJump\",\"StarSystem\":\"LHS 3447\"}",
            "{\"timestamp\":\"2024-01-01T00:00:02Z\",\"event\":\"Scan\",\"BodyName\":\"Achenar B 1\"}"
        });

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        var starSystemRepository = new StarSystemRepository(context);
        var commanderRepository = new CommanderRepository(context);
        var evidenceRepository = new EvidenceRepository(context);
        var reader = new JournalReader();
        var resolver = new JournalPathResolver(new[] { journalDirectory });

        await reader.ImportAutoDetectedPathAsync(resolver, starSystemRepository, commanderRepository, evidenceRepository);

        Assert.Equal(1, await starSystemRepository.CountAsync());
        Assert.Equal(1, await commanderRepository.CountAsync());
        Assert.Equal(1, await evidenceRepository.CountAsync());
    }

    [Fact]
    public async Task ImportAsync_IgnoresAlreadyImportedJournalContentOnRepeatRuns()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        var starSystemRepository = new StarSystemRepository(context);
        var commanderRepository = new CommanderRepository(context);
        var evidenceRepository = new EvidenceRepository(context);
        var reader = new JournalReader();

        using var journal = new StringReader("""
{"timestamp":"2024-01-01T00:00:00Z","event":"LoadGame","Commander":"Juno Quill"}
{"timestamp":"2024-01-01T00:00:01Z","event":"FSDJump","StarSystem":"LHS 3447"}
{"timestamp":"2024-01-01T00:00:02Z","event":"Scan","BodyName":"Achenar B 1"}
""");

        await reader.ImportAsync(journal, starSystemRepository, commanderRepository, evidenceRepository);
        await reader.ImportAsync(journal, starSystemRepository, commanderRepository, evidenceRepository);

        Assert.Equal(1, await starSystemRepository.CountAsync());
        Assert.Equal(1, await commanderRepository.CountAsync());
        Assert.Equal(1, await evidenceRepository.CountAsync());
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
