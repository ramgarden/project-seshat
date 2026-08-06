using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ProjectSeshat.Data;

/// <summary>
/// Design-time factory used by the EF Core tools (dotnet ef) to build a context
/// when generating migrations outside of the running desktop application.
/// </summary>
public sealed class ProjectSeshatDbContextFactory : IDesignTimeDbContextFactory<ProjectSeshatDbContext>
{
    public ProjectSeshatDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ProjectSeshatDbContext>()
            .UseSqlite("Data Source=project-seshat.db")
            .Options;

        return new ProjectSeshatDbContext(options);
    }
}
