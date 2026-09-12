# AI Handoff — Project Seshat

This document is the operational starting point for an AI coding agent continuing Project Seshat. Read it with [architecture.md](architecture.md) and [roadmap.md](roadmap.md) before making changes.

## Purpose and current state

Project Seshat is an open-source Galactic Research Platform for *Elite Dangerous*. It is a .NET 10 desktop application using Avalonia and MVVM, backed by an EF Core + SQLite database.

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

To keep the app smooth while playing, the EDDN `Changed` notification is **coalesced to ~4/s** (`OnEventReceived` throttles via `_lastChangedTick`, `ChangedIntervalMs = 250`), so a live-relay burst never floods the Avalonia binding layer. Distinct events (Start/Stop/connection change/flush/transport error) still raise `Changed` immediately.

A file logger (`SeshatLog`, `src/ProjectSeshat.App/SeshatLog.cs`) writes timestamped lines plus every unhandled/unobserved exception to `%APPDATA%\ProjectSeshat\logs\seshat.log`; it installs AppDomain + TaskScheduler crash handlers on startup. The EDDN toggle button uses the standard action-button palette with explicit `/template/ ContentPresenter` hover/pressed overrides so the Fluent theme's default layer can't wash it out.


The tests exercise the parser, the listener flow, and Spansh parsing with injected fakes, so the unit-test suite runs offline. Live-stream connectivity and persisting EDDN discoveries into the survey/systems store are the planned follow-ups (network-only, not exercisable in this sandbox). Current suite: 142 tests passing under .NET 10.

### Galactic sky-map

`ProjectSeshat.App/Controls/AtlasSkyMapControl.cs` is a custom, interactive 3D projection control rendered with Avalonia's `DrawingContext`. It draws surveyed systems (cyan), ranked undiscovered regions (green, from `AtlasService.RankUndiscoveredRegionsAsync`), the commander's current position (gold), and the next jump target (red, a line back to center). Drag rotates the view, scroll zooms, and points are drawn far-to-near for a depth cue. `GalaxyMapViewModel.Refresh` builds `SkyMapPoints` (a `SkyPoint`/`SkyPointKind` collection) and the control's `Points` binding re-renders automatically; the map refreshes on live journal import.

## Quick start

Run all commands from the repository root with the .NET 10 SDK installed:

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
- `BuildOutwardCrawlAsync` / `RecommendSearchGateAsync` / `RecommendSearchGatesAsync` — the **systematic outward survey** (Milestone 1.9): an outward crawl from a recommended gate, nearest-unsearched-first, auto-advancing the hop when a system has nothing left to FSS/DSS, and back-tracking through charted stars when the local neighbourhood is exhausted. Returns an `OutwardCrawl` (recommended `SearchGate` + score/reasoning, ordered `CrawlHop` route, and a single `CrawlStep` next move). The Search Guide page shows the gate, the best-nearest gate (`SearchGateSuggestions`), and the next move; both refresh on journal import. Gate scoring favours charted systems with dense unsearched neighbours, frontier proximity, reachability, and low community footprint.
- `FindRaxxlaIntelAsync` — **community Raxxla search intel** (Milestone 1.13): scans known systems/bodies and returns ranked `RaxxlaIntelHit`s. Criteria come from `Core/Domain/RaxxlaSearchIntel.cs` (notable body classes, suspicious signal types, lore-name terms, the Sol 200-ly hunt bubble, and the 8th-moon Dark Wheel clue), distilled from **docs/raxxla-search-criteria.md** (Great Raxxla Potato Hunt playbook + lore wiki, sources cited). These are investigation *priorities*, not claimed locations. The Search Guide renders a "RAXXLA INTEL" panel from these.

