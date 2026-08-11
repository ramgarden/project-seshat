using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Community;

/// <summary>Normalizes an EDDN journal-schema message into the fields the platform cares about.</summary>
public sealed record EddnEvent(
    string Event,
    string? StarSystem,
    GalacticCoordinates? Position,
    string? BodyName,
    string? PlanetClass,
    bool? IsTerraformable,
    double? DistanceFromArrivalLs,
    DateTimeOffset ReportedAt);

/// <summary>Parses raw EDDN JSON messages (respecting the EDDN envelope <c>message</c> wrapper).</summary>
public static class EddnMessageParser
{
    /// <summary>Returns a normalized event, or null when the message is not one we handle or is malformed.</summary>
    public static EddnEvent? Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var document = ParseDocument(json);
        if (document is null)
        {
            return null;
        }

        var root = document.RootElement;

        // EDDN wraps journal events under a "message" property; tolerate raw events too.
        var message = root.TryGetProperty("message", out var messageElement) ? messageElement : root;

        if (!message.TryGetProperty("event", out var eventName))
        {
            return null;
        }

        var eventType = eventName.GetString();
        DateTimeOffset? reportedAt = null;
        if (message.TryGetProperty("timestamp", out var timestamp) && DateTimeOffset.TryParse(timestamp.GetString(), out var parsed))
        {
            reportedAt = parsed;
        }

        if (eventType == "FSDJump")
        {
            var system = GetString(message, "StarSystem");
            var position = GetPosition(message);
            return new EddnEvent(eventType, system, position, null, null, null, null, reportedAt ?? DateTimeOffset.UtcNow);
        }

        if (eventType is "FSSDiscoveryScan" or "DiscoveryScan")
        {
            return new EddnEvent(eventType, GetString(message, "StarSystem"), null, null, null, null, null, reportedAt ?? DateTimeOffset.UtcNow);
        }

        if (eventType == "Scan")
        {
            var planetClass = GetString(message, "PlanetClass");
            var terraformable = message.TryGetProperty("TerraformState", out var tf)
                ? tf.GetString() == "Terraformable"
                : (bool?)null;
            double? distanceLs = message.TryGetProperty("DistanceFromArrivalLS", out var dist) && dist.ValueKind == System.Text.Json.JsonValueKind.Number
                ? dist.GetDouble()
                : null;

            return new EddnEvent(
                eventType,
                null,
                null,
                GetString(message, "BodyName"),
                planetClass,
                terraformable,
                distanceLs,
                reportedAt ?? DateTimeOffset.UtcNow);
        }

        return null;
    }

    private static System.Text.Json.JsonDocument? ParseDocument(string json)
    {
        try
        {
            return System.Text.Json.JsonDocument.Parse(json);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static string? GetString(System.Text.Json.JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String && value.GetString() is { Length: > 0 } s
            ? s
            : null;

    private static GalacticCoordinates? GetPosition(System.Text.Json.JsonElement element)
    {
        if (!element.TryGetProperty("StarPos", out var starPos) || starPos.ValueKind != System.Text.Json.JsonValueKind.Array || starPos.GetArrayLength() < 3)
        {
            return null;
        }

        return new GalacticCoordinates(starPos[0].GetDouble(), starPos[1].GetDouble(), starPos[2].GetDouble());
    }
}