# AI Handoff — Project Seshat

This document is the operational starting point for an AI coding agent continuing Project Seshat. Read it with [architecture.md](architecture.md) and [roadmap.md](roadmap.md) before making changes.

## Purpose and current state

Project Seshat is an open-source Galactic Research Platform for *Elite Dangerous*. It is a .NET 9 desktop application using Avalonia and MVVM, backed by an EF Core + SQLite database.

Milestones 0.2–0.9 built up the production features: a visible dashboard, the shared Core domain model, SQLite/EF Core persistence, journal ingestion, sidebar navigation, atlas/codex/observation data, research-thread workflows, and evidence capture/investigations. Milestone 1.0 added the Atlas undiscovered-region survey (galactic coordinates + region ranking), and Milestone 1.1 delivered **guided search**.

The current product focus is **guided search**, embodied in the **Atlas Survey** page, which is now driven by a **live journal auto-watch**. It walks the user through the Elite Dangerous scan pipeline so they always know where to search next, and stays current automatically as the game writes journal events:

1. **HONK** — systems you've reached but haven't discovery-scanned yet, ordered closest-first.
2. **FSS** — systems the honk flagged with non-body signals, ordered by signal count (most interesting first), to resolve with the Full Spectrum Scanner.
3. **DSS** — specific bodies that turned out worth Surface-mapping (terraformable or notable planet class), ordered by distance from the arrival point.

### Survey pipeline model

Each star system tracks how far its survey has progressed (`SystemSurveyState` in `ResearchRecords.cs`):

- `Unexplored` — arrived but not yet honked; needs a honk.
- `Honked` — discovery-scan/census done and the non-body signal count is known; may need FSS.
- `FssScanned` — FSS resolved the bodies and signals.

Bodies carry a `ScanStatus` (`Discovered → FssScanned → Mapped`) and a `WorthDss` flag; the DSS list is driven by bodies flagged worth mapping.

The guide is driven by journal imports: coordinates come from `StarPos` on `FSDJump`, the system honk and signal count from `FSSDiscoveryScan`/`DiscoveryScan`, and body details from `Scan` events. `AtlasService.BuildSearchGuideAsync` composes the tiers; each tier's targets are exposed through `IStarSystemRepository` (`ListBySurveyStateAsync`) and `ICelestialBodyRepository` (`ListDssCandidatesAsync`).

### Jump-plotting

The guide is anchored to the commander's real position. `JournalReader` records the latest `FSDJump`/`Location` into a persisted `NavigationState` (single row, `INavigationStateRepository`; EF table `NavigationStates`, added via the `AddNavigationState` migration). `AtlasService.BuildSearchGuideAsync` uses that current position as the reference:

- The **honk list is the jump plot**: unexplored systems are ordered nearest-first from where the commander is now, so it reads as "jump here next, then here, then here."
- FSS targets are ranked by signal count then distance from the current position; DSS bodies stay ordered by distance from their arrival point.
- When no position is known yet, it falls back to the centroid of all surveyed systems.

The **Search Guide is the default landing page** (top-level `SearchGuideViewModel` / `SearchGuideView`). It surfaces `CurrentPositionText`, the ordered honk route (step-numbered), dedicated `NextJumpTitle` / `NextJumpDetail`, and the guide lists with per-target "why" explanations:

- **FSS "why"** — `JournalReader` parses `FSSSignalsFound` events and stores the real signal types (`Biological`, `Geological`, etc.) on the system (`StarSystem.SignalTypes`, column added via the `AddSignalTypesToSystems` migration). `FssTarget` carries them and the guide explains which interesting signals are present.
- **DSS "why"** — body value/terraformability drives the reason text (terraformable, Earth-like, water, ammonia worlds).

The **Galaxy Map** is its own top-level tab (`GalaxyMapViewModel` / `GalaxyMapView`).

### Source-of-truth Atlas survey

`AtlasService.RefreshSurveyRegionsAsync` recomputes the frontier regions and persists them as `SurveyRegion` rows (EF table `SurveyRegions`, added via the `AddSurveyRegions` migration) keyed by galactic grid cell, so the survey survives restarts. Regions are flagged `Surveyed` once systems are charted inside their cell. This runs automatically on journal import and on the Atlas Survey page. The **Atlas Survey** page (`SurveyViewModel` / `SurveyView`) lists the regions with rank, distance, score, nearby charted systems, and status; the **Galaxy Map** draws its region markers from this persisted source of truth (`ISurveyRegionRepository.ListUnsurveyedAsync`) instead of recomputing live.

