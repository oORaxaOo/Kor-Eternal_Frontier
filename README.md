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
- A unified real-time coordinator (`Simulation/WorldSimulation.cs`) that currently shares a clock between colony simulation and the temporary legacy regional model. This is an interim migration layer, not the target continuous world.

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
- **Left click** — at settlement scale, select a colonist or place a blueprint; at strategic scale, inspect a world entity.
- **Right click** — at settlement scale, issue a colonist command; at strategic scale, order the selected Kor army or caravan to move in continuous world coordinates.
- **Middle-mouse drag** — pan the camera.

No keyboard shortcut is required for core gameplay. The local map remains a square-tile settlement view; the wider world is being redesigned around continuous space rather than the old linked-hex campaign map.

## Source map

- Program.cs: SDL startup, input mapping, simulation stepping.
- Simulation/ColonySimulation.cs: current local settlement state and rules.
- WorldModel/ContinuousWorldState.cs: continuous world-space entities, spatial queries, travel-time movement and world clock state.
- Simulation/ColonySaveService.cs: validated versioned JSON persistence.
- Campaign/: deterministic strategic model and campaign save data.
- Rendering/ColonyRenderer.cs: presentation-only local square-grid and continuous strategic world views.
- Rendering/ContinuousWorldProjection.cs: continuous world coordinate/screen projection.
- Rendering/OrthogonalGridProjection.cs: screen/world coordinate conversion.
- Tests/: automated colony and campaign checks.
- build-linux.sh and launch-kor.sh: Linux package and launch scripts.
- .github/workflows/gondwana-game.yml: canonical build, tests, and Linux packaging.

## Engineering direction

Keep simulation logic independent of rendering and input. Extend the game through tested slices: material reservations and stockpiles, shared job scheduling, recipe catalogs, workplaces and needs, real research unlocks, and connected world-map/colony state. A successful build alone does not imply that the complete game is implemented.


## Unified real-time world migration

The new `WorldSimulation` coordinates colony and temporary legacy regional updates under one clock. The mouse-first UI now exposes pause/resume, speed, save/load, zoom, construction, object selection and cancel actions; middle-mouse drag pans the camera. The old campaign map remains legacy scaffolding and is not the target world representation.

This is the first migration slice, not the finished unified-world architecture. Regional resources are still held in a separate legacy campaign state, and the old transfer bridge remains in code for migration/tests but is no longer exposed as a gameplay keyboard control. The continuous world-space entity model is now rendered at strategic zoom, with mouse selection/inspection, middle-drag navigation, and movement orders for the seeded Kor army and caravan. The legacy regional production/Blight rules and separate strategic resources are still adapters to replace; canonical resource ownership and real cargo shipments remain unfinished. The old nine-region linked-hex map must not be extended as the new world.

**Save / Load** in the mouse menu save or restore the current session. Run `dotnet run --project GondwanaGame.csproj --configuration Release -- --self-test` to check colony behavior, regional progression, the shared clock, resource accounting, and session persistence.
