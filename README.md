# KOR ETERNAL FRONTIER

Kor Eternal Frontier is a developing colony, civilization, and world simulation. The goal is Dwarf-Fortress-inspired simulation depth with a deliberately legible, low-detail symbolic presentation: residents should have persistent lives and needs, logistics should emerge from real constraints, and wildlife, settlements and the Blight should evolve even when the player is looking elsewhere. This is an inspiration target, not a verbatim copy of Dwarf Fortress assets or interface. The game centers on autonomous population, construction, logistics, production, research, regional development, and an evolving Blight ecosystem—not conventional RTS unit combat.

## Current foundation

The canonical application is the root .NET 8 project. The Unity prototype, duplicate nested project, legacy isometric artwork pipeline, duplicated art/metadata folders, and checked-in build output have been removed from the active source tree.

The local map is a 40 by 26 orthogonal square grid. A world seed now drives both the strategic overworld and its local spawn map: terrain scatter, resource-deposit positions/amounts, regional biomes, faction settlements, armies, caravans, Blight fronts and strategic resource sites are reproducible for the same seed and differ across seeds. The starter habitat and stockpile remain in a protected starting area so a new run is playable. The current simulation foundation has:

- Three independently simulated colonists with movement, carrying, tasks, and hunger.
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
- An event-driven incident manager converts relationship milestones into bounded, auditable incidents with per-pair cooldowns. Relationship and incident state are included in colony save schema v4; legacy schema v2/v3 saves initialize empty social state.
- A unified real-time coordinator (Simulation/WorldSimulation.cs) advances the colony and continuous world under one clock. The colony and strategic layer read and mutate the same settlement inventory; session saves store that inventory only in the world ledger.

This is a playable foundation, not the complete game. RimWorld is used only as a reference for high-level colony-management patterns (categorised build orders, planning, demolition, and resident autonomy); KOR uses its own code, data definitions, interface, and visual assets. No RimWorld code or game assets are copied. The simulation now has a regression check for 2,000 individually represented citizen agents, and citizen ID allocation is linear-time across bulk recruitment. This is an early scalability guardrail, not a claim that thousands of complete colony workers already run at target frame rates: job allocation, UI rendering, save size, and simulation level-of-detail still need profiling and dedicated optimization. The new route cache reduces repeated identical searches; it does not yet provide shared multi-source flow fields or prove target performance at 5,000–10,000 full residents. Richer social interaction types, resident family/faction histories, capacity-aware stockpile zoning, multiple production chains, research unlocks, a fully interactive combat resolution layer, and a deeper Blight ecosystem remain future work.

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
- The storyteller samples stored-resource and built-structure wealth every 2,000 colony ticks. Market-valued wealth produces a bounded 0–250 threat-point budget. Wealth incidents translate that budget into a deterministic number of strategic raider units that move toward Kor. This is an initial incident layer; raiders do not yet resolve tactical combat or damage local buildings.

The obsolete instant campaign transfer bridge and linked-hex campaign model have been removed. Colony construction, production, worker hauling and strategic shipments read or mutate the same settlement-owned resource ledger; a session snapshot does not duplicate the colony inventory. Settlement storage capacity is shared across local and strategic views, and deliveries wait at their destination if storage is full. The session format now represents the active unified world directly; old saves are intentionally not migrated. A dynamic Blight ecosystem and population-level strategic simulation remain unfinished.

Save / Load in the mouse menu saves or restores the current session. Run the self-test command shown above to check colony behavior, shared-ledger debits, capacity-aware storage, continuous production, the shared clock, shipment conservation, and session persistence.


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
