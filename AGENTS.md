# Project Seshat Agent Instructions

## Start Here

- Read `docs/ai-handoff.md`, `docs/roadmap.md`, and `docs/architecture.md` before changing code.
- Check `git status --short` and preserve unrelated uncommitted work.
- Update `docs/ai-handoff.md` and `docs/roadmap.md` when moving a milestone.

## Commands

Use the .NET 10 SDK from the repository root:

```powershell
dotnet restore
dotnet build ProjectSeshat.sln
dotnet test ProjectSeshat.sln
dotnet run --project src/ProjectSeshat.App
```

Run one test or test class with:

```powershell
dotnet test tests/ProjectSeshat.Tests/ProjectSeshat.Tests.csproj --filter "FullyQualifiedName~<TestName>"
```

For schema changes, add an EF migration instead of changing the database directly:

```powershell
dotnet ef migrations add <Name> --project src/ProjectSeshat.Data
```

There is no configured lint, formatter, typechecker, CI, or lockfile command; build and test are the available verification commands.

## Architecture

- `ProjectSeshat.App` owns Avalonia views, view models, composition, and startup; keep domain rules out of presentation.
- `ProjectSeshat.Core` owns domain records and contracts and has no project dependencies.
- Feature projects depend only on Core; use Core contracts for cross-feature work.
- `ProjectSeshat.Data` owns EF entities, `DbContext`, SQLite repositories, and migrations; do not expose EF entities outside Data.
- `tests/ProjectSeshat.Tests` uses xUnit. Prefer in-memory SQLite for repository tests because it exercises SQLite behavior.

Package versions are managed centrally in `Directory.Packages.props`; never add a version directly to a `.csproj`.

## Runtime and Data

- Production startup is `App.CreateViewModel()` → `ProjectSeshatDbContext.Database.Migrate()`; the database is `%APPDATA%\ProjectSeshat\project-seshat.db`.
- Do not use `EnsureCreated()` for schema changes. The parameterless `MainWindow` constructor is a legacy path that still uses a relative `project-seshat.db` and `EnsureCreated()`; use the production composition path.
- `ProjectSeshatDbContextFactory` also uses a relative `project-seshat.db`; do not assume EF design-time commands target the AppData database.
- The journal watcher starts automatically, watches `Journal*.log`, imports each file once, then tails appended bytes. Imports are deduplicated by SHA-256 fingerprint plus file path.
- The guidance overlay defaults off; its position is saved at `%APPDATA%\ProjectSeshat\overlay-position.json`.

## Tests and Gotchas

- `AppStartupTests` calls production `App.CreateViewModel()` and can create/migrate the user's real AppData database; avoid running it casually on a machine with important local data.
- Live EDDN connectivity, Windows TTS, and real Elite Dangerous key automation require on-device validation; parser and decision logic should be tested with injected fakes.
- `JournalReader` derives system IDs with `GetHashCode()`; do not treat them as stable cross-process identities.
- `SearchGuideViewModel.Refresh()` currently blocks on async work with `.GetAwaiter().GetResult()`; avoid adding more synchronous UI work.

## UI Changes

- The app is dark-themed. Give interactive controls explicit foreground/background and `/template/ ContentPresenter` hover/pressed states; Avalonia's default light hover can make controls disappear.
- `PathIcon` does not inherit `Foreground`; set it explicitly for every icon state.
- Keep user-facing labels in view models and application logic out of code-behind.
- After UI edits, run `dotnet build` and inspect touched `.axaml` for low-contrast colors and labels without explicit foregrounds.

## Current Handoff

- Work is on branch `main` with Milestone 1.14 implemented and committed.
- Canonical action model is `ProjectSeshat.Core.Domain.NextActionKind` and `NextAction`; `CrawlStep` remains a legacy compatibility model.
- Priority is current HONK, then FSS, then DSS, then JUMP/BACK-TRACK. DSS should prefer unmapped eighth-moon/notable bodies.
- Search Guide now has async refresh, `CurrentAction`, `NextActionTitle`, `NextActionReason`, `NextActionDetail`, `HasNextAction`, `NextActionUpdated`, and `CrawlUpdated`.
- `GuidanceFormatter` and `GuidanceOverlayViewModel` use `NextAction`; `GuidanceOverlayWindow` and `KeyAutomationService` retain compatibility with `CrawlStep`.
- `MainWindowViewModel` subscribes to `NextActionUpdated` and converts the action to a legacy `CrawlStep` for overlay, voice, and automation.
- `SearchGuideView.axaml` includes the prominent `NEXT RAXXLA MOVE` card.
- `NavigationStateRepository.SaveAsync` updates existing rows in place to avoid EF tracking and SQLite uniqueness conflicts during repeated journal imports.
- `docs/ai-handoff.md` and `docs/roadmap.md` document the completed milestone and current build/test state.

### Current Build State

- The project targets .NET 10.
- `dotnet build ProjectSeshat.sln --no-restore` succeeds.
- `dotnet test ProjectSeshat.sln --no-build --no-restore` passes all 142 tests.
- Existing dependency warnings include `NU1902`, `NU1903`, and `NU1904` (SQLitePCLRaw, System.Drawing.Common, System.Security.Cryptography.Xml, and Tmds.DBus.Protocol).
- `src/ProjectSeshat.Atlas\AtlasService.cs` has an LF/CRLF line-ending warning.

### Handoff Checklist

1. Preserve unrelated uncommitted work when continuing.
2. Keep journal import and navigation-state updates covered by tests.
3. Validate live journal watching and dashboard counts on-device before relying on the watcher end to end.
4. Run build and test before further milestone changes.
