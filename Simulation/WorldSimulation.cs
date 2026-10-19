using KorTheBlight.Campaign;
using KorTheBlight.WorldModel;

namespace KorTheBlight.Simulation;

/// <summary>
/// Coordinates the local settlement and continuous world entities under one clock. CampaignSimulation remains a save-migration compatibility snapshot, not an active world simulation.
/// The renderer is a consumer of this state; it does not advance either simulation.
/// </summary>
internal sealed class WorldSimulation
{
    private float _simulationAccumulator;
    private const float FixedStepSeconds = 0.1f;

    public ColonySimulation Colony { get; private set; }
    public CampaignSimulation World { get; private set; }
    public ContinuousWorldState SpatialWorld { get; private set; }
    public double ElapsedSeconds { get; private set; }
    public bool IsPaused { get; set; } = false;
    public float SpeedMultiplier { get; private set; } = 1f;

    public WorldSimulation(int seed)
        : this(new ColonySimulation(), new CampaignSimulation(seed))
    {
    }

    public WorldSimulation(ColonySimulation colony, CampaignSimulation world)
    {
        Colony = colony ?? throw new ArgumentNullException(nameof(colony));
        World = world ?? throw new ArgumentNullException(nameof(world));
        SpatialWorld = ContinuousWorldState.Generate(World.State.Seed);
    }

    /// <summary>Advances the unified simulation clock. deltaSeconds is real elapsed time.</summary>
    public void Step(float deltaSeconds)
    {
        if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds), "Elapsed time must be finite and non-negative.");
        if (deltaSeconds == 0f || IsPaused) return;

        float scaledSeconds = deltaSeconds * SpeedMultiplier;
        if (!float.IsFinite(scaledSeconds))
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds), "Scaled elapsed time must be finite.");

        // Use a stable fixed step even if callers supply uneven frame times.
        _simulationAccumulator += scaledSeconds;
        while (_simulationAccumulator >= FixedStepSeconds)
        {
            Colony.Step(FixedStepSeconds);
            _simulationAccumulator -= FixedStepSeconds;
        }
        SpatialWorld.Step(scaledSeconds);
        ElapsedSeconds = SpatialWorld.ClockSeconds;
    }

    public void SetSpeed(float multiplier)
    {
        if (!float.IsFinite(multiplier) || multiplier is < 0.25f or > 4f)
            throw new ArgumentOutOfRangeException(nameof(multiplier), "Speed must be between 0.25x and 4x.");
        SpeedMultiplier = multiplier;
    }

    public void Restore(LoadedGameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        Colony = session.Colony;
        World = session.Campaign;
        SpatialWorld = session.ContinuousWorld;
        ElapsedSeconds = SpatialWorld.ClockSeconds;
        _simulationAccumulator = 0f;
    }
}
