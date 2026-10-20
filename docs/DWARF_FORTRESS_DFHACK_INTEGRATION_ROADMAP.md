# Dwarf Fortress + DFHack systems integration plan

## Intent

Kor Eternal Frontier should grow into a deep, interconnected colony-and-world simulation inspired by the *simulation breadth* of Dwarf Fortress and the *tooling, automation, diagnostics, and quality-of-life workflows* of DFHack. This is a project-wide engineering direction, not a materials-preview feature.

DFHack is an extension framework for Dwarf Fortress, not a standalone implementation of Dwarf Fortress's simulation. Its tools interact with Dwarf Fortress internals. Therefore, this project will recreate compatible gameplay concepts and tool behaviors inside Kor Eternal Frontier's own simulation architecture. Do not inject DFHack into this game, copy its source/assets, or assume its APIs can operate on this C# codebase. Review applicable licenses before adapting any specific implementation.

## Non-negotiable project constraints

- Keep the root .NET 8 / Gondwana project as the only active application.
- Keep simulation independent of rendering, SDL, camera state, and screen coordinates.
- Preserve mouse-first visible controls; keyboard shortcuts remain optional.
- Use a single fixed-step real-time clock and deterministic rules where practical.
- Use one authoritative resource ownership model. Gathering, hauling, storage, crafting, construction, and trade must conserve resources.
- Every blueprint is an order, not a completed object. Work must be blocked by missing inputs, invalid placement, access/pathing problems, or capacity limits, with an inspectable reason.
- Persist the complete world in a versioned save with validation and round-trip tests.
- Extend the existing working simulation incrementally; do not replace functioning systems wholesale without migration tests.
- Every subsystem needs focused self-tests. Never describe builds or tests as passing unless they were actually run.

## System map: the intended breadth

### 1. Simulation kernel and observability
- One authoritative tick/update schedule, pause/speed handling, stable entity IDs, deterministic random streams, event queue, and explicit system ordering.
- Centralized entity/component or equivalent domain model; spatial queries and navigation that do not depend on rendering.
- Inspectable simulation events, structured diagnostics, reproducible bug reports, and a developer-only state inspector.
- Save/load snapshots with schema versions, migration paths, corruption checks, and round-trip validation.

### 2. Terrain, geology, and environment
- Continuous world-space geography plus a local square-tile settlement map.
- Terrain layers, finite deposits, materials, elevation, soil, water, underground spaces, and resource discovery.
- Digging/excavation orders with access, support, pathfinding, and material yield rules.
- Environmental simulation introduced in measured slices: water flow and containment first, then temperature, weather, fire, and other hazards only when performance and tests support them.
- The Blight as an ecosystem with spread conditions, local state, environmental feedback, and observable countermeasures—not a simple scripted enemy counter.

### 3. People, needs, and health
- Individual citizens with identity, needs, hunger/thirst/rest, comfort, health state, skills, traits, preferences, memories, and relationships.
- Housing, room assignment, privacy, cleanliness, recreation, social spaces, morale, stress, and consequences for neglected needs.
- Work eligibility, shift/rest behavior, injuries and recovery represented as game simulation states, and clear player-readable status reasons.
- Population growth and life-cycle systems only after the existing citizen model and save migration are stable.

### 4. Jobs, priorities, and autonomous behavior
- A single job broker for construction, hauling, gathering, farming, crafting, cleaning, maintenance, research, and other work.
- Job priorities, labor permissions, skill suitability, prerequisites, cancellation, reservations, ownership, retry/backoff, and deadlock diagnostics.
- Citizens choose reachable eligible jobs based on priority, distance, skill, urgency, and current state.
- Explicit task state transitions; jobs must not silently duplicate resources or be completed twice.
- Player-facing job and backlog panels inspired by DFHack's inspection and automation tools.

