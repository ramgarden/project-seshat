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
    GalacticCoordinates? CurrentPosition = null,
    NextAction? NextAction = null);

/// <summary>A system the user should discovery-scan (honk) first.</summary>
public sealed record HonkTarget(string SystemName, double? DistanceLy);

/// <summary>A system the user should FSS next (it has known signals worth resolving).</summary>
public sealed record FssTarget(string SystemName, int Signals, double? DistanceLy, string? SignalTypes = null);

/// <summary>A specific body the user should Surface-map with DSS.</summary>
public sealed record DssTarget(string BodyName, string SystemName, double DistanceLs, string Reason);

/// <summary>Classifies how a crawl hop moves the player.</summary>
public enum CrawlHopKind
{
    /// <summary>A normal outward jump to a not-yet-searched system.</summary>
    Jump,

    /// <summary>A step through an already-charted system toward the frontier.</summary>
    BackTrack
}

/// <summary>A single hop in the outward survey crawl.</summary>
public sealed record CrawlHop(
    CrawlHopKind Kind,
    string SystemName,
    double? DistanceLy,
    string Reason,
    GalacticCoordinates? ArriveAt);

/// <summary>A single immediate step the player should act on now.</summary>
public sealed record CrawlStep(string Action, string Target, string Reason, string? Detail = null);

/// <summary>A recommended starting point for an outward survey, derived from current findings.</summary>
public sealed record SearchGate(string SystemName, GalacticCoordinates Position, double Score, double DistanceLy, string Reasoning);

/// <summary>The two useful gate recommendations: the best overall, and the best that's near the commander.</summary>
public sealed record SearchGateSuggestions(SearchGate? Best, SearchGate? BestNearest);

