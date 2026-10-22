# Gameplay dependency map and implementation audit

This document is the integration contract for new gameplay systems. It is deliberately explicit about the difference between an implemented mechanism, a partial bridge, and a planned feature. A successful build does not mean a gameplay loop is complete.

## Non-negotiable rule

Every new feature must be designed as a connected set of state, rules, jobs, resources, feedback, persistence, and tests. Do not add an object, faction, vehicle, need, weapon, or infection rule as an isolated yes/no flag. Before implementation, inspect the current code and mark each dependency as **implemented**, **partial**, **missing**, or **verified by test**. Reuse the canonical system where one exists; do not create a second owner for the same state.

## Runtime layers and ownership

| Layer | Current canonical owner | Integration contract |
|---|---|---|
| Real-time clock / coordination | `Simulation/WorldSimulation.cs` | Advance colony and strategic systems under one clock; pausing and speed affect the same session. |
| Local colony and worker tasks | `Simulation/ColonySimulation.cs` | Logical square-grid coordinates; construction, hauling, inventory and object state must be simulation-owned, never renderer-owned. |
| Citizen needs and workplace jobs | `Simulation/ColonyEcosystem.cs` | Needs change over time; consumption must withdraw real resources; workplaces reserve outputs and respect worker/storage capacity. Current integration is partial, not a replacement of every legacy scheduler path. |
| Local storage and inventory | Colony inventory plus ecosystem food-grid bridge | Existing inventory is pooled; food has bounded grid slots, while non-food resource ownership still has legacy paths. Do not assume all resources already use one footprint-backed grid. |
| Strategic continuous world | `WorldModel/ContinuousWorldState.cs`, `MacroWorldState.cs` | Factions, settlements, routes, shipments and macro activity continue independently of the local view. Use travel time and conserved cargo; no instant transfer or map-view-dependent simulation. |
| Strategic faction decision-making | `WorldModel/FactionIntelligence.cs`, `FactionStrategicDirector.cs` | NPC factions need their own goals, resources, diplomacy, logistics and action cadence, not merely map markers. Verify actual autonomous state changes before describing a faction as simulated. |
| Research and recipes | `Simulation/Research/ResearchSystem.cs`, workbench/workshop systems | Unlocks gate real recipes; inputs are debited once at the defined commit point; outputs appear only after work and must have storage space. |
| Tactical combat and structure damage | `Simulation/Combat/CombatSystems.cs` and integration/session systems | Units, weapons, ammunition, armor, damage, cover, repairs, casualties, salvage and save/load must agree on one combat state. New vehicles must use these shared rules rather than a special-case damage flag. |
| The Blight | `WorldModel/WorldEcologyState.cs`, `Simulation/BlightThreatModel.cs` and structure infection paths | Infection must propagate through explicit rules, have observable effects, interact with treatment/resources and remain deterministic/persisted where state is durable. |
| Rendering and input | `Rendering/ColonyRenderer.cs`, `Program.cs` | Display and issue commands to model APIs; never become the source of gameplay truth. Every core action remains mouse-accessible. |
| Save/load | `Simulation/ColonySaveService.cs`, `GameSessionService.cs` | New durable state needs versioned persistence, validation, migration defaults, and round-trip tests. Runtime caches/reservations must be safely rebuilt or restored. |

## Current evidence-based status

The active repository describes and tests the following foundations:

- A 40×26 local square-grid colony, finite deposits, worker movement/carry/delivery, construction blueprints, multi-tile footprints, material costs, and priority-based work orders.
- A completed storage rack increases pooled colony capacity by 80 units.
- A completed garden registers a live workplace, reserves food output capacity, and can deliver food into the colony inventory.
- A completed workbench supports a component-production workflow; research and workshop systems exist.
- Citizen agents are bridged into the legacy worker state. Hunger/food and some mood/stress data are synchronized, but the ecosystem is not yet the sole owner of all needs and jobs.
- A continuous strategic world, autonomous-faction-related systems, regional/macro state, time-based cargo shipments, research, tactical combat, structural damage/repair, Blight infection and unified session saves exist in some form.
- The last verified `main` CI run built the root project, passed simulation/unified-session checks, captured UI comparison images, published the Linux x64 build, validated launch scripts and passed graphical startup smoke tests at 1280×720 and 1920×1080.

