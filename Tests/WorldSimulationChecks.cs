using KorTheBlight.Campaign;
using KorTheBlight.Simulation;
using KorTheBlight.WorldModel;

namespace KorTheBlight.Tests;

internal static class WorldSimulationChecks
{
    public static void Run()
    {
        var world = new WorldSimulation(seed: 70241);
        int initialTurn = world.World.State.Turn;
        int initialSupplies = world.World.State.Supplies;

        world.Step(WorldSimulation.CampaignUpdateIntervalSeconds - 0.2f);
        Check(world.World.State.Turn == initialTurn,
            "regional economy must not jump ahead of elapsed simulation time");
        Check(world.World.State.Supplies == initialSupplies,
            "regional stock must not change before its elapsed-time interval");
        Check(world.ElapsedSeconds > 0,
            "the unified simulation clock must advance");

        world.Step(0.2f);
        Check(world.World.State.Turn == initialTurn + 1,
            "regional simulation must advance automatically when elapsed time reaches its interval");
        Check(world.World.State.Supplies > initialSupplies,
            "regional production must run without a player-issued end-turn command");

        world.IsPaused = true;
        double elapsed = world.ElapsedSeconds;
        int pausedTurn = world.World.State.Turn;
        world.Step(5f);
        Check(world.ElapsedSeconds == elapsed && world.World.State.Turn == pausedTurn,
            "paused world must not advance either clock");

        world.IsPaused = false;
        world.SetSpeed(2f);
        world.Step(0.5f);
        Check(world.ElapsedSeconds == elapsed + 1d,
            "speed multiplier must scale unified simulation time");

        CheckThrows(() => world.SetSpeed(10f), "invalid simulation speeds must be rejected");
        CheckThrows(() => world.Step(float.NaN), "invalid elapsed time must be rejected");

        var home = world.SpatialWorld.Entities.Single(entity => entity.Faction == "Kor" && entity.Kind == WorldEntityKind.Settlement);
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