The **overlay auto-updates with the next thing to do** on every journal import (and on launch): `SearchGuide.Refresh()` → `CrawlUpdated` → `MainWindowViewModel.OnCrawlUpdated` → overlay + voice + auto-target. The next-step (`BuildNextStep`) is **intel-aware** — suspicious FSS signals and intel-flagged bodies (8th moons / notable classes) are surfaced in the step's reason, and the outward jump plot (`BuildCrawlRoute`) **prefers intel-flagged systems** (lore names, suspicious signals, Sol-bubble systems) over mere nearest, so the search bubble heads at interesting systems first.

Presentation lives in `src/ProjectSeshat.App/ViewModels/SearchGuideViewModel.cs` (guide lists + "why" text) and `Views/SearchGuideView.axaml`, with the galaxy map in `GalaxyMapViewModel.cs` / `GalaxyMapView.axaml`. `MainWindowViewModel` wires both pages into navigation (Search Guide is the landing page) and refreshes them whenever journal data is imported.

## Live journal auto-watch

`ProjectSeshat.Journals/JournalWatcher.cs` makes journal ingestion fully automatic — there is no manual import button and no rescan button:

- Uses a `FileSystemWatcher` on the resolved journal directory (debounced ~500 ms) so new events are picked up as you play.
- On first touch of each file it imports it **in full**, using the `JournalImportTrackerRepository` to skip content that was already loaded (SHA-256 fingerprint and file path) and to record what has been ingested — so re-runs and app restarts never double-import.
- Thereafter it **tail-reads** each journal file by byte offset, importing only appended lines (`ScanFileAsync`/`ScanDirectoryAsync`, used directly by tests), keeping re-scans cheap and idempotent.
- After each pass with new lines it raises `Imported`; `MainWindowViewModel` refreshes the dashboard stats/status, exploration list, and Atlas Survey guide.
- Started automatically on app launch (`App.CreateMainWindow` → `MainWindowViewModel.StartJournalWatcher`).

Because every import is idempotent at the domain level (existence checks on systems, bodies, evidence, codex) and guarded by the import tracker, automatic scanning is safe. The dashboard reflects live activity via `DashboardViewModel.ReportLiveActivity`.

## On-screen guidance overlay & voice (Milestone 1.10)

The **in-game guidance overlay** (`Views/GuidanceOverlayWindow.cs`) is an always-on-top, borderless, click-through caption (Win32 `WS_EX_TRANSPARENT`/`WS_EX_TOOLWINDOW`/`WS_EX_NOACTIVATE` applied via P/Invoke on Windows) positioned at the bottom-centre of the primary display. It renders the live outward-crawl step (`GuidanceOverlayViewModel` bound via `GuidanceOverlayView.axaml`): `JUMP → SOL`, `HONK → HERE`, `FSS → SYS`, `DSS → BODY`, `BACK-TRACK → X`, or `NO NEXT MOVE`.

- Text/TTS copy is derived offline-testably in `GuidanceFormatter`/`GuidanceOverlayViewModel` (pure logic over the 1.9 `CrawlStep`), so overlay text and the voice transcript are fully unit-tested.
- **Voice pings**: `IVoicePinger` + `WindowsSpeechVoicePinger` (System.Speech TTS, Windows-only, fails safe) and `SilentVoicePinger` for tests/non-Windows. The main window has a "Speak next step" button; steps are spoken automatically when the overlay is visible.
- **Overlay toggle** lives in the sidebar footer; visibility is owned by `MainWindowViewModel.OverlayVisible` and the window is shown/hidden in `App.CreateMainWindow` on property change. **Defaults to OFF** — an always-on-top transparent window floating over the game adds compositor/GPU overhead that can make the game laggy, so the user opts in with the "Show overlay" toggle each session.
- **Gate picker**: the Search Guide's recommended-gate card has a "Use as gate" button (`SelectRecommendedGateCommand`) that anchors the outward crawl at that system via `gateSystemId`, plus a "Clear" path through the same button. There's also a **best-nearest gate** card ("Use as gate (nearest)") `SelectNearestGateCommand`. The crawl defaults to starting from the **current system** unless a gate is chosen.
- **Raxxla intel panel**: Search Guide shows a "RAXXLA INTEL" column (`RaxxlaIntelItems`) built by `AtlasService.FindRaxxlaIntelAsync`, using `Core/Domain/RaxxlaSearchIntel.cs` criteria. See `docs/raxxla-search-criteria.md` for the community research and sources.
- **Position persistence is crash-safe**: the overlay saves its screen position to `overlay-position.json` (beside the DB, via `OverlayPositionStore`) **immediately on `WM_MOVE`/`WM_EXITSIZEMOVE` from the Win32 message loop** (`GuidanceOverlayWindow.WinProc` → `SaveFromNativeRect`), then also on Avalonia's `PositionChanged` and on window close. Do NOT rely on Avalonia `Window.PositionChanged` alone for drags — native `HTCAPTION` drags don't reliably raise it, so the Win32 `WM_MOVE` path is the primary saver. This is what keeps the dragged position after a crash.