### Community data (EDDN / Spansh)

`ProjectSeshat.Community` ingests crowdsourced data:
- `EddnMessageParser` normalizes EDDN journal-schema JSON (honoring the `message` envelope) into `EddnEvent` records (FSDJump position/honk, Scan body details).
- `IEddnTransport` abstracts the stream; `NetMqEddnTransport` subscribes to the EDDN ZeroMQ relay (`tcp://eddn.edcd.io:9500`) via NetMQ; `EddnListener` parses each frame and raises `EventReceived`.
- `CommunityService` coordinates start/stop and exposes `IsConnected` / `ReceivedCount`; the Dashboard has an EDDN toggle + live count.
- `SpanshRouteService` plots jump routes against the Spansh public HTTP API into `RouteStop` legs.
- Discoveries are summarized (deduped by system name, bounded/pruned) into the `CommunityDiscoveries` EF table via `ICommunityDiscoveryRepository`.

The **EDDN transport is crash-hardened**: `NetMqEddnTransport` reads the full multipart message (an empty subscription-topic frame plus the JSON payload), catches connect/read failures, and raises `IEddnTransport.TransportError` instead of an exception escaping on the poller thread. It also attaches a NetMQ monitor socket and raises `IEddnTransport.ConnectionChanged`, so `CommunityService.IsConnected` reflects **real** relay connectivity rather than "start requested" — the dashboard toggle shows actual state and transport failures as red text. Failures flow up through `EddnListener.TransportErrorReceived` → `CommunityService.LastError`, which the Dashboard surfaces in red — so a live-stream/network failure can never crash the app. `EddnMessageParser.Parse` tolerates malformed JSON (returns null) rather than throwing.

Community discoveries are persisted as a **bounded, deduplicated summary** — not raw EDDN. `CommunityService` buffers system sightings in memory and flushes them in batches to `ICommunityDiscoveryRepository` (EF `CommunityDiscoveries` table, keyed by a unique `SystemName`, `AddCommunityDiscoveries` migration) on a 10-second timer and on stop; then it prunes to 250,000 rows and 180 days of age, so the local SQLite database stays constant even at full relay volume. The dashboard shows the summary (`CommunityDiscoveryCount`) and the most recently reported systems (`CommunityRecentDiscoveries`, a wrap of name chips). Persisting raw EDDN is deliberately avoided.

A file logger (`SeshatLog`, `src/ProjectSeshat.App/SeshatLog.cs`) writes timestamped lines plus every unhandled/unobserved exception to `%APPDATA%\ProjectSeshat\logs\seshat.log`; it installs AppDomain + TaskScheduler crash handlers on startup. The EDDN toggle button uses the standard action-button palette with explicit `/template/ ContentPresenter` hover/pressed overrides so the Fluent theme's default layer can't wash it out.


The tests exercise the parser, the listener flow, and Spansh parsing with injected fakes, so the unit-test suite runs offline. Live-stream connectivity and persisting EDDN discoveries into the survey/systems store are the planned follow-ups (network-only, not exercisable in this sandbox).

### Galactic sky-map

`ProjectSeshat.App/Controls/AtlasSkyMapControl.cs` is a custom, interactive 3D projection control rendered with Avalonia's `DrawingContext`. It draws surveyed systems (cyan), ranked undiscovered regions (green, from `AtlasService.RankUndiscoveredRegionsAsync`), the commander's current position (gold), and the next jump target (red, a line back to center). Drag rotates the view, scroll zooms, and points are drawn far-to-near for a depth cue. `GalaxyMapViewModel.Refresh` builds `SkyMapPoints` (a `SkyPoint`/`SkyPointKind` collection) and the control's `Points` binding re-renders automatically; the map refreshes on live journal import.

## Quick start

Run all commands from the repository root with the .NET 9 SDK installed:

```powershell
dotnet build ProjectSeshat.sln
dotnet test ProjectSeshat.sln
dotnet run --project src/ProjectSeshat.App
```

Before changing anything, run `git status --short`: work may be intentionally uncommitted, so preserve unrelated user changes.

## Solution map

