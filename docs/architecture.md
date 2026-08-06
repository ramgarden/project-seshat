# Architecture

Project Seshat follows a small, dependency-directed architecture. `ProjectSeshat.Core` is the shared domain layer and has no project dependencies. Feature projects depend only on Core. The Avalonia desktop application composes the feature projects and owns presentation concerns.

```
                      ProjectSeshat.App (Avalonia / MVVM)
                                      |
     ---------------------------------------------------------------
     |        |       |        |            |         |             |
   Data   Journals  Atlas  ThreadEngine   Codex  Observatory  Investigations
     \        |       |        |            |         |             /
      ------------------- ProjectSeshat.Core ----------------------
```

## Responsibilities

- **App** contains views, view models, and composition. It must not hold domain rules.
- **Core** contains stable domain concepts and storage contracts shared by the solution: systems, celestial bodies, observations, codex, evidence, research threads, survey state, and galactic coordinates, without prescribing persistence.
- **Feature projects** own their respective use cases and depend on Core rather than each other. Cross-feature collaboration is defined through Core contracts.
- **Data** is the persistence boundary. SQLite and Entity Framework Core live here behind repository contracts; schema changes go through EF migrations, and the DbContext never leaks outside Data.
- **Tests** verify behavior through public contracts.

The first UI is intentionally a small XAML-based Avalonia dashboard with a view model. It establishes the MVVM direction and grows into a left-sidebar navigation shell. The dashboard reads live system, commander, and evidence counts from an EF Core + SQLite data layer, the journals boundary imports a slice of Elite Dangerous events, and the Atlas boundary composes the guided honk → FSS → DSS search guide for the Atlas Survey page. Schema changes are applied via EF Core migrations on startup rather than `EnsureCreated`, so data survives schema updates.
