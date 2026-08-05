using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProjectSeshat.Core.Domain;
using ProjectSeshat.Data;
using ProjectSeshat.Data.Repositories;
using ProjectSeshat.Journals;
using Xunit;

namespace ProjectSeshat.Tests.Journals;

public sealed class PersistenceProbeTests
{
    [Fact]
    public async Task Import_IsDeduplicatedAcrossSessions_OnDiskDb()
    {
        var dbFile = Path.Combine(Path.GetTempPath(), $"seshat-probe-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<ProjectSeshatDbContext>()
                .UseSqlite($"Data Source={dbFile}")
                .Options;

            var fileA = "C:\\probe\\Journal.20260101T000000.01.log";
            var fileB = "C:\\probe\\Journal.20260102T000000.01.log";
            var contentA = """
{"timestamp":"2026-01-01T00:00:00Z","event":"FSDJump","StarSystem":"PROBE 1","StarPos":[10,20,30]}
{"timestamp":"2026-01-01T00:01:00Z","event":"Scan","BodyName":"PROBE 1 A 1","StarType":null,"DistanceFromArrivalLS":2000}
""";
            var contentB = """
{"timestamp":"2026-01-02T00:00:00Z","event":"FSDJump","StarSystem":"PROBE 2","StarPos":[40,50,60]}
""";

            async Task ImportAll(string fileA2, string fileB2)
            {
                await using var ctx = new ProjectSeshatDbContext(options);
                var systems = new StarSystemRepository(ctx);
                var commanders = new CommanderRepository(ctx);
                var evidence = new EvidenceRepository(ctx);
                var tracker = new JournalImportTrackerRepository(ctx);
                var bodies = new CelestialBodyRepository(ctx);
                var reader = new JournalReader();

                await reader.ImportAsync(new StringReader(contentA), systems, commanders, evidence, tracker, fileA2, cancellationToken: default, celestialBodyRepository: bodies);
                await reader.ImportAsync(new StringReader(contentB), systems, commanders, evidence, tracker, fileB2, cancellationToken: default, celestialBodyRepository: bodies);
            }

            // Session 1: fresh DB
            await using (var init = new ProjectSeshatDbContext(options))
            {
                init.Database.EnsureCreated();
            }
            await ImportAll(fileA, fileB);

            // Session 2: reopen the same on-disk DB and import the same two files again
            await ImportAll(fileA, fileB);

            await using (var verify = new ProjectSeshatDbContext(options))
            {
                var systems = await new StarSystemRepository(verify).ListAsync(100);
                var evidenceCount = await new EvidenceRepository(verify).CountAsync();
                var systemCount = await new StarSystemRepository(verify).CountAsync();
                var trackerCount = await verify.JournalImportTrackers.CountAsync();

                Assert.Equal(2, systemCount);          // no growth across sessions
                Assert.Equal(1, evidenceCount);        // no growth across sessions
                Assert.Equal(2, trackerCount);         // both files tracked exactly once
                Assert.Equal(2, systems.Count);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(dbFile);
        }
    }

    [Fact]
    public async Task Import_DeduplicatesByContentAcrossDifferentPaths()
    {
        var dbFile = Path.Combine(Path.GetTempPath(), $"seshat-probe-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<ProjectSeshatDbContext>()
                .UseSqlite($"Data Source={dbFile}")
                .Options;

            var content = """
{"timestamp":"2026-01-01T00:00:00Z","event":"FSDJump","StarSystem":"RENAMED 1","StarPos":[10,20,30]}
""";

            async Task ImportAs(string path)
            {
                await using var ctx = new ProjectSeshatDbContext(options);
                var systems = new StarSystemRepository(ctx);
                var commanders = new CommanderRepository(ctx);
                var evidence = new EvidenceRepository(ctx);
                var tracker = new JournalImportTrackerRepository(ctx);
                var fingerprint = JournalReader.ComputeFingerprint(content);
                await new JournalReader().ImportAsync(
                    new StringReader(content), systems, commanders, evidence, tracker, path,
                    cancellationToken: default, contentFingerprint: fingerprint);
            }

            await using (var init = new ProjectSeshatDbContext(options))
            {
                init.Database.EnsureCreated();
            }

            // Same content imported under two different paths (a moved/renamed file)
            await ImportAs("C:\\old\\Journal.00001.log");
            await ImportAs("C:\\new\\Journal.00001.log");

            await using var verify = new ProjectSeshatDbContext(options);
            Assert.Equal(1, await new StarSystemRepository(verify).CountAsync()); // not re-imported
            Assert.Equal(1, await verify.JournalImportTrackers.CountAsync());     // single content entry despite two paths
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(dbFile);
        }
    }
}
