using System.Text.Json;
using ProjectSeshat.Core.Contracts;
using ProjectSeshat.Core.Domain;

namespace ProjectSeshat.Journals;

/// <summary>Imports a narrow slice of Elite Dangerous journal events into the research repositories.</summary>
public sealed class JournalReader
{
    public async Task ImportAsync(
        TextReader journalText,
        IStarSystemRepository starSystemRepository,
        ICommanderRepository commanderRepository,
        IEvidenceRepository evidenceRepository,
        IJournalImportTrackerRepository? importTrackerRepository = null,
        string? filePath = null,
        CancellationToken cancellationToken = default,
        ICelestialBodyRepository? celestialBodyRepository = null,
        ICodexEntryRepository? codexEntryRepository = null)
    {
        if (importTrackerRepository is not null && !string.IsNullOrWhiteSpace(filePath))
        {
            var alreadyImported = await importTrackerRepository.HasImportedAsync(filePath, cancellationToken);
            if (alreadyImported)
            {
                return;
            }
        }

        var lineNumber = 0;
        var fingerprintBuilder = new System.Text.StringBuilder();
        string? line;
        while ((line = await journalText.ReadLineAsync(cancellationToken)) != null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (lineNumber % 1000 == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            fingerprintBuilder.AppendLine(line);
            await ImportLineAsync(
                line,
                starSystemRepository,
                commanderRepository,
                evidenceRepository,
                celestialBodyRepository,
                codexEntryRepository,
                cancellationToken);
        }

        if (importTrackerRepository is not null && !string.IsNullOrWhiteSpace(filePath))
        {
            var fingerprint = fingerprintBuilder.ToString();
            var existing = await importTrackerRepository.HasImportedAsync(filePath, cancellationToken);
            if (!existing)
            {
                await importTrackerRepository.MarkImportedAsync(filePath, fingerprint, cancellationToken);
            }
        }
    }

    public async Task ImportDirectoryAsync(
        string directory,
        IStarSystemRepository starSystemRepository,
        ICommanderRepository commanderRepository,
        IEvidenceRepository evidenceRepository,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        var files = Directory.EnumerateFiles(directory, "Journal*.log", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Take(5)
            .ToList();

        foreach (var file in files)
        {
            await using var stream = File.OpenRead(file);
            using var reader = new StreamReader(stream);
            await ImportAsync(reader, starSystemRepository, commanderRepository, evidenceRepository, null, file, cancellationToken);
        }
    }

    public async Task ImportAutoDetectedPathAsync(
        JournalPathResolver pathResolver,
        IStarSystemRepository starSystemRepository,
        ICommanderRepository commanderRepository,
        IEvidenceRepository evidenceRepository,
        CancellationToken cancellationToken = default)
    {
        var resolvedPath = pathResolver.ResolvePath();
        if (resolvedPath is null)
        {
            return;
        }

        await ImportDirectoryAsync(resolvedPath, starSystemRepository, commanderRepository, evidenceRepository, cancellationToken);
    }

    private static async Task ImportLineAsync(
        string line,
        IStarSystemRepository starSystemRepository,
        ICommanderRepository commanderRepository,
        IEvidenceRepository evidenceRepository,
        ICelestialBodyRepository? celestialBodyRepository,
        ICodexEntryRepository? codexEntryRepository,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line);
        if (payload is null)
        {
            return;
        }

        if (!payload.TryGetValue("event", out var eventName))
        {
            return;
        }

        var eventType = eventName.GetString();

        if (eventType == "LoadGame" && payload.TryGetValue("Commander", out var commanderName))
        {
            var commanderNameValue = commanderName.GetString() ?? "Unknown Commander";
            var exists = await commanderRepository.ExistsByNameAsync(commanderNameValue, cancellationToken);
            if (exists)
            {
                return;
            }

            var commanderId = new CommanderId(Guid.NewGuid());
            var commander = new Commander(commanderId, commanderNameValue);
            await commanderRepository.SaveAsync(commander, cancellationToken);
            return;
        }

        if (eventType == "FSDJump" && payload.TryGetValue("StarSystem", out var systemName))
        {
            var systemNameValue = systemName.GetString() ?? "Unknown System";
            var exists = await starSystemRepository.ExistsByNameAsync(systemNameValue, cancellationToken);
            if (exists)
            {
                return;
            }

            var systemId = new StarSystemId(Math.Abs(systemNameValue.GetHashCode()));
            var system = new StarSystem(systemId, systemNameValue);
            await starSystemRepository.SaveAsync(system, cancellationToken);
            return;
        }

        if (eventType == "Scan" && payload.TryGetValue("BodyName", out var scanBodyName))
        {
            var bodyNameValue = scanBodyName.GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(bodyNameValue))
            {
                return;
            }

            // Persist basic evidence for the scan (existing behaviour)
            var evidenceSummary = $"Scanned {bodyNameValue}";
            var evidenceExists = await evidenceRepository.ExistsBySummaryAsync(evidenceSummary, cancellationToken);
            if (!evidenceExists)
            {
                var evidence = new EvidenceRecord(
                    new EvidenceId(Guid.NewGuid()),
                    EvidenceKind.Observation,
                    evidenceSummary,
                    DateTimeOffset.UtcNow);
                await evidenceRepository.SaveAsync(evidence, cancellationToken);
            }

            // Persist celestial body into the atlas when repository is available
            if (celestialBodyRepository is not null)
            {
                var bodyExists = await celestialBodyRepository.ExistsByNameAsync(bodyNameValue, cancellationToken);
                if (!bodyExists)
                {
                    var kind = DetermineBodyKind(payload);
                    var starClass = payload.TryGetValue("StarType", out var st) ? st.GetString() : null;
                    var planetClass = payload.TryGetValue("PlanetClass", out var pc) ? pc.GetString() : null;
                    bool? isTerraformable = payload.TryGetValue("TerraformState", out var tf)
                        ? tf.GetString() == "Terraformable"
                        : null;
                    double? distanceLs = payload.TryGetValue("DistanceFromArrivalLS", out var dist)
                        ? dist.TryGetDouble(out var d) ? d : null
                        : null;

                    // Derive system ID from the system name embedded in the body name (best effort)
                    var systemId = DeriveSystemIdFromBodyName(bodyNameValue);

                    var body = new CelestialBody(
                        new CelestialBodyId(Guid.NewGuid()),
                        systemId,
                        bodyNameValue,
                        kind,
                        starClass,
                        planetClass,
                        isTerraformable,
                        distanceLs);
                    await celestialBodyRepository.SaveAsync(body, cancellationToken);
                }
            }

            return;
        }

        if (eventType == "CodexEntry" && payload.TryGetValue("Name_Localised", out var codexName) && codexEntryRepository is not null)
        {
            var nameValue = codexName.GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nameValue))
            {
                return;
            }

