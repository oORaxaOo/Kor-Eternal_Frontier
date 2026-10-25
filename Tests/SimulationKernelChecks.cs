using KorTheBlight.Simulation;

namespace KorTheBlight.Tests;

internal static class SimulationKernelChecks
{
    public static void Run()
    {
        var kernel = new SimulationKernel();
        var calls = new List<string>();
        kernel.Register("world", SimulationPhase.World, 1, tick => calls.Add($"world:{tick.Number}"));
        kernel.Register("colony", SimulationPhase.Colony, 1, tick => calls.Add($"colony:{tick.Number}"));
        kernel.Register("planning", SimulationPhase.Strategic, 6, tick => calls.Add($"planning:{tick.Number}"));

        var first = kernel.AdvanceFixedTick();
        Check(first.Number == 1 && Math.Abs(first.DeltaSeconds - 1f / 60f) < 1e-7f,
            "the master kernel must provide one deterministic 60 Hz tick");
        Check(calls.SequenceEqual(new[] { "colony:1", "world:1" }),
            "systems must execute in phase order rather than registration or render order");

        calls.Clear();
        for (int i = 0; i < 5; i++) kernel.AdvanceFixedTick();
        Check(calls.Count == 11 &&
              calls.TakeLast(3).SequenceEqual(new[] { "colony:6", "world:6", "planning:6" }) &&
              calls.Count(item => item == "planning:6") == 1,
            "every-tick systems must continue running while slow systems run exactly on 10 Hz boundaries");

        var restored = new SimulationKernel(600);
        Check(Math.Abs(restored.SimulationSeconds - 10d) < 1e-9 &&
              restored.AdvanceFixedTick().Number == 601,
            "restored world time must continue from its exact integer tick without clock drift");

        bool duplicateRejected = false;
        try { kernel.Register("world", SimulationPhase.World, 1, _ => { }); }
        catch (InvalidOperationException) { duplicateRejected = true; }
        Check(duplicateRejected, "system IDs must be unique to prevent duplicate ticking");

        var realWorld = new WorldSimulation(55117);
        realWorld.Step(1f / 60f);
        Check(realWorld.SimulationTickCount == 1 &&
              realWorld.Colony.Clock.TickCount == 1 &&
              Math.Abs(realWorld.ElapsedSeconds - 1d / 60d) < 1e-7 &&
              Math.Abs(realWorld.SpatialWorld.ClockSeconds - realWorld.ElapsedSeconds) < 1e-7,
            "live colony, world and coordinator must advance from one master tick");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Simulation kernel check failed: {message}");
    }
}
