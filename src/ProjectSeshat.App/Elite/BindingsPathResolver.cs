namespace ProjectSeshat.App.Elite;

/// <summary>
/// Locates the Elite Dangerous key-bindings file (<c>Options\Bindings\*.binds</c>). Mirrors the
/// journal path resolver so it works whether ED is installed via the Frontier store or Steam.
/// </summary>
public sealed class BindingsPathResolver
{
    private readonly IEnumerable<string> _candidatePaths;

    public BindingsPathResolver(IEnumerable<string>? candidatePaths = null)
    {
        _candidatePaths = candidatePaths ?? GetDefaultCandidatePaths();
    }

    /// <summary>Returns the path to the most likely <c>.binds</c> file, or null if none is found.</summary>
    public string? ResolveBindingsFile()
    {
        foreach (var directory in _candidatePaths)
        {
            // Prefer the preset ED is actually using, then any .binds in that folder.
            var active = StartPresetResolver.ResolveActiveBindingsFile(directory);
            if (active is not null)
            {
                return active;
            }
        }

        return null;
    }

    /// <summary>Returns the first existing bindings directory among the candidates, or null.</summary>
    public string? ResolveBindingsDirectory()
    {
        foreach (var directory in _candidatePaths)
        {
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                return directory;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetDefaultCandidatePaths()
    {
        var paths = new List<string>();

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var savedGames = Path.Combine(userProfile, "Saved Games", "Frontier Developments", "Elite Dangerous", "Options", "Bindings");
        AddIfExists(paths, savedGames);

        // Local application data mirror of the bindings (some installs write here instead).
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        AddIfExists(paths, Path.Combine(localAppData, "Frontier Developments", "Elite Dangerous", "Options", "Bindings"));

        // Steam userdata: <id>\248820\remote\Options\Bindings.
        foreach (var root in new[]
                 {
                     Path.Combine("C:", "Program Files (x86)", "Steam", "userdata"),
                     Path.Combine("C:", "Program Files", "Steam", "userdata")
                 })
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var userDir in Directory.EnumerateDirectories(root))
            {
                AddIfExists(paths, Path.Combine(userDir, "248820", "remote", "Options", "Bindings"));
            }
        }

        return paths;
    }

    private static void AddIfExists(List<string> paths, string path)
    {
        if (Directory.Exists(path))
        {
            paths.Add(path);
        }
    }
}