/// <summary>
/// The full state of the systematic outward survey: a recommended gate, the ordered outward
/// route (nearest-unsearched-first with back-track steps when the local neighbourhood is
/// exhausted), and a single immediate next step to act on.
/// </summary>
public sealed record OutwardCrawl(
    SearchGate? RecommendedGate,
    IReadOnlyList<CrawlHop> Route,
    NextAction? NextAction,
    string? CurrentSystemName,
    GalacticCoordinates? CurrentPosition)
{
    /// <summary>Compatibility alias for the canonical next action.</summary>
    public CrawlStep? NextStep => NextAction is null
        ? null
        : new CrawlStep(
            NextAction.Action.ToString(),
            NextAction.Target,
            NextAction.Reason,
            NextAction.Detail);
}

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
        StarSystemId? currentSystemId = null;
        if (navigationRepository is not null)
        {
            var state = await navigationRepository.GetAsync(cancellationToken);
            if (state?.CurrentSystemId is { } resolvedCurrentSystemId)
            {
                var currentSystem = await systemRepository.FindByIdAsync(resolvedCurrentSystemId, cancellationToken);
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

        var nextAction = await BuildNextActionAsync(
            currentSystemId,
            allSystems,
            bodyRepository,
            route: Array.Empty<CrawlHop>(),
            cancellationToken);

        return new SearchGuide(
            honk,
            fss,
            dss,
            honk.Count,
            fss.Count,
            dss.Count,
            currentSystemName,
            currentPosition,
            nextAction);
    }

    /// <summary>
    /// Scans the current findings for things the community considers "worth investigating" while
    /// hunting Raxxla: notable body classes, 8th-moon designations, unusual signal types, lore-name
    /// matches, and anything inside the Sol-centred hunt bubble. Returns hits ranked by priority.
    /// </summary>
    public async Task<IReadOnlyList<RaxxlaIntelHit>> FindRaxxlaIntelAsync(
        IStarSystemRepository systemRepository,
        ICelestialBodyRepository? bodyRepository = null,
        int maxHits = 50,
        CancellationToken cancellationToken = default)
    {
        var hits = new List<RaxxlaIntelHit>();
        var systems = await systemRepository.ListAsync(100000, cancellationToken);

        foreach (var system in systems)
        {
            string systemName = system.Name;

            if (RaxxlaSearchIntel.IsWithinSolSearchArea(system.Position))
            {
                hits.Add(new RaxxlaIntelHit(
                    "Proximity", systemName, systemName,
                    "Inside the Sol hunt bubble (<200 ly) — the community search area",
                    1));
            }

            if (RaxxlaSearchIntel.ReasonForLoreName(systemName) is { } loreReason)
            {
                hits.Add(new RaxxlaIntelHit("Name", systemName, systemName, loreReason, 5));
            }

            if (RaxxlaSearchIntel.ReasonForSignalTypes(system.SignalTypes) is { } signalReason && system.NonBodySignals > 0)
            {
                hits.Add(new RaxxlaIntelHit("Signal", systemName, system.Name, signalReason, 4));
            }
        }

        if (bodyRepository is not null)
        {
            foreach (var system in systems)
            {
                var bodies = await bodyRepository.FindBySystemIdAsync(system.Id, cancellationToken);
                foreach (var body in bodies)
                {
                    if (RaxxlaSearchIntel.ReasonForBodyClass(body.PlanetClass) is { } bodyReason)
                    {
                        hits.Add(new RaxxlaIntelHit("Body", system.Name, body.Name, bodyReason, 3));
                    }

                    if (RaxxlaSearchIntel.ReasonForEighthMoon(body.Name) is { } moonReason)
                    {
                        hits.Add(new RaxxlaIntelHit("Body", system.Name, body.Name, moonReason, 5));
                    }
                }
            }
        }

        return hits
            .OrderByDescending(h => h.Priority)
            .ThenBy(h => h.SystemName, StringComparer.OrdinalIgnoreCase)
            .Take(maxHits)
            .ToList();
    }

    /// <summary>
    /// Recommends starting systems for a systematic outward survey, derived from current findings.
    /// Candidates are scored by the density of unsearched neighbours, proximity to high-scoring
    /// frontier regions, and (when community data is present) how little of the area the wider
    /// playerbase has already been in. Returns the single best overall candidate.
    /// </summary>
    public async Task<SearchGate?> RecommendSearchGateAsync(
        IStarSystemRepository systemRepository,
        INavigationStateRepository? navigationRepository = null,
        ICommunityDiscoveryRepository? communityRepository = null,
        double gateRadiusLy = 100,
        double frontierReachLy = 500,
        CancellationToken cancellationToken = default)
    {
        var gates = await ScoreSearchGatesAsync(systemRepository, navigationRepository, communityRepository, gateRadiusLy, frontierReachLy, cancellationToken);
        return gates.FirstOrDefault();
    }

    /// <summary>
    /// Recommends both the best starting system overall and the best one nearest the commander's
    /// current position (so a player can start searching right now without a long trip). Each
    /// carries its score, distance, and the reasoning behind it.
    /// </summary>
    public async Task<SearchGateSuggestions> RecommendSearchGatesAsync(
        IStarSystemRepository systemRepository,
        INavigationStateRepository? navigationRepository = null,
        ICommunityDiscoveryRepository? communityRepository = null,
        double gateRadiusLy = 100,
        double frontierReachLy = 500,
        CancellationToken cancellationToken = default)
    {
        var gates = await ScoreSearchGatesAsync(systemRepository, navigationRepository, communityRepository, gateRadiusLy, frontierReachLy, cancellationToken);

        var best = gates.FirstOrDefault();

        // Best nearest: the top-scoring gate close enough to depart from right now. Gates are
        // sorted by score, so the first one within a comfortable radius is the best nearby option.
        var bestNearest = gates.FirstOrDefault(g => g.DistanceLy <= gateRadiusLy)
                          ?? gates.OrderBy(g => g.DistanceLy).FirstOrDefault();

        return new SearchGateSuggestions(best, bestNearest);
    }

    private async Task<IReadOnlyList<SearchGate>> ScoreSearchGatesAsync(
        IStarSystemRepository systemRepository,
        INavigationStateRepository? navigationRepository,
        ICommunityDiscoveryRepository? communityRepository,
        double gateRadiusLy,
        double frontierReachLy,
        CancellationToken cancellationToken)
    {
        var allSystems = await systemRepository.ListAsync(100000, cancellationToken);
        var positioned = allSystems.Where(s => s.Position is not null).ToList();
        if (positioned.Count == 0)
        {
            return Array.Empty<SearchGate>();
        }

        var unsearched = positioned.Where(s => s.SurveyState == SystemSurveyState.Unexplored).ToList();
        // A search gate is a known base you radiate outward from, so only charted systems qualify.
        var candidates = positioned.Where(s => s.SurveyState != SystemSurveyState.Unexplored).ToList();
        if (candidates.Count == 0)
        {
            return Array.Empty<SearchGate>();
        }

        var reference = await ResolveReferenceAsync(systemRepository, navigationRepository, positioned, cancellationToken);

        var regions = await RankUndiscoveredRegionsAsync(systemRepository, maxRegions: 40, cancellationToken: cancellationToken);

        IReadOnlyList<CommunityDiscovery> community = Array.Empty<CommunityDiscovery>();
        if (communityRepository is not null)
        {
            community = await communityRepository.ListRecentAsync(100000, cancellationToken);
        }

        var gates = new List<SearchGate>();
        foreach (var candidate in candidates)
        {
            var position = candidate.Position!;
            var unsearchedNearby = unsearched.Count(s =>
                Distance(position, s.Position!) <= gateRadiusLy);

            var frontierProximity = 0.0;
            if (regions.Count > 0)
            {
                foreach (var region in regions)
                {
                    var distanceToRegion = Distance(position, region.Center);
                    var contribution = region.Score / (1 + distanceToRegion / frontierReachLy);
                    if (contribution > frontierProximity)
                    {
                        frontierProximity = contribution;
                    }
                }
            }

            var communityNearby = community.Count(d => d.Position is not null && Distance(position, d.Position!) <= gateRadiusLy);
            // Reachability modestly favours being able to start now, but does not dominate score.
            var reachability = 1.0 / (1 + Distance(position, reference) / gateRadiusLy);

            var score = (unsearchedNearby * 2.0) + frontierProximity - (communityNearby * 1.0) + (reachability * 0.5);
            if (score <= 0)
            {
                continue;
            }

            var distanceFromCommander = Distance(position, reference);
            var reasoning = $"{unsearchedNearby} unsearched neighbour{(unsearchedNearby == 1 ? "" : "s")} within {gateRadiusLy:0} Ly; " +
                            $"frontier region score {frontierProximity:F1} within {frontierReachLy:0} Ly; " +
                            $"{communityNearby} community sighting{(communityNearby == 1 ? "" : "s")} nearby; " +
                            $"{distanceFromCommander:F0} Ly from commander.";

            gates.Add(new SearchGate(candidate.Name, position, score, distanceFromCommander, reasoning));
        }

        return gates
            .OrderByDescending(g => g.Score)
            .ThenBy(g => g.DistanceLy)
            .ToList();
    }

    /// <summary>
    /// Builds the systematic outward survey: a recommended gate (unless one is supplied), the ordered
    /// route from the current position — nearest-unsearched-first, with back-track steps through charted
    /// stars when the local neighbourhood is exhausted — and the single immediate next step.
    /// </summary>
    public async Task<OutwardCrawl> BuildOutwardCrawlAsync(
        IStarSystemRepository systemRepository,
        INavigationStateRepository? navigationRepository = null,
        ICelestialBodyRepository? bodyRepository = null,
        ICommunityDiscoveryRepository? communityRepository = null,
        StarSystemId? gateSystemId = null,
        double neighbourhoodRadiusLy = 60,
        int maxHops = 40,
        CancellationToken cancellationToken = default)
    {
        var allSystems = await systemRepository.ListAsync(100000, cancellationToken);
        var positioned = allSystems.Where(s => s.Position is not null).ToList();
        if (positioned.Count == 0)
        {
            return new OutwardCrawl(null, Array.Empty<CrawlHop>(), null, null, null);
        }

        var (currentSystemName, currentPosition, currentSystemId) =
            await ResolveCurrentAsync(systemRepository, navigationRepository, cancellationToken);
        var reference = currentPosition
                        ?? (positioned.FirstOrDefault(s => s.Id == gateSystemId)?.Position)
                        ?? Centroid(positioned.Select(s => s.Position!).ToList());

        // When a gate is chosen it anchors the crawl origin.
        StarSystem? gateSystem = null;
        if (gateSystemId is { } gateId)
        {
            gateSystem = positioned.FirstOrDefault(s => s.Id == gateId);
            if (gateSystem?.Position is not null)
            {
                reference = gateSystem.Position;
            }
        }

        var recommendedGate = gateSystem is null
            ? await RecommendSearchGateAsync(systemRepository, navigationRepository, communityRepository, cancellationToken: cancellationToken)
            : null;

        var unsearched = positioned.Where(s => s.SurveyState == SystemSurveyState.Unexplored).ToList();
        var charted = positioned
            .Where(s => s.SurveyState != SystemSurveyState.Unexplored && s.Position is not null)
            .ToList();

        var route = BuildCrawlRoute(reference, unsearched, charted, neighbourhoodRadiusLy, maxHops);

        var nextStep = await BuildNextActionAsync(
            currentSystemId,
            allSystems,
            bodyRepository,
            route,
            cancellationToken);

        return new OutwardCrawl(recommendedGate, route, nextStep, currentSystemName, currentPosition);
    }

    /// <summary>The immediate step to act on next, preferring in-system work before a jump.</summary>
    public async Task<NextAction?> BuildOutwardNextActionAsync(
        IStarSystemRepository systemRepository,
        INavigationStateRepository? navigationRepository = null,
        ICelestialBodyRepository? bodyRepository = null,
        double neighbourhoodRadiusLy = 60,
        CancellationToken cancellationToken = default)
    {
        var allSystems = await systemRepository.ListAsync(100000, cancellationToken);
        var positioned = allSystems.Where(s => s.Position is not null).ToList();
        if (positioned.Count == 0)
        {
            return null;
        }

        var (_, currentPosition, currentSystemId) =
            await ResolveCurrentAsync(systemRepository, navigationRepository, cancellationToken);
        var reference = currentPosition ?? Centroid(positioned.Select(s => s.Position!).ToList());

        var unsearched = positioned.Where(s => s.SurveyState == SystemSurveyState.Unexplored).ToList();
        var charted = positioned
            .Where(s => s.SurveyState != SystemSurveyState.Unexplored && s.Position is not null)
            .ToList();

        var route = BuildCrawlRoute(reference, unsearched, charted, neighbourhoodRadiusLy, 1);
        return await BuildNextActionAsync(currentSystemId, allSystems, bodyRepository, route, cancellationToken);
    }

    private static async Task<(string? SystemName, GalacticCoordinates? Position, StarSystemId? SystemId)> ResolveCurrentAsync(
        IStarSystemRepository systemRepository,
        INavigationStateRepository? navigationRepository,
        CancellationToken cancellationToken)
    {
        if (navigationRepository is null)
        {
            return (null, null, null);
        }

        var state = await navigationRepository.GetAsync(cancellationToken);
        if (state?.CurrentSystemId is not { } currentSystemId)
        {
            return (null, null, null);
        }

        var currentSystem = await systemRepository.FindByIdAsync(currentSystemId, cancellationToken);
        return (currentSystem?.Name, currentSystem?.Position, currentSystemId);
    }

    private static async Task<GalacticCoordinates> ResolveReferenceAsync(
        IStarSystemRepository systemRepository,
        INavigationStateRepository? navigationRepository,
        IReadOnlyList<StarSystem> positioned,
        CancellationToken cancellationToken)
    {
        if (navigationRepository is not null)
        {
            var state = await navigationRepository.GetAsync(cancellationToken);
            if (state?.CurrentSystemId is { } resolvedCurrentSystemId)
            {
                var currentSystem = await systemRepository.FindByIdAsync(resolvedCurrentSystemId, cancellationToken);
                if (currentSystem?.Position is not null)
                {
                    return currentSystem.Position;
                }
            }
        }

        return positioned.Count > 0
            ? Centroid(positioned.Select(s => s.Position!).ToList())
            : new GalacticCoordinates(0, 0, 0);
    }

    private static IReadOnlyList<CrawlHop> BuildCrawlRoute(
        GalacticCoordinates reference,
        IReadOnlyList<StarSystem> unsearched,
        IReadOnlyList<StarSystem> charted,
        double neighbourhoodRadiusLy,
        int maxHops)
    {
        var route = new List<CrawlHop>();
        var remaining = unsearched
            .OrderByDescending(RaxxlaPriority)
            .ThenBy(s => RaxxlaSearchIntel.IsWithinSolSearchArea(s.Position) ? 0 : 1)
            .ThenBy(s => Distance(reference, s.Position!))
            .ToList();
        var availableCharted = charted.Where(s => s.Position is not null).ToList();

        var cursor = reference;
        while (remaining.Count > 0 && route.Count < maxHops)
        {
            // Prefer a Raxxla-intel system (lore name / suspicious signal / in the Sol bubble) so
            // the search-bubble jump plot heads at interesting systems first; distance breaks ties.
            var next = remaining
                .OrderByDescending(s => RaxxlaPriority(s))
                .ThenBy(s => RaxxlaSearchIntel.IsWithinSolSearchArea(s.Position) ? 0 : 1)
                .ThenBy(s => Distance(cursor, s.Position!))
                .First();

            var distance = Distance(cursor, next.Position!);
            if (distance <= neighbourhoodRadiusLy)
            {
                // A not-yet-searched star is right here: take the normal outward hop.
                route.Add(new CrawlHop(CrawlHopKind.Jump, next.Name, distance, "Nearest unsearched star within reach", next.Position));
                cursor = next.Position!;
                remaining.Remove(next);
                continue;
            }

            // All stars in the local neighbourhood are searched. Back-track through a charted
            // star that lies on the way toward the nearest still-unsearched one.
            var target = next;
            var waypoint = availableCharted
                .Where(w => Distance(cursor, w.Position!) <= neighbourhoodRadiusLy * 2)
                .Where(w => Distance(w.Position!, target.Position!) < Distance(cursor, target.Position!))
                .OrderBy(w => Distance(cursor, w.Position!) + Distance(w.Position!, target.Position!))
                .FirstOrDefault();

            if (waypoint is not null)
            {
                route.Add(new CrawlHop(
                    CrawlHopKind.BackTrack,
                    waypoint.Name,
                    Distance(cursor, waypoint.Position!),
                    $"All nearby stars searched — head via {waypoint.Name} toward {target.Name}",
                    waypoint.Position));
                cursor = waypoint.Position!;
                availableCharted.Remove(waypoint);
                continue;
            }

            // No charted stepping stone: stretch jump straight to the next unsearched star.
            route.Add(new CrawlHop(CrawlHopKind.Jump, target.Name, distance, "No back-track waypoint — long jump to nearest unsearched", target.Position));
            cursor = target.Position!;
            remaining.Remove(target);
        }

        return route;
    }

    private async Task<NextAction?> BuildNextActionAsync(
        StarSystemId? currentSystemId,
        IReadOnlyList<StarSystem> allSystems,
        ICelestialBodyRepository? bodyRepository,
        IReadOnlyList<CrawlHop> route,
        CancellationToken cancellationToken)
    {
        if (currentSystemId is { } currentId)
        {
            var current = allSystems.FirstOrDefault(s => s.Id == currentId);
            if (current is not null && current.Position is not null)
            {
                if (current.SurveyState == SystemSurveyState.Unexplored)
                {
                    return new NextAction(
                        NextActionKind.Honk,
                        current.Name,
                        "Arrived but not yet discovery-scanned",
                        TargetSystem: current.Name);
                }

                if (current.SurveyState == SystemSurveyState.Honked && current.NonBodySignals > 0)
                {
                    var signalReason = RaxxlaSearchIntel.ReasonForSignalTypes(current.SignalTypes);
                    var reason = signalReason is null
                        ? "Honk detected ordinary signals — resolve them with FSS"
                        : $"Honk detected {signalReason} — resolve them with FSS";
                    return new NextAction(
                        NextActionKind.Fss,
                        current.Name,
                        reason,
                        current.SignalTypes,
                        TargetSystem: current.Name);
                }

                if (bodyRepository is not null &&
                    await FindDssTargetAsync(currentId, bodyRepository, cancellationToken) is { } dssBody)
                {
                    var intelReason = RaxxlaSearchIntel.ReasonForEighthMoon(dssBody.Name)
                                      ?? RaxxlaSearchIntel.ReasonForBodyClass(dssBody.PlanetClass);
                    var reason = intelReason is null
                        ? $"DSS candidate in {current.Name}"
                        : $"DSS candidate in {current.Name} — {intelReason}";
                    return new NextAction(
                        NextActionKind.Dss,
                        dssBody.Name,
                        reason,
                        DssReason(dssBody),
                        TargetSystem: current.Name,
                        TargetBody: dssBody.Name);
                }
            }
        }

        var hop = route.FirstOrDefault();
        if (hop is not null)
        {
            return hop.Kind == CrawlHopKind.BackTrack
                ? new NextAction(
                    NextActionKind.BackTrack,
                    hop.SystemName,
                    hop.Reason,
                    TargetSystem: hop.SystemName,
                    DistanceLy: hop.DistanceLy)
                : new NextAction(
                    NextActionKind.Jump,
                    hop.SystemName,
                    hop.Reason,
                    TargetSystem: hop.SystemName,
                    DistanceLy: hop.DistanceLy);
        }

        return null;
    }

    private static async Task<CelestialBody?> FindDssTargetAsync(
        StarSystemId systemId,
        ICelestialBodyRepository bodyRepository,
        CancellationToken cancellationToken)
    {
        var bodies = await bodyRepository.FindBySystemIdAsync(systemId, cancellationToken);
        if (bodies.Count == 0)
        {
            return null;
        }

        var intelBody = bodies
            .Where(b => b.ScanStatus != ScanStatus.Mapped)
            .Where(b => RaxxlaSearchIntel.ReasonForEighthMoon(b.Name) is not null
                        || RaxxlaSearchIntel.ReasonForBodyClass(b.PlanetClass) is not null)
            .OrderByDescending(b => RaxxlaSearchIntel.ReasonForEighthMoon(b.Name) is not null)
            .ThenBy(BodyCreditOrder)
            .FirstOrDefault();

        if (intelBody is not null)
        {
            return intelBody;
        }

        return bodies
            .Where(b => b.WorthDss && b.ScanStatus != ScanStatus.Mapped)
            .OrderBy(BodyCreditOrder)
            .FirstOrDefault();
    }

    private static int RaxxlaPriority(StarSystem system)
    {
        var score = 0;
        if (RaxxlaSearchIntel.ReasonForLoreName(system.Name) is not null)
        {
            score += 3;
        }

        if (system.SurveyState == SystemSurveyState.Honked && RaxxlaSearchIntel.ReasonForSignalTypes(system.SignalTypes) is not null)
        {
            score += 2;
        }

        if (RaxxlaSearchIntel.IsWithinSolSearchArea(system.Position))
        {
            score += 1;
        }

        return score;
    }

    private static int BodyCreditOrder(CelestialBody body)
        => body.PlanetClass?.Contains("Earthlike", StringComparison.OrdinalIgnoreCase) == true ? 0
            : body.IsTerraformable == true ? 1
            : body.PlanetClass?.Contains("Water world", StringComparison.OrdinalIgnoreCase) == true ? 2
            : body.PlanetClass?.Contains("Ammonia world", StringComparison.OrdinalIgnoreCase) == true ? 3
            : 4;

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
