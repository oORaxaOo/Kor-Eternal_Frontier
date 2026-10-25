namespace KorTheBlight.Simulation;

internal enum PawnBodyPartKind : byte { Head, Torso, LeftArm, RightArm, LeftLeg, RightLeg }
internal enum PawnDamageKind : byte { Blunt, Cut, Ballistic, Thermal, Cold, Infection }

internal sealed record PawnBodyPartView(
    PawnBodyPartKind Part,
    float CurrentHealth,
    float MaximumHealth,
    float MobilityFactor,
    float ManipulationFactor);

internal sealed record PawnInjuryView(
    int Id,
    PawnBodyPartKind Part,
    PawnDamageKind Kind,
    float Severity,
    float BleedingPerMinute,
    float Infection,
    bool IsTreated,
    bool IsHealed,
    double CreatedAtSeconds);

/// <summary>
/// Persistent anatomical condition for one resident. Body-part health sums to 100 initially, so
/// aggregate health remains comparable to the tactical combat health pool while individual injuries
/// influence consciousness, walking, manipulation, pain, and medical urgency.
/// </summary>
internal sealed class PawnHealthState
{
    public const int MaximumInjuries = 256;
    public const float MaximumBloodLoss = 100f;

    private sealed class BodyPartState(PawnBodyPartKind kind, float maximumHealth, float mobilityFactor, float manipulationFactor)
    {
        public PawnBodyPartKind Kind { get; } = kind;
        public float MaximumHealth { get; } = maximumHealth;
        public float Health { get; set; } = maximumHealth;
        public float MobilityFactor { get; } = mobilityFactor;
        public float ManipulationFactor { get; } = manipulationFactor;
    }

    private sealed class InjuryState(
        int id, PawnBodyPartKind part, PawnDamageKind kind, float severity,
        float bleedingPerMinute, float infection, double createdAtSeconds)
    {
        public int Id { get; } = id;
        public PawnBodyPartKind Part { get; } = part;
        public PawnDamageKind Kind { get; } = kind;
        public float Severity { get; set; } = severity;
        public float BleedingPerMinute { get; set; } = bleedingPerMinute;
        public float Infection { get; set; } = infection;
        public bool IsTreated { get; set; }
        public bool IsHealed { get; set; }
        public double CreatedAtSeconds { get; } = createdAtSeconds;
    }

    private readonly SortedDictionary<PawnBodyPartKind, BodyPartState> _parts = new();
    private readonly List<InjuryState> _injuries = [];
    private int _nextInjuryId = 1;

    public double ElapsedSeconds { get; private set; }
    public float BloodLoss { get; private set; }
    public bool IsDead { get; private set; }
    public int InjuryCount => _injuries.Count;
    public IReadOnlyList<PawnBodyPartView> BodyParts => _parts.Values
        .Select(part => new PawnBodyPartView(part.Kind, part.Health, part.MaximumHealth,
            part.MobilityFactor, part.ManipulationFactor)).ToArray();
    public IReadOnlyList<PawnInjuryView> Injuries => _injuries
        .Select(injury => new PawnInjuryView(injury.Id, injury.Part, injury.Kind, injury.Severity,
            injury.BleedingPerMinute, injury.Infection, injury.IsTreated, injury.IsHealed,
            injury.CreatedAtSeconds)).ToArray();

    public float OverallHealth => Math.Clamp(_parts.Values.Sum(part => part.Health), 0f, 100f);
    public float Pain => Math.Clamp(_injuries.Where(injury => !injury.IsHealed)
        .Sum(injury => injury.Severity * (injury.IsTreated ? 0.15f : 0.30f)), 0f, 100f);
    public float Mobility => FactorAverage(PawnBodyPartKind.LeftLeg, PawnBodyPartKind.RightLeg);
    public float Manipulation => FactorAverage(PawnBodyPartKind.LeftArm, PawnBodyPartKind.RightArm);
    public float Consciousness
    {
        get
        {
            float head = PartRatio(PawnBodyPartKind.Head);
            float torso = PartRatio(PawnBodyPartKind.Torso);
            float circulation = Math.Clamp(1f - BloodLoss / MaximumBloodLoss, 0f, 1f);
            float painPenalty = Math.Clamp(1f - Pain / 180f, 0.25f, 1f);
            return Math.Clamp((head * 0.45f + torso * 0.55f) * circulation * painPenalty, 0f, 1f);
        }
    }
    public bool IsIncapacitated => IsDead || Consciousness < 0.18f || Mobility < 0.10f;
    public bool NeedsMedicalCare => IsDead || BloodLoss >= 10f ||
        _injuries.Any(injury => !injury.IsHealed && (injury.Severity >= 1f || injury.BleedingPerMinute > 0.05f || injury.Infection >= 0.15f));

