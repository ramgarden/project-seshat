using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectSeshat.App;

/// <summary>
/// Persists the in-game guidance overlay's screen position between runs so it reopens
/// exactly where the player dragged it. Stored as a tiny JSON file next to the database
/// (UI preference, not survey data, so it deliberately avoids a schema migration).
/// </summary>
public sealed class OverlayPositionStore
{
    private readonly string _filePath;

    public OverlayPositionStore(string? appDataDirectory = null)
    {
        var directory = appDataDirectory ?? App.ResolveAppDataDirectory();
        _filePath = Path.Combine(directory, "overlay-position.json");
    }

    /// <summary>Loads the saved position, or null when none has been stored yet (first run).</summary>
    public (int X, int Y)? Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            using var stream = File.OpenRead(_filePath);
            var data = JsonSerializer.Deserialize(stream, AppOverlayPositionSerializerContext.Default.AppOverlayPositionDto);
            return data is { X: var x, Y: var y } ? (x, y) : null;
        }
        catch
        {
            // Corrupt/absent settings must never block the app from starting.
            return null;
        }
    }

    public void Save(int x, int y)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            var data = new AppOverlayPositionDto { X = x, Y = y };
            File.WriteAllText(_filePath, JsonSerializer.Serialize(data, AppOverlayPositionSerializerContext.Default.AppOverlayPositionDto));
        }
        catch
        {
            // Best-effort; losing the position is not fatal.
        }
    }
}

internal sealed record AppOverlayPositionDto
{
    public int X { get; init; }

    public int Y { get; init; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppOverlayPositionDto))]
internal sealed partial class AppOverlayPositionSerializerContext : JsonSerializerContext
{
}