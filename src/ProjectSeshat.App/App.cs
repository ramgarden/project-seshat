using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using ProjectSeshat.App.ViewModels;
using ProjectSeshat.Data;
using ProjectSeshat.Data.Repositories;

namespace ProjectSeshat.App;

public sealed class App : Application
{
    private const string DatabasePath = "project-seshat.db";

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var options = new DbContextOptionsBuilder<ProjectSeshatDbContext>()
                .UseSqlite($"Data Source={DatabasePath}")
                .Options;

            var context = new ProjectSeshatDbContext(options);
            context.Database.EnsureCreated();

            var viewModel = new MainWindowViewModel(
                new StarSystemRepository(context),
                new CommanderRepository(context),
                new EvidenceRepository(context));

            desktop.MainWindow = new MainWindow(viewModel);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
