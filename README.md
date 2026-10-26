# KOR ETERNAL FRONTIER

Kor Eternal Frontier is a developing colony, civilization, and world simulation. The goal is Dwarf-Fortress-inspired simulation depth with a deliberately legible, low-detail symbolic presentation: residents should have persistent lives and needs, logistics should emerge from real constraints, and wildlife, settlements and the Blight should evolve even when the player is looking elsewhere. This is an inspiration target, not a verbatim copy of Dwarf Fortress assets or interface. The game centers on autonomous population, construction, logistics, production, research, regional development, and an evolving Blight ecosystem—not conventional RTS unit combat.

## Current foundation

The live simulation uses one fixed-step 60 Hz kernel as the authoritative clock for the colony, continuous world, macro regions, combat and consequences. Systems execute in a stable phase order, while planning, storyteller checks, strategic arrival handling, research and workshop work are scheduled at 10 Hz. The kernel is independent of render-frame timing and restores its integer tick from the saved world clock.

The canonical application is the root .NET 8 project. The Unity prototype, duplicate nested project, legacy isometric artwork pipeline, duplicated art/metadata folders, and checked-in build output have been removed from the active source tree.

The local map is a 40 by 26 orthogonal square grid. A world seed now drives both the strategic overworld and its local spawn map: terrain scatter, resource-deposit positions/amounts, regional biomes, faction settlements, armies, caravans, Blight fronts and strategic resource sites are reproducible for the same seed and differ across seeds. The starter habitat and stockpile remain in a protected starting area so a new run is playable. The current simulation foundation has:

- Three initially present, independently simulated colonists with movement, carrying, tasks, hunger, fatigue, deterministic personality traits, personal memories, relationships, stress, daily work/recreation/sleep schedules, and body-part health.
- Residents can autonomously take short, deterministic walks during recreation and when no suitable work is available; a saved scheduling window prevents them from continuously choosing new destinations after every short walk.
- Finite resource deposits and real gather/carry/deliver trips that conserve resource quantities.
- Capacity-limited colony inventory: the completed starter stockpile provides 120 units; each completed storage rack adds 80. Gathering pauses when no space remains, and workers retain undelivered cargo until space becomes available.
- Tile-by-tile architecture design with an independent construction state and work order per tile.
- Separate placeable-object blueprints with multi-tile footprints and real construction work.
- Inventory-based material costs for both construction workflows.
- A workstation recipe that consumes alloy and wood when queued; a worker performs the job before a component is added to inventory.
- A minimal symbolic renderer: square tiles and buildings, colonists as dots, resource markers as squares.
- A bounded 8,192-entry successful-route cache for repeated local path requests, with defensive route copies and cache invalidation when construction changes walkability. Internal counters track breadth-first searches and cache hits.
- Versioned colony save/load for terrain, architecture orders, objects, colonists, cargo, tasks/routes, deposits, inventory, production orders, IDs, selection, and simulation time.
- Deterministic resident social relationships: nearby encounters are discovered through tile buckets, resident pairs are canonicalized, affinity/trust/tension and bounded encounter history persist, and need/stress/mental-break state influences outcomes.
- A seed- and clock-driven seasonal/weather model with Spring, Summer, Autumn and Winter; weather fronts change deterministically on stable time boundaries. Climate changes enter the world event history and alter wildlife forage, regional food growth, and the colony's ambient temperature.
- Rain and storms now create bounded, deterministic precipitation pulses in the colony's real water layer. Existing fluid movement, terrain, wall, and magma rules decide where that water can accumulate or flow; this does not claim a complete evaporation/hydrology model.
- Local heat exchange remains active between tiles and materials; external weather warms/cools exposed ground while walls insulate, doors leak more heat, and resident thermal exposure continues to drive stress, fatigue and physical trauma.
- Local Blight is a spreading organism rather than only a structure-damage flag: infected structures and organic ground seed nearby growth, rock/open water/deep water/magma block spread, and magma/water can cleanse infected ground. Residents standing in infected ground or buildings accumulate stress and record the exposure/recovery in their autobiographical memories.
- Strategic Blight fronts are now linked one-to-one with ecology hotspots in the overworld. Newly spawned hotspots become visible world entities in the same simulation tick; older saves without hotspot links reconnect deterministically on the next world step.
- Strategic Blight growth and front radius react to the authoritative season/weather model: wet and cold conditions suppress spread while heat and drought accelerate it. Local pressure continuously reduces wildlife forage and consumes nearby finite organic resource sites (food and wood); exhausting a site creates a persistent world event naming the lost reserve.
- Sustained strategic or macro Blight pressure now has a deterministic chance to establish a local foothold on exposed ground along the colony perimeter every ten simulated seconds. The local spread, natural water/magma barriers, structure damage, resident stress/memories, and colony save/load systems then handle the foothold; precise directional projection from each individual world front into a corresponding local entry point is still future work.
- Each resident has stable generated personality traits and a bounded autobiographical memory that records needs, hazards, mental breaks, work, construction, deliveries and social encounters. Recent experiences influence work and social-stress calculations.
- Body-part health and injury records persist per resident. Sustained severe heat or cold can cause torso trauma; leg injuries slow movement, arm/torso injuries reduce construction and production rates, and recovery occurs during safe rest.
- An event-driven incident manager converts relationship milestones into bounded, auditable incidents with per-pair cooldowns. Relationship and incident state are included in colony save schema v4; legacy schema v2/v3 saves initialize empty social state.
- A unified real-time coordinator (Simulation/WorldSimulation.cs) advances the colony and continuous world under one clock. The colony and strategic layer read and mutate the same settlement inventory; session saves store that inventory only in the world ledger.

