using KorTheBlight.WorldModel;

namespace KorTheBlight.Tests;

internal static class ContinuousWorldChecks
{
    public static void Run()
    {
        var world = ContinuousWorldState.Generate(70241);
        var home = world.Entities.Single(entity => entity.Faction == "Kor" && entity.Kind == WorldEntityKind.Settlement);
        var nearby = world.QueryRadius(home.X, home.Y, 1);
        Check(nearby.Count == 1 && nearby[0].Id == home.Id,
            "continuous spatial queries must use distance rather than hex adjacency");

        Check(world.OrderMove(home.Id, home.X + 20, home.Y, 10),
            "world entities must accept coordinate destinations");
        world.Step(1);
        Check(Math.Abs(home.X - 50_010) < 0.0001 && home.IsMoving,
            "entity movement must progress continuously according to speed and elapsed time");
        world.Step(1);
        Check(Math.Abs(home.X - 50_020) < 0.0001 && !home.IsMoving,
            "entity movement must stop exactly at its destination without overshoot");
        Check(world.ClockSeconds == 2,
            "continuous world state must maintain its own serializable simulation clock");

        string json = ContinuousWorldState.Serialize(world);
        var restored = ContinuousWorldState.Deserialize(json);
        var restoredHome = restored.Entities.Single(entity => entity.Id == home.Id);
        Check(restored.ClockSeconds == world.ClockSeconds &&
              Math.Abs(restoredHome.X - home.X) < 0.0001 &&
              restored.Entities.Count == world.Entities.Count,
            "continuous world save/load must preserve clock, entities and positions");

        CheckThrows(() => world.OrderMove(home.Id, 50_030, 50_000, 0),
            "movement speed must be positive");
        CheckThrows(() => world.QueryRadius(50_000, 50_000, double.NaN),
            "invalid spatial query radii must be rejected");
    }

    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException($"Continuous world check failed: {reason}");
    }

    private static void CheckThrows(Action action, string reason)
    {
        try { action(); }
        catch (ArgumentOutOfRangeException) { return; }
        throw new InvalidOperationException($"Continuous world check failed: {reason}");
    }
}