    public PawnHealthState()
    {
        _parts.Add(PawnBodyPartKind.Head, new BodyPartState(PawnBodyPartKind.Head, 15f, 0f, 0f));
        _parts.Add(PawnBodyPartKind.Torso, new BodyPartState(PawnBodyPartKind.Torso, 30f, 0.6f, 0.6f));
        _parts.Add(PawnBodyPartKind.LeftArm, new BodyPartState(PawnBodyPartKind.LeftArm, 10f, 0f, 0.5f));
        _parts.Add(PawnBodyPartKind.RightArm, new BodyPartState(PawnBodyPartKind.RightArm, 10f, 0f, 0.5f));
        _parts.Add(PawnBodyPartKind.LeftLeg, new BodyPartState(PawnBodyPartKind.LeftLeg, 17.5f, 0.5f, 0f));
        _parts.Add(PawnBodyPartKind.RightLeg, new BodyPartState(PawnBodyPartKind.RightLeg, 17.5f, 0.5f, 0f));
    }

    public float PartHealth(PawnBodyPartKind part) => _parts.TryGetValue(part, out var state) ? state.Health : 0f;

    /// <summary>Applies damage to a selected body part; excess damage spills to the torso/other parts.</summary>
    public PawnInjuryView ApplyDamage(PawnBodyPartKind part, PawnDamageKind kind, float damage, double elapsedSeconds)
    {
        if (!Enum.IsDefined(part)) throw new ArgumentOutOfRangeException(nameof(part));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (!float.IsFinite(damage) || damage <= 0f || damage > 100f)
            throw new ArgumentOutOfRangeException(nameof(damage), "Damage must be finite and between 0 and 100.");
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0d || elapsedSeconds < ElapsedSeconds)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds), "Health events must use monotonic simulation time.");
        if (IsDead) throw new InvalidOperationException("Cannot injure a dead resident.");

        ElapsedSeconds = elapsedSeconds;
        float remaining = Math.Min(damage, OverallHealth);
        var affected = new List<PawnBodyPartKind> { part };
        foreach (var candidate in new[] { PawnBodyPartKind.Torso, PawnBodyPartKind.Head, PawnBodyPartKind.LeftArm,
                     PawnBodyPartKind.RightArm, PawnBodyPartKind.LeftLeg, PawnBodyPartKind.RightLeg })
            if (!affected.Contains(candidate)) affected.Add(candidate);

        foreach (var targetPart in affected)
        {
            if (remaining <= 0.0001f) break;
            var state = _parts[targetPart];
            float applied = Math.Min(state.Health, remaining);
            state.Health = Math.Max(0f, state.Health - applied);
            remaining -= applied;
        }

        float bleeding = kind switch
        {
            PawnDamageKind.Cut => damage * 0.12f,
            PawnDamageKind.Ballistic => damage * 0.18f,
            PawnDamageKind.Thermal => damage * 0.035f,
            PawnDamageKind.Infection => 0.0f,
            _ => 0.0f
        };
        float infection = kind is PawnDamageKind.Cut or PawnDamageKind.Ballistic ? Math.Clamp(damage * 0.004f, 0f, 0.35f) :
            kind == PawnDamageKind.Infection ? Math.Clamp(damage / 100f, 0f, 1f) : 0f;
        var injury = AddInjury(part, kind, damage, bleeding, infection, elapsedSeconds);

        if (_parts[PawnBodyPartKind.Head].Health <= 0f || _parts[PawnBodyPartKind.Torso].Health <= 0f || OverallHealth <= 0.001f)
            IsDead = true;
        return injury;
    }

    public bool TreatInjury(int injuryId)
    {
        var injury = _injuries.FirstOrDefault(item => item.Id == injuryId && !item.IsHealed);
        if (injury is null) return false;
        injury.IsTreated = true;
        injury.BleedingPerMinute *= 0.10f;
        injury.Infection *= 0.20f;
        return true;
    }

    public int? TreatMostUrgentInjury()
    {
        var injury = _injuries.Where(item => !item.IsHealed)
            .OrderByDescending(item => item.BleedingPerMinute * 2f + item.Infection * 20f + item.Severity)
            .ThenBy(item => item.Id).FirstOrDefault();
        if (injury is null || !TreatInjury(injury.Id)) return null;
        return injury.Id;
    }

    public void Advance(float deltaSeconds)
    {
        if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0f || deltaSeconds > 5f)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds), "Health advances in bounded simulation ticks.");
        if (deltaSeconds == 0f || IsDead) return;

        ElapsedSeconds += deltaSeconds;
        foreach (var injury in _injuries)
        {
            if (injury.IsHealed) continue;
            float bleedingRate = injury.BleedingPerMinute * (injury.IsTreated ? 0.10f : 1f);
            BloodLoss = Math.Clamp(BloodLoss + bleedingRate * deltaSeconds / 60f, 0f, MaximumBloodLoss);

            // Infections develop slowly in untreated wounds; treatment reduces the rate and current load.
            if (!injury.IsTreated && injury.Infection > 0f)
            {
                double age = ElapsedSeconds - injury.CreatedAtSeconds;
                if (age > 21_600d)
                    injury.Infection = Math.Clamp(injury.Infection + (float)(deltaSeconds / 86_400d) * 0.08f, 0f, 1f);
            }

            float healingPerSecond = injury.IsTreated ? 0.00040f : 0.00007f;
            injury.Severity = Math.Max(0f, injury.Severity - deltaSeconds * healingPerSecond);
            if (injury.Severity <= 0.001f && injury.BleedingPerMinute <= 0.05f && injury.Infection <= 0.05f)
            {
                injury.Severity = 0f;
                injury.BleedingPerMinute = 0f;
                injury.Infection = 0f;
                injury.IsHealed = true;
            }
        }

        foreach (var part in _parts.Values)
        {
            if (part.Health >= part.MaximumHealth) continue;
            bool treatedHere = _injuries.Any(injury => injury.Part == part.Kind && injury.IsTreated && !injury.IsHealed);
            float healPerSecond = (treatedHere ? 0.00040f : 0.00007f) * deltaSeconds;
            part.Health = Math.Min(part.MaximumHealth, part.Health + healPerSecond);
        }

        if (BloodLoss >= MaximumBloodLoss || _parts[PawnBodyPartKind.Head].Health <= 0f ||
            _parts[PawnBodyPartKind.Torso].Health <= 0f || OverallHealth <= 0.001f)
            IsDead = true;
    }

    public PawnHealthSnapshot CaptureSnapshot() => new()
    {
        ElapsedSeconds = ElapsedSeconds,
        BloodLoss = BloodLoss,
        IsDead = IsDead,
        NextInjuryId = _nextInjuryId,
        BodyParts = _parts.Values.Select(part => new PawnBodyPartSnapshot
        {
            Part = part.Kind, CurrentHealth = part.Health, MaximumHealth = part.MaximumHealth
        }).ToList(),
        Injuries = _injuries.Select(injury => new PawnInjurySnapshot
        {
            Id = injury.Id, Part = injury.Part, Kind = injury.Kind, Severity = injury.Severity,
            BleedingPerMinute = injury.BleedingPerMinute, Infection = injury.Infection,
            IsTreated = injury.IsTreated, IsHealed = injury.IsHealed, CreatedAtSeconds = injury.CreatedAtSeconds
        }).ToList()
    };

    public void RestoreSnapshot(PawnHealthSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        snapshot.Validate();
        foreach (var part in snapshot.BodyParts)
            _parts[part.Part].Health = part.CurrentHealth;
        _injuries.Clear();
        _injuries.AddRange(snapshot.Injuries.OrderBy(injury => injury.Id).Select(injury => new InjuryState(
            injury.Id, injury.Part, injury.Kind, injury.Severity, injury.BleedingPerMinute,
            injury.Infection, injury.CreatedAtSeconds)
        {
            IsTreated = injury.IsTreated, IsHealed = injury.IsHealed
        }));
        ElapsedSeconds = snapshot.ElapsedSeconds;
        BloodLoss = snapshot.BloodLoss;
        IsDead = snapshot.IsDead;
        _nextInjuryId = snapshot.NextInjuryId;
    }

    private float FactorAverage(PawnBodyPartKind first, PawnBodyPartKind second)
    {
        var a = _parts[first]; var b = _parts[second];
        float firstRatio = a.Health / a.MaximumHealth;
        float secondRatio = b.Health / b.MaximumHealth;
        return Math.Clamp((firstRatio * a.MobilityFactor + secondRatio * b.MobilityFactor) /
                          Math.Max(0.001f, a.MobilityFactor + b.MobilityFactor), 0f, 1f);
    }

    private float PartRatio(PawnBodyPartKind part)
    {
        var state = _parts[part];
        return state.Health / state.MaximumHealth;
    }

    private PawnInjuryView AddInjury(PawnBodyPartKind part, PawnDamageKind kind, float severity,
        float bleeding, float infection, double atSeconds)
    {
        if (_injuries.Count >= MaximumInjuries)
        {
            var healed = _injuries.FirstOrDefault(item => item.IsHealed);
            if (healed is not null) _injuries.Remove(healed);
            else
            {
                var existing = _injuries.FirstOrDefault(item => item.Part == part && item.Kind == kind && !item.IsHealed)
                    ?? _injuries.OrderBy(item => item.Id).First();
                existing.Severity = Math.Clamp(existing.Severity + severity, 0f, 100f);
                existing.BleedingPerMinute = Math.Clamp(existing.BleedingPerMinute + bleeding, 0f, 100f);
                existing.Infection = Math.Clamp(existing.Infection + infection, 0f, 1f);
                existing.IsTreated = false;
                return ToView(existing);
            }
        }

        var created = new InjuryState(_nextInjuryId++, part, kind, severity, bleeding, infection, atSeconds);
        _injuries.Add(created);
        return ToView(created);
    }

    private static PawnInjuryView ToView(InjuryState injury) => new(injury.Id, injury.Part, injury.Kind,
        injury.Severity, injury.BleedingPerMinute, injury.Infection, injury.IsTreated, injury.IsHealed,
        injury.CreatedAtSeconds);
}