This is a developing playable foundation, not a claim that every intended system is finished. RimWorld is used only as a reference for high-level colony-management patterns (categorised build orders, planning, demolition, and resident autonomy); KOR uses its own code, data definitions, interface, and visual assets. No RimWorld code or game assets are copied. The simulation has regression coverage for 10,000 individually represented citizen agents and a separate one-second smoke run with 5,000 complete colony-worker records. Citizen ID allocation is linear-time across bulk recruitment, and autonomous leisure pathfinding has a fixed per-planning-cycle budget. These are scalability guardrails, not proof of target frame rates for thousands of full residents: long-duration simulation, UI rendering, save size, multi-source route planning, and simulation level-of-detail still need profiling and dedicated optimization. The route cache reduces repeated identical searches but does not yet provide shared multi-source flow fields or prove target performance at 5,000–10,000 full residents. Remaining work includes deep performance testing, medical treatment and combat-connected injuries, richer thought/mood interactions, resident family/faction histories, capacity-aware stockpile zoning, expanded production chains and research progression, broader long-duration ecological feedback loops, detailed soil/hydrology simulation, and precise directional mapping from each strategic Blight front to a local colony entry point.

## Run on Linux

Install the .NET 8 SDK and the native runtime dependencies required by Gondwana/SDL3. From the repository root:

    dotnet build GondwanaGame.csproj --configuration Release
    dotnet run --project GondwanaGame.csproj --configuration Release -- --self-test
    ./build-linux.sh
    ./launch-kor.sh

The build script publishes a self-contained linux-x64 package under artifacts/linux-x64/. Generated output is ignored by Git. If the graphical application fails to start, launch it from a terminal to see the SDL/runtime error.

## Controls

The primary controls are visible mouse-operated menus in the right panel:

- **Pause / Resume** — pause or resume the shared real-time simulation.
- **Speed − / Speed +** — slow down or speed up the world clock.
- **Save / Load** — save or restore the current session.
- **Mouse-wheel zoom** — zoom continuously between the local settlement and continuous strategic world view; Zoom − / Zoom + remain available as buttons.
- **Wall / Floor / Path / Door / Conduit** — select a tile-construction mode, then left-click a tile.
- **Bench / Pod / Rack / Gen / Garden** — select a placeable object, then left-click a location.
- **Cancel** — cancel the active placement mode. Select an unfinished tile/object blueprint and use **Cancel Order** to remove it before construction starts.
- **Remove** — toggle demolition mode and click a completed player-built structure or built tile; starter habitat/stockpile are protected.
- **Left click** — at settlement scale, select a colonist or place a blueprint; selecting a colonist opens the contextual inspector with status and social tabs. At strategic scale, inspect entities or dispatch selected cargo to a settlement.
- **WORK** — open the per-colonist work-priority matrix. Each category uses priorities 0–4; 0 disables the category, and carried cargo is always delivered before unrelated work.
- **Right click** — at settlement scale, issue a colonist command; at strategic scale, order the selected Kor army or idle caravan to move in continuous world coordinates.
- **Strategic cargo controls** — choose Wood, Stone, Food or Alloy; select a caravan; then click a destination settlement. Cargo is deducted at dispatch and only credited on arrival.
- **Middle-mouse drag** — pan the camera.

