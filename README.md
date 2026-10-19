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
- A first unified real-time simulation coordinator (`Simulation/WorldSimulation.cs`) that owns the colony and regional world, advances both from one clock, supports pause/speed controls, and automatically advances regional systems on elapsed-time intervals. The strategic model still has legacy resource fields and is being migrated incrementally.

This is a playable foundation, not the complete game. A reusable central job board, material reservations, capacity-aware stockpiles, multiple production chains, fuller population simulation, research unlocks, a connected strategic map, and a deeper Blight ecosystem remain future work.

## Run on Linux

Install the .NET 8 SDK and the native runtime dependencies required by Gondwana/SDL3. From the repository root:

    dotnet build GondwanaGame.csproj --configuration Release
    dotnet run --project GondwanaGame.csproj --configuration Release -- --self-test
    ./build-linux.sh
    ./launch-kor.sh

The build script publishes a self-contained linux-x64 package under artifacts/linux-x64/. Generated output is ignored by Git. If the graphical application fails to start, launch it from a terminal to see the SDL/runtime error.

## Controls

- W/A/S/D: move camera.
- + / -: zoom.
- Left click: place selected tile architecture/object or select a colonist.
- Right click: move the selected colonist; right-click a resource deposit to gather and deliver.
- 1: wall, 2: floor, 3: path, 4: door, 5: conduit.
- O: toggle object placement; Tab: cycle object type.
- Space: pause/resume the unified world simulation.
- F5: save colony to `~/.local/share/kor-eternal-frontier/colony.json` (or `$XDG_DATA_HOME/kor-eternal-frontier/colony.json`).
- F9: load the colony save.
- The HUD shows used storage versus available capacity; complete storage racks to expand it.
- Regional development now advances automatically while the simulation runs; there is no player-issued campaign-turn key. World speed controls are exposed by the simulation coordinator and will be surfaced in the UI in a later slice.
- Esc: cancel placement mode.

## Source map

- Program.cs: SDL startup, input mapping, simulation stepping.
- Simulation/ColonySimulation.cs: current colony state and rules.
- Simulation/ColonySaveService.cs: validated versioned JSON persistence.
- Campaign/: deterministic strategic model and campaign save data.
- Rendering/ColonyRenderer.cs: presentation-only square-grid view.
- Rendering/OrthogonalGridProjection.cs: screen/world coordinate conversion.
- Tests/: automated colony and campaign checks.
- build-linux.sh and launch-kor.sh: Linux package and launch scripts.
- .github/workflows/gondwana-game.yml: canonical build, tests, and Linux packaging.

## Engineering direction

Keep simulation logic independent of rendering and input. Extend the game through tested slices: material reservations and stockpiles, shared job scheduling, recipe catalogs, workplaces and needs, real research unlocks, and connected world-map/colony state. A successful build alone does not imply that the complete game is implemented.


## Unified real-time world migration

The new `WorldSimulation` coordinates colony and regional updates under one clock. Regional economy, army orders, upkeep, and Blight progression are advanced automatically at elapsed-time intervals; the player no longer presses **T** to end a campaign turn. Pause/resume applies to the shared clock. A bounded speed multiplier API is implemented and covered by self-tests, though speed selection is not yet exposed in the UI.

This is the first migration slice, not the finished unified-world architecture. Regional resources are still held in the legacy campaign state, and **I/E** remain temporary legacy transfer controls while resource ownership is migrated into shared world entities. The next slices must replace those transfers with real shipments, integrate regional settlements and population, unify save data without competing subsystem snapshots, and replace the nine-region campaign map with a scale level of the same world.

**F5 / F9** save or restore the current session. Run `dotnet run --project GondwanaGame.csproj --configuration Release -- --self-test` to check colony behavior, regional progression, the shared clock, resource accounting, and session persistence.
