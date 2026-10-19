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

    WorldSimulation
       |               \
       v                v
    ContinuousWorldState   CampaignSimulation (save migration only)
       |
       +-- continuous X/Y entities and spatial queries
       +-- continuous food production
       +-- settlement inventories and conserved cargo shipments

    CampaignSimulation
       +-- legacy save snapshot; not advanced during active play

    GameSessionService
             |
             +-- schema-v2 atomic session envelope: colony, legacy adapter, continuous world

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
- Continuous world-space settlements, resource sites, armies, caravans and Blight fronts with Euclidean spatial queries and coordinate-based travel-time movement.
- Continuous-time settlement food production, per-settlement strategic inventories, and caravan shipments that reserve origin cargo and deliver only after travel time.
- The active world coordinator no longer advances the legacy campaign turn/interval model; the campaign snapshot remains only for old-save compatibility.
- Schema-v2 unified saves include continuous world entity positions, in-flight destinations and the shared world clock; schema-v1 saves migrate by generating a fresh continuous world from the stored seed.
- Minimal symbol-based top-down rendering.

## Remaining integration work

- Extract worker scheduling into a reusable central job board and durable task ownership.
- Explicit material reservations and physically distinct stockpile locations.
- General recipe catalogs, workplace queues and multiple production chains.
- Broader needs, housing, work eligibility, skills and social relationships.
- Research unlocks wired to local buildings, production and systems.
- Migrate local colony inventory, construction consumption, production and hauling into the canonical world resource ledger without double-counting.
- Replace/remove the remaining legacy CampaignSimulation/CampaignState save payload and compatibility bridge once migration is tested.
- Add population-linked consumption, settlement development, diplomacy/conflict, and a dynamic multi-stage Blight ecosystem.
- Large world generation and settlement transitions.

## Design constraints

- Logical state belongs to simulation; screen transforms and drawing belong to presentation.
- Tile coordinates and object footprints use consistent zero-based grid positions.
- Resources must be conserved across transfer, construction, production, consumption, and persistence.
- Saved data must have explicit schema version and validated references.
- Deterministic tests must cover actual mechanics, not only application startup.
- Graphics remain minimal until integrated mechanics are legible.


## Legacy migration adapter contract (temporary)

- The old Campaign Supplies-to-Food and Alloy transfer mapping exists only as compatibility code and must not become the target world resource model. Energy and Knowledge remain in the legacy adapter until canonical world inventories replace it.
- Legacy imports are capped per order and limited by local free storage. This is not a real shipment and must be retired.
- Legacy exports are capped per order and limited by local inventory. Local balances are debited before the legacy strategic balances are credited; this bridge is not exposed as a player keyboard control.
- The visible mouse-menu Save/Load actions use `GameSessionService`, which validates subsystem formats and atomically writes the schema-v2 session envelope.
- IntegrationChecks exercise resource conservation, full-storage rejection, and save/load of both systems.


## Target architecture: continuous real-time world

The nine-region linked-hex campaign model is legacy scaffolding. It was built around turn-based adjacency and must not be used as the representation for the new world. Keep it only as a temporary compatibility layer while replacement systems are migrated and tested.

The target world uses continuous world-space coordinates and spatial queries. Local settlement construction may remain square-tile based, but the wider world is not a graph of linked tiles. All scales share one simulation clock and canonical world state; camera zoom changes the level of detail only. Population, resources, trade, caravans, armies and hazards move through space over elapsed time. Resource transfers must be represented as shipments with travel progress, not instant imports/exports. Save/load must serialize one validated world snapshot.

Mouse-first is a product requirement. Visible menus must expose pause/resume, speed, save/load, zoom, build/object selection and cancellation. Mouse interaction is the primary input; keyboard shortcuts are optional only.


## Cleanup boundary (2026-10)

`CampaignState` and its JSON serializer remain temporarily because the session envelope and schema-v1 migration still read the old campaign payload. They are not an active simulation or world-map architecture. The obsolete standalone `campaign.json` save/load path has been removed; all active persistence goes through `GameSessionService` and the unified session file. Do not add new gameplay features, UI controls, or save paths to `CampaignSimulation`. Replace the legacy payload only after the canonical world ledger can migrate old saves and the migration tests cover resources, army/battle flags, selection, and corrupt input.
