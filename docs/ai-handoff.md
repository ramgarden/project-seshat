# AI Handoff — Project Seshat

This document is the operational starting point for an AI coding agent continuing Project Seshat. Read it with [architecture.md](architecture.md) and [roadmap.md](roadmap.md) before making changes.

## Purpose and current state

Project Seshat is an open-source Galactic Research Platform for *Elite Dangerous*. It is a .NET 9 desktop application using Avalonia and MVVM, backed by an EF Core + SQLite database.

Milestones 0.2–0.9 built up the production features: a visible dashboard, the shared Core domain model, SQLite/EF Core persistence, journal ingestion, sidebar navigation, atlas/codex/observation data, research-thread workflows, and evidence capture/investigations. Milestone 1.0 added the Atlas undiscovered-region survey (galactic coordinates + region ranking), and Milestone 1.1 delivered **guided search**.

The current product focus is **guided search**, embodied in the **Atlas Survey** page. It walks the user through the Elite Dangerous scan pipeline so they always know where to search next:

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
| `ProjectSeshat.Journals` | Elite Dangerous journal ingestion | Imports commander identity, jumps (+ StarPos), scans, and the system honk; deduped by content fingerprint. |
| `ProjectSeshat.Atlas` | Spatial and astronomical research | Coordinates survey, region ranking, and the guided honk/FSS/DSS search guide (`AtlasService`). |
| `ProjectSeshat.ThreadEngine` | Research-thread workflows | `ResearchThreadEngine` implemented. |
| `ProjectSeshat.Codex` | Discovery and codex knowledge | Codex entries modeled and persisted. |
| `ProjectSeshat.Observatory` | Observation analysis | Observations modeled and persisted. |
| `ProjectSeshat.Investigations` | Evidence-based investigations | `InvestigationService` captures evidence attached to research threads. |
| `ProjectSeshat.Tests` | Unit and integration tests | Core records, SQLite repository round trips, journal reader, and Atlas search guide. |

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
- `JournalImportTracker.cs` — import de-duplication by content fingerprint.
- `Contracts/` — `IStarSystemRepository`, `ICommanderRepository`, `IEvidenceRepository`, `ICelestialBodyRepository`, `ICodexEntryRepository`, `IObservationRepository`, `IResearchThreadRepository`, `IJournalImportTrackerRepository`.

Repository contracts accept a `CancellationToken`; persistence implementations must follow these public contracts.

## Atlas / guided search

`src/ProjectSeshat.Atlas/AtlasService.cs` exposes:

- `GetBodiesForSystemAsync` — catalogued bodies ordered by distance from arrival.
- `GetSurveySnapshotAsync` — a spatial snapshot (reference coordinates, surveyed systems, surveyed cells).
- `RankUndiscoveredRegionsAsync` — ranks largely uncharted cells near the surveyed frontier (Milestone 1.0).
- `BuildSearchGuideAsync` — builds the `SearchGuide` (`NeedHonk`, `NeedFss`, `NeedDss`) used by the Atlas Survey page (Milestone 1.1).

Presentation lives in `src/ProjectSeshat.App/ViewModels/AtlasViewModel.cs` (`HonkItems`, `FssItems`, `DssItems`, selection detail) and `Views/AtlasView.axaml`. `MainWindowViewModel` wires the `Atlas` page into navigation and refreshes it whenever journal data is imported.

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

Tests are in `tests/ProjectSeshat.Tests` and currently pass (37 tests). They cover architecture constraints, domain records, SQLite repository round trips (in-memory SQLite), journal reader import/dedup, and the Atlas search guide tiers. Prefer in-memory SQLite over EF Core's non-relational in-memory provider because it exercises SQLite behavior.

## Dependencies and project conventions

- Target framework: `net9.0`
- Nullable reference types and implicit usings: enabled in `Directory.Build.props`
- Package versions: managed centrally in `Directory.Packages.props`
- UI: Avalonia `11.2.3`; Data: EF Core / SQLite `9.0.8` (includes `Microsoft.EntityFrameworkCore.Design` for migrations)
- Testing: xUnit

Do not add a package version directly to a `.csproj`; add it to `Directory.Packages.props` and reference the package without a version in the consuming project.

## Recommended next work

Follow the `Next` section in [roadmap.md](roadmap.md). Current candidate next steps:

- Expand the guided search experience (e.g., jump-plotting between honk targets, richer FSS/DSS details, filtering).
- Expand unit/integration test coverage and add CI/formatting (Quality section).

## Documentation maintenance

Keep these documents current when changing the architecture or milestone state:

- [README.md](../README.md) — public project overview and commands.
- [architecture.md](architecture.md) — dependency direction and responsibilities.
- [roadmap.md](roadmap.md) — completed and upcoming milestones.
- This handoff document — operational details that help an agent resume safely.

Update this handoff and `roadmap.md` whenever you move a milestone so the next agent can resume instantly.
