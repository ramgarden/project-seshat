using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProjectSeshat.App.ViewModels;
using ProjectSeshat.Core.Domain;
using ProjectSeshat.Data;
using ProjectSeshat.Data.Repositories;
using Xunit;

namespace ProjectSeshat.Tests;

public sealed class AppExploreViewTests
{
    [Fact]
    public async Task ViewModel_PopulatesExploreItemsFromImportedData()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        var starSystemRepository = new StarSystemRepository(context);
        var commanderRepository = new CommanderRepository(context);
        var evidenceRepository = new EvidenceRepository(context);

        await starSystemRepository.SaveAsync(new StarSystem(new StarSystemId(1), "LHS 3447"));
        await evidenceRepository.SaveAsync(new EvidenceRecord(
            new EvidenceId(Guid.NewGuid()),
            EvidenceKind.Observation,
            "Scanned a body in LHS 3447",
            DateTimeOffset.UtcNow));

        var viewModel = new MainWindowViewModel(starSystemRepository, commanderRepository, evidenceRepository);

        Assert.Contains(viewModel.ExploreItems, item => item.SystemName == "LHS 3447");
        Assert.Contains(viewModel.ExploreItems, item => item.Detail.Contains("evidence", StringComparison.OrdinalIgnoreCase));
        Assert.NotEmpty(viewModel.ExplorationSummary);
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
