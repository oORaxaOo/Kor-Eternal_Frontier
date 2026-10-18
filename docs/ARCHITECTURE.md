# Kor Eternal Frontier — architecture baseline

## Active application

One canonical application exists at the root, targeting .NET 8. Gondwana is the game host; SDL3 supplies window/input and SkiaSharp draws presentation. Simulation and persistence checks run without a game window.

## Dependency direction

    Program / SDL input
             |
             v
    OrthogonalGridProjection ---> ColonyRenderer
             |                        ^
             v                        |
    ColonySimulation (logical state and fixed-step rules)
             |
             +-- tiles, construction, objects, colonists, deposits,
                 hauling, inventory, production, needs and persistence

    CampaignSimulation / CampaignState / CampaignSaveService
             |
             +-- deterministic strategic model
                 (currently tested separately from the local colony)

The projection layer owns screen/world mapping. Gameplay APIs receive logical world coordinates. Rendering reads state but does not decide simulation rules.

## Implemented mechanics

- 40 by 26 orthogonal square-tile grid.
- Three colonists with movement, task state, carrying, and hunger.
- Finite deposits and resource-conserving gather/carry/deliver transfers.
- Independent per-tile architecture orders with material costs and work progress.
- Separate multi-tile placeable objects with validated footprints and construction work.
- A workstation recipe that consumes real inventory and yields a component after labor.
- Versioned, validated colony save/load for tiles, jobs/routes, workers, deposits, inventory, construction, production and clock.
- Minimal symbol-based top-down rendering.

## Remaining integration work

- Extract worker scheduling into a reusable central job board and durable task ownership.
- Material reservations and capacity-aware stockpiles.
- General recipe catalogs, workplace queues and multiple production chains.
- Broader needs, housing, work eligibility, skills and social relationships.
- Research unlocks wired to local buildings, production and systems.
- Campaign screen connected to the colony state and real regional resource/population flows.
- Large world generation and settlement transitions.
- Dynamic multi-stage Blight ecosystem.

## Design constraints

- Logical state belongs to simulation; screen transforms and drawing belong to presentation.
- Tile coordinates and object footprints use consistent zero-based grid positions.
- Resources must be conserved across transfer, construction, production, consumption, and persistence.
- Saved data must have explicit schema version and validated references.
- Deterministic tests must cover actual mechanics, not only application startup.
- Graphics remain minimal until integrated mechanics are legible.
