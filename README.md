# KOR ETERNAL FRONTIER

Kor Eternal Frontier is a developing colony, civilization, and world simulation. The game is intended to focus on autonomous population, construction, logistics, production, research, regional development, and the evolving Blight ecosystem—not conventional RTS unit combat.

## Current foundation

The canonical application is the root .NET 8 project. The old Unity prototype, duplicate nested project, legacy isometric artwork pipeline, duplicated art/metadata folders, and checked-in build output have been removed from the active source tree.

The local map is a 40 by 26 orthogonal square grid. The current simulation foundation has:

- Three independently simulated colonists with movement and hunger.
- Resource deposits and real gather/carry/deliver trips that conserve resource quantities.
- Tile-by-tile architecture design, with an independent construction status and work order per tile.
- Separate placeable-object blueprints with multi-tile footprints and real construction work.
- Inventory-based material costs for both construction workflows.
- A workstation recipe that consumes alloy and wood and produces components after a worker completes its labor.
- A minimal symbolic renderer: square tiles and buildings, colonists as dots, resources as marked squares.
- A retained deterministic campaign model with regional development, research, turn processing, battle reconciliation, and save/load checks. That strategic model is not yet wired into the active game screen flow.

This is a functioning foundation, not the complete game. A generic extensible job board, production catalog, proper stockpile reservation system, save/load for the local colony, a full research tree, broader population simulation, a large world map, campaign-to-colony transitions, and a deeper Blight ecosystem remain future work.

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
- Left click: place the selected tile architecture/object or select a colonist.
- Right click: move the selected colonist; right-click a resource deposit to gather and deliver its materials.
- 1: wall, 2: floor, 3: path, 4: door, 5: conduit.
- O: toggle object placement; Tab: cycle object type.
- Space: pause/resume simulation.
- Esc: cancel the current placement mode.

## Source map

- Program.cs: SDL startup, input mapping, simulation stepping.
- Simulation/ColonySimulation.cs: current colony state and gameplay rules.
- Campaign/: deterministic strategic model and campaign save data.
- Rendering/ColonyRenderer.cs: presentation-only square-grid view.
- Rendering/OrthogonalGridProjection.cs: screen/world coordinate conversion.
- Tests/: automated colony and campaign checks.
- build-linux.sh and launch-kor.sh: Linux package and launch scripts.
- .github/workflows/gondwana-game.yml: canonical build, tests, and Linux packaging.

## Engineering direction

Keep simulation logic independent of rendering and input. Extend the game through tested, integrated slices: richer tile definitions, material reservations and stockpiles, shared job scheduling, multiple production recipes, needs/workplaces, persistence, research unlocks, and connected world-map/colony state. A successful build alone does not imply that the complete game is implemented.
