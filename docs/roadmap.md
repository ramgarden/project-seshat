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

## Milestone 0.6 — Navigation and feature composition

- [x] Establish navigation and feature composition in the desktop app.

## Milestone 0.7 — Atlas, codex, and observation data

- [x] Model atlas, codex, and observation data.

## Milestone 0.8 — Research-thread workflows

- [x] Build research-thread workflows.
- [x] Track scan completeness (FSS/DSS) per celestial body and surface it in exploration.
- [x] Persist the database at a stable location (user AppData) so data survives relaunch.
- [x] De-duplicate journal imports by content fingerprint (SHA-256), not just file path.

## Milestone 0.9 — Evidence capture and investigations

- [x] Capture evidence records attached to a research thread as an investigation.
- [x] Review a thread's captured evidence in the desktop UI.

## Milestone 1.0 — .NET 10 Migration
- [ ] Migrate solution to .NET 10.

## Milestone 1.1 — Atlas undiscovered-region survey

- [x] Capture galactic coordinates for star systems from journal StarPos.
- [x] Identify and rank largely uncharted regions near the surveyed frontier.

## Milestone 1.2 — Guided search (honk, FSS, DSS)

- [x] Model a system survey pipeline: Unexplored → Honked → FssScanned.
- [x] Import the system honk (`FSSDiscoveryScan`/`DiscoveryScan`) and its signal count.
- [x] Guide the user in order: where to honk, then which systems to FSS, then which specific bodies to DSS.

## Milestone 1.3 — Schema migrations

- [x] Add EF Core migrations so schema changes no longer require regenerating the database.
- [x] Apply migrations on app startup (replaces `EnsureCreated`).

## Milestone 1.3 — Live journal auto-watch

- [x] Watch the Elite Dangerous journal directory and tail-import new events as gameplay writes them.
- [x] Auto-refresh the dashboard stats, exploration list, and Atlas Survey guide on import (no manual rescan).
- [x] Start watching automatically on app launch; remove the manual "Load journal files" and "Rescan" buttons.
- [x] Deduplicate already-loaded journal content (content fingerprint + file path) so restarts never double-import.

## Milestone 1.4 — Jump-plotting

- [x] Track the commander's current system from the latest `FSDJump`/`Location` journal event (persisted).
- [x] Order the honk/FSS/DSS search guide by distance from the commander's current position.
- [x] Present an ordered jump route (nearest-first honk list) plus an explicit "next jump" target in the Atlas Survey UI.

## Next

- [ ] Add source-of-truth listing for Atlas Survey (persisted region/mapping records beyond the live journal view).
- [ ] Sky-map / region visualization for the Raxxla hunt.
- [ ] Richer FSS/DSS detail and filtering on the guided search.

## Quality

- [ ] Expand unit and integration test coverage.
- [ ] Add continuous integration, formatting, and packaging.
