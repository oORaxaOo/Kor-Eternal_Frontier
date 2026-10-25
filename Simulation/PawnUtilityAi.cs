namespace KorTheBlight.Simulation;

/// <summary>Normalized resident needs for data-driven, object-offered autonomous actions.</summary>
internal enum PawnNeedKind
{
    Hunger,
    Sleep,
    Social,
    Recreation,
    Comfort
}

/// <summary>
/// Current satisfaction for one need. Value is clamped to [0,1], where 0 is desperate
/// and 1 is fully satisfied. A monotonically increasing revision can be added by persistence later.
/// </summary>
internal readonly record struct PawnNeed(PawnNeedKind Kind, float Value)
{
    public float NormalizedValue => float.IsFinite(Value) ? Math.Clamp(Value, 0f, 1f) : 0f;
}

/// <summary>Per-resident weights; values above one express a stronger-than-normal preference.</summary>
internal sealed class PawnPersonality
{
    private readonly Dictionary<PawnNeedKind, float> _weights = [];

    public float WeightFor(PawnNeedKind kind) =>
        _weights.TryGetValue(kind, out float weight) && float.IsFinite(weight)
            ? Math.Clamp(weight, 0f, 3f)
            : 1f;

    public void SetWeight(PawnNeedKind kind, float weight)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (!float.IsFinite(weight) || weight < 0f || weight > 3f)
            throw new ArgumentOutOfRangeException(nameof(weight), "Personality weights must be between 0 and 3.");
        _weights[kind] = weight;
    }

    public PawnPersonalitySnapshot CaptureSnapshot() => new()
    {
        HungerWeight = WeightFor(PawnNeedKind.Hunger),
        SleepWeight = WeightFor(PawnNeedKind.Sleep),
        SocialWeight = WeightFor(PawnNeedKind.Social),
        RecreationWeight = WeightFor(PawnNeedKind.Recreation),
        ComfortWeight = WeightFor(PawnNeedKind.Comfort)
    };

    public void RestoreSnapshot(PawnPersonalitySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!snapshot.IsValid())
            throw new InvalidDataException("Pawn personality contains an invalid need weight.");
        SetWeight(PawnNeedKind.Hunger, snapshot.HungerWeight);
        SetWeight(PawnNeedKind.Sleep, snapshot.SleepWeight);
        SetWeight(PawnNeedKind.Social, snapshot.SocialWeight);
        SetWeight(PawnNeedKind.Recreation, snapshot.RecreationWeight);
        SetWeight(PawnNeedKind.Comfort, snapshot.ComfortWeight);
    }
}

/// <summary>Versioned, validated preference data stored with its resident.</summary>
internal sealed class PawnPersonalitySnapshot
{
    public float HungerWeight { get; set; } = 1f;
    public float SleepWeight { get; set; } = 1f;
    public float SocialWeight { get; set; } = 1f;
    public float RecreationWeight { get; set; } = 1f;
    public float ComfortWeight { get; set; } = 1f;

    public bool IsValid() =>
        Valid(HungerWeight) && Valid(SleepWeight) && Valid(SocialWeight) &&
        Valid(RecreationWeight) && Valid(ComfortWeight);

    private static bool Valid(float value) => float.IsFinite(value) && value is >= 0f and <= 3f;
}

/// <summary>
/// An action offered by a nearby smart object (bed, dining spot, recreation item, etc.).
/// The planner knows the offer's effect and location, not how the object performs the action.
/// </summary>
internal sealed record SmartObjectOffer(
    int Id,
    string ObjectId,
    string ActionId,
    PawnNeedKind Need,
    CellPosition Position,
    float SatisfactionGain,
    bool IsAvailable = true);

/// <summary>Chosen offer and its score; score is useful for in-game inspection and debugging.</summary>
internal sealed record PawnUtilityDecision(SmartObjectOffer Offer, float Score, PawnNeedKind Need);

/// <summary>
/// Small engine-independent utility planner. Offers are supplied by the world, so adding a new
/// object/action does not require adding special cases to the resident decision logic.
/// </summary>
internal static class PawnUtilityPlanner
{
    public static PawnUtilityDecision? Choose(
        CellPosition pawnPosition,
        IEnumerable<PawnNeed> needs,
        IEnumerable<SmartObjectOffer> offers,
        PawnPersonality? personality = null)
    {
        ArgumentNullException.ThrowIfNull(needs);
        ArgumentNullException.ThrowIfNull(offers);

        var needValues = needs
            .Where(need => Enum.IsDefined(need.Kind))
            .GroupBy(need => need.Kind)
            .ToDictionary(group => group.Key, group => group.Last().NormalizedValue);

        PawnUtilityDecision? best = null;
        foreach (var offer in offers)
        {
            if (!offer.IsAvailable || offer.Id < 0 || string.IsNullOrWhiteSpace(offer.ObjectId) ||
                string.IsNullOrWhiteSpace(offer.ActionId) || !Enum.IsDefined(offer.Need) ||
                !float.IsFinite(offer.SatisfactionGain) || offer.SatisfactionGain <= 0f ||
                !needValues.TryGetValue(offer.Need, out float value))
                continue;

            int distance = Math.Abs(pawnPosition.X - offer.Position.X) + Math.Abs(pawnPosition.Y - offer.Position.Y);
            float urgency = 1f - value;
            // Squared urgency means a nearly satisfied need is much less compelling than a desperate one.
            float score = urgency * urgency * Math.Clamp(offer.SatisfactionGain, 0f, 1f)
                * (personality?.WeightFor(offer.Need) ?? 1f) / (1f + distance);

            if (!float.IsFinite(score) || score <= 0f) continue;
            if (best is null || score > best.Score ||
                (score == best.Score && offer.Id < best.Offer.Id))
                best = new PawnUtilityDecision(offer, score, offer.Need);
        }

        return best;
    }
}
