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
        Check(world.ElapsedSeconds > 0,
            "the unified simulation clock must advance");
        Check(world.SpatialWorld.InventoryAt(home.Id, WorldResourceKind.Food) >= 0 &&
              world.Colony.ResourceCount(ResourceKind.Food) == (int)Math.Floor(world.SpatialWorld.InventoryAt(home.Id, WorldResourceKind.Food)),
            "colony food needs and continuous settlement production must use the same ledger balance");

        world.IsPaused = true;
        double elapsed = world.ElapsedSeconds;
        world.Step(5f);
        Check(world.ElapsedSeconds == elapsed,
            "paused world must not advance the unified clock");

        world.IsPaused = false;
        world.SetSpeed(2f);
        world.Step(0.5f);
        Check(world.ElapsedSeconds == elapsed + 1d,
            "speed multiplier must scale unified simulation time");

        CheckThrows(() => world.SetSpeed(10f), "invalid simulation speeds must be rejected");
        CheckThrows(() => world.Step(float.NaN), "invalid elapsed time must be rejected");

        var caravan = world.SpatialWorld.Entities.Single(entity => entity.Kind == WorldEntityKind.Caravan);
        var destination = world.SpatialWorld.Entities.First(entity => entity.Kind == WorldEntityKind.Settlement && entity.Faction == "Neutral");
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
