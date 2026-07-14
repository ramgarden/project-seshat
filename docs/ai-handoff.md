# AI Handoff — Project Seshat

This document is the operational starting point for an AI coding agent continuing Project Seshat. Read it with [architecture.md](architecture.md) and [roadmap.md](roadmap.md) before making changes.

## Purpose and current state

Project Seshat is an open-source Galactic Research Platform for *Elite Dangerous*. It is a .NET 9 desktop application using Avalonia and MVVM.

Milestone 0.2 delivered a visible dashboard. Milestone 0.3 currently provides the shared domain model and storage contracts. The desktop dashboard intentionally displays only zero-valued placeholder statistics; no database has been implemented or wired into the application yet.

The next planned product work is Elite Dangerous journal ingestion and persistence, followed by navigation and feature composition in the desktop app.

## Quick start

Run all commands from the repository root with the .NET 9 SDK installed:

```powershell
dotnet restore ProjectSeshat.sln
dotnet build ProjectSeshat.sln
dotnet test ProjectSeshat.sln
dotnet run --project src/ProjectSeshat.App
```

Before changing anything, run `git status --short`: work may be intentionally uncommitted, so preserve unrelated user changes.

## Solution map

| Project | Responsibility | Current state |
| --- | --- | --- |
| `ProjectSeshat.App` | Avalonia desktop UI and presentation composition | Dashboard is implemented; no navigation or database wiring. |
| `ProjectSeshat.Core` | Stable domain model and contracts | Systems, commanders, evidence, and repository abstractions are implemented. |
| `ProjectSeshat.Data` | Persistence boundary | Placeholder only; SQLite/EF Core is the next planned implementation. |
| `ProjectSeshat.Journals` | Elite Dangerous journal ingestion | Placeholder only. |
| `ProjectSeshat.Atlas` | Spatial and astronomical research | Placeholder only. |
| `ProjectSeshat.ThreadEngine` | Research-thread workflows | Placeholder only. |
| `ProjectSeshat.Codex` | Discovery and codex knowledge | Placeholder only. |
| `ProjectSeshat.Observatory` | Observation analysis | Placeholder only. |
| `ProjectSeshat.Investigations` | Evidence-based investigations | Placeholder only. |
| `ProjectSeshat.Tests` | Unit and integration tests | Tests Core records and the SQLite repository round trip. |

## Architectural rules

- Keep `ProjectSeshat.Core` free of Avalonia, EF Core, SQLite, and file-system dependencies.
- Put domain records, IDs, enums, and contracts in Core.
- Put EF entities, `DbContext`, and repository implementations in Data. Do not expose EF entities outside Data.
- The App project owns Avalonia views, view models, presentation composition, and later dependency injection setup. Do not place domain rules in view models or code-behind.
- Feature projects depend on Core. Introduce cross-feature collaboration through Core contracts rather than direct feature-project references unless the architecture is deliberately revised.
- Keep additions narrow. Database migrations, journal parsing, navigation, and application startup composition are separate pending work items.

## Implemented domain model

The Core API is located under `src/ProjectSeshat.Core`.

- `Domain/Identifiers.cs`
  - `StarSystemId(long Value)`
  - `CommanderId(Guid Value)`
  - `EvidenceId(Guid Value)`
- `Domain/ResearchRecords.cs`
  - `StarSystem(StarSystemId Id, string Name)`
  - `Commander(CommanderId Id, string Name)`
  - `EvidenceRecord(EvidenceId Id, EvidenceKind Kind, string Summary, DateTimeOffset RecordedAt)`
  - `EvidenceKind`: `Observation`, `Discovery`, `Investigation`
- `Contracts/`
  - `IStarSystemRepository`
  - `ICommanderRepository`
  - `IEvidenceRepository`

Each repository contract currently defines `FindByIdAsync` and `SaveAsync`, accepts a `CancellationToken`, and uses `ValueTask`. Any persistence implementation must follow these public contracts.

## Data layer status

`ProjectSeshat.Data` is intentionally a placeholder. It currently has no database provider, EF Core context, migrations, repositories, or production connection-string configuration.

The next Data milestone should add `Microsoft.EntityFrameworkCore.Sqlite` through `Directory.Packages.props`, implement the three Core repository contracts, and define a deliberate database-file location and context lifetime in the App composition root. Do not introduce a database dependency into Core.

## Desktop application

The first-light UI lives in `src/ProjectSeshat.App`.

- `App.axaml` defines application resources and the Fluent theme. It must remain present because `App.Initialize()` loads it.
- `MainWindow.axaml` is the dark sci-fi dashboard.
- `MainWindow.axaml.cs` only loads XAML and assigns `MainWindowViewModel` as the data context.
- `ViewModels/MainWindowViewModel.cs` supplies all visible text and statistic values through bindings.

Preserve these UI conventions:

- Displayed labels and values belong in the view model; do not hardcode user-facing text in `MainWindow.axaml`.
- Keep code-behind free of application logic.
- Retain the title `Project Seshat - Galactic Research Platform` unless product direction changes it.
- The current dashboard’s zeros are intentional placeholders until a defined application service and database composition path exist.

## Tests

Tests are in `tests/ProjectSeshat.Tests`.

- `ArchitectureTests.cs` confirms Core is available.
- `Domain/ResearchRecordTests.cs` verifies domain records retain their data.

When the Data layer is implemented, add repository integration tests. Prefer an in-memory SQLite connection over EF Core’s non-relational in-memory provider because it exercises SQLite behavior.

## Dependencies and project conventions

- Target framework: `net9.0`
- Nullable reference types and implicit usings: enabled in `Directory.Build.props`
- Package versions: managed centrally in `Directory.Packages.props`
- UI: Avalonia `11.2.3`
- Testing: xUnit

Do not add a package version directly to a `.csproj`; add it to `Directory.Packages.props` and reference the package without a version in the consuming project.

## Recommended next work

Follow the roadmap order unless the user explicitly reprioritizes:

1. Implement SQLite and Entity Framework Core in `ProjectSeshat.Data` behind the existing Core repository contracts.
2. Define the App composition root, database location, and context lifetime without putting data access in view models.
3. Design a narrow journal-ingestion contract in `ProjectSeshat.Journals`; do not parse files directly from the UI.
4. Define only the journal event data needed for the first persistence slice, then extend Core contracts if necessary.
5. Introduce navigation when there is a concrete second workflow to navigate to.

Before implementing any of these, confirm the requested scope. Avoid adding Atlas, Codex, Observatory, ThreadEngine, or Investigations functionality speculatively.

## Documentation maintenance

Keep these documents current when changing the architecture or milestone state:

- [README.md](../README.md) — public project overview and commands.
- [architecture.md](architecture.md) — dependency direction and responsibilities.
- [roadmap.md](roadmap.md) — completed and upcoming milestones.
- This handoff document — operational details that help an agent resume safely.

The README correctly describes SQLite/EF Core as future work. Update it when the Data milestone is implemented.
