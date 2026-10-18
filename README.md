# KOR ETERNAL FRONTIER

Kor Eternal Frontier is a developing colony, civilization, and world simulation. The intended game centers on autonomous population, construction, logistics, production, research, regional development, and the evolving Blight ecosystem—not conventional RTS unit combat.

## Current repository state

This is an early playable foundation, not the complete game.

- The canonical application is the root .NET 8 project: GondwanaGame.csproj.
- The active local view is a square-tile, orthogonal top-down map.
- The current local scenario includes a worker, a Guardian, resource deposits, purifier construction, and deterministic Blight spread.
- A separate deterministic campaign model contains a small regional map, development, research, turns, battle setup/reconciliation, and campaign save/load checks. It is not yet connected to the main game's screen flow.
- Generic tile-by-tile construction, a separate multi-tile object system, a shared job board, stockpiles/reservations/hauling, multiple production chains, a population needs simulation, an expanded research tree, and a large-scale world map are still implementation work.

The Unity prototype, duplicated project implementation, redundant art/metadata trees, art-generation pipeline, and checked-in .NET build outputs have been removed from the active source tree. The first visual pass deliberately uses simple shapes rather than polished art.

## Run on Linux

Install the .NET 8 SDK and the native runtime dependencies required by Gondwana/SDL3. From the repository root, run:

    dotnet build GondwanaGame.csproj --configuration Release
    dotnet run --project GondwanaGame.csproj --configuration Release -- --self-test
    ./build-linux.sh
    ./launch-kor.sh

The build script publishes a self-contained linux-x64 package to artifacts/linux-x64/. Generated output is ignored by Git. If the UI does not start, launch from a terminal to see the SDL/runtime error.

## Controls in the current scenario

- W/A/S/D: move the camera.
- + / -: zoom.
- Left-click: select the worker or Guardian.
- Right-click: command the selected unit to move or work at a reachable resource node.
- B: enter purifier placement mode.
- Esc: cancel placement mode.

These controls describe the current scenario only. They will evolve as colony management, selection, construction, and the strategic map are implemented.

## Source map

- Program.cs: application startup, SDL input, fixed-step host loop.
- Simulation/: current local simulation rules and balance configuration.
- Campaign/: strategic world state, turn rules, procedural region setup, and campaign save data.
- Rendering/OutpostRenderer.cs: presentation-only square-grid local view.
- Rendering/OrthogonalGridProjection.cs: screen/world coordinate conversion; no simulation rules.
- Tests/: dependency-free simulation and campaign checks.
- Data/Balance/scenario.json: starter scenario values.
- build-linux.sh and launch-kor.sh: local package and launch scripts.
- .github/workflows/gondwana-game.yml: canonical build, tests, and Linux packaging.

## Development principles

Keep simulation logic independent of rendering and input. Add new systems in tested slices: explicit tile state, tile-level architecture jobs, placeable objects, inventories and reservations, hauling, worker task selection, production, population needs, persistence, and a connected world-map loop. A successful build alone does not imply that the complete game is implemented.
