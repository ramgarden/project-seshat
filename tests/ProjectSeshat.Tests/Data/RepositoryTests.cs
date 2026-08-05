using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProjectSeshat.Core.Domain;
using ProjectSeshat.Data;
using ProjectSeshat.Data.Repositories;
using Xunit;

namespace ProjectSeshat.Tests.Data;

public sealed class RepositoryTests
{
    [Fact]
    public async Task StarSystemRepository_RoundTripsEntities()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        var repository = new StarSystemRepository(context);
        var system = new StarSystem(new StarSystemId(42), "LHS 3447");

        await repository.SaveAsync(system);

        var loaded = await repository.FindByIdAsync(system.Id);
        Assert.NotNull(loaded);
        Assert.Equal(system, loaded);
        Assert.Equal(1, await repository.CountAsync());
    }

    [Fact]
    public async Task CommanderRepository_RoundTripsEntities()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        var repository = new CommanderRepository(context);
        var commander = new Commander(new CommanderId(Guid.NewGuid()), "Juno Quill");

        await repository.SaveAsync(commander);

        var loaded = await repository.FindByIdAsync(commander.Id);
        Assert.NotNull(loaded);
        Assert.Equal(commander, loaded);
        Assert.Equal(1, await repository.CountAsync());
    }

    [Fact]
    public async Task EvidenceRepository_RoundTripsEntities()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        var repository = new EvidenceRepository(context);
        var evidence = new EvidenceRecord(new EvidenceId(Guid.NewGuid()), EvidenceKind.Observation, "Orbital survey complete", DateTimeOffset.UtcNow);

        await repository.SaveAsync(evidence);

        var loaded = await repository.FindByIdAsync(evidence.Id);
        Assert.NotNull(loaded);
        Assert.Equal(evidence, loaded);
        Assert.Equal(1, await repository.CountAsync());
    }

    [Fact]
    public async Task ResearchThreadRepository_RoundTripsEntities()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        var repository = new ResearchThreadRepository(context);
        var thread = new ResearchThread(
            new ResearchThreadId(Guid.NewGuid()),
            "Trace the wandering comet",
            "Possible stellar phenomena rendezvous.",
            new StarSystemId(42),
            null,
            ThreadStatus.Investigating,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        await repository.SaveAsync(thread);

        var loaded = await repository.FindByIdAsync(thread.Id);
        Assert.NotNull(loaded);
        Assert.Equal(thread, loaded);
        Assert.Equal(1, await repository.CountAsync());
        Assert.Single(await repository.FindBySubjectAsync(new StarSystemId(42), null));
    }

    [Fact]
    public async Task CelestialBodyRepository_TracksScanStatus()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        var repository = new CelestialBodyRepository(context);
        var mapped = new CelestialBody(
            new CelestialBodyId(Guid.NewGuid()),
            new StarSystemId(1),
            "LHS 3447 1",
            BodyKind.Planet,
            null,
            "Rocky body",
            null,
            1200,
            ScanStatus.Mapped);
        var needsDss = new CelestialBody(
            new CelestialBodyId(Guid.NewGuid()),
            new StarSystemId(1),
            "LHS 3447 2",
            BodyKind.Planet,
            null,
            "Rocky body",
            null,
            800,
            ScanStatus.FssScanned);

        await repository.SaveAsync(mapped);
        await repository.SaveAsync(needsDss);

        Assert.Equal(1, await repository.CountNeedingSurfaceScanAsync());
        var scheduled = await repository.ListNeedingSurfaceScanAsync(10);
        Assert.Single(scheduled);
        Assert.Equal(needsDss.Id, scheduled[0].Id);

        await repository.UpdateScanStatusAsync(needsDss.Id, ScanStatus.Mapped);
        Assert.Equal(0, await repository.CountNeedingSurfaceScanAsync());
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
}
