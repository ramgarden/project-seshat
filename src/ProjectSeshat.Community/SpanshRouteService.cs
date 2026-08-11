using System.Net.Http.Json;
using System.Text.Json;

namespace ProjectSeshat.Community;

/// <summary>A plotted leg of a Spansh route.</summary>
public sealed record RouteStop(string SystemName, double? DistanceLy, double? X, double? Y, double? Z);

/// <summary>
/// Plots jump routes and coverage using the Spansh public HTTP API.
/// </summary>
public sealed class SpanshRouteService
{
    private readonly HttpClient _http;

    public static string DefaultRouteEndpoint => "https://www.spansh.co.uk/api/route";

    public SpanshRouteService(HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
    }

    /// <summary>
    /// Requests a jump route between the given source and destination at the given jump range.
    /// </summary>
    public async Task<IReadOnlyList<RouteStop>> PlotRouteAsync(
        string source,
        string destination,
        double jumpRangeLy = 60,
        CancellationToken cancellationToken = default)
    {
        var payload = new { source, destination, range = jumpRangeLy };
        using var response = await _http.PostAsJsonAsync(DefaultRouteEndpoint, payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        if (!root.TryGetProperty("jumps", out var jumps) || jumps.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<RouteStop>();
        }

        var stops = new List<RouteStop>();
        foreach (var jump in jumps.EnumerateArray())
        {
            var system = jump.TryGetProperty("system", out var s) ? s.GetString() : null;
            if (system is null)
            {
                continue;
            }

            double? distance = jump.TryGetProperty("distance", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetDouble() : null;
            double? x = null, y = null, z = null;
            if (jump.TryGetProperty("coords", out var coords) && coords.ValueKind == JsonValueKind.Array && coords.GetArrayLength() >= 3)
            {
                x = coords[0].GetDouble();
                y = coords[1].GetDouble();
                z = coords[2].GetDouble();
            }

            stops.Add(new RouteStop(system, distance, x, y, z));
        }

        return stops;
    }
}
