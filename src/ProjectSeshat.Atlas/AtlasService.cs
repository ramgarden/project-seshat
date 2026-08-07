using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Atlas;

/// <summary>A spatial snapshot of the surveyed region of the galaxy.</summary>
public sealed record SurveySnapshot(
    GalacticCoordinates Reference,
    int SurveyedSystems,
    int SurveyedCells);

/// <summary>A candidate region that is largely unexplored and close to the surveyed frontier.</summary>
public sealed record UndiscoveredRegion(
    GalacticCoordinates Center,
    int NearbyVisitedSystems,
    double Score,
    double DistanceFromReferenceLy);

/// <summary>The next survey actions, tiered by what the user should do next.</summary>
public sealed record SearchGuide(
    IReadOnlyList<HonkTarget> NeedHonk,
    IReadOnlyList<FssTarget> NeedFss,
    IReadOnlyList<DssTarget> NeedDss,
    int HonkCount,
    int FssCount,
    int DssCount,
    string? CurrentSystemName = null,
    GalacticCoordinates? CurrentPosition = null);

/// <summary>A system the user should discovery-scan (honk) first.</summary>
public sealed record HonkTarget(string SystemName, double? DistanceLy);

/// <summary>A system the user should FSS next (it has known signals worth resolving).</summary>
public sealed record FssTarget(string SystemName, int Signals, double? DistanceLy, string? SignalTypes = null);

/// <summary>A specific body the user should Surface-map with DSS.</summary>
public sealed record DssTarget(string BodyName, string SystemName, double DistanceLs, string Reason);

/// <summary>Provides spatial and astronomical research operations for the atlas boundary.</summary>
public sealed class AtlasService
{
    /// <summary>
    /// Returns all celestial bodies catalogued for the given star system,
    /// ordered by distance from the arrival point ascending.
    /// </summary>
    public async Task<IReadOnlyList<CelestialBody>> GetBodiesForSystemAsync(
        StarSystemId systemId,
        ICelestialBodyRepository repository,
        CancellationToken cancellationToken = default)
    {
        var bodies = await repository.FindBySystemIdAsync(systemId, cancellationToken);
        return bodies
            .OrderBy(b => b.DistanceFromArrivalLs ?? double.MaxValue)
            .ToList();
    }

    public async Task<SurveySnapshot> GetSurveySnapshotAsync(
        IStarSystemRepository repository,
        int cellSizeLy = 500,
        CancellationToken cancellationToken = default)
    {
        var systems = await repository.ListWithPositionAsync(10000, cancellationToken);
        var positions = systems.Select(s => s.Position!).Where(p => p is not null).Select(p => (GalacticCoordinates)p!).ToList();

        if (positions.Count == 0)
        {
            return new SurveySnapshot(new GalacticCoordinates(0, 0, 0), 0, 0);
        }

        var reference = Centroid(positions);
        var cells = positions.Select(p => Cell(p, cellSizeLy)).Distinct().ToList();
        return new SurveySnapshot(reference, positions.Count, cells.Count);
    }

