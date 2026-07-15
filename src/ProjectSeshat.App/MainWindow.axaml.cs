using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using ProjectSeshat.App.ViewModels;
using ProjectSeshat.Data;
using ProjectSeshat.Data.Repositories;

namespace ProjectSeshat.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
        : this(CreateDefaultViewModel())
    {
    }

    public MainWindow(MainWindowViewModel viewModel)
    {
        AvaloniaXamlLoader.Load(this);
        DataContext = viewModel;
    }

    private static MainWindowViewModel CreateDefaultViewModel()
    {
        var options = new DbContextOptionsBuilder<ProjectSeshatDbContext>()
            .UseSqlite("Data Source=project-seshat.db")
            .Options;

        var context = new ProjectSeshatDbContext(options);
        context.Database.EnsureCreated();

        return new MainWindowViewModel(
            new StarSystemRepository(context),
            new CommanderRepository(context),
            new EvidenceRepository(context));
    }
}
