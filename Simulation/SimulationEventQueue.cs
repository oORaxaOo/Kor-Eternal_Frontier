namespace KorTheBlight.Simulation;

/// <summary>
/// Stable event categories shared by the colony's needs, jobs, logistics, ecology and incident systems.
/// Payloads intentionally contain IDs and scalar values, never references to mutable simulation objects.
/// </summary>
internal enum SimulationEventKind
{
    NeedThresholdCrossed,
    JobBecameAvailable,
    StockChanged,
    RouteInvalidated,
    HazardChanged,
    SocialInteractionRequested,
    IncidentRequested,
    MentalBreakStarted,
    MentalBreakEnded
}

/// <summary>A deterministic, data-only notification emitted by a simulation subsystem.</summary>
internal readonly record struct SimulationEvent(
    long Tick,
    long Sequence,
    SimulationEventKind Kind,
    long SubjectId,
    long RelatedId = 0,
    double Value = 0);

/// <summary>
/// Tick-indexed event queue. Same-tick events preserve insertion order; future events remain queued.
/// Draining is bounded so a chain of notifications cannot monopolize a simulation frame.
/// This class does not dispatch handlers or mutate world state: the simulation coordinator owns that.
/// </summary>
internal sealed class SimulationEventQueue
{
    private readonly PriorityQueue<SimulationEvent, (long Tick, long Sequence)> _pending = new();
    private long _nextSequence;

    public int Count => _pending.Count;

    public SimulationEvent Enqueue(
        long tick,
        SimulationEventKind kind,
        long subjectId,
        long relatedId = 0,
        double value = 0)
    {
        if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (subjectId < 0 || relatedId < 0) throw new ArgumentOutOfRangeException(nameof(subjectId));
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));

        var item = new SimulationEvent(tick, _nextSequence++, kind, subjectId, relatedId, value);
        _pending.Enqueue(item, (item.Tick, item.Sequence));
        return item;
    }

    /// <summary>Removes up to maxCount events due at or before currentTick in deterministic order.</summary>
    public List<SimulationEvent> DrainDue(long currentTick, int maxCount = 4096)
    {
        if (currentTick < 0) throw new ArgumentOutOfRangeException(nameof(currentTick));
        if (maxCount <= 0) throw new ArgumentOutOfRangeException(nameof(maxCount));

        var due = new List<SimulationEvent>(Math.Min(maxCount, _pending.Count));
        while (due.Count < maxCount &&
               _pending.TryPeek(out var next, out var priority) &&
               priority.Tick <= currentTick)
        {
            due.Add(_pending.Dequeue());
        }
        return due;
    }
}
