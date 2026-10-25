namespace KorTheBlight.Simulation;

/// <summary>Non-violent colony safety hazards. These never apply damage to people or entities.</summary>
internal enum ColonyHazardKind { Fire, ThermalExposure, Blight, StructuralInstability }

internal readonly record struct ColonyHazard(
    int Id, ColonyHazardKind Kind, CellPosition Position, float Severity);

internal readonly record struct SafetyAlarm(
    long Tick, int SensorId, int HazardId, ColonyHazardKind Kind, CellPosition Position, float Severity);

internal sealed class SafetySensor
{
    internal SafetySensor(int id, CellPosition position, int detectionRadius, float powerPerSecond,
        int woodCost, int componentCost)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        if (detectionRadius < 0) throw new ArgumentOutOfRangeException(nameof(detectionRadius));
        if (!float.IsFinite(powerPerSecond) || powerPerSecond < 0)
            throw new ArgumentOutOfRangeException(nameof(powerPerSecond));
        if (woodCost < 0 || componentCost < 0) throw new ArgumentOutOfRangeException(nameof(woodCost));
        Id = id;
        Position = position;
        DetectionRadius = detectionRadius;
        PowerPerSecond = powerPerSecond;
        WoodCost = woodCost;
        ComponentCost = componentCost;
    }

    public int Id { get; }
    public CellPosition Position { get; }
    public int DetectionRadius { get; }
    public float PowerPerSecond { get; }
    public int WoodCost { get; }
    public int ComponentCost { get; }
    public float Condition { get; internal set; } = 100f;
    public bool Enabled { get; set; } = true;
    public bool NeedsMaintenance => Condition < 35f;
}

/// <summary>
/// Deterministic hazard detection and alarm state, independent from rendering and combat.
/// Sensors consume power before they can detect hazards; condition degrades during operation
/// and can be restored through an explicit maintenance action. The system never inflicts damage.
/// </summary>
internal sealed class ColonySafetySystem
{
    private readonly SortedDictionary<int, SafetySensor> _sensors = [];
    private readonly List<SafetyAlarm> _alarms = [];
    private readonly HashSet<(int SensorId, int HazardId)> _activeAlarmPairs = [];
    private long _tick;

    public IReadOnlyCollection<SafetySensor> Sensors => _sensors.Values;
    public IReadOnlyList<SafetyAlarm> Alarms => _alarms.AsReadOnly();
    public long Tick => _tick;
    public float PowerUsedLastStep { get; private set; }

    public bool TryInstallSensor(SafetySensor sensor)
    {
        ArgumentNullException.ThrowIfNull(sensor);
        return _sensors.TryAdd(sensor.Id, sensor);
    }

    public bool RemoveSensor(int sensorId) => _sensors.Remove(sensorId);

    public bool MaintainSensor(int sensorId, float restoredCondition = 25f)
    {
        if (!_sensors.TryGetValue(sensorId, out var sensor)) return false;
        if (!float.IsFinite(restoredCondition) || restoredCondition <= 0f)
            throw new ArgumentOutOfRangeException(nameof(restoredCondition));
        sensor.Condition = Math.Min(100f, sensor.Condition + restoredCondition);
        return true;
    }

    /// <summary>
    /// Advances one fixed simulation interval. Available power is a budget for this interval,
    /// not a stored resource. Sensors are served in stable ID order to keep outcomes reproducible.
    /// </summary>
    public IReadOnlyList<SafetyAlarm> Step(float deltaSeconds, float availablePower,
        IReadOnlyCollection<ColonyHazard> hazards)
    {
        if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        if (!float.IsFinite(availablePower) || availablePower < 0f)
            throw new ArgumentOutOfRangeException(nameof(availablePower));
        ArgumentNullException.ThrowIfNull(hazards);
        _tick++;
        PowerUsedLastStep = 0f;
        var emitted = new List<SafetyAlarm>();
        float remainingPower = availablePower;

        foreach (var sensor in _sensors.Values)
        {
            if (!sensor.Enabled || sensor.Condition <= 0f) continue;
            float demand = sensor.PowerPerSecond * deltaSeconds;
            if (demand > remainingPower + 1e-6f) continue;
            remainingPower -= demand;
            PowerUsedLastStep += demand;
            sensor.Condition = Math.Max(0f, sensor.Condition - deltaSeconds * 0.0025f);

            foreach (var hazard in hazards.OrderBy(item => item.Id))
            {
                if (hazard.Id <= 0 || !float.IsFinite(hazard.Severity) || hazard.Severity <= 0f)
                    continue;
                long dx = (long)sensor.Position.X - hazard.Position.X;
                long dy = (long)sensor.Position.Y - hazard.Position.Y;
                if (dx * dx + dy * dy > (long)sensor.DetectionRadius * sensor.DetectionRadius)
                    continue;
                var pair = (sensor.Id, hazard.Id);
                if (!_activeAlarmPairs.Add(pair)) continue;
                var alarm = new SafetyAlarm(_tick, sensor.Id, hazard.Id, hazard.Kind,
                    hazard.Position, Math.Clamp(hazard.Severity, 0f, 100f));
                _alarms.Add(alarm);
                emitted.Add(alarm);
            }
        }

        // Clear pairs for hazards that are no longer present so a later recurrence is observable.
        var currentHazardIds = hazards.Select(hazard => hazard.Id).ToHashSet();
        _activeAlarmPairs.RemoveWhere(pair => !currentHazardIds.Contains(pair.HazardId));
        return emitted.AsReadOnly();
    }

    public void ClearAlarmHistory() => _alarms.Clear();
}
