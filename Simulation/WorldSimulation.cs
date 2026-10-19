using KorTheBlight.Campaign;

namespace KorTheBlight.Simulation;

/// <summary>
/// Coordinates the local settlement and regional world under one fixed-step clock.
/// The renderer is a consumer of this state; it does not advance either simulation.
/// </summary>
internal sealed class WorldSimulation
{
    public const float CampaignUpdateIntervalSeconds = 10f;

    private float _campaignElapsedSeconds;

    public ColonySimulation Colony { get; private set; }
    public CampaignSimulation World { get; private set; }
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

        // The local simulation already supports small fixed steps; the host supplies 0.1s steps.
        Colony.Step(scaledSeconds);
        ElapsedSeconds += scaledSeconds;
        _campaignElapsedSeconds += scaledSeconds;

        // Regional systems evolve automatically on the same clock; the player does not end turns.
        while (_campaignElapsedSeconds >= CampaignUpdateIntervalSeconds)
        {
            _campaignElapsedSeconds -= CampaignUpdateIntervalSeconds;
            World.AdvanceWorldInterval();
        }
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
        _campaignElapsedSeconds = 0f;
        ElapsedSeconds = 0d;
    }
}
