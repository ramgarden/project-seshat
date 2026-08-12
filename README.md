# Project Seshat

Project Seshat is an open-source Galactic Research Platform for *Elite Dangerous*. It provides a desktop foundation for working with commander journals, discoveries, spatial data, observations, and investigations.

The project name comes from Seshat, the ancient Mesopotamian goddess of writing, wisdom, and surveying. The name fits the project’s role as a research companion for cataloguing systems, evidence, and discoveries across the galaxy.

## Solution layout

- `src/ProjectSeshat.App` — Avalonia desktop shell and MVVM presentation layer.
- `src/ProjectSeshat.Core` — shared domain abstractions with no application dependencies.
- `src/ProjectSeshat.Data` — persistence boundary; implements SQLite and Entity Framework Core repositories.
- `src/ProjectSeshat.Journals` — Elite Dangerous journal ingestion boundary.
- `src/ProjectSeshat.Atlas` — spatial and astronomical data boundary.
- `src/ProjectSeshat.ThreadEngine` — research-thread processing boundary.
- `src/ProjectSeshat.Codex` — codex and discovery knowledge boundary.
- `src/ProjectSeshat.Observatory` — observation-analysis boundary.
- `src/ProjectSeshat.Investigations` — evidence-based investigation boundary.
- `tests/ProjectSeshat.Tests` — solution tests.

## Prerequisites

Install the .NET 10 SDK, then restore, build, and test from the repository root:

```powershell
dotnet restore
dotnet build ProjectSeshat.sln
dotnet test ProjectSeshat.sln
```

The desktop application uses Avalonia, wired to a SQLite database via Entity Framework Core. It automatically watches your Elite Dangerous journal folder and tail-imports new events as you play, so the statistics and search guide stay current without any manual action (already-loaded content is deduplicated by fingerprint/file path). It can also stream live crowdsourced discoveries from **EDDN** and plot routes through **Spansh**.

## Guided search

The **Atlas Survey** page is a guided search tool that tells you what to do next as you work outward from surveyed space, in order:

1. **HONK** — which systems you've reached but haven't discovery-scanned yet.
2. **FSS** — which systems (the honk flagged with signals) to Full Spectrum Scan next.
3. **DSS** — which specific bodies turned out worth Surface-mapping with the Detailed Surface Scanner.

Each entry explains **why** it's a priority: FSS shows the interesting signal types found there, and DSS explains a body's value (terraformable, Earth-like, water, or ammonia world).

It's driven by your journal files: coordinates come from `StarPos` on `FSDJump`, the system honk from `FSSDiscoveryScan`/`DiscoveryScan`, and body details from `Scan` events. The app also provides an interactive **3D galactic sky-map** of surveyed systems, ranked undiscovered regions, your current position, and the next jump target, plus a persisted **Atlas Survey** listing of frontier regions that survives restarts.

## In-game guidance

For single-screen, sound-off play, the app can guide you live over the game window:

- **Overlay** — a transparent, draggable, click-through caption over the game shows the next step (`JUMP → SOL`, `HONK`, `FSS`, `DSS`, `BACK-TRACK`). Its position is remembered between runs.
- **Voice** — optional Windows TTS pings each step.
- **Auto-targeting** — reads your Elite Dangerous key bindings (`Options\Bindings\*.binds`) and can target the next route star and charge the hyperjump with your real keys, so you just confirm the FSD charge. A setup assistant (sidebar → **Keybind Setup**) detects your bindings, lets you test the press, and can write a minimal default binding set if none exist.

See [architecture.md](docs/architecture.md) and [roadmap.md](docs/roadmap.md) for the initial direction.

Build: passing
Tests: passing
.NET: 10
Status: Alpha