### 5. Logistics, stockpiles, and inventory
- Resource stacks/items with unique identity where needed, explicit location, quantity, ownership, quality, and reservation state.
- Stockpiles and storage racks with filters, capacities, reachability, and item categories.
- Real gather → carry → deliver journeys; source quantity decreases when gathered and inventory increases only after delivery.
- Partial loads and deliveries, reserved inputs, output-space reservations, spoilage where relevant, and no instant long-distance transfers.
- Hauling priorities, stockpile links, import/export policies, and travel-time shipments between settlements.

### 6. Construction, rooms, and infrastructure
- Blueprints for tile architecture and separate footprint-based objects.
- Placement validation, cost estimates, material reservations, job queues, worker access, construction progress, blockers, and cancellation/refund rules.
- Doors, walls, floors, paths, conduits, room detection, room purposes, access rules, and infrastructure connectivity.
- Construction planner inspired by DFHack buildingplan: plan ahead, show pending requirements, and automatically resume when conditions become valid.
- A visible explanation for every waiting/blocked order, not just a color.

### 7. Workshops, crafting, and production chains
- Data-driven recipes with inputs, outputs, labor, skill, duration, by-products, quality, and output capacity.
- Workshop queues, repeat/maintain-stock orders, conditional work orders, material filters, and job reservation.
- Atomic production: inputs are committed and outputs created only when a job completes and storage capacity exists.
- Multi-step chains (raw material → processed material → components → advanced goods), with bottleneck and missing-input diagnostics.

### 8. Food, farming, and living ecosystems
- Soil and season-aware farming, planting/growing/harvest jobs, seeds, food storage, and spoilage.
- Food preparation and distribution based on citizen needs and available stock.
- Livestock and wildlife with needs, reproduction, habitats, migration, and ecological interactions, added in tested increments.
- Resource regeneration and ecosystem pressures that connect to settlement choices and The Blight.

### 9. Skills, quality, research, and progression
- Skill growth through work, skill suitability, item/building quality, and transparent effects on outcomes.
- Research projects with prerequisites and meaningful unlocks for actual buildings, recipes, policies, and infrastructure.
- Progression must emerge from the simulation rather than be a disconnected technology menu.

### 10. Society, culture, and settlement identity
- Citizen relationships, social groups, roles, preferences, morale, conflicts, and community-level state.
- Visitors, migrants, trade partners, diplomacy, requests, and consequences of settlement policies.
- Keep the system data-driven and avoid hard-coded assumptions that prevent different settlement identities.

### 11. World simulation and continuity
- Replace the legacy nine-region hex campaign model; do not expand it.
- Continuous world-space entities, spatial queries, travel-time movement, routes, settlements, populations, and shipments.
- One canonical resource model and one versioned world save spanning the local settlement and wider world.
- Regional events and The Blight must update through the same clock and persist across save/load.

### 12. Threats and tactical incidents
- A simulation-first incident framework for hostile wildlife, environmental danger, raids, emergencies, defenses, and recovery.
- Threats should interact with terrain, visibility, pathfinding, citizen needs, morale, and infrastructure.
- Keep tactical rules deterministic and inspectable; avoid making incidents into isolated scripted encounters that ignore the colony simulation.

### 13. DFHack-inspired player tools
Translate the *categories* of DFHack utility into native Kor tools:
- **Automation:** rule-based routine management with visible enable/disable state and clear effects.
- **Planning:** blueprints, bulk orders, work-order templates, and repeatable settlement layouts.
- **Inspection:** inspect any citizen, item, building, job, stockpile, resource deposit, or world event.
- **Diagnostics:** explain why a job cannot run, where a resource is, why a citizen is idle, and what blocks a construction.
- **Quality of life:** bulk designation, cancellation, filtering, sorting, status overlays, alerts, and configurable priorities.
- **Modding/data definitions:** eventually expose safe data-driven recipes, buildings, jobs, and events with validation.
- **Developer utilities:** deterministic scenario runner, simulation step controls, state dumps, and invariant checks, separated from normal player-facing tools.

## Delivery sequence

This is a multi-release program. “Complete” means the systems are integrated and interact, not that every subsystem merely has a menu or placeholder.

