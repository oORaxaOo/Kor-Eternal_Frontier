# Colony systems: grid storage, citizen needs, and job broker

This module documents the active colony-simulation integration layer. Its fixed-step runtime is coordinated by the canonical world simulation kernel, while its logical map and simulation objects remain independent of SDL, Gondwana, rendering, and per-entity engine objects. It uses the active root .NET 8/C# project and logical CellPosition coordinates; it does not depend on SDL, Gondwana, rendering, or engine objects per tile/citizen.

## Components

- **GridManager** owns a flat GridTile[] array. Each tile has a kind, zone ID, optional resource type, amount, and maximum stack size. Resource-specific stockpile zones must be in-bounds, non-overlapping, and connected through orthogonal neighbours. The live colony now has one spatial stack grid per resource type; those grids can share a stockpile's coordinates while keeping each type's stacks distinct. The canonical settlement ledger still owns aggregate quantities/capacity, and grid contents are reconciled from that ledger at each simulation work cycle.
- **Storage reservations** let a workplace reserve output capacity before work begins. CommitReservation converts the reservation into a real stack; CancelReservation releases it. Direct additions and withdrawals are bounded by the same stack and zone rules.
- **CitizenAgent** owns the decision/job overlay, but in the live colony it attaches to the exact same ColonyWorker identity used by construction, gathering, carrying, trade, departure, and combat. Position, hunger, fatigue, stress, and personality are direct properties of that canonical resident; the system no longer copies values between two independently mutable pawn objects every work cycle. Hunger/fatigue are satisfaction values (100 means fed/rested), while ColonyWorker.Hunger remains the legacy deficit value for save/UI compatibility. Food consumption withdraws a physical stack from the local spatial food grid.
- **JobBroker** registers workplaces with worker capacity, output resource, output amount, and work duration. It assigns the nearest available workplace deterministically and releases assignments when a workplace is removed.
- **Work-order priorities** are configurable from 1 (low) to 5 (critical) on tile construction, object blueprints, component production, and excavation orders. The scheduler searches priority levels from highest to lowest across eligible work, preserves stable ID ordering within each type, and leaves already-assigned work uninterrupted. Automatic material gathering also ranks deposits by the highest-priority blocked order that needs that resource, then by travel distance.
- **Excavation** turns a designated solid-rock tile into walkable ground after worker travel and labor. It creates a finite stone deposit on the newly opened tile; resource accounting remains physical until a worker gathers, carries, and delivers that stone. The order, progress, priority, mined terrain, and uncollected deposit are preserved by the versioned colony save.
- **ColonyEcosystem** coordinates needs, grid pathfinding, movement, workplace assignment, work completion, and reserved output delivery. Its update is driven by the canonical integer 60 Hz master tick; work planning is aligned to deterministic 10 Hz boundaries.

## Minimal setup

    var colony = new ColonyEcosystem(width: 40, height: 26);

    colony.Grid.DefineStockpileZone(ResourceKind.Food,
        [new CellPosition(18, 8), new CellPosition(19, 8)]);
    colony.Grid.DefineStockpileZone(ResourceKind.Wood,
        [new CellPosition(18, 9), new CellPosition(19, 9)]);
    colony.Grid.TryAdd(ResourceKind.Food, 10);

    colony.AddCitizen("Kor-01", new CellPosition(16, 8));
    colony.JobBroker.RegisterWorkplace("Lumber yard", new CellPosition(25, 12),
        workerCapacity: 3, outputResource: ResourceKind.Wood,
        outputAmount: 2, workSeconds: 5f);

    // Call with the game's fixed simulation step, not render-frame delta.
    colony.Step(0.1f);

## Invariants covered by self-tests

- Stockpile zones are connected, resource-specific, and cannot overlap.
- Stack limits, storage reservations, additions, and withdrawals conserve quantities.
- A citizen reserves storage before travelling to a workplace; output cannot overfill a stockpile.
- A resource deposit accepts only one active gather assignment at a time, preventing redundant worker trips and overcommitted finite sources.
- Hunger causes a real trip to food storage and consumes one stored unit.
- Worker capacity is enforced, and removing a workplace releases its citizens and storage reservations.
- Movement cannot path into a blocked destination.


## Live integration boundary

SimulationKernel is the authoritative runtime clock and dispatches named systems in a stable phase order: colony residents/local environment, pre-world incident and capacity checks, continuous world time, strategic arrivals/Blight, tactical combat, combat consequences, and research/workshop economy. It advances at a fixed 60 Hz simulation rate independently of render-frame chunking. Expensive planning, incident checks, strategic arrival processing, research, and workshop progression use deterministic 10 Hz boundaries. The integer tick is restored from the saved world clock so the colony, macro simulation, and continuous world remain synchronized.

The active colony uses one ColonyWorker object as each resident's canonical identity and mutable state. CitizenAgent binds to it for autonomous needs and workplace selection rather than maintaining separate position/hunger/stress copies. Resident needs/personality now participate in colony saves (schema version 10), with validated personality weights for hunger, sleep, social, recreation, and comfort. The live map's rock and wall topology is propagated to all resource-grid navigation layers, so autonomous resident paths cannot silently use a different blocked-cell map.

Resource storage is improved but deliberately not described as a completed physical-item simulation. Each resource type has its own bounded spatial stack grid, allowing local storage to represent different materials without mixing stack types. The settlement inventory ledger still owns aggregate quantities and capacity. Workplaces reserve output across all resource layers against one shared capacity budget, and the local grids reconcile available quantities into built stockpile/rack footprints. When strategic inventory exceeds the actual local footprint, only locally storable quantities materialize; the remainder stays in the settlement ledger instead of inventing map storage.

A strict full RimWorld/Dwarf-Fortress-style architecture is not yet complete. The legacy construction/gather/carry/recipe scheduler and ColonyEcosystem.JobBroker still coexist, coordinated by exclusive resident-task ownership rather than replaced by one universal job graph. Local stock grids are still reconciled views, not the sole inventory source of truth; per-stack locations and hauling reservations are not yet authoritative or persisted. Body-part health, injuries/medicine, a full vertical world/shaft/cavern model, broad historical simulation, and fully unified saveable pawn-job state also remain outside the implemented slice. Those are intentional engineering boundaries, not completed features.

Self-tests cover the fixed-tick kernel and phase order, save/load clock continuity, shared resident state, resource-specific production output, aggregate cross-resource output reservations, map-topology pathfinding, and the pre-existing colony/strategic/combat integration checks.
