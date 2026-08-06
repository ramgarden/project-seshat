using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProjectSeshat.Core.Domain;
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

    [Fact]
    public async Task ImportAsync_ParsesStarPosIntoCoordinates()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        var starSystemRepository = new StarSystemRepository(context);
        var commanderRepository = new CommanderRepository(context);
        var evidenceRepository = new EvidenceRepository(context);
        var reader = new JournalReader();

        using var journal = new StringReader("""
{"timestamp":"2024-01-01T00:00:01Z","event":"FSDJump","StarSystem":"LHS 3447","StarPos":[-23.40625,-72.4375,-35.34375]}
""");

        await reader.ImportAsync(journal, starSystemRepository, commanderRepository, evidenceRepository);

        var system = await context.StarSystems.SingleAsync();
        Assert.NotNull(system.Position);
        Assert.Equal(-23.40625, system.Position!.X);
        Assert.Equal(-72.4375, system.Position.Y);
        Assert.Equal(-35.34375, system.Position.Z);
    }

    [Fact]
    public async Task ImportAsync_GuidesSurveyPipeline()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        var starSystemRepository = new StarSystemRepository(context);
        var commanderRepository = new CommanderRepository(context);
        var evidenceRepository = new EvidenceRepository(context);
        var bodyRepository = new CelestialBodyRepository(context);
        var reader = new JournalReader();

        using var journal = new StringReader("""
{"timestamp":"2024-01-01T00:00:01Z","event":"FSDJump","StarSystem":"LHS 3447","StarPos":[-23.4,-72.4,-35.3]}
{"timestamp":"2024-01-01T00:00:02Z","event":"FSSDiscoveryScan","StarSystem":"LHS 3447","BodyCount":5,"NonBodyCount":2}
{"timestamp":"2024-01-01T00:00:03Z","event":"Scan","BodyName":"LHS 3447 1","PlanetClass":"Earthlike body","DistanceFromArrivalLS":1200,"ScanType":"FSS"}
""");

        await reader.ImportAsync(
            journal,
            starSystemRepository,
            commanderRepository,
            evidenceRepository,
            celestialBodyRepository: bodyRepository);

        var system = await starSystemRepository.FindByNameAsync("LHS 3447");
        Assert.NotNull(system);
        Assert.Equal(SystemSurveyState.FssScanned, system!.SurveyState);

        var body = await context.CelestialBodies.SingleAsync();
        Assert.True(body.WorthDss);
        Assert.Equal(ScanStatus.FssScanned, body.ScanStatus);
    }

    [Fact]
    public async Task ImportAsync_HonkMarksSystemNeedingFss()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        var starSystemRepository = new StarSystemRepository(context);
        var commanderRepository = new CommanderRepository(context);
        var evidenceRepository = new EvidenceRepository(context);
        var reader = new JournalReader();

        using var journal = new StringReader("""
{"timestamp":"2024-01-01T00:00:01Z","event":"FSDJump","StarSystem":"LHS 3447"}
{"timestamp":"2024-01-01T00:00:02Z","event":"FSSDiscoveryScan","StarSystem":"LHS 3447","BodyCount":5,"NonBodyCount":2}
""");

        await reader.ImportAsync(journal, starSystemRepository, commanderRepository, evidenceRepository);

        var system = await starSystemRepository.FindByNameAsync("LHS 3447");
        Assert.NotNull(system);
        Assert.Equal(SystemSurveyState.Honked, system!.SurveyState);
        Assert.Equal(2, system.NonBodySignals);
    }

    [Fact]
    public async Task ImportAsync_DetailedScan_MarksBodyMappedAutomatically()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        var starSystemRepository = new StarSystemRepository(context);
        var commanderRepository = new CommanderRepository(context);
        var evidenceRepository = new EvidenceRepository(context);
        var bodyRepository = new CelestialBodyRepository(context);
        var reader = new JournalReader();

        using var journal = new StringReader("""
{"timestamp":"2024-01-01T00:00:01Z","event":"FSDJump","StarSystem":"LHS 3447"}
{"timestamp":"2024-01-01T00:00:02Z","event":"Scan","BodyName":"LHS 3447 1","PlanetClass":"Earthlike body","ScanType":"Detailed"}
""");

        await reader.ImportAsync(
            journal,
            starSystemRepository,
            commanderRepository,
            evidenceRepository,
            celestialBodyRepository: bodyRepository);

        var body = await context.CelestialBodies.SingleAsync();
        Assert.Equal(ScanStatus.Mapped, body.ScanStatus);
    }

    [Fact]
    public async Task ImportAsync_BasicScan_FallsBackToDiscovered()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        var starSystemRepository = new StarSystemRepository(context);
        var commanderRepository = new CommanderRepository(context);
        var evidenceRepository = new EvidenceRepository(context);
        var bodyRepository = new CelestialBodyRepository(context);
        var reader = new JournalReader();

        using var journal = new StringReader("""
{"timestamp":"2024-01-01T00:00:01Z","event":"FSDJump","StarSystem":"LHS 3447"}
{"timestamp":"2024-01-01T00:00:02Z","event":"Scan","BodyName":"LHS 3447 1","PlanetClass":"Earthlike body","ScanType":"Basic"}
""");

        await reader.ImportAsync(
            journal,
            starSystemRepository,
            commanderRepository,
            evidenceRepository,
            celestialBodyRepository: bodyRepository);

        var body = await context.CelestialBodies.SingleAsync();
        Assert.Equal(ScanStatus.Discovered, body.ScanStatus);
    }

    [Fact]
    public async Task ImportAsync_SaaScanComplete_MarksExistingBodyMapped()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        var starSystemRepository = new StarSystemRepository(context);
        var commanderRepository = new CommanderRepository(context);
        var evidenceRepository = new EvidenceRepository(context);
        var bodyRepository = new CelestialBodyRepository(context);
        var reader = new JournalReader();

        using var firstJournal = new StringReader("""
{"timestamp":"2024-01-01T00:00:01Z","event":"FSDJump","StarSystem":"LHS 3447"}
{"timestamp":"2024-01-01T00:00:02Z","event":"Scan","BodyName":"LHS 3447 1","PlanetClass":"Earthlike body","ScanType":"FSS"}
""");
        await reader.ImportAsync(
            firstJournal,
            starSystemRepository,
            commanderRepository,
            evidenceRepository,
            celestialBodyRepository: bodyRepository);

        var bodyBefore = await context.CelestialBodies.SingleAsync();
        Assert.Equal(ScanStatus.FssScanned, bodyBefore.ScanStatus);

        using var dssComplete = new StringReader("""
{"timestamp":"2024-01-01T00:00:03Z","event":"SAAScanComplete","BodyName":"LHS 3447 1","ProbesUsed":4}
""");
        await reader.ImportAsync(
            dssComplete,
            starSystemRepository,
            commanderRepository,
            evidenceRepository,
            celestialBodyRepository: bodyRepository);

        var bodyAfter = await context.CelestialBodies.SingleAsync();
        Assert.Equal(ScanStatus.Mapped, bodyAfter.ScanStatus);
    }

    [Fact]
    public async Task ImportAsync_TracksCurrentSystemOnJump()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        var starSystemRepository = new StarSystemRepository(context);
        var commanderRepository = new CommanderRepository(context);
        var evidenceRepository = new EvidenceRepository(context);
        var navigationRepository = new NavigationStateRepository(context);
        var reader = new JournalReader();

        using var journal = new StringReader("""
{"timestamp":"2024-01-01T00:00:01Z","event":"FSDJump","StarSystem":"LHS 3447","StarPos":[-23.4,-72.4,-35.3]}
""");

        await reader.ImportAsync(
            journal,
            starSystemRepository,
            commanderRepository,
            evidenceRepository,
            navigationRepository: navigationRepository);

        var state = await navigationRepository.GetAsync();
        Assert.NotNull(state);
        Assert.NotNull(state!.CurrentSystemId);

        var current = await starSystemRepository.FindByIdAsync(state.CurrentSystemId.Value);
        Assert.Equal("LHS 3447", current!.Name);
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
