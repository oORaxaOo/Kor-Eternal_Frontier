namespace KorTheBlight.Simulation;

/// <summary>Stable phase order for all systems advanced by the canonical world clock.</summary>
internal enum SimulationPhase
{
    Colony = 0,
    PreWorld = 10,
    World = 20,
    Strategic = 30,
    Combat = 40,
    Consequences = 50,
    Economy = 60
}

/// <summary>
/// The single authoritative 60 Hz simulation scheduler. Systems are registered once, sorted by phase,
/// and updated only on configured tick boundaries. Rendering and UI never call systems directly.
/// </summary>
internal sealed class SimulationKernel
{
    private sealed record SystemRegistration(
        string Id,
        SimulationPhase Phase,
        int IntervalTicks,
        long Sequence,
        Action<SimulationTick> Update);

    private readonly List<SystemRegistration> _systems = [];
    private long _nextSequence;

    public long TickCount { get; private set; }
    public double SimulationSeconds => TickCount / (double)TickManager.TicksPerSecond;
    public IReadOnlyList<string> SystemIds => _systems.Select(system => system.Id).ToArray();

    public SimulationKernel(long initialTick = 0)
    {
        if (initialTick < 0) throw new ArgumentOutOfRangeException(nameof(initialTick));
        TickCount = initialTick;
    }

    public void Register(string id, SimulationPhase phase, int intervalTicks, Action<SimulationTick> update)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A simulation system needs a stable ID.", nameof(id));
        if (!Enum.IsDefined(phase)) throw new ArgumentOutOfRangeException(nameof(phase));
        if (intervalTicks < 1 || intervalTicks > 60 * 60 * 24)
            throw new ArgumentOutOfRangeException(nameof(intervalTicks));
        ArgumentNullException.ThrowIfNull(update);
        if (_systems.Any(system => string.Equals(system.Id, id, StringComparison.Ordinal)))
            throw new InvalidOperationException($"Simulation system '{id}' is already registered.");

        _systems.Add(new SystemRegistration(id, phase, intervalTicks, _nextSequence++, update));
        _systems.Sort((left, right) =>
        {
            int phaseOrder = left.Phase.CompareTo(right.Phase);
            return phaseOrder != 0 ? phaseOrder : left.Sequence.CompareTo(right.Sequence);
        });
    }

    public SimulationTick AdvanceFixedTick()
    {
        if (TickCount == long.MaxValue) throw new InvalidOperationException("The simulation clock has reached its maximum tick.");
        TickCount++;
        var tick = new SimulationTick(TickCount, 1f / TickManager.TicksPerSecond);

        // The registration list is pre-sorted, avoiding per-tick sorting or iterator allocations.
        foreach (var system in _systems)
        {
            if (tick.Number % system.IntervalTicks == 0)
                system.Update(tick);
        }

        return tick;
    }
}
