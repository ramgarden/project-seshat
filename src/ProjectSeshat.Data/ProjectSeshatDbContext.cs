using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Data;

public sealed class ProjectSeshatDbContext : DbContext
{
    private static readonly ValueConverter<GalacticCoordinates?, string?> PositionConverter = new(
        position => position == null ? null : ToPositionString(position),
        value => value == null ? null : ParseCoordinates(value));

    public ProjectSeshatDbContext(DbContextOptions<ProjectSeshatDbContext> options)
        : base(options)
    {
    }

    public DbSet<StarSystem> StarSystems => Set<StarSystem>();

    public DbSet<Commander> Commanders => Set<Commander>();

    public DbSet<EvidenceRecord> Evidence => Set<EvidenceRecord>();

    public DbSet<JournalImportTracker> JournalImportTrackers => Set<JournalImportTracker>();

    public DbSet<CelestialBody> CelestialBodies => Set<CelestialBody>();

    public DbSet<CodexEntry> CodexEntries => Set<CodexEntry>();

    public DbSet<Observation> Observations => Set<Observation>();

    public DbSet<ResearchThread> ResearchThreads => Set<ResearchThread>();

    public DbSet<NavigationState> NavigationStates => Set<NavigationState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StarSystem>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasConversion(
                id => id.Value,
                value => new StarSystemId(value));
            entity.Property(x => x.Name).IsRequired();
            entity.Property(x => x.Position).HasColumnType("TEXT").HasConversion(PositionConverter);
            entity.Property(x => x.SurveyState).HasConversion<string>();
            entity.Property(x => x.NonBodySignals);
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
            entity.Property(x => x.ThreadId).HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                value => value.HasValue ? new ResearchThreadId(value.Value) : (ResearchThreadId?)null);
        });

        modelBuilder.Entity<JournalImportTracker>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FilePath).IsRequired();
            entity.HasIndex(x => x.FilePath).IsUnique();
            entity.Property(x => x.Fingerprint).IsRequired();
        });

        modelBuilder.Entity<CelestialBody>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasConversion(
                id => id.Value,
                value => new CelestialBodyId(value));
            entity.Property(x => x.SystemId).HasConversion(
                id => id.Value,
                value => new StarSystemId(value));
            entity.HasIndex(x => x.SystemId);
            entity.Property(x => x.Name).IsRequired();
            entity.Property(x => x.Kind).HasConversion<string>();
            entity.Property(x => x.ScanStatus).HasConversion<string>();
            entity.Property(x => x.WorthDss);
        });

        modelBuilder.Entity<CodexEntry>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasConversion(
                id => id.Value,
                value => new CodexEntryId(value));
            entity.Property(x => x.Name).IsRequired();
            entity.Property(x => x.Category).HasConversion<string>();
            entity.Property(x => x.DiscoveredAt).IsRequired();
        });

        modelBuilder.Entity<Observation>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasConversion(
                id => id.Value,
                value => new ObservationGuid(value));
            entity.Property(x => x.BodyId).HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                value => value.HasValue ? new CelestialBodyId(value.Value) : (CelestialBodyId?)null);
            entity.Property(x => x.CommanderId).HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                value => value.HasValue ? new CommanderId(value.Value) : (CommanderId?)null);
            entity.HasIndex(x => x.BodyId);
            entity.Property(x => x.Notes).IsRequired();
            entity.Property(x => x.ObservedAt).IsRequired();
        });

        modelBuilder.Entity<ResearchThread>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasConversion(
                id => id.Value,
                value => new ResearchThreadId(value));
            entity.Property(x => x.Subject).IsRequired();
            entity.Property(x => x.SystemId).HasConversion(
                id => id.HasValue ? id.Value.Value : (long?)null,
                value => value.HasValue ? new StarSystemId(value.Value) : (StarSystemId?)null);
            entity.Property(x => x.BodyId).HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                value => value.HasValue ? new CelestialBodyId(value.Value) : (CelestialBodyId?)null);
            entity.Property(x => x.Status).HasConversion<string>();
            entity.Property(x => x.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<NavigationState>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasConversion(
                id => id.Value,
                value => new NavigationStateId(value));
            entity.Property(x => x.CurrentSystemId).HasConversion(
                id => id.HasValue ? id.Value.Value : (long?)null,
                value => value.HasValue ? new StarSystemId(value.Value) : (StarSystemId?)null);
            entity.Property(x => x.LastUpdatedAt).IsRequired();

            // Only one navigation state row at a time.
            entity.HasIndex(x => x.Id).IsUnique();
        });
    }

    private static string? ToPositionString(GalacticCoordinates? position)
        => position is null ? null : $"{position.X};{position.Y};{position.Z}";

    private static GalacticCoordinates? ParseCoordinates(string value)
    {
        var parts = value.Split(';');
        if (parts.Length != 3 ||
            !double.TryParse(parts[0], out var x) ||
            !double.TryParse(parts[1], out var y) ||
            !double.TryParse(parts[2], out var z))
        {
            return null;
        }

        return new GalacticCoordinates(x, y, z);
    }
}