internal sealed class PawnHealthSnapshot
{
    public double ElapsedSeconds { get; set; }
    public float BloodLoss { get; set; }
    public bool IsDead { get; set; }
    public int NextInjuryId { get; set; } = 1;
    public List<PawnBodyPartSnapshot> BodyParts { get; set; } = [];
    public List<PawnInjurySnapshot> Injuries { get; set; } = [];

    public void Validate()
    {
        if (!double.IsFinite(ElapsedSeconds) || ElapsedSeconds < 0d ||
            !float.IsFinite(BloodLoss) || BloodLoss is < 0f or > PawnHealthState.MaximumBloodLoss ||
            NextInjuryId < 1 || BodyParts is null || Injuries is null ||
            BodyParts.Count != Enum.GetValues<PawnBodyPartKind>().Length ||
            Injuries.Count > PawnHealthState.MaximumInjuries)
            throw new InvalidDataException("Pawn health save contains invalid top-level state.");

        var parts = new HashSet<PawnBodyPartKind>();
        foreach (var part in BodyParts)
        {
            if (!Enum.IsDefined(part.Part) || !parts.Add(part.Part) ||
                !float.IsFinite(part.CurrentHealth) || !float.IsFinite(part.MaximumHealth) ||
                part.CurrentHealth < 0f || part.MaximumHealth <= 0f || part.CurrentHealth > part.MaximumHealth)
                throw new InvalidDataException("Pawn health save contains an invalid body part.");
        }

        var injuryIds = new HashSet<int>();
        foreach (var injury in Injuries)
        {
            if (injury.Id < 1 || !injuryIds.Add(injury.Id) || injury.Id >= NextInjuryId ||
                !Enum.IsDefined(injury.Part) || !Enum.IsDefined(injury.Kind) ||
                !float.IsFinite(injury.Severity) || injury.Severity < 0f || injury.Severity > 100f ||
                !float.IsFinite(injury.BleedingPerMinute) || injury.BleedingPerMinute < 0f || injury.BleedingPerMinute > 100f ||
                !float.IsFinite(injury.Infection) || injury.Infection < 0f || injury.Infection > 1f ||
                !double.IsFinite(injury.CreatedAtSeconds) || injury.CreatedAtSeconds < 0d ||
                injury.CreatedAtSeconds > ElapsedSeconds ||
                (injury.IsHealed && (injury.Severity > 0.01f || injury.BleedingPerMinute > 0.05f || injury.Infection > 0.05f)))
                throw new InvalidDataException("Pawn health save contains an invalid injury.");
        }

        if (IsDead && BodyParts.Sum(part => part.CurrentHealth) > 0.001f &&
            BloodLoss < PawnHealthState.MaximumBloodLoss &&
            BodyParts.All(part => part.Part is not (PawnBodyPartKind.Head or PawnBodyPartKind.Torso) ||
                                  part.CurrentHealth > 0f))
            throw new InvalidDataException("Pawn health save marks a living body as dead without a lethal condition.");
    }
}

internal sealed class PawnBodyPartSnapshot
{
    public PawnBodyPartKind Part { get; set; }
    public float CurrentHealth { get; set; }
    public float MaximumHealth { get; set; }
}

internal sealed class PawnInjurySnapshot
{
    public int Id { get; set; }
    public PawnBodyPartKind Part { get; set; }
    public PawnDamageKind Kind { get; set; }
    public float Severity { get; set; }
    public float BleedingPerMinute { get; set; }
    public float Infection { get; set; }
    public bool IsTreated { get; set; }
    public bool IsHealed { get; set; }
    public double CreatedAtSeconds { get; set; }
}
