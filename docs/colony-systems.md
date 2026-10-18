# Colony systems: grid storage, citizen needs, and job broker

This module is the first deterministic core for the next colony-simulation slice. It uses the active root .NET 8/C# project and logical CellPosition coordinates; it does not depend on SDL, Gondwana, rendering, or engine objects per tile/citizen.

## Components

- **GridManager** owns a flat GridTile[] array. Each tile has a kind, zone ID, optional resource type, amount, and maximum stack size. Resource-specific stockpile zones must be in-bounds, non-overlapping, and connected through orthogonal neighbours.
- **Storage reservations** let a workplace reserve output capacity before work begins. CommitReservation converts the reservation into a real stack; CancelReservation releases it. Direct additions and withdrawals are bounded by the same stack and zone rules.
- **CitizenAgent** stores needs, happiness, position, current job, movement route, and work progress in a lightweight simulation class. Hunger and fatigue are satisfaction values: they decay over time, food is physically withdrawn from a stockpile, and exhausted citizens recover while idle.
- **JobBroker** registers workplaces with worker capacity, output resource, output amount, and work duration. It assigns the nearest available workplace deterministically and releases assignments when a workplace is removed.
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
- Hunger causes a real trip to food storage and consumes one stored unit.
- Worker capacity is enforced, and removing a workplace releases its citizens and storage reservations.
- Movement cannot path into a blocked destination.

## Integration boundary

The existing playable colony still uses ColonySimulation's established gather/carry/build/production scheduler and central inventory. This module is tested and available as a separate core, but it is **not yet the live source of truth for the visible map or legacy worker scheduler**. The next integration step is a controlled migration adapter that maps built stockpile/object footprints and existing colonists into this core, then retires duplicate inventory/job rules without breaking save compatibility. Until that adapter lands, do not claim these new stockpile zones or jobs are visible in the running game.

Save serialization for this new module is also intentionally deferred until its live-state ownership and migration path are settled.
