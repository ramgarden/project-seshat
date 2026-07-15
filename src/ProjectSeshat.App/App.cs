using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using ProjectSeshat.App.ViewModels;
using ProjectSeshat.Data;
using ProjectSeshat.Data.Repositories;
using ProjectSeshat.Journals;

namespace ProjectSeshat.App;

public sealed class App : Application
{
    private const string DatabasePath = "project-seshat.db";

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = CreateMainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    public static MainWindowViewModel CreateViewModel()
    {
        var options = new DbContextOptionsBuilder<ProjectSeshatDbContext>()
            .UseSqlite($"Data Source={DatabasePath}")
            .Options;

        var context = new ProjectSeshatDbContext(options);
        context.Database.EnsureCreated();

        var starSystemRepository = new StarSystemRepository(context);
        var commanderRepository = new CommanderRepository(context);
        var evidenceRepository = new EvidenceRepository(context);
        var importTrackerRepository = new JournalImportTrackerRepository(context);
        var journalReader = new JournalReader();
        var pathResolver = new JournalPathResolver();

        return new MainWindowViewModel(
            starSystemRepository,
            commanderRepository,
            evidenceRepository,
            pathResolver,
            journalReader,
            importTrackerRepository);
    }

    public static MainWindow CreateMainWindow() => new(CreateViewModel());
}
