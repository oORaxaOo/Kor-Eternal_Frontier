namespace KorTheBlight.Simulation;

/// <summary>Deterministic fixed-step clock independent from rendering FPS.</summary>
internal sealed class TickManager
{
    public const int TicksPerSecond = 60;
    public const int RareInterval = 250;
    public const int GlobalInterval = 2_000;
    private const int MaxCatchUpTicks = 600;
    private double _accumulator;
    public long TickCount { get; private set; }
    public double SimulationSeconds => TickCount / (double)TicksPerSecond;
    public float SpeedMultiplier { get; private set; } = 1f;
    public int LastAdvanceTickCount { get; private set; }

    public void SetSpeed(float multiplier)
    {
        if (!float.IsFinite(multiplier) || multiplier is < 0.1f or > 6f)
            throw new ArgumentOutOfRangeException(nameof(multiplier), "Speed must be between 0.1x and 6x.");
        SpeedMultiplier = multiplier;
    }

    public int Advance(float elapsedRealSeconds, Action<SimulationTick> onTick)
    {
        ArgumentNullException.ThrowIfNull(onTick);
        if (!float.IsFinite(elapsedRealSeconds) || elapsedRealSeconds <= 0f)
        { LastAdvanceTickCount = 0; return 0; }
        _accumulator = Math.Min(_accumulator + elapsedRealSeconds * SpeedMultiplier,
            MaxCatchUpTicks / (double)TicksPerSecond);
        int processed = 0;
        const double duration = 1d / TicksPerSecond;
        while (_accumulator + 1e-12 >= duration && processed < MaxCatchUpTicks)
        {
            _accumulator -= duration;
            TickCount++;
            processed++;
            onTick(new SimulationTick(TickCount, 1f / TicksPerSecond));
        }
        LastAdvanceTickCount = processed;
        return processed;
    }
}

internal readonly record struct SimulationTick(long Number, float DeltaSeconds)
{
    public bool IsRare => Number % TickManager.RareInterval == 0;
    public bool IsGlobal => Number % TickManager.GlobalInterval == 0;
    public bool IsWorkCycle => Number % 6 == 0; // 10 Hz planning/economy cadence.
}