The window/TTS behaviour itself requires manual on-device validation (can't be exercised in this sandbox); all derivation logic is covered by unit tests. **Milestone 1.11 (keybinding auto-targeting) is also implemented** — see below.

## Keybinding auto-targeting (Milestone 1.11)

`ProjectSeshat.App/Elite/` implements driving ED with the commander's real key bindings:

- `BindingsParser` parses `*.binds` XML into named `BindingEntry` bindings (device, key, modifier); `BindingsParser.FindKeyboard` returns a keyboard binding by action name.
- `BindingsPathResolver` locates the ED bindings directory (mirrors the journal resolver: Saved Games + LocalAppData + Steam userdata).
- `IGameInputSender` abstracts synthetic input; `SendInputGameInputSender` is the Windows `SendInput` implementation with an `IsEliteInForeground` guard; tests inject a fake.
- `KeyAutomationService.TryEnable()` reads the bindings, resolves `SelectTarget` + `HyperSuperCombination`, and arms automation. `AutoTargetNextStar(step)` sends the target key then the jump key only for Jump/Back-track steps, only when armed and the game is focused — otherwise it no-ops and the overlay names the star.
- The main-window sidebar shows an "Enable auto-target" toggle (with an active on-state) and status text (the resolved keys) or the fallback note. Automatically fires on crawl-step changes when enabled.

The parser/resolver/automation decision logic is unit-tested offline with fakes; the real `SendInput` + foreground detection need an on-device smoke test with ED running and a real `*.binds` file.

#### Auto-target in-system work (Milestone 1.12)

The overlay now always reflects exactly what to do next inside the current system, and auto-targeting follows it:

- `GuidanceFormatter` phrases in-system steps so the caption names the action against the target: `HONK <star>`, `INSPECT THE TARGETED SIGNAL`, `DSS TARGETED BODY` (plus `JUMP/TO BACK-TRACK TO <star>`). Detail lines name the target (`signal in <system>` / `body <name>`) then the reason; TTS transcripts match ("Target the signal, then run the FSS…").
- `KeyAutomationService.AutoTargetNextStar` now sends the **target-selection key for FSS and DSS steps too** (not just Jump/Back-track), so a suspicious body/signal is targeted in-game when bindings are armed and ED is focused. Jump/Back-track still target + charge the hyperjump afterwards.

### Keybind setup assistant

`KeybindSetupView`/`KeybindSetupViewModel` (sidebar "Keybind Setup" entry) walks the player through making auto-targeting usable:

- **Detection**: `StartPresetResolver` reads the active preset name from `StartPreset.start` to pick the right `.binds` (falling back to the first `.binds`); `BindingsPathResolver.ResolveBindingsFile`/`ResolveBindingsDirectory` now prefer that and expose the resolved path/dir on `KeyAutomationService`.
- Shows target/jump keys, the resolved binds file path, and a ready/not-ready badge.
- **Test**: `KeyAutomationService.TestJump()` sends one jump keypress; the view reports "Transmitted" vs "Not sent — game not focused" (`CanTest` gates it).
- **Auto-write** (`BindingsWriter`): opt-in via an "I understand…needs restart" checkbox (`AllowAutoWrite`). Writes a sparse `Seshat.Auto.4.0.binds` (only `SelectTarget`=T and `HyperSuperCombination`=J; ED fills the rest from defaults) plus `StartPreset.start`, then re-detects. Best-effort; requires an ED restart and is version-sensitive.
- **Manual guide**: step-by-step ED → Options → Controls instructions, then **Re-detect**.
- Commands: `RedetectCommand`, `TestJumpCommand`, `WriteDefaultBindingsCommand`. Wired via `OpenKeybindSetupCommand` on `MainWindowViewModel` and a `KeybindSetupViewModel` DataTemplate in `App.axaml`.

The write→relaunch→load cycle and the live test press need on-device validation; all detection/writer/view-model logic is unit-tested.

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

### UI color / contrast rules (buttons must never "disappear")

**Rule of thumb: verify colors on every change.** Before finishing any UI work, check that no foreground/text/hover color matches or closely matches the background it sits on (dark `~#08111D` / panels `~#0E1D2E` / inner cards `~#0E1B28` / `~#08111D`). The most common failure is a **label inside a checkbox, toggle, or button** whose text turns invisible on hover because it inherits the theme's light hover foreground — always set the text/element's own `Foreground` explicitly, plus the matching `:pointerover`/`:pressed` ContentPresenter overrides.

The app is a dark sci-fi theme on a dark background (`~#08111D` / panels `#0E1D2E`). Avalonia's default Fluent `:pointerover` overlay is light and can wash a dark button into matching the background, making it invisible on hover/click. Follow these conventions everywhere, including **all buttons, checkboxes, toggles, and any interactive control**:

- **Always give every interactive state an explicit, high-contrast color** — set both the control's `Background`/`Foreground` **and** the matching `/template/ ContentPresenter` override for `:pointerover` and `:pressed`. (See `MainWindow.axaml` `Button.nav-btn`, `Button.game-tool`, and SearchGuide/Threads `Button.action`/`Button.ghost` for the pattern.)
- State color set (industrial standard used across the app):
  - default fill `#1C8BC4` (teal), text `#02101B` / `#03111B`
  - hover `#36C5F0` (bright cyan)
  - pressed `#0F5F82` with white text
- **Active / "on" toggle state** must be distinct, e.g. `Button.game-tool.active` uses green `#2C8F6E` (hover `#3FB98E`, pressed `#1F6E55`). Bind it via `Classes.active="{Binding SomeBool}"`.
- **Transparent "ghost"/nav buttons** are only OK when a hover state that clearly differs from the panel is supplied (e.g. `Button.nav-btn:pointerover` = `#22405A`). Never leave a control with the theme's default light hover over dark.
- **Icons: `PathIcon` does NOT inherit `Foreground`.** Every `PathIcon` in a button (e.g. the sidebar nav/game-tool icons) must get an explicit `Foreground` per state (`Button.nav-btn PathIcon` → `#A8D4EA`, `:pointerover` → `#FFFFFF`, `.active` → `#02101B`, and matching rules for `game-tool`). Without these the icons render default-dark and vanish on the dark sidebar. (See `MainWindow.axaml`.)
- **Buttons with only a `Background` hover setter are not enough** — Avalonia's light hover overlay is painted by the template's `ContentPresenter`; always also add `<selector>:pointerover /template/ ContentPresenter` (and `:pressed`) with the matching background/foreground, exactly as `ThreadsView`/`SearchGuideView`/`KeybindSetupView` do. A bare `Button.action:pointerover { Background=… }` still lets the theme wash the button into the panel.
- **Checkboxes / toggles / radio labels**: give the label `TextBlock` an explicit `Foreground` (e.g. `#E5F5FF`) rather than relying on inheritance; give the `CheckBox` an explicit `Background`/`BorderBrush`/`BorderThickness` so the box is visible, plus `CheckBox:pointerover → Foreground #FFFFFF` and `CheckBox /template/ ContentPresenter` overrides so the caption never washes out. (See `KeybindSetupView.axaml` "I understand this writes a minimal binding file…".)
- When adding any control, grep existing views for the `:pointerover` / `:pressed` / `:active` styles and mirror them; verify nothing uses only the default theme hover.
- **Self-check habit**: after any UI edit, run `dotnet build` and scan the touched `.axaml` for (a) any `Foreground`/`Background` hex close to a panel color with no state override, and (b) any label without an explicit foreground inside an interactive control.

## Data layer and migrations

`ProjectSeshat.Data` implements the repositories over SQLite/EF Core. The DbContext is `ProjectSeshatDbContext`; migrations live in `src/ProjectSeshat.Data/Migrations`.

The application now uses `context.Database.Migrate()` on startup so schema changes apply without regenerating the database or re-importing journals. To add a migration after a schema change:

```powershell
dotnet ef migrations add <Name> --project src/ProjectSeshat.Data
```

A design-time factory (`ProjectSeshatDbContextFactory`) lets the EF tools build the context outside the running app.

## Tests

Tests are in `tests/ProjectSeshat.Tests` and currently pass (142 tests). They cover architecture constraints, domain records, SQLite repository round trips (in-memory SQLite), journal reader import/dedup, the Atlas search guide + crawl tiers, Raxxla intel scoring, overlay/voice/auto-targeting logic, and the guidance overlay position store. Prefer in-memory SQLite over EF Core's non-relational in-memory provider because it exercises SQLite behavior.

## Dependencies and project conventions

- Target framework: `net10.0`
- Nullable reference types and implicit usings: enabled in `Directory.Build.props`
- Package versions: managed centrally in `Directory.Packages.props`
- UI: Avalonia `11.2.3`; Data: EF Core / SQLite `9.0.8` (includes `Microsoft.EntityFrameworkCore.Design` for migrations)
- Testing: xUnit

Do not add a package version directly to a `.csproj`; add it to `Directory.Packages.props` and reference the package without a version in the consuming project.

## Milestone 1.14 — Guided search action model and Raxxla intel
- Canonical `NextActionKind` / `NextAction` model is now used for the immediate action flow; legacy `CrawlStep` is retained only for compatibility.
- Search Guide refresh is asynchronous and cancellation-safe; `NextActionUpdated` notifies overlay, voice, and automation consumers.
- Priority is HONK → FSS → DSS → JUMP/BACK-TRACK, with DSS preferring unmapped eighth-moon/notable bodies.
- Search Guide includes a prominent `NEXT RAXXLA MOVE` card and ranked `RAXXLA INTEL` panel.
- `NavigationStateRepository.SaveAsync` now updates existing EF rows instead of attaching duplicate entities, fixing repeated journal imports.
- Current suite passes 142 tests under .NET 10.

## Recommended next work

Follow the `Next` / next-milestone sections in [roadmap.md](roadmap.md). **Milestones 1.9 (outward search), 1.10 (overlay + voice), 1.11 (keybinding auto-targeting + setup assistant), 1.12 (auto-target in-system work), and 1.13 (community Raxxla search intel) are implemented.** Remaining roadmap candidates are the "Next" items (Spansh route plotting, guided-search filtering, recently-discovered views). The auto-targeting device layer needs on-device smoke tests (ED present + a real `.binds` file). The Raxxla intel is criteria-derived, not a proven lead — see `docs/raxxla-search-criteria.md`. Other next steps:

- Add a source-of-truth Atlas Survey listing and richer FSS/DSS detail/filtering.
- Expand unit/integration test coverage and add CI/formatting (Quality section).

## Documentation maintenance

Keep these documents current when changing the architecture or milestone state:

- [README.md](../README.md) — public project overview and commands.
- [architecture.md](architecture.md) — dependency direction and responsibilities.
- [roadmap.md](roadmap.md) — completed and upcoming milestones.
- This handoff document — operational details that help an agent resume safely.

Update this handoff and `roadmap.md` whenever you move a milestone so the next agent can resume instantly.
