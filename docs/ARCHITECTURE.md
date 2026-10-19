# Kor Eternal Frontier — architecture baseline

## Active application

One canonical application exists at the repository root, targeting .NET 8. Gondwana is the game host; SDL3 supplies window/input and SkiaSharp draws presentation. Simulation and persistence checks run without a game window.

## Dependency direction

    Program / SDL input
             |
             v
    OrthogonalGridProjection ---> ColonyRenderer
             |                        ^
             v                        |
    ColonySimulation (local settlement state and rules)

    WorldSimulation
       |               \
       v                v
    ContinuousWorldState   CampaignSimulation (save migration only)
       |
       +-- continuous X/Y entities and spatial queries
       +-- continuous food production
       +-- settlement inventories and conserved cargo shipments

    GameSessionService
             |
             +-- schema-v2 session envelope: colony state, legacy compatibility snapshot, canonical continuous world ledger

The projection layer owns screen/world mapping. Gameplay APIs receive logical world coordinates. Rendering reads state but does not decide simulation rules.

## Implemented mechanics

- 40 by 26 orthogonal square-tile colony grid.
- Three colonists with movement, task state, carrying, and hunger.
- Finite deposits and resource-conserving gather/carry/deliver transfers.
- Aggregate inventory capacity supplied by completed stockpiles (120 units each) and storage racks (80 units each).
- Capacity-aware gathering, partial delivery, retained cargo while storage is full, and production output that waits for room.
- Independent per-tile architecture orders with material costs and work progress.
- Separate multi-tile placeable objects with validated footprints and construction work.
- A workstation recipe that consumes real inventory and yields a component after labor.
- Versioned, validated colony save/load for tiles, jobs/routes, workers, deposits, inventory, construction, production and clock.
- Continuous world-space settlements, resource sites, armies, caravans and Blight fronts with Euclidean spatial queries and coordinate-based travel-time movement.
- Continuous-time settlement food production constrained by shared settlement storage capacity; per-settlement inventories are the only resource authority for colony and strategic actions.
- Caravan shipments reserve cargo at dispatch and deliver only after travel time and destination storage capacity are available.
- One active real-time clock; the legacy campaign turn/interval model is not advanced during active play.
- Schema-v2 session saves include continuous-world state; schema-v1 saves migrate by generating a fresh continuous world from the stored seed.
- Minimal symbol-based top-down rendering.

## Legacy debt and retirement order

The old campaign model remains only as a compatibility snapshot for now. Its linked-hex geometry, turn counter, region production and separate resource balances are not the target world simulation.

The obsolete instant Campaign-to-Colony transfer bridge has been removed. Transfers between strategic settlements must use canonical inventories and travel-time shipments; instant imports/exports are not a supported gameplay path.

Remaining migration work, in order:

1. Remove the legacy campaign save payload and obsolete compatibility APIs now that the canonical world ledger owns inventory.
2. Remove the linked-hex campaign model and its dead rules/tests; existing saves do not need migration support.
4. Add population-linked consumption/production, settlement development, diplomacy/conflict and dynamic Blight behavior.
5. Improve the continuous camera transition and perform a graphical human playtest.

## Design constraints

- Logical state belongs to simulation; screen transforms and drawing belong to presentation.
- Tile coordinates and object footprints use consistent zero-based grid positions.
- Resources must be conserved across transfer, construction, production, consumption, and persistence.
- Saved data must have explicit schema version and validated references.
- Deterministic tests must cover actual mechanics, not only application startup.
- Graphics remain minimal until integrated mechanics are legible.

## Target architecture: continuous real-time world

The nine-region linked-hex campaign model is legacy scaffolding and must not be used as the representation for the new world. The target world uses continuous world-space coordinates and spatial queries. Local settlement construction may remain square-tile based, but the wider world is not a graph of linked tiles. All scales share one simulation clock and canonical world state; camera zoom changes the level of detail only. Population, resources, trade, caravans, armies and hazards move through space over elapsed time. Resource transfers are shipments with travel progress, not instant imports/exports. Save/load must converge on one validated world snapshot.

Mouse-first is a product requirement. Visible menus must expose pause/resume, speed, save/load, zoom, build/object selection and cancellation. Mouse interaction is primary; keyboard shortcuts are optional only.
