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
- [x] Migrate solution to .NET 10.

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

## Milestone 1.5 — Galactic sky-map

- [x] Render surveyed systems, ranked undiscovered regions, the commander's current position, and the next jump target on an interactive 3D galactic sky-map.
- [x] Support drag-to-rotate and scroll-to-zoom on the map; auto-update on journal import.

## Milestone 1.6 — Search guide front-page + signal intelligence

- [x] Promote the search guide (honk / FSS / DSS with jump plotting) to the default landing page as its own top-level tab; split the galaxy map onto its own tab.
- [x] Add "why" explanations to each guided target (interesting signal types for FSS; body value / terraformability for DSS).
- [x] Parse `FSSSignalsFound` journal events to capture real signal types per system.

## Milestone 1.7 — Source-of-truth Atlas survey

- [x] Persist frontier survey regions (per grid cell) so the survey survives restarts.
- [x] Flag a region as charted once systems are surveyed inside it.
- [x] Add an Atlas Survey listing view; the galaxy map draws regions from the persisted survey.

## Milestone 1.8 — Community data foundation (EDDN + Spansh)

- [x] Add `ProjectSeshat.Community` with an EDDN message parser, injectable transport + listener (ZeroMQ via NetMQ).
- [x] Add a `CommunityService` (start/stop, live event counts) and a dashboard toggle for the EDDN stream.
- [x] Add a Spansh HTTP route service for jump/coverage plotting.
- [x] Unit tests for the parser, listener flow, and Spansh route parsing.
- [x] Monitor-based connection state: the EDDN toggle now shows *real* relay connectivity (via a NetMQ monitor socket) instead of "started = connected", and transport failures surface as text instead of crashing.
- [x] Persist community discoveries as a **bounded, deduplicated summary**: `CommunityDiscoveries` rows keyed by system name, batched from memory on a timer (and on stop), pruned to 250,000 rows / 180 days so disk stays constant. The dashboard shows the summary count and recent systems.

> Note: milestone provides the testable ingestion foundation; live-stream wiring is network-only and not exercisable in this sandbox.

## Milestone 1.9 — Systematic outward search (Core guidance)

Goal: turn the guided search into a **methodical outward survey** computed in `AtlasService` (fully unit-testable offline) — the app proposes a gate, walks the player outward star-by-star, and always knows the next hop.

- [x] Define an **outward crawl**: starting from a chosen gate, explore systems ring-by-ring by distance. Priority stays the existing pipeline — `Honk` (rescan), `FSS` for interesting signals, `DSS` for worth-mapping bodies — but the route is always **nearest-unsearched-first from the current position** so the player naturally radiates outward.
- [x] When the current system has **nothing left to FSS or DSS**, automatically advance the target to the **next nearest star not yet searched** (the honk route already scores this; make it explicit as the "next hop").
- [x] When **all stars in the surveyed neighbourhood are searched**, **back-track**: target a nearby (already charted) star that lies on the way toward the nearest still-unsearched star, so the player returns along the frontier instead of jumping in a straggly line — then resume outward once they arrive.
- [x] **Propose a good starting star**: expose a "recommended search gate" computed from current findings — combine the frontier/region score (`RankUndiscoveredRegionsAsync`), the density of unsearched neighbours, distance from the commander (wants to be reachable now), and community data (low community presence = fresher ground). Show it with its score and the reasoning, selectable as the crawl origin.
- [x] Expose the crawl state (gate, current hop, next hop, reasoning) through Core contracts; the Search Guide page renders the recommended gate and the immediate next move, refreshing on journal import.

## Milestone 1.10 — On-screen guidance overlay (single-screen, sound-off)

Goal: show the next action as a transparent subtitle **over the game** so the player never needs to alt-tab. Depends on 1.9's crawl state; window/voice behaviour needs manual on-device validation.

- [x] Transparent subtitle window **over the game**: always-on-top, click-through (Win32 `WS_EX_TRANSPARENT`/`WS_EX_LAYERED`), bottom-of-screen caption style. Renders the live next action from 1.9: `Honk here`, `FSS <system> for <signals>`, `DSS <body>`, `Jump to <star>` (nearest unsearched), `Back-track to <star>`, or `Nothing interesting — jump to <next star>`.
- [x] "Proposed search gate" picker in the app UI: shows the recommended starting star with score + reasoning from 1.9, clickable to set the crawl origin.
- [x] If keybind automation can't run (no bindings found / refused), the overlay's job is simply to **name the star** so the player picks it from the in-game Navigation panel's nearby list — no hotkey required.
- [x] Optional voice pings (Windows TTS via `SpeechSynthesizer`) speaking each step for sound-off play.
- [x] Keep text/transcript derivation testable offline: overlay copy and TTS transcript come from the 1.9 Core contracts (`GuidanceFormatter` / `GuidanceOverlayViewModel`, fully unit-tested).

## Milestone 1.11 — Keybinding auto-targeting

Goal: use the commander's actual ED bindings so the player only engages the jump. Highest device dependency — needs an ED install + keybindings file to verify on-device.

