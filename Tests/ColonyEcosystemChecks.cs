using KorTheBlight.Simulation;

namespace KorTheBlight.Tests;

internal static class ColonyEcosystemChecks
{
    public static void Run()
    {
        var grid = new GridManager(5, 5, maxStackSize: 10);
        int foodZone = grid.DefineStockpileZone(ResourceKind.Food,
            [new CellPosition(0, 0), new CellPosition(1, 0)]);
        Check(foodZone > 0, "a valid connected stockpile zone should be registered");
        Check(grid.TryAdd(ResourceKind.Food, 17), "food should be distributed over available tile stacks");
        Check(grid.ResourceCount(ResourceKind.Food) == 17 && grid.FreeCapacity(ResourceKind.Food) == 3,
            "resource totals and free space should reflect per-tile stack limits");
        Check(!grid.CanStore(ResourceKind.Food, 4) && grid.CanStore(ResourceKind.Food, 3),
            "capacity checks must reject overfilling while allowing the exact remaining capacity");

        Check(grid.TryReserveSpace(ResourceKind.Food, 3, out var reservation),
            "free stack space should be reservable by a future producer");
        Check(grid.FreeCapacity(ResourceKind.Food) == 0,
            "reserved storage must no longer be offered to competing producers");
        Check(grid.CommitReservation(reservation.Id) && grid.ResourceCount(ResourceKind.Food) == 20,
            "committing a reservation should add exactly the reserved quantity");
        Check(grid.TryTake(ResourceKind.Food, 7) == 7 && grid.ResourceCount(ResourceKind.Food) == 13,
            "withdrawal must never remove more resources than requested or available");
        Check(grid.TryAdd(ResourceKind.Food, 7) && grid.ResourceCount(ResourceKind.Food) == 20,
            "withdrawn space should become available for later deliveries");

        bool disconnectedRejected = false;
        try
        {
            grid.DefineStockpileZone(ResourceKind.Wood,
                [new CellPosition(3, 3), new CellPosition(4, 4)]);
        }
        catch (ArgumentException) { disconnectedRejected = true; }
        Check(disconnectedRejected, "a stockpile zone must be orthogonally connected");

        var colony = new ColonyEcosystem(8, 8);
        colony.Grid.DefineStockpileZone(ResourceKind.Food,
            [new CellPosition(0, 0), new CellPosition(0, 1)]);
        colony.Grid.DefineStockpileZone(ResourceKind.Wood,
            [new CellPosition(6, 6), new CellPosition(6, 7)]);
        Check(colony.Grid.TryAdd(ResourceKind.Food, 5),
            "the integrated colony should be able to seed a food stockpile");
        var citizen = colony.AddCitizen("Test-Kor", new CellPosition(1, 0));
        colony.AddCitizen("Second-Kor", new CellPosition(1, 1));
        int jobId = colony.JobBroker.RegisterWorkplace("Lumber yard", new CellPosition(5, 6),
            workerCapacity: 1, outputResource: ResourceKind.Wood, outputAmount: 2, workSeconds: 0.5f);

        colony.Step(0.1f);
        Check(citizen.JobId == jobId && citizen.State == CitizenState.MovingToJob,
            "an idle eligible citizen should reserve a job and move toward its workplace");
        Check(colony.Grid.ReservedCount(ResourceKind.Wood) == 2,
            "a worker must reserve output capacity before starting its job");
        Check(colony.RemoveWorkplace(jobId), "a registered workplace should be removable");
        Check(citizen.JobId == null && citizen.State == CitizenState.Idle &&
              colony.Grid.ReservedCount(ResourceKind.Wood) == 0,
            "removing a workplace must release its citizen and all reserved storage");

        int replacementJob = colony.JobBroker.RegisterWorkplace("Lumber yard", new CellPosition(5, 6),
            workerCapacity: 1, outputResource: ResourceKind.Wood, outputAmount: 2, workSeconds: 0.5f);
        for (int i = 0; i < 100; i++) colony.Step(0.1f);
        Check(colony.Grid.ResourceCount(ResourceKind.Wood) > 0 &&
              colony.Grid.ResourceCount(ResourceKind.Wood) <= 20,
            "work must produce real stored output without exceeding tile stack capacity");
        Check(colony.JobBroker.FindJob(replacementJob)?.AssignedWorkers <= 1,
            "workplace assignment must never exceed its worker capacity");
        Check(citizen.Happiness >= 0 && citizen.Happiness <= 100,
            "happiness should be the bounded average of citizen needs");

        var needsColony = new ColonyEcosystem(4, 4);
        needsColony.Grid.DefineStockpileZone(ResourceKind.Food,
            [new CellPosition(0, 0), new CellPosition(0, 1)]);
        needsColony.Grid.TryAdd(ResourceKind.Food, 2);
        var hungryCitizen = needsColony.AddCitizen("Hungry-Kor", new CellPosition(1, 0));
        hungryCitizen.Hunger = 25f;
        int foodBefore = needsColony.Grid.ResourceCount(ResourceKind.Food);
        for (int i = 0; i < 10; i++) needsColony.Step(0.1f);
        Check(needsColony.Grid.ResourceCount(ResourceKind.Food) == foodBefore - 1 &&
              hungryCitizen.Hunger > 30f,
            "a hungry citizen should physically reach food storage, consume one unit, and restore hunger");

        hungryCitizen.Fatigue = 10f;
        needsColony.Step(1f);
        Check(hungryCitizen.Fatigue > 10f,
            "an exhausted citizen should stop working and recover while idle");

        var wallGrid = new GridManager(3, 3);
        wallGrid.SetWall(new CellPosition(1, 0));
        wallGrid.SetWall(new CellPosition(0, 1));
        Check(wallGrid.FindPath(new CellPosition(0, 0), new CellPosition(1, 0)) == null,
            "pathfinding should reject a blocked destination tile");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Colony ecosystem check failed: {message}");
    }
}
