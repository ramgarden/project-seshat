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

The desktop application uses Avalonia, wired to a SQLite database via Entity Framework Core.

## Guided search

The **Atlas Survey** page is a guided search tool that tells you what to do next as you work outward from surveyed space, in order:

1. **HONK** — which systems you've reached but haven't discovery-scanned yet.
2. **FSS** — which systems (the honk flagged with signals) to Full Spectrum Scan next.
3. **DSS** — which specific bodies turned out worth Surface-mapping with the Detailed Surface Scanner.

It's driven by your journal files: coordinates come from `StarPos` on `FSDJump`, the system honk from `FSSDiscoveryScan`/`DiscoveryScan`, and body details from `Scan` events.

See [architecture.md](docs/architecture.md) and [roadmap.md](docs/roadmap.md) for the initial direction.

Build: passing
Tests: passing
.NET: 10
Status: Alpha