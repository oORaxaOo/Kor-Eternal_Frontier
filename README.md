# KOR ETERNAL FRONTIER

Kor Eternal Frontier is a developing colony, civilization, and world simulation. The intended game centers on autonomous population, construction, logistics, production, research, regional development, and the evolving Blight ecosystem—not conventional RTS unit combat.

## Current foundation

The canonical application is the root .NET 8 project. The Unity prototype, duplicate nested project, legacy isometric artwork pipeline, duplicated art/metadata folders, and checked-in build output have been removed from the active source tree.

The local map is a 40 by 26 orthogonal square grid. The current simulation foundation has:

- Three independently simulated colonists with movement, carrying, tasks, and hunger.
- Finite resource deposits and real gather/carry/deliver trips that conserve resource quantities.
- Capacity-limited colony inventory: the completed starter stockpile provides 120 units; each completed storage rack adds 80. Gathering pauses when no space remains, and workers retain undelivered cargo until space becomes available.
- Tile-by-tile architecture design with an independent construction state and work order per tile.
- Separate placeable-object blueprints with multi-tile footprints and real construction work.
- Inventory-based material costs for both construction workflows.
- A workstation recipe that consumes alloy and wood when queued; a worker performs the job before a component is added to inventory.
- A minimal symbolic renderer: square tiles and buildings, colonists as dots, resource markers as squares.
- Versioned colony save/load for terrain, architecture orders, objects, colonists, cargo, tasks/routes, deposits, inventory, production orders, IDs, selection, and simulation time.
- A unified real-time coordinator (Simulation/WorldSimulation.cs) advances the colony and continuous world under one clock. The colony and strategic layer read and mutate the same settlement inventory; session saves store that inventory only in the world ledger.

This is a playable foundation, not the complete game. A reusable central job board, material reservations, capacity-aware stockpiles, multiple production chains, fuller population simulation, research unlocks, a connected strategic map, and a deeper Blight ecosystem remain future work.

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
- **Zoom − / Zoom +** — continuously zoom between the local settlement and continuous strategic world view.
- **Wall / Floor / Path / Door / Conduit** — select a tile-construction mode, then left-click a tile.
- **Bench / Pod / Rack / Gen / Garden** — select a placeable object, then left-click a location.
- **Cancel** — cancel the active placement mode.
- **Left click** — at settlement scale, select a colonist or place a blueprint; at strategic scale, inspect entities or dispatch selected cargo to a settlement.
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

Keep simulation logic independent of rendering and input. Extend the game through tested slices: canonical inventory and shipments, material reservations and stockpiles, shared job scheduling, recipe catalogs, workplaces and needs, research unlocks, and connected world-map/colony state. A successful build alone does not imply that the complete game is implemented.

## Unified real-time world migration

The WorldSimulation advances the colony and continuous world under one clock; the legacy campaign turn counter no longer advances during active play. Strategic settlement food production integrates continuously from elapsed time. The mouse-first UI exposes pause/resume, speed, save/load, zoom, construction, object selection and cancel actions; middle-mouse drag pans the camera. The old campaign map remains legacy scaffolding and is not the target world representation.

The obsolete instant campaign transfer bridge and linked-hex campaign model have been removed. Colony construction, production, worker hauling and strategic shipments read or mutate the same settlement-owned resource ledger; a session snapshot does not duplicate the colony inventory. Settlement storage capacity is shared across local and strategic views, and deliveries wait at their destination if storage is full. The session format now represents the active unified world directly; old saves are intentionally not migrated. A dynamic Blight ecosystem and population-level strategic simulation remain unfinished.

Save / Load in the mouse menu saves or restores the current session. Run the self-test command shown above to check colony behavior, shared-ledger debits, capacity-aware storage, continuous production, the shared clock, shipment conservation, and session persistence.
