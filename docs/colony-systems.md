# Colony systems: grid storage, citizen needs, and job broker

This module is the first deterministic core for the next colony-simulation slice. It uses the active root .NET 8/C# project and logical CellPosition coordinates; it does not depend on SDL, Gondwana, rendering, or engine objects per tile/citizen.

## Components

- **GridManager** owns a flat GridTile[] array. Each tile has a kind, zone ID, optional resource type, amount, and maximum stack size. Resource-specific stockpile zones must be in-bounds, non-overlapping, and connected through orthogonal neighbours. The live colony now has one spatial stack grid per resource type; those grids can share a stockpile's coordinates while keeping each type's stacks distinct. The canonical settlement ledger still owns aggregate quantities/capacity, and grid contents are reconciled from that ledger at each simulation work cycle.
- **Storage reservations** let a workplace reserve output capacity before work begins. CommitReservation converts the reservation into a real stack; CancelReservation releases it. Direct additions and withdrawals are bounded by the same stack and zone rules.
- **CitizenAgent** stores needs, happiness, position, current job, movement route, and work progress in a lightweight simulation class. Citizen fatigue is persisted in the versioned colony snapshot and restored with the worker it belongs to. Hunger and fatigue are satisfaction values: they decay over time, food is physically withdrawn from a stockpile, and exhausted citizens recover while idle.
- **JobBroker** registers workplaces with worker capacity, output resource, output amount, and work duration. It assigns the nearest available workplace deterministically and releases assignments when a workplace is removed.
- **Work-order priorities** are configurable from 1 (low) to 5 (critical) on tile construction, object blueprints, component production, and excavation orders. The scheduler searches priority levels from highest to lowest across eligible work, preserves stable ID ordering within each type, and leaves already-assigned work uninterrupted. Automatic material gathering also ranks deposits by the highest-priority blocked order that needs that resource, then by travel distance.
- **Excavation** turns a designated solid-rock tile into walkable ground after worker travel and labor. It creates a finite stone deposit on the newly opened tile; resource accounting remains physical until a worker gathers, carries, and delivers that stone. The order, progress, priority, mined terrain, and uncollected deposit are preserved by colony save schema v6.
- **ColonyEcosystem** coordinates needs, grid pathfinding, movement, workplace assignment, work completion, and reserved output delivery in a fixed-step update.

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

The active `ColonySimulation.Step` now advances a `ColonyEcosystem` alongside the established construction/gather/carry scheduler. Each visible worker has a corresponding `CitizenAgent`; citizen hunger is synchronized with the save-compatible worker state, food withdrawals are reconciled against the existing inventory, citizen mood/state is available to the HUD, and completed Garden objects register food-producing workplaces with reserved output storage.

This remains a bounded migration rather than a fully unified physical inventory. The live colony has a separate spatial stack grid for every resource type, and the starter stockpile/storage-rack footprints are registered into every resource grid. The food grid is used for citizen consumption and Garden output; the other grids mirror aggregate ledger changes from gathering, construction, production and strategic shipments at each work cycle. The canonical settlement ledger still enforces total quantity and capacity, and the grid is reconstructed from the saved world/colony state.

Remaining integration work: make grid stacks the source of truth for every gather/deliver/build/recipe operation (rather than reconciling mirrors), route construction/gather/recipe tasks through the shared job broker, persist durable per-stack location/reservation state, and deepen map-aware pathfinding around terrain and buildings. The current priority manager governs construction and component-production assignment; hauling, gathering, citizen-need tasks, and every remaining scheduler path still need consolidation under the same shared job model. Do not describe those remaining systems as completed.