    public async Task<IReadOnlyList<UndiscoveredRegion>> RankUndiscoveredRegionsAsync(
        IStarSystemRepository repository,
        int maxRegions = 15,
        double cellSizeLy = 500,
        int gridRadius = 4,
        CancellationToken cancellationToken = default)
    {
        var systems = await repository.ListWithPositionAsync(10000, cancellationToken);
        var positions = systems.Select(s => s.Position!).Where(p => p is not null).Select(p => (GalacticCoordinates)p!).ToList();

        if (positions.Count == 0)
        {
            return Array.Empty<UndiscoveredRegion>();
        }

        var reference = Centroid(positions);
        var surveyedCells = positions
            .GroupBy(p => Cell(p, cellSizeLy))
            .ToDictionary(g => g.Key, g => g.Count());

        // Walk a cube around the surveyed centroid and flag frontier cells (not visited,
        // but sharing a face with at least one visited cell).
        var home = Cell(reference, cellSizeLy);
        var homeIndex = (home.Item1, home.Item2, home.Item3);
        var candidates = new List<(int, int, int)>();

        for (var x = homeIndex.Item1 - gridRadius; x <= homeIndex.Item1 + gridRadius; x++)
        for (var y = homeIndex.Item2 - gridRadius; y <= homeIndex.Item2 + gridRadius; y++)
        for (var z = homeIndex.Item3 - gridRadius; z <= homeIndex.Item3 + gridRadius; z++)
        {
            if (surveyedCells.ContainsKey((x, y, z)))
            {
                continue;
            }

            if (HasSurveyedNeighbor((x, y, z), surveyedCells))
            {
                candidates.Add((x, y, z));
            }
        }

        var regions = new List<UndiscoveredRegion>();
        foreach (var cell in candidates)
        {
            var center = new GalacticCoordinates(
                (cell.Item1 + 0.5) * cellSizeLy,
                (cell.Item2 + 0.5) * cellSizeLy,
                (cell.Item3 + 0.5) * cellSizeLy);
            var nearbyVisited = CountVisitedNear(cell, positions, cellSizeLy);
            var distance = Distance(center, reference);
            var proximity = 1.0 / (1.0 + distance / cellSizeLy);
            var score = nearbyVisited + proximity;

            regions.Add(new UndiscoveredRegion(center, nearbyVisited, score, distance));
        }

        return regions
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.DistanceFromReferenceLy)
            .Take(maxRegions)
            .ToList();
    }

    /// <summary>
    /// Recomputes the frontier regions from the current survey data and persists them as the
    /// source-of-truth survey listing. Regions are stable per grid cell: already-persisted cells
    /// keep their identity, and cells that become charted are flagged surveyed.
    /// </summary>
    public async Task RefreshSurveyRegionsAsync(
        IStarSystemRepository systemRepository,
        ISurveyRegionRepository regionRepository,
        double cellSizeLy = 500,
        int gridRadius = 4,
        CancellationToken cancellationToken = default)
    {
        var systems = await systemRepository.ListWithPositionAsync(10000, cancellationToken);
        var positions = systems.Select(s => s.Position!).Where(p => p is not null).Select(p => (GalacticCoordinates)p!).ToList();
        if (positions.Count == 0)
        {
            return;
        }

        var reference = Centroid(positions);
        var surveyedCells = positions
            .GroupBy(p => Cell(p, cellSizeLy))
            .ToDictionary(g => g.Key, g => g.Count());

        // Frontier cells: not yet charted, but sharing a face with at least one charted cell.
        var home = Cell(reference, cellSizeLy);
        var homeIndex = (home.Item1, home.Item2, home.Item3);
        var candidates = new List<(int, int, int)>();
        for (var x = homeIndex.Item1 - gridRadius; x <= homeIndex.Item1 + gridRadius; x++)
        for (var y = homeIndex.Item2 - gridRadius; y <= homeIndex.Item2 + gridRadius; y++)
        for (var z = homeIndex.Item3 - gridRadius; z <= homeIndex.Item3 + gridRadius; z++)
        {
            if (surveyedCells.ContainsKey((x, y, z)))
            {
                continue;
            }

            if (HasSurveyedNeighbor((x, y, z), surveyedCells))
            {
                candidates.Add((x, y, z));
            }
        }

        var existing = (await regionRepository.ListAsync(100000, cancellationToken)).ToList();
        var now = DateTimeOffset.UtcNow;
        var toSave = new List<SurveyRegion>();

        foreach (var cell in candidates)
        {
            var center = new GalacticCoordinates(
                (cell.Item1 + 0.5) * cellSizeLy,
                (cell.Item2 + 0.5) * cellSizeLy,
                (cell.Item3 + 0.5) * cellSizeLy);
            var nearby = CountVisitedNear(cell, positions, cellSizeLy);
            var distance = Distance(center, reference);
            var score = nearby + 1.0 / (1.0 + distance / cellSizeLy);

            var existingRegion = existing.FirstOrDefault(r =>
                r.CellX == cell.Item1 && r.CellY == cell.Item2 && r.CellZ == cell.Item3);

            if (existingRegion is not null)
            {
                toSave.Add(existingRegion with
                {
                    Score = score,
                    NearbyVisitedSystems = nearby,
                    DistanceFromReferenceLy = distance,
                    Surveyed = false,
                    LastUpdatedAt = now
                });
            }
            else
            {
                toSave.Add(new SurveyRegion(
                    new SurveyRegionId(Guid.NewGuid()),
                    cell.Item1,
                    cell.Item2,
                    cell.Item3,
                    center,
                    score,
                    nearby,
                    distance,
                    false,
                    now));
            }
        }

        // Flag any previously persisted region whose cell has since been charted.
        foreach (var region in existing)
        {
            if (surveyedCells.ContainsKey((region.CellX, region.CellY, region.CellZ)))
            {
                toSave.Add(region with { Surveyed = true, LastUpdatedAt = now });
            }
        }

        await regionRepository.SaveAllAsync(toSave, cancellationToken);
    }

    public async Task<SearchGuide> BuildSearchGuideAsync(
        IStarSystemRepository systemRepository,
        ICelestialBodyRepository bodyRepository,
        INavigationStateRepository? navigationRepository = null,
        CancellationToken cancellationToken = default)
    {
        var allSystems = await systemRepository.ListAsync(100000, cancellationToken);
        var positioned = allSystems.Where(s => s.Position is not null).Select(s => s.Position!).Cast<GalacticCoordinates>().ToList();

        // Anchor the plot to the commander's current position when known, otherwise fall back
        // to the centroid of everything already surveyed.
        string? currentSystemName = null;
        GalacticCoordinates? currentPosition = null;
        if (navigationRepository is not null)
        {
            var state = await navigationRepository.GetAsync(cancellationToken);
            if (state?.CurrentSystemId is { } currentSystemId)
            {
                var currentSystem = await systemRepository.FindByIdAsync(currentSystemId, cancellationToken);
                if (currentSystem?.Position is not null)
                {
                    currentSystemName = currentSystem.Name;
                    currentPosition = currentSystem.Position;
                }
            }
        }

        var reference = currentPosition ?? (positioned.Count > 0
            ? Centroid(positioned)
            : new GalacticCoordinates(0, 0, 0));

        double? DistanceFrom(StarSystem s) => s.Position is null ? null : Distance(s.Position, reference);

        // Honk targets are ordered nearest-first from the current position: this ordered list is
        // the jump plot the user follows next.
        var honkSystems = await systemRepository.ListBySurveyStateAsync(SystemSurveyState.Unexplored, 200, cancellationToken);
        var honk = honkSystems
            .Select(s => new HonkTarget(s.Name, DistanceFrom(s)))
            .OrderBy(h => h.DistanceLy ?? double.MaxValue)
            .ToList();

        var fssSystems = await systemRepository.ListBySurveyStateAsync(SystemSurveyState.Honked, 200, cancellationToken);
        var fss = fssSystems
            .Select(s => new FssTarget(s.Name, s.NonBodySignals, DistanceFrom(s), s.SignalTypes))
            .OrderByDescending(t => t.Signals)
            .ThenBy(t => t.DistanceLy ?? double.MaxValue)
            .ToList();

        var dssBodies = await bodyRepository.ListDssCandidatesAsync(500, cancellationToken);
        var systemNameById = allSystems.ToDictionary(s => s.Id, s => s.Name);
        var dss = dssBodies
            .Select(b => new DssTarget(
                b.Name,
                systemNameById.TryGetValue(b.SystemId, out var systemName) ? systemName : "Unknown system",
                b.DistanceFromArrivalLs ?? 0,
                DssReason(b)))
            .ToList();

        return new SearchGuide(honk, fss, dss, honk.Count, fss.Count, dss.Count, currentSystemName, currentPosition);
    }

    private static string DssReason(CelestialBody body)
        => body.IsTerraformable == true
            ? "Terraformable world"
            : string.IsNullOrWhiteSpace(body.PlanetClass)
                ? "Candidate"
                : body.PlanetClass!;

    private static bool HasSurveyedNeighbor((int, int, int) cell, Dictionary<(int, int, int), int> surveyedCells)
    {
        (int x, int y, int z) = cell;
        var neighbors = new (int, int, int)[]
        {
            (x + 1, y, z), (x - 1, y, z),
            (x, y + 1, z), (x, y - 1, z),
            (x, y, z + 1), (x, y, z - 1)
        };
        return neighbors.Any(n => surveyedCells.ContainsKey(n));
    }

    private static int CountVisitedNear((int, int, int) cell, List<GalacticCoordinates> positions, double cellSizeLy)
    {
        var count = 0;
        foreach (var p in positions)
        {
            var c = Cell(p, cellSizeLy);
            if (Math.Abs(c.Item1 - cell.Item1) <= 1 &&
                Math.Abs(c.Item2 - cell.Item2) <= 1 &&
                Math.Abs(c.Item3 - cell.Item3) <= 1)
            {
                count++;
            }
        }

        return count;
    }

    private static GalacticCoordinates Centroid(List<GalacticCoordinates> positions)
    {
        var sum = positions.Aggregate(
            (X: 0.0, Y: 0.0, Z: 0.0),
            (acc, p) => (acc.X + p.X, acc.Y + p.Y, acc.Z + p.Z));
        return new GalacticCoordinates(
            sum.X / positions.Count,
            sum.Y / positions.Count,
            sum.Z / positions.Count);
    }

    private static (int, int, int) Cell(GalacticCoordinates p, double cellSizeLy)
        => ((int)Math.Floor(p.X / cellSizeLy), (int)Math.Floor(p.Y / cellSizeLy), (int)Math.Floor(p.Z / cellSizeLy));

    private static double Distance(GalacticCoordinates a, GalacticCoordinates b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        var dz = a.Z - b.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}