### Stage A — Audit and shared contracts
1. Inventory current systems, duplicated state, legacy paths, save schema, and tests.
2. Define canonical IDs and contracts for entities, items, storage, jobs, reservations, events, world coordinates, and save persistence.
3. Add invariant tests for resource conservation, unique job ownership, valid job transitions, fixed-step determinism, and save/load continuity.
4. Keep current gathering, hauling, construction, and production behavior working through adapters during migration.

### Stage B — Reliable simulation backbone
1. **First slice implemented in this branch:** player-selectable priorities 1–5 for tile construction, object blueprints, and component production; the live scheduler chooses critical/high-priority work before lower-priority eligible orders, automatic material gathering prioritizes resources needed by the most urgent blocked order, and save schema v5 persists order priorities.
2. Consolidate job scheduling and resource reservations.
3. Complete storage capacity, item location, partial delivery, and output reservation.
4. Add shared job diagnostics, priority controls for hauling/gathering/citizen needs, and a useful filterable backlog/inspection UI.
5. Ensure every order displays its exact blocked/waiting reason.

### Stage C — Deep local colony simulation
1. **First terrain slice implemented in this branch:** designate solid rock for excavation, assign a miner through the prioritized scheduler, convert completed rock to walkable ground, and spawn a finite physical stone deposit that must be hauled. Mining orders and terrain/deposit outcomes are covered by schema-v6 save/load.
2. Extend citizens' needs, rest, housing, skills, and morale.
3. Add general work orders and data-driven production chains.
4. Add room/zone semantics, farming seasons, food processing, spoilage, and maintenance.
5. Expand beyond this first excavation slice into layered geology, underground resource discovery, and terrain-dependent yields.

### Stage D — Ecosystem and emergent events
1. Extend The Blight into an environmental/ecological simulation.
2. Add wildlife/livestock and their interaction with habitat, food, and settlement activity.
3. Add incidents, visitor/migrant behavior, and settlement-level consequences.

### Stage E — Continuous world
1. Replace legacy hex-region assumptions with continuous world-space entities and spatial queries.
2. Unify population and resources between settlement and world.
3. Implement real travel-time routes and shipments with loading/unloading and lossless accounting.
4. Persist the complete world through one validated save format.

### Stage F — DFHack-style automation and tooling
1. Rule-based automation and reusable work-order templates.
2. Bulk planning, blueprint templates, and material/worker assignment controls.
3. Deep object/job inspection, simulation diagnostics, and developer scenario tools.
4. Optional safe data-driven modding interface.

### Stage G — Integration, performance, and playable releases
1. Run deterministic simulation scenarios and regression tests for every cross-system loop.
2. Profile population, pathfinding, terrain, and event workloads; use bounded update budgets where required.
3. Test save migrations and recovery from invalid save data.
4. Build and launch the Linux x86_64 package on a real environment before declaring it playable.
5. Keep release notes honest about completed systems versus planned systems.

## Acceptance criteria

A system is not considered integrated just because a class, button, or document exists. It must:
- participate in the live simulation tick;
- exchange state through the canonical contracts;
- affect at least one other relevant subsystem;
- be inspectable in the UI;
- survive save/load where applicable;
- have deterministic or invariant-based regression coverage;
- explain failure states instead of silently stalling.

## Reference material

- DFHack introduction: https://docs.dfhack.org/en/53.16-r3/docs/Introduction.html
- DFHack tools catalog: https://docs.dfhack.org/en/stable/docs/Tools.html
- DFHack core architecture: https://docs.dfhack.org/en/53.16-r3/docs/Core.html
- DFHack Lua API: https://docs.dfhack.org/en/stable/docs/dev/Lua%20API.html
- DFHack development overview: https://docs.dfhack.org/en/53.16-r3/docs/dev/Dev-intro.html
- DFHack source repository: https://github.com/DFHack/dfhack

The reference links describe DFHack itself. Kor Eternal Frontier must implement its own native systems and APIs.