No keyboard shortcut is required for core gameplay. The local map remains a square-tile settlement view; the wider world is being redesigned around continuous space rather than the old linked-hex campaign map.

## Source map

- Program.cs: SDL startup, input mapping, simulation stepping.
- Simulation/ColonySimulation.cs: current local settlement state and rules.
- WorldModel/ContinuousWorldState.cs: continuous world-space entities, canonical per-settlement inventories and storage capacities, conserved cargo shipments with travel time, spatial queries and the shared world clock.
- Simulation/ColonySaveService.cs: validated versioned JSON persistence.
- Simulation/GameSessionService.cs: atomic session persistence for the colony and continuous world; legacy campaign migration is intentionally unsupported.
- Rendering/ColonyRenderer.cs: presentation-only local square-grid and continuous strategic world views.
- Rendering/ContinuousWorldProjection.cs: continuous world coordinate/screen projection.
- Rendering/OrthogonalGridProjection.cs: screen/world coordinate conversion.
- Tests/: automated colony, campaign compatibility, and integration checks.
- build-linux.sh and launch-kor.sh: Linux package and launch scripts.
- .github/workflows/gondwana-game.yml: canonical build, tests, and Linux packaging.

## Engineering direction

Keep simulation logic independent of rendering and input. Extend the game through tested slices: canonical inventory and shipments, capacity-aware stockpile zoning, shared job scheduling, recipe catalogs, workplaces and needs, research unlocks, tactical incident resolution, and connected world-map/colony state. A successful build alone does not imply that the complete game is implemented.

## Seeded overworld generation

- The strategic world contains a generated biome layer across the full continuous world, with region boundaries and terrain colours visible when zoomed out.
- Each seed generates the Kor home settlement, five other factions, their settlement locations, guards, occasional caravans, 28–44 finite strategic resource sites, and 2–4 Blight fronts.
- Strategic resource-site entities store a resource kind and reserve amount; the map labels sites with that resource and its reserve.
- The same seed reproduces the same overworld placements and local spawn layout. A different seed changes placements, local rock scatter, deposit positions and deposit quantities.
- The starter settlement footprint stays readable and accessible while surrounding terrain/resources vary, so procedural generation does not bury the opening base.

## Unified real-time world

The WorldSimulation advances the colony and continuous world under one clock. Strategic settlement food production integrates continuously from elapsed time and respects shared storage capacity. The mouse-first UI exposes pause/resume, speed, save/load, zoom, construction, object selection, the colonist inspector, the work-priority matrix and cancel actions; middle-mouse drag pans the camera.

## Colony intelligence, logistics and storyteller

- The colonist inspector exposes current job/toil, route length, hunger, mood, stress, fatigue, carried cargo and available affinity/trust/tension relationships.
- The WORK matrix stores an independent 0–4 priority per colonist and work category. The scheduler respects those priorities while retaining work-order importance, needs, route checks and resource availability.
- `ReservationManager` reserves finite stack quantities and work targets, prevents over-reservation, releases reservations when jobs transition and reconstructs active gather reservations when a colony save is restored. Construction inputs are still committed atomically when the job is accepted.
- `WorldRoutePlanner` uses a deterministic 40×40 coarse grid with biome-weighted costs to estimate strategic route travel times separately from the local 40×26 colony A* grid.
- Rival factions run macro decisions every 20,000 strategic ticks. Their live settlement ledgers drive trade dispatch or defensive army generation; macro decision cadence is persisted with faction intelligence.
- The storyteller samples stored-resource and built-structure wealth every 2,000 colony ticks. Market-valued wealth produces a bounded 0–250 threat-point budget. Wealth incidents translate that budget into a deterministic number of strategic raider units that move toward Kor. As they enter the local engagement radius, raiders materialize as tactical combat entities, use the same deterministic combat engine as Kor defenders, and can leave salvage. Tactical unit combat is integrated; direct damage to local buildings remains unfinished.
- Blight escalation is deliberately independent of wealth-based raider incidents. A bounded threat model combines local hotspot pressure, macro-cell Blight presence and a scarcity term that rises as colony wealth falls. Every 6,000 strategic ticks it can generate a deterministic Blight assault; incident identity is represented in saved world entities, so reloads do not duplicate the same wave. Blight threat points are calculated from pressure, never reduced by a falling colony valuation. These forces move toward Kor on the strategic map and materialize as local Blight combatants when they enter the engagement radius. Local Blight can now establish perimeter footholds under sustained high strategic pressure, then spread over local ground and infected structures; a directionally accurate front-to-entry mapping remains unfinished.

