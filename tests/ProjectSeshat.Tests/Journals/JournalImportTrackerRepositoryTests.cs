using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProjectSeshat.Data;
using ProjectSeshat.Data.Repositories;
using Xunit;

namespace ProjectSeshat.Tests.Journals;

public sealed class JournalImportTrackerRepositoryTests
{
    [Fact]
    public async Task MarkImportedAsync_PersistsAProcessedJournalFile()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        var tracker = new JournalImportTrackerRepository(context);

        await tracker.MarkImportedAsync(@"C:\journals\Journal.20240101T000000.01.log", "fingerprint-1");

        Assert.True(await tracker.HasImportedAsync(@"C:\journals\Journal.20240101T000000.01.log"));
        Assert.False(await tracker.HasImportedAsync(@"C:\journals\Journal.20240102T000000.01.log"));
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
