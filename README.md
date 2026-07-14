# Project Seshat

Project Seshat is an open-source Galactic Research Platform for *Elite Dangerous*. It provides a desktop foundation for working with commander journals, discoveries, spatial data, observations, and investigations.

## Solution layout

- `src/ProjectSeshat.App` — Avalonia desktop shell and MVVM presentation layer.
- `src/ProjectSeshat.Core` — shared domain abstractions with no application dependencies.
- `src/ProjectSeshat.Data` — persistence boundary; SQLite and Entity Framework Core will be added later.
- `src/ProjectSeshat.Journals` — Elite Dangerous journal ingestion boundary.
- `src/ProjectSeshat.Atlas` — spatial and astronomical data boundary.
- `src/ProjectSeshat.ThreadEngine` — research-thread processing boundary.
- `src/ProjectSeshat.Codex` — codex and discovery knowledge boundary.
- `src/ProjectSeshat.Observatory` — observation-analysis boundary.
- `src/ProjectSeshat.Investigations` — evidence-based investigation boundary.
- `tests/ProjectSeshat.Tests` — solution tests.

## Prerequisites

Install the .NET 9 SDK, then restore, build, and test from the repository root:

```powershell
dotnet restore
dotnet build ProjectSeshat.sln
dotnet test ProjectSeshat.sln
```

The desktop application uses Avalonia. Database and ORM packages are intentionally not included yet.

See [architecture.md](docs/architecture.md) and [roadmap.md](docs/roadmap.md) for the initial direction.
