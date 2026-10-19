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

    CampaignSimulation / CampaignState
             |                 ^
             v                 |
    CampaignColonyBridge ------+
             |
             +-- explicit Supplies↔Food and Alloy↔Alloy transfers
                 with local capacity checks and exact debits

    GameSessionService
             |
             +-- validated atomic session envelope containing both save schemas

The projection layer owns screen/world mapping. Gameplay APIs receive logical world coordinates. Rendering reads state but does not decide simulation rules.

## Implemented mechanics

- 40 by 26 orthogonal square-tile grid.
- Three colonists with movement, task state, carrying, and hunger.
- Finite deposits and resource-conserving gather/carry/deliver transfers.
- Aggregate inventory capacity supplied by completed stockpiles (120 units each) and storage racks (80 units each).
- Capacity-aware gathering, partial delivery, retained cargo while storage is full, and production output that waits for room.
- Independent per-tile architecture orders with material costs and work progress.
- Separate multi-tile placeable objects with validated footprints and construction work.
- A workstation recipe that consumes real inventory and yields a component after labor.
- Versioned, validated colony save/load for tiles, jobs/routes, workers, deposits, inventory, construction, production and clock.
- Minimal symbol-based top-down rendering.

## Remaining integration work

- Extract worker scheduling into a reusable central job board and durable task ownership.
- Explicit material reservations and physically distinct stockpile locations.
- General recipe catalogs, workplace queues and multiple production chains.
- Broader needs, housing, work eligibility, skills and social relationships.
- Research unlocks wired to local buildings, production and systems.
- Replace the temporary CampaignSimulation/CampaignState linked-hex model with continuous world-space entities, canonical resources, spatial movement and travel-time shipments.
- Large world generation and settlement transitions.
- Dynamic multi-stage Blight ecosystem.

## Design constraints

- Logical state belongs to simulation; screen transforms and drawing belong to presentation.
- Tile coordinates and object footprints use consistent zero-based grid positions.
- Resources must be conserved across transfer, construction, production, consumption, and persistence.
- Saved data must have explicit schema version and validated references.
- Deterministic tests must cover actual mechanics, not only application startup.
- Graphics remain minimal until integrated mechanics are legible.


## Alpha integration contract

- Campaign Supplies map to local Food; strategic Alloy maps to local Alloy. Energy and Knowledge stay strategic-only until corresponding local resources and consumers exist.
- Imports are capped per order and limited by local free storage. Strategic balances are reduced only by accepted local deliveries.
- Exports are capped per order and limited by real local inventory. Local balances are reduced before the strategic balances are credited.
- F5/F9 use `GameSessionService`, which validates both existing subsystem save formats and writes a single temporary file before atomic replacement.
- IntegrationChecks exercise resource conservation, full-storage rejection, and save/load of both systems.


## Target architecture: continuous real-time world

The nine-region linked-hex campaign model is legacy scaffolding. It was built around turn-based adjacency and must not be used as the representation for the new world. Keep it only as a temporary compatibility layer while replacement systems are migrated and tested.

The target world uses continuous world-space coordinates and spatial queries. Local settlement construction may remain square-tile based, but the wider world is not a graph of linked tiles. All scales share one simulation clock and canonical world state; camera zoom changes the level of detail only. Population, resources, trade, caravans, armies and hazards move through space over elapsed time. Resource transfers must be represented as shipments with travel progress, not instant imports/exports. Save/load must serialize one validated world snapshot.

Mouse-first is a product requirement. Visible menus must expose pause/resume, speed, save/load, zoom, build/object selection and cancellation. Mouse interaction is the primary input; keyboard shortcuts are optional only.
