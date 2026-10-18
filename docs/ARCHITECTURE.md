# Kor Eternal Frontier — architecture baseline

## Active application

There is one canonical application at the repository root targeting .NET 8. Gondwana provides the game host and rendering surface; SDL3 supplies window and input integration, and SkiaSharp draws the presentation. Deterministic simulation checks run without creating a window.

## Dependency direction

    Program / SDL input
             |
             v
    OrthogonalGridProjection ---> ColonyRenderer
             |                        ^
             v                        |
    ColonySimulation (logical state, fixed-step rules)
             |
             +-- tile construction, placeable-object blueprints,
                 colonists, deposits, hauling, inventory and production

    CampaignSimulation / CampaignState / CampaignSaveService
             |
             +-- separate deterministic strategic model;
                 tested, not yet wired to colony screens

The projection layer owns screen/world mapping. Gameplay APIs receive logical grid coordinates. The renderer reads state and does not advance or decide simulation rules.

## Implemented mechanics in the current slice

- A 40 by 26 tile grid with passable ground and explicit rock blocking.
- Three colonists with movement orders and a hunger value that affects work/movement.
- Finite resource deposits and a real gather/carry/deliver cycle.
- Inventory quantities with atomic cost checks.
- Per-cell architecture plans, each with its own state, cost, work progress, and completion.
- Separate multi-cell placeable objects with validated footprints, materials, work requirements, and completion state.
- One actual production recipe: a completed workstation consumes alloy and wood and creates a component after worker labor.
- Orthogonal top-down presentation with shapes and markers.

## Not yet implemented or integrated

- A reusable central job board and durable job ownership.
- Resource reservations and capacity-aware stockpiles.
- General recipe catalogs and multiple production chains.
- Local colony save/load and schema migration.
- Workplace assignments, skills, social relationships, and richer needs.
- Research unlocks connected to local mechanics.
- Strategic campaign screen flow connected to the same local world state.
- Large procedural world map and region-to-colony resource flows.
- Dynamic multi-stage Blight ecosystem.

## Required next slices

1. Extract job assignment and resource reservations into testable services without changing the verified gameplay behavior.
2. Generalize recipes, workplace queues, and stockpile capacity.
3. Add validated local colony persistence and save/load round-trip tests.
4. Add research projects that unlock real buildings/recipes/rules.
5. Connect campaign regions to local settlement state.
6. Expand colonist needs, housing, work eligibility and the Blight after shared contracts are stable.

## Design constraints

- Logical state belongs to simulation; screen transforms and drawing belong to presentation.
- Tile positions and object footprints use consistent zero-based coordinates.
- Resources are conserved across transfer, construction, production, and consumption.
- IDs and persistence schema versions must be stable and validated.
- Deterministic tests must cover actual mechanics, not only successful application startup.
- Graphics remain minimal until the simulation mechanics are legible and integrated.
