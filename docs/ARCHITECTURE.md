# Kor Eternal Frontier — architecture baseline

## Active application

There is one canonical application at the repository root, targeting .NET 8. Gondwana provides the game host and rendering surface; SDL3 supplies window and input integration, and SkiaSharp draws the current presentation. The simulation self-tests run without creating a window.

## Dependency direction

    Program / SDL input
             |
             v
    OrthogonalGridProjection ----> OutpostRenderer
             |                         ^
             v                         |
    Simulation (logical coordinates, deterministic rules)
             |
             +---- local scenario state

    CampaignSimulation / CampaignState / CampaignSaveService
             |
             +---- deterministic strategic model
                   (tested, not yet wired to game screens)

The projection layer owns screen/world mapping. Gameplay APIs receive logical world coordinates. The renderer reads state and does not advance or decide simulation rules.

## Current foundation

- One small local map.
- Worker and Guardian movement/task commands.
- Resource gathering.
- Purifier construction with resource cost and work.
- Deterministic local Blight spread and cleansing.
- Deterministic regional campaign model, research, turn processing, battle stock transfer/reconciliation, and campaign save round-trip checks.
- Orthogonal square-tile rendering with basic symbols rather than asset-dependent artwork.

## Not yet implemented or integrated

- Generic tile state and per-tile architecture orders.
- Separate multi-tile placeable-object model.
- Shared worker/job scheduler, stockpiles, reservations, and hauling.
- Complete population needs and workplace/skill simulation.
- General production recipes and economy.
- Research tree integrated with local unlocks.
- Large procedural world map and local-settlement transitions.
- Dynamic multi-stage Blight ecosystem.
- Campaign screen flow linked to local simulation.

## Required next slices

1. Replace the fixed scenario arrays with explicit typed tile state.
2. Introduce separate tile architecture orders and placeable-object blueprints sharing a material/job backend.
3. Add inventory, stockpile, reservation, and hauling invariants.
4. Add generic worker job ownership, priorities, interruptions, and work progress.
5. Build one real production chain from resource deposit through hauling and processing into a finished item.
6. Add population needs and a save-compatible state model.
7. Connect strategic regions to local colony snapshots and real resource/person flows.
8. Expand research and The Blight after shared simulation contracts are stable.

## Design constraints

- Logical state belongs to simulation; screen transforms and drawing belong to presentation.
- Tile positions and object footprints use consistent zero-based grid coordinates.
- Resources must be conserved across transfer, reservation, production, consumption, and save/load.
- IDs and persistence schema versions must be stable and validated.
- Deterministic tests must cover actual mechanics, not only successful application startup.
- Graphics remain minimal until the simulation mechanics are legible and integrated.
