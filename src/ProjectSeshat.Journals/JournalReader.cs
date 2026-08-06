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
        ICodexEntryRepository? codexEntryRepository = null,
        string? contentFingerprint = null,
        INavigationStateRepository? navigationRepository = null)
    {
        if (importTrackerRepository is not null)
        {
            var alreadyImportedContent = !string.IsNullOrWhiteSpace(contentFingerprint)
                && await importTrackerRepository.HasImportedByFingerprintAsync(contentFingerprint, cancellationToken);

            var alreadyImportedPath = string.IsNullOrWhiteSpace(contentFingerprint)
                && !string.IsNullOrWhiteSpace(filePath)
                && await importTrackerRepository.HasImportedAsync(filePath, cancellationToken);

            if (alreadyImportedContent || alreadyImportedPath)
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
                navigationRepository,
                cancellationToken);
        }

        if (importTrackerRepository is not null)
        {
            var fingerprint = string.IsNullOrWhiteSpace(contentFingerprint)
                ? ComputeFingerprint(fingerprintBuilder.ToString())
                : contentFingerprint;

            var alreadyImported = !string.IsNullOrWhiteSpace(fingerprint)
                && await importTrackerRepository.HasImportedByFingerprintAsync(fingerprint, cancellationToken);
            if (!alreadyImported)
            {
                await importTrackerRepository.MarkImportedAsync(filePath ?? "<content>", fingerprint ?? string.Empty, cancellationToken);
            }
        }
    }

    public static string ComputeFingerprint(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return string.Empty;
        }

        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
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
        INavigationStateRepository? navigationRepository,
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
                await UpdateNavigationAsync(navigationRepository, starSystemRepository, systemNameValue, cancellationToken);
                return;
            }

            var systemId = new StarSystemId(Math.Abs(systemNameValue.GetHashCode()));
            var position = payload.TryGetValue("StarPos", out var starPos) && starPos.ValueKind == JsonValueKind.Array && starPos.GetArrayLength() >= 3
                ? new GalacticCoordinates(starPos[0].GetDouble(), starPos[1].GetDouble(), starPos[2].GetDouble())
                : null;
            var system = new StarSystem(systemId, systemNameValue, position);
            await starSystemRepository.SaveAsync(system, cancellationToken);

            await UpdateNavigationAsync(navigationRepository, starSystemRepository, systemNameValue, cancellationToken);
            return;
        }

        if ((eventType == "FSSDiscoveryScan" || eventType == "DiscoveryScan") &&
            payload.TryGetValue("StarSystem", out var honkSystem))
        {
            var systemNameValue = honkSystem.GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(systemNameValue))
            {
                return;
            }

            var existing = await starSystemRepository.FindByNameAsync(systemNameValue, cancellationToken);
            if (existing is null)
            {
                return;
            }

            var nonBodySignals = 0;
            if (payload.TryGetValue("NonBodyCount", out var nonBodyCount) && nonBodyCount.ValueKind == JsonValueKind.Number)
            {
                nonBodySignals = nonBodyCount.GetInt32();
            }

            await starSystemRepository.SaveAsync(existing with
            {
                SurveyState = SystemSurveyState.Honked,
                NonBodySignals = nonBodySignals
            }, cancellationToken);
            return;
        }

        if (eventType == "FSSSignalsFound" && payload.TryGetValue("SystemName", out var signalSystemElement))
        {
            var signalSystemName = signalSystemElement.GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(signalSystemName))
            {
                return;
            }

            var existing = await starSystemRepository.FindByNameAsync(signalSystemName, cancellationToken);
            if (existing is null)
            {
                return;
            }

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (payload.TryGetValue("Signals", out var signals) && signals.ValueKind == JsonValueKind.Array)
            {
                foreach (var signal in signals.EnumerateArray())
                {
                    if (signal.TryGetProperty("Type_Localised", out var localised) &&
                        localised.GetString() is { } localisedName &&
                        !string.IsNullOrWhiteSpace(localisedName))
                    {
                        names.Add(localisedName);
                    }
                    else if (signal.TryGetProperty("Type", out var typeProp) &&
                             typeProp.GetString() is { } rawType &&
                             DeriveSignalTypeName(rawType) is { } derived)
                    {
                        names.Add(derived);
                    }
                }
            }

            if (existing.SignalTypes is not null)
            {
                foreach (var existingType in existing.SignalTypes.Split(','))
                {
                    names.Add(existingType.Trim());
                }
            }

            if (names.Count > 0)
            {
                await starSystemRepository.SaveAsync(
                    existing with { SignalTypes = string.Join(',', names) },
                    cancellationToken);
            }

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
                var scanType = payload.TryGetValue("ScanType", out var st) ? st.GetString() : null;

                var bodyExists = await celestialBodyRepository.ExistsByNameAsync(bodyNameValue, cancellationToken);
                if (!bodyExists)
                {
                    var kind = DetermineBodyKind(payload);
                    var starClass = payload.TryGetValue("StarType", out var starType) ? starType.GetString() : null;
                    var planetClass = payload.TryGetValue("PlanetClass", out var planetClassElem) ? planetClassElem.GetString() : null;
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
                        distanceLs,
                        DeriveScanStatus(scanType),
                        IsWorthDss(planetClass, isTerraformable));
                    await celestialBodyRepository.SaveAsync(body, cancellationToken);

                    await MarkSystemFssScannedAsync(starSystemRepository, bodyNameValue, cancellationToken);
                }
                else if (DeriveScanStatus(scanType) == ScanStatus.Mapped)
                {
                    // A Detailed Surface Scanner pass on an already-catalogued body upgrades it to Mapped.
                    var existingBody = await celestialBodyRepository.FindByNameAsync(bodyNameValue, cancellationToken);
                    if (existingBody is not null && existingBody.ScanStatus != ScanStatus.Mapped)
                    {
                        await celestialBodyRepository.UpdateScanStatusAsync(existingBody.Id, ScanStatus.Mapped, cancellationToken);
                    }
                }
            }

            return;
        }

        if (eventType == "SAAScanComplete" && payload.TryGetValue("BodyName", out var mappedBodyName))
        {
            var mappedName = mappedBodyName.GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(mappedName) || celestialBodyRepository is null)
            {
                return;
            }

            // A Detailed Surface Scanner pass completed: the body is now fully mapped.
            var existingBody = await celestialBodyRepository.FindByNameAsync(mappedName, cancellationToken);
            if (existingBody is not null && existingBody.ScanStatus != ScanStatus.Mapped)
            {
                await celestialBodyRepository.UpdateScanStatusAsync(existingBody.Id, ScanStatus.Mapped, cancellationToken);
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
            var locationSystemName = locationSystem.GetString() ?? string.Empty;
            var summary = $"Visited {locationSystemName}";
            var exists = await evidenceRepository.ExistsBySummaryAsync(summary, cancellationToken);
            if (!exists)
            {
                var evidence = new EvidenceRecord(
                    new EvidenceId(Guid.NewGuid()),
                    EvidenceKind.Observation,
                    summary,
                    DateTimeOffset.UtcNow);
                await evidenceRepository.SaveAsync(evidence, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(locationSystemName))
            {
                await UpdateNavigationAsync(navigationRepository, starSystemRepository, locationSystemName, cancellationToken);
            }
        }
    }

    private static async Task UpdateNavigationAsync(
        INavigationStateRepository? navigationRepository,
        IStarSystemRepository starSystemRepository,
        string systemName,
        CancellationToken cancellationToken)
    {
        if (navigationRepository is null || string.IsNullOrWhiteSpace(systemName))
        {
            return;
        }

        var system = await starSystemRepository.FindByNameAsync(systemName, cancellationToken);
        if (system is null)
        {
            return;
        }

        var state = await navigationRepository.GetAsync(cancellationToken);
        var updated = state is null
            ? new NavigationState(new NavigationStateId(Guid.NewGuid()), system.Id, DateTimeOffset.UtcNow)
            : state with { CurrentSystemId = system.Id, LastUpdatedAt = DateTimeOffset.UtcNow };

        await navigationRepository.SaveAsync(updated, cancellationToken);
    }

    /// <summary>
    /// Maps the journal <c>ScanType</c> field to a scan depth. A Detailed Surface Scanner
    /// pass (<c>Detailed</c>) fully maps the body; FSS/auto scans resolve it; a basic
    /// discovery scan only detects it.
    /// </summary>
    /// <summary>
    /// Converts a journal signal <c>Type</c> code like <c>$SAA_SignalType_Biological;</c> into a
    /// friendly name (e.g. "Biological"), or returns null when it cannot be derived.
    /// </summary>
    private static string? DeriveSignalTypeName(string rawType)
    {
        if (string.IsNullOrWhiteSpace(rawType))
        {
            return null;
        }

        var marker = "SignalType_";
        var index = rawType.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        var name = rawType[(index + marker.Length)..].TrimEnd(';').Trim();
        if (string.IsNullOrWhiteSpace(name) || name.StartsWith("$SAA_", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return name;
    }

    private static ScanStatus DeriveScanStatus(string? scanType) => scanType switch
    {
        "Detailed" => ScanStatus.Mapped,
        "FSS" or "AutoScan" => ScanStatus.FssScanned,
        _ => ScanStatus.Discovered
    };

    private static async Task MarkSystemFssScannedAsync(
        IStarSystemRepository starSystemRepository,
        string bodyName,
        CancellationToken cancellationToken)
    {
        var systemName = DeriveSystemNameFromBodyName(bodyName);
        var system = await starSystemRepository.FindByNameAsync(systemName, cancellationToken);
        if (system is not null && system.SurveyState != SystemSurveyState.FssScanned)
        {
            await starSystemRepository.SaveAsync(system with { SurveyState = SystemSurveyState.FssScanned }, cancellationToken);
        }
    }

    private static string DeriveSystemNameFromBodyName(string bodyName)
    {
        var parts = bodyName.Split(' ');
        return parts.Length > 1 ? string.Join(' ', parts[..^1]) : bodyName;
    }

    /// <summary>Bodies that are conventionally worthwhile to Surface-map with DSS.</summary>
    private static bool IsWorthDss(string? planetClass, bool? terraformable)
    {
        if (terraformable == true)
        {
            return true;
        }

        return planetClass is "Earthlike body" or "Water world" or "Ammonia world" or "High metal content body";
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