| Project | Responsibility | Current state |
| --- | --- | --- |
| `ProjectSeshat.App` | Avalonia desktop UI, view models, and composition | Dashboard, Exploration, Research Threads, and Atlas Survey views in a left-sidebar navigation shell. |
| `ProjectSeshat.Core` | Stable domain model and contracts | Systems, bodies, evidence, threads, observations, codex, survey state, and repository abstractions are implemented. |
| `ProjectSeshat.Data` | Persistence boundary (SQLite/EF Core) | DbContext, repositories, and EF migrations implemented. Database lives in user AppData. |
| `ProjectSeshat.Journals` | Elite Dangerous journal ingestion | Imports commander identity, jumps (+ StarPos), scans, and the system honk; deduped by content fingerprint. `JournalWatcher` live-tails the journal directory. |
| `ProjectSeshat.Atlas` | Spatial and astronomical research | Coordinates survey, region ranking, and the guided honk/FSS/DSS search guide (`AtlasService`). |
| `ProjectSeshat.ThreadEngine` | Research-thread workflows | `ResearchThreadEngine` implemented. |
| `ProjectSeshat.Codex` | Discovery and codex knowledge | Codex entries modeled and persisted. |
| `ProjectSeshat.Observatory` | Observation analysis | Observations modeled and persisted. |
| `ProjectSeshat.Investigations` | Evidence-based investigations | `InvestigationService` captures evidence attached to research threads. |
| `ProjectSeshat.Community` | Crowdsourced galactic data | EDDN message parser + livestream listener (NetMQ), Spansh route service, and a start/stop `CommunityService`. |
| `ProjectSeshat.Tests` | Unit and integration tests | Core records, SQLite repository round trips, journal reader, Atlas guide/map/survey, and community parsing. |

## Architectural rules

- Keep `ProjectSeshat.Core` free of Avalonia, EF Core, SQLite, and file-system dependencies.
- Put domain records, IDs, enums, and contracts in Core.
- Put EF entities, `DbContext`, and repository implementations in Data. Do not expose EF entities outside Data.
- The App project owns Avalonia views, view models, presentation composition, and startup composition. Do not place domain rules in view models or code-behind.
- Feature projects depend only on Core. Introduce cross-feature collaboration through Core contracts rather than direct feature-project references.
- Schema changes go through EF Core migrations (see below), never `EnsureCreated`.

## Implemented domain model

The Core API is located under `src/ProjectSeshat.Core/Domain` and `src/ProjectSeshat.Core/Contracts`:

- `Identifiers.cs` — `StarSystemId(long)`, `CommanderId`, `EvidenceId`, `CelestialBodyId`, `CodexEntryId`, `ObservationGuid`, `ResearchThreadId`.
- `ResearchRecords.cs` — `StarSystem` (with `Position`, `SurveyState`, `NonBodySignals`), `Commander`, `JournalImportKey`, `EvidenceRecord`/`EvidenceKind`, plus Atlas records `GalacticCoordinates`, `BodyKind`, `ScanStatus`, `CelestialBody`, and codex/observatory records.
- `Threads.cs` — research thread records.
- `NavigationState.cs` — the commander's current system (for jump-plotting).
- `SurveyRegion.cs` — persisted frontier/uncharted regions keyed by grid cell (the source-of-truth survey).
- `JournalImportTracker.cs` — import de-duplication by content fingerprint.
- `Contracts/` — `IStarSystemRepository`, `ICommanderRepository`, `IEvidenceRepository`, `ICelestialBodyRepository`, `ICodexEntryRepository`, `IObservationRepository`, `IResearchThreadRepository`, `IJournalImportTrackerRepository`, `INavigationStateRepository`, `ISurveyRegionRepository`, `ICommunityDiscoveryRepository`.

Repository contracts accept a `CancellationToken`; persistence implementations must follow these public contracts.

## Atlas / guided search

`src/ProjectSeshat.Atlas/AtlasService.cs` exposes:

- `GetBodiesForSystemAsync` — catalogued bodies ordered by distance from arrival.
- `GetSurveySnapshotAsync` — a spatial snapshot (reference coordinates, surveyed systems, surveyed cells).
- `RankUndiscoveredRegionsAsync` — ranks largely uncharted cells near the surveyed frontier (Milestone 1.0).
- `BuildSearchGuideAsync` — builds the `SearchGuide` (`NeedHonk`, `NeedFss`, `NeedDss`) used by the Search Guide page (Milestone 1.1).

Presentation lives in `src/ProjectSeshat.App/ViewModels/SearchGuideViewModel.cs` (guide lists + "why" text) and `Views/SearchGuideView.axaml`, with the galaxy map in `GalaxyMapViewModel.cs` / `GalaxyMapView.axaml`. `MainWindowViewModel` wires both pages into navigation (Search Guide is the landing page) and refreshes them whenever journal data is imported.

