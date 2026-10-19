# Kor Eternal Frontier — agent instructions

## Canonical project
- The only active application is the root .NET 8 project.
- The Unity prototype and duplicate nested Gondwana project have been removed. Do not recreate them.
- The local colony mechanics currently live in Simulation/ColonySimulation.cs. Campaign/ contains a temporary legacy model and is not the target architecture for the world simulation.
- Rendering/input may translate screen positions into world coordinates. Simulation APIs must not depend on SDL, Gondwana, SkiaSharp, windows, camera offsets, or screen pixels.
- The local view is orthogonal top-down with square tiles. People are dots; objects use square footprints; hostile creatures use triangle markers. No isometric projection or polished art pipeline is active.

## Product requirements — preserve these constraints
- Mouse-first is mandatory: every core action must be reachable through visible clickable menus or contextual mouse interactions. Keyboard shortcuts are optional conveniences only, never required gameplay input.
- The previous nine-region linked-hex campaign map is legacy scaffolding and is not a foundation to extend. Do not add new world features based on hex adjacency, region-hop movement, or end-turn completion.
- The target world uses continuous world-space coordinates and spatial queries. Zooming/panning changes detail level and camera presentation, not the simulation mode or underlying world state.
- One real-time clock, one canonical resource ownership model, travel-time movement/shipments, and one versioned world save are the target. No instant colony/world resource transfers in the final architecture.
- Preserve the local settlement's square-tile building grid where useful; that local grid must not dictate the representation of the wider world.

## Commands
- Build: dotnet build GondwanaGame.csproj --configuration Release
- Rules and campaign checks: dotnet run --project GondwanaGame.csproj --configuration Release -- --self-test
- Linux package: ./build-linux.sh

## Engineering rules
- Simulation systems must be testable and fixed-step behavior independent of frame rate.
- Keep resource accounting explicit; never create or duplicate inventory implicitly.
- A blueprint is not a completed building.
- Keep per-tile architecture orders distinct from placeable furniture/machine objects with footprints.
- A worker gathering a resource must reduce the source amount, carry it, and deliver it before inventory increases.
- Built stockpiles and storage racks provide explicit capacity; gathering must stop at full capacity, partial delivery must preserve cargo, and production may only commit outputs when space exists.
- Production must commit inputs and add outputs only when work completes.
- Saved data must have schema versioning, structural validation, and round-trip tests.
- Preserve deterministic behavior where practical; separate presentation concerns from simulation rules.
- Self-test must execute colony and campaign checks.
- Prefer small, tested changes over replacing unrelated working code.
- Do not commit generated build output, temporary files, downloaded assets, or Python caches.
- Work on feature branches. Preserve unrelated changes and avoid destructive history rewrites.
- Never claim a build, graphical launch, or test succeeded without running it and observing the result.
