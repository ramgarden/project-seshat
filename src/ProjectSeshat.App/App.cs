using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using ProjectSeshat.App.ViewModels;
using ProjectSeshat.App.Views;
using ProjectSeshat.Atlas;
using ProjectSeshat.Community;
using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Data;
using ProjectSeshat.Data.Repositories;
using ProjectSeshat.Investigations;
using ProjectSeshat.Journals;
using ProjectSeshat.ThreadEngine;

namespace ProjectSeshat.App;

public sealed class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        SeshatLog.InstallGlobalHandlers();
        SeshatLog.Info("Application starting");

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = CreateMainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    public static string ResolveDatabasePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dataDirectory = Path.Combine(appData, "ProjectSeshat");
        Directory.CreateDirectory(dataDirectory);
        return Path.Combine(dataDirectory, "project-seshat.db");
    }

    public static MainWindowViewModel CreateViewModel()
    {
        var databasePath = ResolveDatabasePath();
        var options = new DbContextOptionsBuilder<ProjectSeshatDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        var context = new ProjectSeshatDbContext(options);
        context.Database.Migrate();

        var starSystemRepository = new StarSystemRepository(context);
        var commanderRepository = new CommanderRepository(context);
        var evidenceRepository = new EvidenceRepository(context);
        var importTrackerRepository = new JournalImportTrackerRepository(context);
        var celestialBodyRepository = new CelestialBodyRepository(context);
        var codexEntryRepository = new CodexEntryRepository(context);
        var observationRepository = new ObservationRepository(context);
        var navigationRepository = new NavigationStateRepository(context);
        var surveyRegionRepository = new SurveyRegionRepository(context);
        var communityDiscoveryRepository = new CommunityDiscoveryRepository(context);
        var researchThreadRepository = new ResearchThreadRepository(context);
        var researchThreadEngine = new ResearchThreadEngine(researchThreadRepository);
        var investigationService = new InvestigationService(evidenceRepository, researchThreadRepository);
        var atlasService = new AtlasService();
        var journalReader = new JournalReader();
        var pathResolver = new JournalPathResolver();
        var journalWatcher = CreateJournalWatcher(pathResolver, journalReader, starSystemRepository, commanderRepository, evidenceRepository, importTrackerRepository, celestialBodyRepository, codexEntryRepository, navigationRepository);
        var communityService = new CommunityService(new EddnListener(new NetMqEddnTransport()), communityDiscoveryRepository);

        var viewModel = new MainWindowViewModel(
            starSystemRepository,
            commanderRepository,
            evidenceRepository,
            pathResolver,
            celestialBodyRepository,
            codexEntryRepository,
            observationRepository,
            navigationRepository,
            researchThreadEngine,
            investigationService,
            atlasService,
            journalWatcher,
            surveyRegionRepository,
            communityService,
            new WindowsSpeechVoicePinger());

        return viewModel;
    }

    private static JournalWatcher? CreateJournalWatcher(
        JournalPathResolver pathResolver,
        JournalReader journalReader,
        IStarSystemRepository starSystemRepository,
        ICommanderRepository commanderRepository,
        IEvidenceRepository evidenceRepository,
        IJournalImportTrackerRepository importTrackerRepository,
        ICelestialBodyRepository? celestialBodyRepository,
        ICodexEntryRepository? codexEntryRepository,
        INavigationStateRepository navigationRepository)
    {
        var resolvedPath = pathResolver.ResolvePath();
        if (resolvedPath is null)
        {
            return null;
        }

        return new JournalWatcher(
            resolvedPath,
            journalReader,
            starSystemRepository,
            commanderRepository,
            evidenceRepository,
            importTrackerRepository,
            celestialBodyRepository,
            codexEntryRepository,
            navigationRepository);
    }

    public static MainWindow CreateMainWindow()
    {
        var viewModel = CreateViewModel();
        viewModel.StartJournalWatcher();

        var overlay = new GuidanceOverlayWindow(viewModel.Guidance);
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.OverlayVisible))
            {
                if (viewModel.OverlayVisible)
                {
                    overlay.Show();
                }
                else
                {
                    overlay.Hide();
                }
            }
        };

        return new MainWindow(viewModel);
    }
}