These are foundation claims, not claims that every listed system is complete or deeply interconnected.

## Explicit gaps and proof obligations

The following must be treated as unverified/incomplete until code inspection and focused tests prove otherwise:

- **Every-resource logistics:** non-food resources, construction costs, hauling and production are not yet fully unified behind the footprint-backed grid and one shared job model.
- **Furniture and needs:** a placeable object existing in the enum/placement UI does not prove it provides a gameplay effect. Tables need social/eating interactions; beds/pods need sleep and fatigue effects; comfort objects need a defined recreation/social effect; lamps need a lighting/visibility effect; generators need a real power network with consumers.
- **Power:** define producers, consumers, connectivity, output/demand, enabled/damaged state, allocation/failure behavior, and persistence before claiming a generator powers anything. Conduits must connect actual power nodes, not only be buildable tiles.
- **Social loops:** needs and social relationships must connect to reachable locations, schedules/job arbitration, encounters, mood/stress and consequences. No instant “social bonus” without a cause and a test.
- **Production chains:** each recipe must have explicit inputs, work duration, skill/worker assignment where appropriate, outputs/by-products, storage reservations, unlock conditions and failure/cancellation semantics.
- **Faction autonomy:** seed generation is not autonomy. Each faction must advance goals, economy, logistics, settlement growth, diplomacy and conflict without the player opening the map; deterministic tests should advance time and assert meaningful state changes.
- **Start selection:** the player must choose a valid starting location/faction/seed as appropriate; generated terrain, resources, local spawn, nearby factions and save identity must remain consistent with that selection.
- **Trade and war:** routes, travel time, stock availability, prices/agreements or diplomacy, cargo ownership, arrival, raids/war declarations, army supply, battle outcomes, casualties and post-conflict state must form one closed loop. A button or dispatched marker is not sufficient.
- **Vehicles and armor:** before adding a vehicle, inspect and connect movement/terrain, crew and assignments, fuel/energy, inventory/parts, armor penetration, weapon firing, ammunition, targeting, damage, destruction, repairs, production/research unlocks, AI use, save/load and UI. Do not implement a tank as a single boolean or isolated unit type.
- **Blight:** distinguish local infection, structure/environment effects, propagation, treatment, resource costs, ecological/strategic spread, counterplay and persistence. Avoid disconnected duplicate infection models.
- **Save/load and determinism:** every new durable field must round-trip; same seed and same inputs should produce stable outcomes where intended. Test missing/invalid resources and interrupted jobs, not just happy paths.

## Required workflow for every feature

1. **Inspect first:** read the owning model, scheduler, resource accounting, save schema, input path and existing tests. Search for duplicate/legacy owners and determine what is actually used at runtime.
2. **Write a dependency slice:** list producer → transport/job → consumer → need/outcome → feedback/next demand. Mark every edge implemented/partial/missing.
3. **Define invariants:** resource conservation, one owner per durable state, no over-capacity storage, no work without inputs, no output before completion, no impossible path/assignment, and no invisible UI-only state.
4. **Implement the smallest complete loop:** prefer one vertical slice that works end-to-end over many placeholder systems. Extend existing APIs rather than adding parallel mechanisms.
5. **Test interactions:** direct model tests, time-advance tests, resource-conservation tests, failure/capacity tests, save/load round trips, and a session-level integration test. Add UI smoke checks when controls or rendering change.
6. **Verify integration:** build, full self-test, CI, and relevant graphical smoke tests. Inspect failures and fix regressions before calling the slice complete.
7. **Update this map:** only change a status to implemented when tests and code support the claim. Keep known gaps visible.

## Gameplay-loop acceptance example: meal and social table

A table is not complete when it can be placed. A complete loop needs:
1. A food-producing or gathering source with finite inputs and worker time.
2. Hauling and storage capacity that conserve food units.
3. A colonist whose hunger rises and whose schedule/job priority allows eating.
4. A reachable dining location and a reservation so several colonists do not occupy the same interaction slot.
5. A meal interaction that consumes one real food unit and reduces hunger.
6. Optional social encounters only when residents are actually co-present and available; those encounters feed the relationship/mood/stress system.
7. Consequences when food, paths, seats, workers or storage are unavailable.
8. Save/load coverage for any durable needs, jobs, reservations or relationship state.
9. Tests proving food decreases by the right amount, hunger changes, no resource is duplicated, the table affects behavior, and the loop still works after a session round trip.

