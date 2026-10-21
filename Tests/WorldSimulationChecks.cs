using KorTheBlight.Simulation;
using KorTheBlight.WorldModel;

namespace KorTheBlight.Tests;

internal static class WorldSimulationChecks
{
    public static void Run()
    {
        var world = new WorldSimulation(seed: 70241);
        var home = world.SpatialWorld.Entities.Single(entity => entity.Faction == "Kor" && entity.Kind == WorldEntityKind.Settlement);
        Check(world.Colony.ResourceCount(ResourceKind.Wood) ==
              world.SpatialWorld.InventoryAt(home.Id, WorldResourceKind.Wood),
            "colony and strategic world must read the same authoritative resource balance");

        world.Step(10f);
        Check(world.ElapsedSeconds > 0 && world.ElapsedSeconds <= 0.8 + 1e-6,
            "a long frame must process only the bounded fixed-step budget");
        Check(world.SpatialWorld.MacroWorld.SimulationTicks == world.Colony.Clock.TickCount &&
              Math.Abs(world.ElapsedSeconds - world.Colony.Clock.SimulationSeconds) < 0.0001,
            "macro, world, and colony clocks must advance by the same consumed fixed steps");
        Check(world.SpatialWorld.InventoryAt(home.Id, WorldResourceKind.Food) >= 0 &&
              world.Colony.ResourceCount(ResourceKind.Food) == (int)Math.Floor(world.SpatialWorld.InventoryAt(home.Id, WorldResourceKind.Food)),
            "colony food needs and continuous settlement production must use the same ledger balance");

        world.IsPaused = true;
        double elapsed = world.ElapsedSeconds;
        world.Step(5f);
        Check(world.ElapsedSeconds == elapsed,
            "paused world must not advance the unified clock");

        world.IsPaused = false;
        var speedWorld = new WorldSimulation(seed: 70242);
        speedWorld.SetSpeed(2f);
        for (int frame = 0; frame < 5; frame++) speedWorld.Step(0.1f);
        Check(Math.Abs(speedWorld.ElapsedSeconds - 1d) < 0.0001 &&
              speedWorld.SpatialWorld.MacroWorld.SimulationTicks == speedWorld.Colony.Clock.TickCount,
            "speed multiplier must scale the unified clock while keeping macro and colony ticks synchronized");
        world.Step(0.01f);
        Check(world.ElapsedSeconds >= elapsed,
            "bounded catch-up must retain backlog rather than moving the world clock backwards");

        var macro = world.SpatialWorld.MacroWorld;
        int localCellId = macro.Cells.First(cell => cell.IsLocalMapGenerated).CellId;
        int visitorFactionId = macro.FactionIds.First(pair => pair.Key != "Kor").Value;
        long arrivalId = macro.NextCaravanId++;
        macro.Arrivals.Add(new MacroCaravanArrival
        {
            Id = arrivalId, FactionId = visitorFactionId, TargetCellId = localCellId,
            NotificationId = macro.NextNotificationId++,
            Cargo = new Dictionary<MacroResource, float> { [MacroResource.Food] = 4f }
        });
        int populationBeforeArrival = world.Colony.Workers.Count;
        world.Step(0.1f);
        var visitor = world.Colony.Workers.SingleOrDefault(worker => worker.IsExternalVisitor && worker.TradeCargo.ContainsKey(ResourceKind.Food));
        Check(world.Colony.Workers.Count == populationBeforeArrival + 1 && visitor is not null &&
              visitor.TradeCargo[ResourceKind.Food] == 4 && macro.Arrivals.All(arrival => arrival.Id != arrivalId),
            "a strategic arrival must decompress into one local trader with matching cargo and leave the pending queue");
        macro.Validate();

        CheckThrows(() => world.SetSpeed(10f), "invalid simulation speeds must be rejected");
        CheckThrows(() => world.Step(float.NaN), "invalid elapsed time must be rejected");

        var caravan = world.SpatialWorld.Entities.Single(entity => entity.Kind == WorldEntityKind.Caravan && entity.Faction == "Kor");
        var destination = world.SpatialWorld.Entities.First(entity => entity.Kind == WorldEntityKind.Settlement && entity.Faction != "Kor");
        Check(world.SpatialWorld.CreateShipment(caravan.Id, home.Id, destination.Id, WorldResourceKind.Wood, 5, 1_000),
            "the canonical home ledger must fund a strategic shipment");
        Check(world.Colony.ResourceCount(ResourceKind.Wood) == 15,
            "strategic shipment dispatch must immediately debit the same balance used by colony gameplay");

        Check(world.SpatialWorld.OrderMove(home.Id, home.X + 100, home.Y, 10),
            "unified world must expose continuous entity movement");
        world.Step(0.5f);
        double savedClock = world.ElapsedSeconds;
        string sessionJson = GameSessionService.Serialize(world);
        var restoredSession = GameSessionService.Deserialize(sessionJson);
        Check(Math.Abs(restoredSession.ContinuousWorld.ClockSeconds - savedClock) < 0.0001 &&
              restoredSession.ContinuousWorld.Entities.Single(entity => entity.Id == home.Id).IsMoving,
            "the unified session must preserve continuous world time and in-flight movement");
        var restoredVisitor = restoredSession.Colony.Workers.SingleOrDefault(worker => worker.Id == visitor!.Id);
        Check(restoredVisitor is { IsExternalVisitor: true } && restoredVisitor.TradeCargo[ResourceKind.Food] == 4 &&
              restoredSession.ContinuousWorld.MacroWorld.Arrivals.All(arrival => arrival.Id != arrivalId),
            "session save/load must preserve a local trader's cargo without resurrecting drained macro arrivals");
        world.Restore(restoredSession);
        Check(Math.Abs(world.ElapsedSeconds - savedClock) < 0.0001 &&
              Math.Abs(world.SpatialWorld.ClockSeconds - savedClock) < 0.0001,
            "restoring a session must restore the one continuous world clock");
        Check(world.Colony.ResourceCount(ResourceKind.Wood) == 15,
            "session persistence must store the shared resource balance only once and restore it into both views");
    }

    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException($"World simulation check failed: {reason}");
    }

    private static void CheckThrows(Action action, string reason)
    {
        try { action(); }
        catch (ArgumentOutOfRangeException) { return; }
        throw new InvalidOperationException($"World simulation check failed: {reason}");
    }
}
