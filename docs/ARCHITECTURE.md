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
             |
             +-- ColonyEcosystem (individual citizens, needs, workplaces)
             +-- GridManager (compact tile data, storage zones, reservations)
             +-- SimulationEventQueue (tick-indexed deterministic notifications)

    WorldSimulation
       |
       v
    ContinuousWorldState
    GameSessionService
             |
             +-- schema-v1 session envelope: colony state and canonical continuous world ledger

The projection layer owns screen/world mapping. Gameplay APIs receive logical world coordinates. Rendering reads state but does not decide simulation rules.

## Implemented mechanics

- 40 by 26 orthogonal square-tile colony grid.
- Individually identified colonists with movement, task state, carrying, hunger and fatigue.
- Finite deposits and resource-conserving gather/carry/deliver transfers.
- Compact tile-array storage grid with bounded resource stacks, connected stockpile zones and storage reservations.
- Capacity-aware gathering, partial delivery, retained cargo while storage is full, and production output that waits for room.
- Independent per-tile architecture orders with material costs and work progress.
- Separate multi-tile placeable objects with validated footprints and construction work.
- A workstation recipe that consumes real inventory and yields a component after labor.
- A deterministic workplace broker selecting the nearest available job with stable-ID tie-breaking.
- Versioned, validated colony save/load for tiles, jobs/routes, workers, deposits, inventory, construction, production and clock.
- Continuous world-space settlements, resource sites, armies, caravans and Blight fronts with coordinate-based movement and a uniform spatial-bucket index for radius queries. The index is rebuilt lazily only after entity positions change, not on every stationary world tick.
- Shared settlement inventories, travel-time shipments and storage-capacity checks.
- One active real-time clock shared by local colony and continuous world simulation.
- Unified session saves contain colony state and continuous world state; resource balances are serialized only in the world ledger.
- A deterministic tick-indexed event queue with stable same-tick ordering and bounded draining; event handlers and broad system integration remain future work.
- A regression check that creates and advances 10,000 individually identified citizen agents. This is not yet a performance guarantee for 10,000 fully integrated workers.

## Combined design target: DF depth + Songs of Syx scale + RimWorld storytelling

These games are design references, not evidence that their proprietary internal implementations match every mechanism in a reverse-engineering description. Kor will combine their useful *design outcomes* while using an architecture suited to this codebase:

1. **World and materials (DF-inspired):** retain the compact local tile grid and make terrain/material, fluid, temperature and occupancy explicit simulation data. Start with 2D tile layers and a stable Z-level contract only when vertical construction is implemented. Fluids and heat must use bounded deterministic updates and material properties; do not introduce a full 3D voxel allocation before gameplay requires it.
2. **Mass simulation (Songs of Syx-inspired):** retain individual resident IDs and consequential state, but simulate distant/off-screen residents in deterministic cohorts and less frequent time slices. Add spatial buckets and target-oriented flow fields only after benchmarks establish where individual pathfinding is expensive. A flow field is an optimization for many agents sharing a destination, not a replacement for obstacle-aware routes in every task.
3. **Personal stories (RimWorld-inspired):** model needs, traits, memories, relationships and health effects as data; process threshold crossings and world incidents through the deterministic event queue. Update expensive systems on staggered tick intervals. Avoid copying RimWorld-specific class names or assuming its exact tick rates are universal.
4. **Jobs and logistics:** use explicit job states and reservations, verify reachability before assigning a job, and reserve both source quantity and destination capacity. Resource movement must conserve stock. Need/stock/job events should eventually wake only relevant planners instead of polling all agents.
5. **Incidents and ecology:** a director may schedule seeded incidents using colony state, security, shortages and environmental pressure. Keep event intensity bounded and testable; avoid an opaque random difficulty jump.
6. **Presentation:** symbolic tile rendering, zoom-dependent detail and inspectable individual records. Camera zoom must never change authoritative simulation outcomes.

## Current implementation gaps — do not mistake these for completed systems

- The local settlement is 2D, not a full 3D voxel world. There is no pressure-driven fluid simulation, heat transfer, ignition/melting system or organ/tissue damage model yet.
- The current route search is grid-based breadth-first search with caching, not A* or a shared flow-field system.
- Individual citizen needs and workplace jobs exist, but full personality-driven mental breaks, long-lived memories, social graphs, body-part health and schedules are not integrated.
- Population cohorts are a separate aggregate model; transitions between cohorts and individually simulated residents still need explicit conservation rules and persistence.
- The new event queue is currently infrastructure with regression tests, not yet wired to every need, job, stock or hazard subsystem.
- The continuous-world radius-query spatial index is implemented and tested against a full-scan oracle; local-agent spatial indexing, route/target invalidation metrics and reproducible 1k/5k/10k end-to-end benchmarks remain planned.
- The world map is continuous 2D space; tactical formations and per-soldier combat are not implemented.

## Persistence and deterministic behavior

- Logical state belongs to simulation; screen transforms and drawing belong to presentation.
- Tile coordinates and object footprints use consistent zero-based grid positions.
- Resources must be conserved across transfer, construction, production, consumption and persistence.
- Saved data must have explicit schema version and validated references.
- Event ordering must be deterministic; do not use wall-clock timestamps as simulation order.
- Deterministic tests must cover actual mechanics, not only application startup.
- Graphics remain minimal until integrated mechanics are legible.

## Implementation sequence

1. Add and test deterministic event scheduling. **Foundation added in this branch.**
2. Connect events to stock changes, job availability and need thresholds; preserve existing direct-call behavior during migration.
3. Add a spatial bucket index for continuous-world queries and local agent lookups, with mutation/invalidation tests.
4. Add multi-source flow fields for shared destinations and retain exact path checks for work reservations.
5. Expand individual needs, traits, memories, relationships and body-part health in small save-compatible slices.
6. Add tile material definitions, temperature exchange and fluid movement with strict per-tick work budgets.
7. Add seeded incident scheduling and bounded population/industry benchmarks.

Mouse-first is a product requirement. Visible menus must expose pause/resume, speed, save/load, zoom, build/object selection and cancellation. Mouse interaction is primary; keyboard shortcuts are optional only.