## Macro-loop acceptance example: independent factions

A faction is not complete when a colored symbol appears on the world map. A complete autonomous loop needs:
1. Persistent identity, population/settlements, resource stores and strategic goals.
2. Independent periodic decisions and a clock that runs when the player is elsewhere.
3. Production/gathering and transport constraints that change the faction's available resources.
4. Trade/diplomacy decisions that depend on stock, distance, trust/relations and route risk.
5. Expansion, defensive posture or conflict decisions that consume resources and have consequences.
6. Movement and travel-time shipments with arrival/interrupt/failure outcomes.
7. Combat, losses, repairs, recovery and post-conflict economy where war exists.
8. Stable save/load and seeded deterministic tests.
9. Tests that advance the world without player commands and verify at least one meaningful autonomous decision and its resource/state consequences.

## Completion standard

A feature is **implemented** only when its model behavior, resource/workflow connections, UI access, failure cases, persistence and tests agree. Otherwise label it partial or planned. Never confuse a visible object, a class, an enum member, a menu entry, or a successful compile with a functioning gameplay system.

## Runtime audit: meal/social loop (2026-10-22)

This section records code-level observations from `Simulation/ColonySimulation.cs`, `Simulation/ColonyEcosystem.cs`, `Simulation/SocialRelationshipNetwork.cs`, and their checks. It is intentionally more specific than the feature roadmap.

### Verified in source

- `ColonySimulation.StepEcosystem` mirrors the settlement's food ledger into the ecosystem grid, runs the ecosystem, then reconciles food consumption and production back to the settlement inventory.
- Completed gardens register a workplace with one worker, one food per completed work cycle, and an eight-second work duration. The job is only registered while the inventory has room relative to existing reservations.
- The ecosystem reserves output capacity before work and commits that reservation only after the work duration. Food need interrupts work/travel and releases the worker assignment and output reservation.
- A hungry citizen can path to the nearest food stockpile, consume one real food unit, and restore hunger. Existing checks cover this path, storage reservations, job release, and resource accounting.
- `StepSocialRelationships` forms stable resident pairs from nearby worker positions and records encounters with stress, hunger, rest need and mental-break state. The relationship network bounds affinity/trust/tension and history, emits threshold events, and supports snapshot restore.
- The main branch workflow run for commit `7fdc11eee617b385b58fcac21a7e3cad09aa3661` completed successfully on 2026-10-22.

### Still not verified as one complete loop

- The ecosystem's citizen and the legacy colony worker are mirrored representations; food consumption is reconciled between their grids/ledgers rather than owned by one unified all-resource inventory.
- The garden job is a production workplace, but its code does not demonstrate a dedicated hauling job that moves harvested food from a garden footprint to a distinct stockpile. Do not describe that transport step as implemented.
- `DiningTable` exists as a placeable kind, but the inspected runtime path does not route hungry/free residents to a table, reserve seats, or require a table for eating.
- Social encounters currently depend on proximity between workers; the inspected method does not require free time, a reachable dining table, or reserved seating. The network's relationship math is implemented, but a table-caused social encounter is not.
- No source evidence in this audit proves that table interactions change resident mood or work effectiveness as a consequence of a completed meal/social encounter.

### Next implementation slice and acceptance criteria

Implement the meal/social loop only after extending the existing canonical state and persistence paths—not by adding a second parallel citizen system. A slice is ready to be called complete only when:

1. A resident can leave work according to need/availability arbitration and path to a reachable dining location.
2. Each table seat is a capacity-limited reservation with deterministic acquire/release on arrival, interruption, destruction and save/load.
3. A meal consumes exactly one food unit from the canonical inventory and updates the resident's hunger exactly once; failure to reach a seat or obtain food consumes nothing.
4. Social relationship updates happen only for eligible residents actually co-present during a valid interaction; proximity elsewhere does not count as dining.
5. Relationship/mood effects feed back into existing resident state and the existing work scheduler, with no presentation-only bonus.
6. Tests cover no food, unreachable table, full seats, interrupted journey, destroyed/removed table, simultaneous diners, resource conservation, stable repeated runs and a save/load round trip.
7. Build, full simulation checks and the graphical smoke tests pass before merging.