- [x] Read the commander's ED key-bindings file (`Options\Bindings\*.binds`) to discover the actual keys for target selection / hyperjump rather than hardcoding bindings (`BindingsParser`, `BindingsPathResolver`).
- [x] **Auto-targeting**: synthesize the target-selection key for the next route star, and charge the hyperjump, using their real keys (`SendInput` via `IGameInputSender`) — so the player just confirms the jump. Runs only on Jump/Back-track steps, only when bindings are armed and the game is the foreground window.
- [x] Graceful degradation: if no bindings are found or the game isn't focused, fall back to naming the star in the `1.10` overlay. The sidebar shows an "Enable auto-target" toggle + status (the resolved keys) or the fallback note.
- [x] Core logic (parser, path resolver, automation decision-making) fully unit-tested offline with injected fakes; only the real `SendInput`/foreground detection needs an on-device smoke test.
- [x] **Keybind setup assistant** (`KeybindSetupView`/`KeybindSetupViewModel`, sidebar entry): detects/parses current binds (respecting `StartPreset.start` via `StartPresetResolver`), shows the resolved target/jump keys, offers a live **Test** press (game must be focused), and — when unusable — can write a **minimal default binds** file (`BindingsWriter`, opt-in, `Seshat.Auto.4.0`, sparse so it doesn't clobber other controls, requires an ED restart) or guide the player through ED's in-game Controls screen, then **Re-detect**.

## Milestone 1.12 — Auto-target in-system work & override the overlay's next action

Goal: make the overlay always name exactly what to do next inside the current system, and auto-target suspicious signals/bodies so the player inspects or maps the right thing.

- [x] Overlay auto-shows the next in-system action (Honk needed → `HONK <star>`; FSS needed → `INSPECT THE TARGETED SIGNAL`; DSS → `DSS TARGETED BODY`) with the target name.
- [x] Auto-targeting now also drives FSS/DSS steps: it sends the target-selection key for the suspicious body/signal (game focused + bindings armed), so the overlay's instruction matches the targeted object.
- [x] Overlay detail lines name the target (`signal in <system>` / `body <name>`) and the reason; TTS transcripts updated to match ("Target the signal, then run the FSS…").

## Milestone 1.13 — Community Raxxla search intel

Goal: apply the community's actual hunt criteria (Great Raxxla Potato Hunt playbook + lore wiki) so the search guide flags what's worth investigating.

- [x] Curated criteria in Core (`RaxxlaSearchIntel`): notable body classes, suspicious signal terms, lore-name terms, Sol 200-ly hunt bubble, and the 8th-moon Dark Wheel clue — pure, unit-tested matching.
- [x] `AtlasService.FindRaxxlaIntelAsync` scans known systems/bodies and returns ranked `RaxxlaIntelHit`s with reasons.
- [x] Search Guide adds a **RAXXLA INTEL** panel (tagged MAP/FSS/LORE/BUBBLE entries) refreshed on journal import.
- [x] Research captured in `docs/raxxla-search-criteria.md` with sources cited; the app's intel is presented as priorities-to-investigate, not claimed locations.
- [x] The **overlay auto-updates the next thing to do** on launch and every journal import, and is **intel-aware**: suspicious FSS signals / intel-flagged bodies drive the next step's wording, and the outward jump plot prefers intel-flagged systems (lore names, suspicious signals, Sol-bubble systems) over nearest-first.

## Milestone 1.14 — Guided search action model and Raxxla intel

- [x] Introduce the canonical `NextActionKind` / `NextAction` model and replace legacy `CrawlStep` flow with the shared action model.
- [x] Prioritize immediate actions in the order HONK → FSS → DSS → JUMP/BACK-TRACK, with DSS preferring unmapped eighth-moon or notable bodies.
- [x] Add a prominent `NEXT RAXXLA MOVE` card and ranked `RAXXLA INTEL` panel to the Search Guide page.
- [x] Make Search Guide refresh asynchronous and cancellation-safe, with `NextActionUpdated` notifications for overlay, voice, and automation consumers.
- [x] Fix navigation-state persistence so repeated journal imports update the existing row without EF tracking/SQLite uniqueness conflicts.
- [x] Add focused tests for planner priority, Raxxla selection, guidance formatting, automation, async refresh, and navigation-state updates.

## Milestone 1.15 — Beacon scanning & Raxxla intel integration

- [x] Add `BeaconScan` domain record + `IBeaconRepository`/`BeaconRepository` for persisting Elite Dangerous beacon scans.
- [x] Parse `BeaconScan`/`BeaconFound` journal events in `JournalReader` (deduped by SHA-256 fingerprint).
- [x] Wire beacon repository through `JournalWatcher` and App composition.
- [x] Dashboard shows "BEACONS INDEXED" count.
- [x] `RaxxlaSearchIntel.ReasonForBeacon` scans beacon name, owner, and type for lore terms.
- [x] Beacon hits appear in "RAXXLA INTEL" panel tagged "BEACON".
- [x] Add EF migration `AddBeaconScans`.
- [x] All 142 tests passing.

## Next

- [ ] Plot Spansh routes into the search guide jump plotter.
- [ ] Filtering and richer detail on the guided search.
- [ ] Surface "recently discovered / uncharted" views fed from the `CommunityDiscoveries` summary (Spansh/EDDN revisit).

## Quality

- [ ] Expand unit and integration test coverage.
- [ ] Add continuous integration, formatting, and packaging.