## Live journal auto-watch

`ProjectSeshat.Journals/JournalWatcher.cs` makes journal ingestion fully automatic — there is no manual import button and no rescan button:

- Uses a `FileSystemWatcher` on the resolved journal directory (debounced ~500 ms) so new events are picked up as you play.
- On first touch of each file it imports it **in full**, using the `JournalImportTrackerRepository` to skip content that was already loaded (SHA-256 fingerprint and file path) and to record what has been ingested — so re-runs and app restarts never double-import.
- Thereafter it **tail-reads** each journal file by byte offset, importing only appended lines (`ScanFileAsync`/`ScanDirectoryAsync`, used directly by tests), keeping re-scans cheap and idempotent.
- After each pass with new lines it raises `Imported`; `MainWindowViewModel` refreshes the dashboard stats/status, exploration list, and Atlas Survey guide.
- Started automatically on app launch (`App.CreateMainWindow` → `MainWindowViewModel.StartJournalWatcher`).

Because every import is idempotent at the domain level (existence checks on systems, bodies, evidence, codex) and guarded by the import tracker, automatic scanning is safe. The dashboard reflects live activity via `DashboardViewModel.ReportLiveActivity`.

## Desktop application

The UI lives in `src/ProjectSeshat.App`.

- `App.axaml` defines application resources and the Fluent theme; it must remain present.
- `App.cs` composes the service graph and resolves the database path under the user's AppData; it now applies EF migrations instead of `EnsureCreated`.
- `MainWindow.axaml` is the dark sci-fi dashboard with the left sidebar.
- `ViewModels/` supply all visible text and values through bindings.

Preserve these UI conventions:

- Displayed labels and values belong in the view model; do not hardcode user-facing text in `MainWindow.axaml`.
- Keep code-behind free of application logic.
- Retain the title `Project Seshat - Galactic Research Platform` unless product direction changes it.

## Data layer and migrations

`ProjectSeshat.Data` implements the repositories over SQLite/EF Core. The DbContext is `ProjectSeshatDbContext`; migrations live in `src/ProjectSeshat.Data/Migrations`.

The application now uses `context.Database.Migrate()` on startup so schema changes apply without regenerating the database or re-importing journals. To add a migration after a schema change:

```powershell
dotnet ef migrations add <Name> --project src/ProjectSeshat.Data
```

A design-time factory (`ProjectSeshatDbContextFactory`) lets the EF tools build the context outside the running app.

## Tests

Tests are in `tests/ProjectSeshat.Tests` and currently pass (63 tests). They cover architecture constraints, domain records, SQLite repository round trips (in-memory SQLite), journal reader import/dedup, and the Atlas search guide tiers. Prefer in-memory SQLite over EF Core's non-relational in-memory provider because it exercises SQLite behavior.

## Dependencies and project conventions

- Target framework: `net9.0`
- Nullable reference types and implicit usings: enabled in `Directory.Build.props`
- Package versions: managed centrally in `Directory.Packages.props`
- UI: Avalonia `11.2.3`; Data: EF Core / SQLite `9.0.8` (includes `Microsoft.EntityFrameworkCore.Design` for migrations)
- Testing: xUnit

Do not add a package version directly to a `.csproj`; add it to `Directory.Packages.props` and reference the package without a version in the consuming project.

## Recommended next work

Follow the `Next` / next-milestone sections in [roadmap.md](roadmap.md). The current candidate milestone is **Milestone 1.9 — On-screen guidance overlay & one-key jump**: an always-on-top click-through overlay + optional voice pings that show the next guided step (jump → honk → FSS → DSS) over the game window, plus a global hotkey that targets and triggers the jump to the next nearest honk target by reading the player's ED key bindings. Other next steps:

- Add a source-of-truth Atlas Survey listing and richer FSS/DSS detail/filtering.
- Expand unit/integration test coverage and add CI/formatting (Quality section).

## Documentation maintenance

Keep these documents current when changing the architecture or milestone state:

- [README.md](../README.md) — public project overview and commands.
- [architecture.md](architecture.md) — dependency direction and responsibilities.
- [roadmap.md](roadmap.md) — completed and upcoming milestones.
- This handoff document — operational details that help an agent resume safely.

Update this handoff and `roadmap.md` whenever you move a milestone so the next agent can resume instantly.
