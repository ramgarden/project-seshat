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
- **Core** will contain stable domain concepts and contracts shared by the solution.
- **Feature projects** own their respective use cases and depend on Core rather than each other. Cross-feature collaboration should be defined through Core contracts when it becomes necessary.
- **Data** is the persistence boundary. SQLite and Entity Framework Core can be introduced here without leaking persistence concerns into Core or the UI.
- **Tests** verify behavior through public contracts.

The first UI is intentionally a tiny code-based Avalonia shell with a view model. It establishes the MVVM direction without adding framework or navigation infrastructure prematurely.
