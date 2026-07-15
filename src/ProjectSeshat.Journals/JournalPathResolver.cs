namespace ProjectSeshat.Journals;

/// <summary>Locates a directory that likely contains Elite Dangerous journal files.</summary>
public sealed class JournalPathResolver
{
    private readonly IEnumerable<string> _candidatePaths;

    public JournalPathResolver(IEnumerable<string>? candidatePaths = null)
    {
        _candidatePaths = candidatePaths ?? GetDefaultCandidatePaths();
    }

    public string? ResolvePath()
    {
        foreach (var candidate in _candidatePaths)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (Directory.Exists(candidate) && DirectoryContainsJournalFiles(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool DirectoryContainsJournalFiles(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return false;
        }

        var journalFiles = Directory.EnumerateFiles(directory, "*.log", SearchOption.TopDirectoryOnly)
            .Where(path => path.Contains("Journal", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (journalFiles.Count > 0)
        {
            return true;
        }

        return Directory.EnumerateDirectories(directory)
            .Any(child => child.Contains("Journal", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> GetDefaultCandidatePaths()
    {
        var paths = new List<string>();

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var savedGamesPath = Path.Combine(userProfile, "Saved Games", "Frontier Developments", "Elite Dangerous");
        var candidates = new[]
        {
            savedGamesPath,
            Path.Combine(savedGamesPath, "Logs"),
            Path.Combine(userProfile, "AppData", "Local", "Frontier Developments", "Elite Dangerous"),
            Path.Combine(userProfile, "AppData", "LocalLow", "Frontier Developments", "Elite Dangerous"),
            Path.Combine("C:", "Program Files (x86)", "Steam", "userdata"),
            Path.Combine("C:", "Program Files", "Steam", "userdata")
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                paths.Add(candidate);
            }
        }

        if (Directory.Exists(Path.Combine("C:", "Program Files (x86)", "Steam")))
        {
            foreach (var userDirectory in Directory.EnumerateDirectories(Path.Combine("C:", "Program Files (x86)", "Steam", "userdata")))
            {
                var journalDirectory = Path.Combine(userDirectory, "248820", "remote", "Journals");
                if (Directory.Exists(journalDirectory))
                {
                    paths.Add(journalDirectory);
                }
            }
        }

        if (Directory.Exists(Path.Combine("C:", "Program Files", "Steam")))
        {
            foreach (var userDirectory in Directory.EnumerateDirectories(Path.Combine("C:", "Program Files", "Steam", "userdata")))
            {
                var journalDirectory = Path.Combine(userDirectory, "248820", "remote", "Journals");
                if (Directory.Exists(journalDirectory))
                {
                    paths.Add(journalDirectory);
                }
            }
        }

        return paths;
    }
}
