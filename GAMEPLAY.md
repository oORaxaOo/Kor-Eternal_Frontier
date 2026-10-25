# Gameplay scope and current implementation

## Long-term direction

Kor Eternal Frontier is a colony, civilization, and world simulation. Population, construction, work, transport, inventory, production, research, regional development, and The Blight should influence one another.

The local map uses orthogonal 2D square tiles. Placeholder symbols prioritize readability: people are dots, buildings and vehicles are squares, and hostile creatures use triangle markers.

Architectural features such as walls, floors, doors, paths, and conduits are designated per tile. Furniture, workstations, storage, and machines are placed as independent footprint-based objects. Both workflows need shared material accounting and worker tasks without conflating placement semantics.

## Currently implemented

- A 40 x 26 square tile map.
- Three colonists with independent position, movement task, cargo state, and hunger.
- Finite resource deposits.
- Real gather/carry/deliver trips that transfer resource from source deposit to inventory.
- Individual per-tile architecture construction orders with costs, work progress, and completion state.
- Footprint-based multi-tile object blueprints that remain unfinished until workers complete construction.
- Inventory-based construction and production costs.
- A functional production recipe: a completed workbench consumes alloy and wood and creates a component only after worker labor.
- Minimal orthogonal top-down rendering.
- Versioned local colony saves with structural validation and round-trip tests.
- A `WorldSimulation` coordinator that advances the local colony and regional world under one real-time clock, with pause/speed support and automatic regional updates.

## Canonical simulation clock and system phases

The active runtime now advances all world and colony systems from one authoritative integer 60 Hz clock. Each fixed tick has a stable phase order: local colony and resident simulation, pre-world incident/ledger checks, continuous world and macro time, strategic arrival and Blight processing, tactical combat, combat consequences, and research/workshop progression. Planning, incident checks, strategic arrivals, research, and workshop work run on deterministic 10 Hz boundaries; they no longer depend on render-frame chunking. Loading a saved session reconstructs the master tick from canonical world time and reattaches the colony to it. The clock and phase scheduler have dedicated deterministic self-tests.

## Known gaps

The active game uses `WorldSimulation` as the shared clock and coordinates the local square-tile colony, continuous strategic world, macro simulation, research, tactical combat and workbench jobs. The gameplay interface is mouse-first: visible controls expose pause/resume, speed, save/load, zoom, construction, object placement and cancellation; middle-mouse drag pans the camera. Colony and world views have separate zoom thresholds; middle-mouse drag pans the active camera. The continuous world owns settlement inventories and shipments, while combatants that arrive near Kor are represented in the local combat simulation. Version 2 unified session saves preserve colony/world state plus active combatants, cover, workshop jobs, Nemesis records and pending salvage. The older version 1 session format remains readable and initializes the combat runtime empty.

## Implementation priority

1. Extend weapon/vehicle loadouts and recipe balancing without bypassing the canonical stockpile or technology gates.
2. Expand combat tactics—retreat, cover seeking, reload resupply and vehicle crew assignment—while keeping the deterministic single-writer simulation.
3. Continue the colony/strategic population migration only where it preserves stable identity, resource conservation and save compatibility.


## Live colony ecosystem integration (migration slice)

`Simulation/ColonyEcosystem.cs` is now stepped by the active `ColonySimulation`. Every visible colonist has a corresponding citizen agent; citizen hunger is synchronized with the existing save-compatible worker model, food is consumed from bounded grid storage, and citizen mood/state are shown in the HUD. Completed Garden objects register capacity-limited food-production jobs with the live job broker. Garden output reserves grid storage before work begins and is reconciled with the colony's existing pooled inventory capacity.

This is an incremental migration, not a wholesale replacement of the established construction/gathering scheduler: non-food materials and construction/recipe jobs still use the legacy inventory and task pipeline. The food grid maps the starter stockpile and completed storage-rack footprints to bounded storage slots while respecting the existing pooled inventory cap; all-resource grid ownership remains follow-up work. Existing save schemas are unchanged; the runtime ecosystem is rebuilt from restored worker and inventory state. See `docs/colony-systems.md` for core contracts and migration boundaries.

## Mouse-first controls

- **Pause / Resume** — pause or resume the shared simulation clock.
- **Speed − / Speed +** — change the world speed between 0.25x and 4x.
- **Save / Load** — persist or restore the current session.
- **Zoom − / Zoom +** — change camera zoom.
- **Wall / Floor / Path / Door / Conduit** — select a tile architecture mode.
- **Bench / Pod / Rack / Gen / Garden** — select a placeable object.
- **Cancel** — exit the active placement mode.
- **Left click** — select colonists or place blueprints; **right click** issues a colonist command; **middle-mouse drag** pans the camera.

The world-view cargo controls select Wood, Stone, Food or Alloy; select a Kor caravan and click a destination settlement to dispatch a capacity-aware shipment. World movement uses right-click commands when a strategic army or caravan is selected.

The deterministic self-test checks colony construction/logistics, resource conservation, regional generation, strategic movement, research-gated production, combat simulation, workshop ledger debits, resident deployment, Nemesis state, structure siege damage, recurring Blight infection, material-consuming repairs and unified-session round-trips.

The **WAR ROOM** button opens the three connected systems:
- **COMBAT:** inspect combatants, select a Kor unit and click an enemy entry to issue an attack order, or deploy eligible colonists as defenders. Deployed residents stop taking colony jobs and stay linked to their combat entities; combat casualties are removed from the active colony roster. Hostiles without an active fighting target besiege the nearest building or architectural tile. Blight attacks infect structures, which then lose integrity every ten simulated seconds.
- **WORKSHOP:** craft researched weapons and vehicle parts from the canonical settlement store. A completed workbench is required, inputs are deducted at job start, outputs are added at completion, and completed outputs wait if storage is full. Assemble a heavy tank when the armored-vehicle technology and all required parts are available.
- **RESEARCH:** start a project only after its prerequisites are complete. Research progresses with the unified simulation clock and unlocks matching workshop recipes.

Local tactical combat integrates automatic target acquisition, attack orders, ammunition, cover, armor, unit health, siege damage, structural health, Blight infection and Nemesis titles. Use **WAR ROOM → REPAIR ALL** to spend shared settlement resources on damaged structures; infected structures additionally require components to cleanse. Habitat and stockpile can be critically damaged but remain repairable. More advanced tactics—retreat, seeking cover, reloading/resupply and crew assignment—remain future work.