The obsolete instant campaign transfer bridge and linked-hex campaign model have been removed. Colony construction, production, worker hauling and strategic shipments read or mutate the same settlement-owned resource ledger; a session snapshot does not duplicate the colony inventory. Settlement storage capacity is shared across local and strategic views, and deliveries wait at their destination if storage is full. The session format now represents the active unified world directly; old saves are intentionally not migrated. The current branch adds deterministic local Blight spread and seasonal climate effects, but a fully coupled Blight ecology and population-level strategic simulation remain unfinished.

Save / Load in the mouse menu saves or restores the current session. Run the self-test command shown above to check colony behavior, shared-ledger debits, capacity-aware storage, continuous production, the shared clock, shipment conservation, and session persistence.


## Threading, timeslicing and frame-budget rules

The colony and strategic world share consumed fixed-step time; drawing never owns simulation state. `WorldSimulation` processes at most eight 0.1-second fixed steps per call. If a frame stalls, the remaining simulation time stays in the accumulator and is consumed over later frames rather than triggering an unbounded catch-up loop or being silently discarded.

- **Macro economy and macro Blight pressure:** the 100×100 value-type cell array schedules a macro turn at each 24,000-tick boundary. The economy pass and the separate relief-donor selection pass each process at most 128 cells per consumed world step. Supply/demand accumulators update during the economy pass, with Blight population losses reconciled against an in-progress sample; turn completion only dispatches the already selected relief routes, applies bounded market prices and emits notifications. It does not run another sort or full-grid market scan.
- **Incident scan:** the separate strategic Blight/famine scan evaluates at most 128 cells per world step and resumes from a persisted cursor.
- **Strategic caravan routes:** route-cost data is copied into an immutable array before work is queued. Background A* tasks only read that snapshot and publish results through a `ConcurrentQueue`; at most `min(4, max(1, processor count / 2))` route tasks are active at once. Only the simulation thread applies completed routes or mutates caravan/world state. Errors produce a visible notification and deterministic direct-route fallback.
- **Arrival handoff:** arrivals are a pending-work queue capped at 256 entries. Once a caravan/refugee group is represented by local visitors, its arrival record is removed instead of being retained and sorted every tick. Overflow preserves the economic cargo in the abstract target cell or restores refugee population to its source, with a notification. Legacy consumed-arrival IDs are pruned when older saves are validated.
- **Local colony routes:** the 40×26 A* remains cached and runs synchronously on the simulation thread because its search space is small. This avoids creating background tasks for short local searches or allowing worker tasks to read mutable tile/worker objects.
- **Thread-safety rule:** never use `Parallel.For` to mutate live pawns, tiles, inventory or UI state. Future parallel economy work must first operate on immutable snapshots or disjoint output buffers, then commit results on the simulation thread.

These controls bound scheduled work; they are not a claim that 10,000 fully simulated local pawns or a specific FPS target has already been benchmarked. Macro population remains abstract outside the active colony.


## Deterministic colony simulation architecture

The colony simulation uses a fixed-step `TickManager` at **60 simulation ticks per simulated second**, independent of render FPS. The existing speed control scales how much simulation time advances; 3x therefore means 180 ticks per real-world second (360 would be 6x at a 60-tick base rate). A bounded catch-up loop prevents an application stall from triggering an unbounded simulation backlog.

