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
        CancellationToken cancellationToken = default)
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
            await ImportLineAsync(line, starSystemRepository, commanderRepository, evidenceRepository, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line);
        if (payload is null)
        {
            return;
        }

        if (payload.TryGetValue("event", out var eventName))
        {
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

            if ((eventType == "Scan" || eventType == "SAASignalsFound" || eventType == "ApproachBody" || eventType == "Location") && payload.TryGetValue("BodyName", out var bodyName))
            {
                var summary = eventType switch
                {
                    "Scan" => $"Scanned {bodyName.GetString()}",
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
    }
}
