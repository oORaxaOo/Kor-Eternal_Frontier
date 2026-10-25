namespace KorTheBlight.Simulation;

internal enum ResidentBodyPart { Head, Torso, LeftArm, RightArm, LeftLeg, RightLeg }
internal enum ResidentTraumaKind { Bruise, Cut, Fracture, ThermalBurn, Frostbite }
internal sealed record ResidentInjury(long Id, ResidentBodyPart Part, ResidentTraumaKind Kind, float Severity, long StartedTick);
internal sealed record ResidentBodyPartHealth(ResidentBodyPart Part, float Health);

internal sealed class ResidentBodySnapshot
{
    public int SchemaVersion { get; set; } = 1;
    public long NextInjuryId { get; set; } = 1;
    public List<ResidentBodyPartHealth> Parts { get; set; } = [];
    public List<ResidentInjury> Injuries { get; set; } = [];

    public bool IsValid(int residentId)
    {
        if (residentId <= 0 || SchemaVersion != 1 || NextInjuryId < 1 ||
            Parts is null || Parts.Count != Enum.GetValues<ResidentBodyPart>().Length ||
            Parts.Any(part => !Enum.IsDefined(part.Part) || !float.IsFinite(part.Health) || part.Health is < 0 or > 100) ||
            Parts.Select(part => part.Part).Distinct().Count() != Parts.Count ||
            !Enum.GetValues<ResidentBodyPart>().All(part => Parts.Any(entry => entry.Part == part)) ||
            Injuries is null || Injuries.Count > ResidentBodyState.MaximumInjuries)
            return false;
        long previousId = 0;
        foreach (var injury in Injuries)
        {
            if (injury is null || injury.Id <= previousId || injury.Id >= NextInjuryId ||
                !Enum.IsDefined(injury.Part) || !Enum.IsDefined(injury.Kind) ||
                !float.IsFinite(injury.Severity) || injury.Severity is <= 0 or > 100 || injury.StartedTick < 0)
                return false;
            previousId = injury.Id;
        }
        return true;
    }
}

/// <summary>Compact individual body state; injuries have real movement/work consequences and persist in saves.</summary>
internal sealed class ResidentBodyState
{
    public const int MaximumInjuries = 16;
    private readonly Dictionary<ResidentBodyPart, float> _health =
        Enum.GetValues<ResidentBodyPart>().ToDictionary(part => part, _ => 100f);
    private readonly List<ResidentInjury> _injuries = [];
    private long _nextInjuryId = 1;

    public IReadOnlyDictionary<ResidentBodyPart, float> Parts => _health;
    public IReadOnlyList<ResidentInjury> Injuries => _injuries;
    public float TotalHealth => _health.Values.Average();
    public int ActiveInjuryCount => _injuries.Count;
    public bool IsCritical => _health[ResidentBodyPart.Head] <= 0 || _health[ResidentBodyPart.Torso] <= 0;
    public float MovementMultiplier
    {
        get
        {
            float legs = (_health[ResidentBodyPart.LeftLeg] + _health[ResidentBodyPart.RightLeg]) / 200f;
            return Math.Clamp(0.25f + 0.75f * legs, 0.25f, 1f);
        }
    }
    public float WorkMultiplier
    {
        get
        {
            float arms = (_health[ResidentBodyPart.LeftArm] + _health[ResidentBodyPart.RightArm]) / 200f;
            float torso = _health[ResidentBodyPart.Torso] / 100f;
            float multiplier = 0.20f + 0.65f * arms + 0.15f * torso;
            if (IsCritical) multiplier *= 0.5f;
            return Math.Clamp(multiplier, 0.10f, 1f);
        }
    }

    /// <returns>True only when this call creates a new injury record.</returns>
    public bool ApplyTrauma(ResidentBodyPart part, ResidentTraumaKind kind, float damage, long tick)
    {
        if (!Enum.IsDefined(part)) throw new ArgumentOutOfRangeException(nameof(part));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (!float.IsFinite(damage) || damage < 0 || damage > 100) throw new ArgumentOutOfRangeException(nameof(damage));
        if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
        if (damage <= 0) return false;

        _health[part] = Math.Max(0f, _health[part] - damage);
        int existingIndex = _injuries.FindIndex(injury => injury.Part == part && injury.Kind == kind);
        if (existingIndex >= 0)
        {
            var existing = _injuries[existingIndex];
            _injuries[existingIndex] = existing with { Severity = Math.Min(100f, existing.Severity + damage) };
            return false;
        }

        if (_injuries.Count >= MaximumInjuries)
        {
            int weakest = _injuries.Select((injury, index) => (injury, index))
                .OrderBy(pair => pair.injury.Severity)
                .ThenByDescending(pair => pair.injury.StartedTick)
                .First().index;
            _injuries.RemoveAt(weakest);
        }
        _injuries.Add(new ResidentInjury(_nextInjuryId++, part, kind, damage, tick));
        return true;
    }

    /// <summary>
    /// Sustained dangerous thermal exposure damages the torso. Recovery requires both a safe tile
    /// and a resting resident; all rates use simulated time rather than render-frame count.
    /// </summary>
    public bool Advance(float deltaSeconds, float thermalExposure, float temperatureCelsius, long tick, bool resting, bool safeEnvironment)
    {
        if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        if (!float.IsFinite(thermalExposure) || thermalExposure is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(thermalExposure));
        if (!float.IsFinite(temperatureCelsius) || temperatureCelsius < -273.15f || temperatureCelsius > 5000)
            throw new ArgumentOutOfRangeException(nameof(temperatureCelsius));
        bool newTrauma = false;
        if (thermalExposure >= 70f && deltaSeconds > 0 && (temperatureCelsius > 35f || temperatureCelsius < 0f))
        {
            float damage = deltaSeconds * (thermalExposure - 70f) / 30f * 0.4f;
            var kind = temperatureCelsius > 35f ? ResidentTraumaKind.ThermalBurn : ResidentTraumaKind.Frostbite;
            newTrauma = ApplyTrauma(ResidentBodyPart.Torso, kind, damage, tick);
        }

        if (resting && safeEnvironment && deltaSeconds > 0)
        {
            float healing = deltaSeconds * 0.04f;
            foreach (var part in Enum.GetValues<ResidentBodyPart>())
                _health[part] = Math.Min(100f, _health[part] + healing);
            for (int i = _injuries.Count - 1; i >= 0; i--)
            {
                var injury = _injuries[i] with { Severity = _injuries[i].Severity - deltaSeconds * 0.08f };
                if (injury.Severity <= 0) _injuries.RemoveAt(i);
                else _injuries[i] = injury;
            }
        }
        return newTrauma;
    }

    public ResidentBodySnapshot CaptureSnapshot() => new()
    {
        SchemaVersion = 1,
        NextInjuryId = _nextInjuryId,
        Parts = _health.OrderBy(pair => pair.Key).Select(pair => new ResidentBodyPartHealth(pair.Key, pair.Value)).ToList(),
        Injuries = _injuries.OrderBy(injury => injury.Id).ToList()
    };

    public void RestoreSnapshot(ResidentBodySnapshot snapshot, int residentId)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!snapshot.IsValid(residentId)) throw new InvalidDataException("Resident body data is invalid.");
        _health.Clear();
        foreach (var part in snapshot.Parts) _health.Add(part.Part, part.Health);
        _injuries.Clear();
        _injuries.AddRange(snapshot.Injuries.OrderBy(injury => injury.Id));
        _nextInjuryId = snapshot.NextInjuryId;
    }
}
