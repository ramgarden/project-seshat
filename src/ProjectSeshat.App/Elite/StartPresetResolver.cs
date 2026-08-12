namespace ProjectSeshat.App.Elite;

/// <summary>
/// Resolves which <c>.binds</c> file Elite Dangerous is actually using. ED records the active
/// preset name in a sibling <c>StartPreset.start</c> file; the binds file is named
/// <c>&lt;PresetName&gt;.binds</c>. Falling back to the first <c>.binds</c> in the directory when
/// no active preset marker is present.
/// </summary>
public static class StartPresetResolver
{
    /// <summary>Returns the path of the active binds file in the given directory, or null.</summary>
    public static string? ResolveActiveBindingsFile(string bindingsDirectory)
    {
        if (string.IsNullOrWhiteSpace(bindingsDirectory) || !Directory.Exists(bindingsDirectory))
        {
            return null;
        }

        // ED names the active preset in StartPreset.start (e.g. "Custom.4.0").
        var startPresetPath = Path.Combine(bindingsDirectory, "StartPreset.start");
        if (File.Exists(startPresetPath))
        {
            var presetName = SafeReadTrimmed(startPresetPath);
            if (!string.IsNullOrWhiteSpace(presetName))
            {
                var candidate = Path.Combine(bindingsDirectory, $"{presetName}.binds");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return Directory.EnumerateFiles(bindingsDirectory, "*.binds", SearchOption.TopDirectoryOnly)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static string? SafeReadTrimmed(string path)
    {
        try
        {
            return File.ReadAllText(path).Trim();
        }
        catch
        {
            return null;
        }
    }
}
