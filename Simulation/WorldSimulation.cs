using KorTheBlight.WorldModel;

namespace KorTheBlight.Simulation;

/// <summary>
/// Coordinates the local settlement and continuous world under one clock. Inventory and storage
/// capacity are owned by the world settlement ledger; the colony is a local simulation view.
/// </summary>
internal sealed class WorldSimulation
{
    private float _simulationAccumulator;
    private const float FixedStepSeconds = 0.1f;

    public ColonySimulation Colony { get; private set; }
    public ContinuousWorldState SpatialWorld { get; private set; }
    public double ElapsedSeconds { get; private set; }
    public bool IsPaused { get; set; }
    public float SpeedMultiplier { get; private set; } = 1f;

    public WorldSimulation(int seed)
    {
        Colony = new ColonySimulation(seed);
        SpatialWorld = ContinuousWorldState.Generate(seed);
        Colony.BindWorldInventory(SpatialWorld, KorSettlementId(SpatialWorld));
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
            ProcessStorytellerThreats();
            _simulationAccumulator -= FixedStepSeconds;
        }
        SpatialWorld.SetInventoryCapacity(KorSettlementId(SpatialWorld), Colony.InventoryCapacity);
        SpatialWorld.Step(scaledSeconds);
        ElapsedSeconds = SpatialWorld.ClockSeconds;
    }

    private void ProcessStorytellerThreats()
    {
        var home = SpatialWorld.Entities.FirstOrDefault(entity =>
            entity.Kind == WorldEntityKind.Settlement && entity.Faction == "Kor");
        if (home == null) return;
        foreach (var incident in Colony.Storyteller.Events
                     .Where(item => item.Code == "wealth_attracts_attention" && item.ThreatPoints > 0)
                     .OrderBy(item => item.Id))
        {
            string prefix = $"Kor Wealth Raid {incident.Id}";
            if (SpatialWorld.Entities.Any(entity => entity.Name.StartsWith(prefix, StringComparison.Ordinal))) continue;
            int units = Math.Clamp(incident.ThreatUnits, 1, 10);
            var random = new Random(unchecked(SpatialWorld.Seed * 397 ^ (int)incident.Id));
            for (int i = 0; i < units; i++)
            {
                double x = Math.Clamp(home.X + random.Next(-4_000, 4_001), 0, ContinuousWorldState.WorldExtent);
                double y = Math.Clamp(home.Y + random.Next(-4_000, 4_001), 0, ContinuousWorldState.WorldExtent);
                if (Math.Abs(x - home.X) + Math.Abs(y - home.Y) < 1_500)
                    x = Math.Clamp(x + 2_000, 0, ContinuousWorldState.WorldExtent);
                var raider = SpatialWorld.AddEntity(WorldEntityKind.Army, $"{prefix} / Unit {i + 1}", "Raiders", x, y);
                var route = WorldRoutePlanner.FindRoute(SpatialWorld, x, y, home.X, home.Y, 40);
                double straightDistance = Math.Sqrt(Math.Pow(home.X - x, 2) + Math.Pow(home.Y - y, 2));
                double speed = straightDistance / Math.Max(1, route.EstimatedSeconds);
                SpatialWorld.OrderMove(raider.Id, home.X, home.Y, Math.Max(1, speed));
            }
            Colony.SetMessage($"STORYTELLER RAID: {units} hostile units generated from {incident.ThreatPoints} threat points and advancing toward Kor.");
        }
    }

    public void SetSpeed(float multiplier)
    {
        if (!float.IsFinite(multiplier) || multiplier is < 0.25f or > 4f)
            throw new ArgumentOutOfRangeException(nameof(multiplier), "Speed must be between 0.25x and 4x.");
        SpeedMultiplier = multiplier;
    }

    private static long KorSettlementId(ContinuousWorldState world)
        => world.Entities.Single(entity => entity.Kind == WorldEntityKind.Settlement && entity.Faction == "Kor").Id;

    public void Restore(LoadedGameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        Colony = session.Colony;
        SpatialWorld = session.ContinuousWorld;
        Colony.BindWorldInventory(SpatialWorld, KorSettlementId(SpatialWorld));
        ElapsedSeconds = SpatialWorld.ClockSeconds;
        _simulationAccumulator = 0f;
    }
}
