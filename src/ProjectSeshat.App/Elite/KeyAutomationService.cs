using ProjectSeshat.Atlas;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.App.Elite;

/// <summary>
/// Drives Elite Dangerous with the commander's real key bindings. Reads <c>*.binds</c>, resolves
/// the keys for the actions we automate, and synthesizes input only when bindings exist and the
/// game is the foreground window. Gracefully falls back to no-op (the overlay simply names the
/// star) whenever a binding is missing or unassigned.
/// </summary>
public sealed class KeyAutomationService
{
    // Elite Dangerous action names used for auto-targeting the next route star.
    public const string TargetBindingName = "SelectTarget";
    public const string JumpBindingName = "HyperSuperCombination";

    private readonly BindingsPathResolver _pathResolver;
    private readonly IGameInputSender _input;
    private readonly Func<string, string?> _readFile;

    public KeyAutomationService(
        BindingsPathResolver? pathResolver = null,
        IGameInputSender? input = null,
        Func<string, string?>? readFile = null)
    {
        _pathResolver = pathResolver ?? new BindingsPathResolver();
        _input = input ?? new SendInputGameInputSender();
        _readFile = readFile ?? (path => File.Exists(path) ? File.ReadAllText(path) : null);
    }

    /// <summary>True when the current setup can actually auto-target the next star.</summary>
    public bool CanAutoTarget { get; private set; }

    /// <summary>Human-readable status for the UI, e.g. "Auto-target: J + T".</summary>
    public string StatusText { get; private set; } = "Auto-target unavailable";

    /// <summary>The binds file that was resolved (may be null).</summary>
    public string? BindingsFilePath { get; private set; }

    /// <summary>The bindings directory ED uses (may be null).</summary>
    public string? BindingsDirectory { get; private set; }

    /// <summary>Loads the commander's bindings and resolves the automation keys. Returns false when unusable.</summary>
    public bool TryEnable()
    {
        var bindsPath = _pathResolver.ResolveBindingsFile();
        BindingsFilePath = bindsPath;
        BindingsDirectory = _pathResolver.ResolveBindingsDirectory();

        if (bindsPath is null)
        {
            StatusText = "No key bindings found — the overlay names the star instead.";
            CanAutoTarget = false;
            return false;
        }

        var xml = _readFile(bindsPath);
        if (string.IsNullOrWhiteSpace(xml))
        {
            StatusText = "Key bindings file unreadable — the overlay names the star instead.";
            CanAutoTarget = false;
            return false;
        }

        var bindings = BindingsParser.Parse(xml);
        var target = BindingsParser.FindKeyboard(bindings, TargetBindingName);
        var jump = BindingsParser.FindKeyboard(bindings, JumpBindingName);

        if (target is null && jump is null)
        {
            StatusText = "No usable target/jump keys bound — the overlay names the star instead.";
            CanAutoTarget = false;
            return false;
        }

        TargetKey = target?.Key;
        JumpKey = jump?.Key;
        CanAutoTarget = true;
        StatusText = BuildStatus(target?.Key, jump?.Key);
        return true;
    }

    /// <summary>The commander's key for selecting/targeting the next star (may be null).</summary>
    public string? TargetKey { get; private set; }

    /// <summary>The commander's key for engaging the hyperjump (may be null).</summary>
    public string? JumpKey { get; private set; }

    /// <summary>
    /// Drives the game for the current step using the commander's real keys: for an FSS or DSS
    /// step it targets the suspicious body/signal to inspect; for a Jump/Back-track step it targets
    /// the next route star then charges the hyperjump. No-op (falling back to the overlay) when
    /// automation isn't armed or the game isn't focused.
    /// </summary>
    public void AutoTargetNextStar(NextAction step)
    {
        if (!CanAutoTarget)
        {
            return;
        }

        if (!_input.IsEliteInForeground)
        {
            return;
        }

        switch (step.Action)
        {
            case NextActionKind.Fss:
            case NextActionKind.Dss:
                // Target the body/signal ahead so the player inspects or maps the right thing.
                if (TargetKey is not null)
                {
                    _input.Press(TargetKey);
                }

                break;

            case NextActionKind.Jump:
            case NextActionKind.BackTrack:
                if (TargetKey is not null)
                {
                    _input.Press(TargetKey);
                }

                if (JumpKey is not null)
                {
                    _input.Press(JumpKey);
                }

                break;
        }
    }

    private static string BuildStatus(string? targetKey, string? jumpKey)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(jumpKey))
        {
            parts.Add($"jump {Naked(jumpKey!)}");
        }

        if (!string.IsNullOrWhiteSpace(targetKey))
        {
            parts.Add($"target {Naked(targetKey!)}");
        }

        return parts.Count == 0 ? "Auto-target unavailable" : $"Auto-target: {string.Join(" · ", parts)}";
    }

    /// <summary>
    /// Sends a single jump-key press to verify the game reacts. Returns true only when the key was
    /// actually transmitted (game focused + a jump key resolved); false otherwise.
    /// </summary>
    public bool TestJump()
    {
        if (JumpKey is null)
        {
            return false;
        }

        if (!_input.IsEliteInForeground)
        {
            return false;
        }

        _input.Press(JumpKey);
        return true;
    }

    private static string Naked(string key) => key.Replace("Key_", "", StringComparison.OrdinalIgnoreCase);
}