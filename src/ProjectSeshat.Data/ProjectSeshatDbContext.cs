using Microsoft.EntityFrameworkCore;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Data;

public sealed class ProjectSeshatDbContext : DbContext
{
    public ProjectSeshatDbContext(DbContextOptions<ProjectSeshatDbContext> options)
        : base(options)
    {
    }

    public DbSet<StarSystem> StarSystems => Set<StarSystem>();

    public DbSet<Commander> Commanders => Set<Commander>();

    public DbSet<EvidenceRecord> Evidence => Set<EvidenceRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StarSystem>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasConversion(
                id => id.Value,
                value => new StarSystemId(value));
            entity.Property(x => x.Name).IsRequired();
        });

        modelBuilder.Entity<Commander>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasConversion(
                id => id.Value,
                value => new CommanderId(value));
            entity.Property(x => x.Name).IsRequired();
        });

        modelBuilder.Entity<EvidenceRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasConversion(
                id => id.Value,
                value => new EvidenceId(value));
            entity.Property(x => x.Kind).HasConversion<string>();
            entity.Property(x => x.Summary).IsRequired();
            entity.Property(x => x.RecordedAt).IsRequired();
        });
    }
}