- **Every tick:** worker movement and environmental exposure advance in small, deterministic steps.
- **Every 6 ticks (10 Hz):** job assignment and broader economy/ecosystem planning run.
- **Every 250 ticks (~4.17 simulated seconds):** social relationship updates run.
- **Every 2,000 ticks (~33.33 simulated seconds):** global storyteller checks run.
- **Pawn jobs:** tasks now expose ordered movement/work/completion toils, while the existing colony work system remains responsible for resource reservations and the actual action.
- **Pathfinding:** worker routes use four-neighbour A* with positive movement costs; path/floor/door tiles can have different traversal costs, and route caching remains in place.
- **Thing grid:** pawns, buildings, resource deposits and architecture are indexed by stable identity and map cell, with cell lookup, nearest-match queries, movement and removal. This is a migration bridge to a single authoritative entity model; existing simulation domain records still own their data during this stage.

These systems use original KOR project code and broad colony-simulation design patterns rather than copying proprietary implementation code or assets.


## Integrated tactical combat, industrial production and Nemesis system

The tactical simulation is coordinated by `WorldSimulation` on the shared fixed-step clock. It runs deterministic local combat for infantry, vehicles, heavy tanks, Blight beasts and titans; derives cover from local rock, walls, doors and completed objects; resolves ammunition, armor, damage and deaths; and emits short-lived symbolic shot traces. The renderer exposes those combatants and health values, and the **WAR ROOM** panel provides unit selection, target orders, deployment of real colony residents and automatic local target acquisition.

Strategic armies—including storyteller raiders and Blight assault units—materialize as local combatants when they approach the Kor settlement. Their stable world IDs are linked to local entities; defeats remove the corresponding strategic entity and queue salvage into the shared settlement ledger. Colony residents can be deployed as combatants; their worker jobs are suspended while deployed, their positions stay synchronized with the local combat map, and a combat casualty is removed from the active colony roster. Deployed links are restored from the session save rather than creating duplicate worker/citizen records.

The **WAR ROOM → WORKSHOP** panel uses the settlement's canonical inventory. Alloy and components remain the typed bulk resources; weapons, vehicle parts and Blight salvage are persisted as item stacks under the same storage-capacity limit. A completed local workbench is required to start a craft. Research projects use the saved macro-world research state, progress on the shared simulation clock, unlock the exact workshop recipe IDs, and remain consistent after loading. Craft inputs are debited at job start; outputs only enter storage at completion. If storage is full, completed output waits safely in its job rather than disappearing. Heavy-tank assembly requires the armored-vehicle research, completed workbench, chassis, engine, four armor-plate items and a cannon; the parts are committed only when an entity slot is available.

The **WAR ROOM → RESEARCH** panel can start eligible projects and displays prerequisite/complete states. The **WAR ROOM → COMBAT** panel provides a roster, health/ammunition state, Nemesis titles and explicit attack-order controls. Nemesis records track stable names and contextual titles, mutations, scars, power and combat kills.

Game session schema version 2 persists the canonical world and colony snapshots, active combat entities, structure integrity/infection, cover state, workshop jobs/counters, Nemesis records, the infection clock and pending combat salvage. Colony snapshot schema version 13 includes the resident's autonomous-activity scheduling window and preserves older schema readers, alongside the historical migrations for structure integrity, personality, life memory and body state. The unified `--self-test` suite covers shared inventory debits, real workbench construction, research unlocks, strategic-army materialization, resident deployment, siege damage, spreading local Blight, Blight exposure stress/memories, seasonal weather transitions, climate-driven colony temperature, paced wildlife populations, resident autonomy, material-consuming repairs and round-trip persistence.

Unopposed hostile combatants now besiege the nearest local building or architectural tile. Blight-affiliated attackers also infect the struck structure; untreated infection causes recurring damage every ten simulated seconds. The **WAR ROOM → REPAIR ALL** control spends wood, stone or alloy for repairs and one component per infected structure to cleanse it. Habitat and stockpile can be critically damaged but stay recoverable, so a siege cannot irreversibly softlock the settlement through destruction of its core infrastructure.