            var alreadyExists = await codexEntryRepository.ExistsByNameAsync(nameValue, cancellationToken);
            if (!alreadyExists)
            {
                var categoryValue = payload.TryGetValue("Category_Localised", out var cat)
                    ? ParseCodexCategory(cat.GetString())
                    : CodexCategory.Other;

                var entry = new CodexEntry(
                    new CodexEntryId(Guid.NewGuid()),
                    nameValue,
                    categoryValue,
                    null,
                    DateTimeOffset.UtcNow);
                await codexEntryRepository.SaveAsync(entry, cancellationToken);
            }

            return;
        }

        if ((eventType == "SAASignalsFound" || eventType == "ApproachBody" || eventType == "Location") &&
            payload.TryGetValue("BodyName", out var bodyName))
        {
            var summary = eventType switch
            {
                "SAASignalsFound" => $"Detected signal near {bodyName.GetString()}",
                "ApproachBody" => $"Approached body {bodyName.GetString()}",
                _ => $"Observed {bodyName.GetString()}"
            };

            var exists = await evidenceRepository.ExistsBySummaryAsync(summary, cancellationToken);
            if (exists)
            {
                return;
            }

            var evidence = new EvidenceRecord(
                new EvidenceId(Guid.NewGuid()),
                EvidenceKind.Observation,
                summary,
                DateTimeOffset.UtcNow);
            await evidenceRepository.SaveAsync(evidence, cancellationToken);
            return;
        }

        if (eventType == "Location" && payload.TryGetValue("StarSystem", out var locationSystem))
        {
            var summary = $"Visited {locationSystem.GetString()}";
            var exists = await evidenceRepository.ExistsBySummaryAsync(summary, cancellationToken);
            if (exists)
            {
                return;
            }

            var evidence = new EvidenceRecord(
                new EvidenceId(Guid.NewGuid()),
                EvidenceKind.Observation,
                summary,
                DateTimeOffset.UtcNow);
            await evidenceRepository.SaveAsync(evidence, cancellationToken);
        }
    }

    private static BodyKind DetermineBodyKind(Dictionary<string, JsonElement> payload)
    {
        if (payload.ContainsKey("StarType"))
        {
            return BodyKind.Star;
        }

        if (payload.TryGetValue("PlanetClass", out _))
        {
            return BodyKind.Planet;
        }

        if (payload.TryGetValue("BodyType", out var bt) && bt.GetString() == "Belt")
        {
            return BodyKind.AsteroidBelt;
        }

        return BodyKind.Unknown;
    }

    /// <summary>
    /// Derives a <see cref="StarSystemId"/> from a body name by stripping the trailing body
    /// designator (e.g. "Sol A" → "Sol"). Falls back to hashing the full body name.
    /// </summary>
    private static StarSystemId DeriveSystemIdFromBodyName(string bodyName)
    {
        // Common pattern: body name ends with " A", " B 1", " 3 a", etc.
        // Strip trailing single-char/digit suffixes to recover the system name.
        var parts = bodyName.Split(' ');
        var systemName = parts.Length > 1 ? string.Join(' ', parts[..^1]) : bodyName;
        return new StarSystemId(Math.Abs(systemName.GetHashCode()));
    }

    private static CodexCategory ParseCodexCategory(string? raw) => raw?.ToLowerInvariant() switch
    {
        "biology" or "biological" => CodexCategory.Biology,
        "geology" or "geological" => CodexCategory.Geology,
        "phenomena" => CodexCategory.Phenomena,
        "astronomy" or "astronomical" => CodexCategory.Astronomy,
        _ => CodexCategory.Other
    };
}
