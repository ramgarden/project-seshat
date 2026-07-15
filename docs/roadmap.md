# Roadmap

## Foundation

- [x] Create the .NET 9 solution and project boundaries.
- [x] Establish an Avalonia desktop shell and MVVM starting point.
- [x] Add the persistence boundary without committing to a data model.

## Milestone 0.3 — Shared domain foundation

- [x] Define shared domain entities and contracts in Core.

## Milestone 0.4 — SQLite persistence foundation

- [x] Add SQLite and Entity Framework Core support in Data.
- [x] Implement repository-backed persistence for systems, commanders, and evidence.
- [x] Wire the desktop dashboard to the data layer for live counts.

## Milestone 0.5 — Journal ingestion foundation

- [x] Parse and persist a narrow slice of Elite Dangerous journal events.
- [x] Capture commander identity, star-system jumps, and scan evidence from journal lines.

## Next
- [ ] Establish navigation and feature composition in the desktop app.

## Research capabilities

- [ ] Model atlas, codex, and observation data.
- [ ] Build research-thread workflows.
- [ ] Support evidence capture and investigations.

## Quality

- [ ] Expand unit and integration test coverage.
- [ ] Add continuous integration, formatting, and packaging.
