using KorTheBlight.Simulation;

namespace KorTheBlight.Tests;

internal static class SimulationEventQueueChecks
{
    public static void Run()
    {
        var queue = new SimulationEventQueue();
        queue.Enqueue(4, SimulationEventKind.StockChanged, 2, value: 10);
        var firstSameTick = queue.Enqueue(2, SimulationEventKind.JobBecameAvailable, 7);
        var secondSameTick = queue.Enqueue(2, SimulationEventKind.NeedThresholdCrossed, 7);
        queue.Enqueue(8, SimulationEventKind.HazardChanged, 3);

        var firstBatch = queue.DrainDue(2, maxCount: 1);
        Check(firstBatch.Count == 1 && firstBatch[0] == firstSameTick,
            "events must be drained by tick and respect the per-drain budget");
        Check(queue.Count == 3, "future and over-budget due events must remain queued");

        var secondBatch = queue.DrainDue(4);
        Check(secondBatch.Count == 2 &&
              secondBatch[0] == secondSameTick &&
              secondBatch[1].Kind == SimulationEventKind.StockChanged,
            "same-tick events must preserve insertion order and due events must precede future events");
        Check(queue.Count == 1 && queue.DrainDue(7).Count == 0,
            "future events must not be dispatched early");
        Check(queue.DrainDue(8).Single().Kind == SimulationEventKind.HazardChanged && queue.Count == 0,
            "an event must become available on its scheduled tick");

        CheckThrows(() => queue.Enqueue(-1, SimulationEventKind.StockChanged, 1),
            "negative event ticks must be rejected");
        CheckThrows(() => queue.Enqueue(0, (SimulationEventKind)999, 1),
            "unknown event kinds must be rejected");
        CheckThrows(() => queue.DrainDue(0, 0),
            "a non-positive drain budget must be rejected");
    }

    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException($"Simulation event queue check failed: {reason}");
    }

    private static void CheckThrows(Action action, string reason)
    {
        try { action(); }
        catch (ArgumentOutOfRangeException) { return; }
        throw new InvalidOperationException($"Simulation event queue check failed: {reason}");
    }
}
