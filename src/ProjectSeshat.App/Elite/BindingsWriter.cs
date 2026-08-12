using System.Xml.Linq;

namespace ProjectSeshat.App.Elite;

/// <summary>
/// Writes a minimal, valid Elite Dangerous key-bindings file (<c>.binds</c>) plus the
/// <c>StartPreset.start</c> marker so ED treats it as the active preset. The file is intentionally
/// sparse: ED fills any unspecified action from its built-in defaults, so this only binds the two
/// actions we automate (target + hyperjump) and does not clobber the player's other controls.
/// </summary>
public sealed class BindingsWriter
{
    private const string DefaultPresetName = "Seshat.Auto.4.0";
    private const string DefaultTargetKey = "Key_T";
    private const string DefaultJumpKey = "Key_J";

    private readonly Func<string, bool> _fileExists;
    private readonly Action<string, string> _writeFile;

    public BindingsWriter(
        Func<string, bool>? fileExists = null,
        Action<string, string>? writeFile = null)
    {
        _fileExists = fileExists ?? File.Exists;
        _writeFile = writeFile ?? ((path, contents) => File.WriteAllText(path, contents));
    }

    /// <summary>Writes the minimal binds preset and the StartPreset marker. Returns the written binds path.</summary>
    public string WriteDefaultBindings(string? bindingsDirectory = null)
    {
        var directory = bindingsDirectory ?? DefaultDirectory();
        Directory.CreateDirectory(directory);

        var presetName = DefaultPresetName;
        var bindsPath = Path.Combine(directory, $"{presetName}.binds");
        var startMarker = Path.Combine(directory, "StartPreset.start");

        _writeFile(bindsPath, BuildXml(presetName));
        _writeFile(startMarker, presetName);

        return bindsPath;
    }

    /// <summary>Returns true when a bindings directory already exists (so the feature can decide whether to offer writing).</summary>
    public bool HasBindings(string? bindingsDirectory = null)
    {
        var directory = bindingsDirectory ?? DefaultDirectory();
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        return Directory.EnumerateFiles(directory, "*.binds", SearchOption.TopDirectoryOnly).Any()
               || _fileExists(Path.Combine(directory, "StartPreset.start"));
    }

    private static string DefaultDirectory()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(profile, "Saved Games", "Frontier Developments", "Elite Dangerous", "Options", "Bindings");
    }

    private static string BuildXml(string presetName)
    {
        var root = new XElement(
            "Root",
            new XAttribute("PresetName", presetName),
            new XElement(KeyAutomationService.TargetBindingName,
                new XAttribute("Primary", "Keyboard"),
                new XAttribute("Key", DefaultTargetKey),
                new XAttribute("Device", "")),
            new XElement(KeyAutomationService.JumpBindingName,
                new XAttribute("Primary", "Keyboard"),
                new XAttribute("Key", DefaultJumpKey),
                new XAttribute("Device", "")));

        return $"<?xml version=\"1.0\" encoding=\"UTF-8\" ?>{Environment.NewLine}{root}{Environment.NewLine}";
    }
}