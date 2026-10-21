namespace KorTheBlight.Simulation;

/// <summary>Deterministic, bounded social state. The caller supplies nearby resident pairs in stable ID order.</summary>
internal sealed class SocialRelationshipNetwork
{
    public const int MinimumScore = -100;
    public const int MaximumScore = 100;
    public const int MaximumHistoryPerPair = 12;
    public const int MaximumPendingEvents = 4096;

    private readonly SortedDictionary<ResidentPair, SocialRelationship> _relationships = new();
    private readonly Queue<SocialSimulationEvent> _events = new();
    private long _nextEventId = 1;

    public IReadOnlyCollection<SocialRelationship> Relationships => _relationships.Values;
    public int RelationshipCount => _relationships.Count;
    public int PendingEventCount => _events.Count;

    public SocialRelationship? Get(int residentA, int residentB)
    {
        if (residentA <= 0 || residentB <= 0 || residentA == residentB) return null;
        return _relationships.GetValueOrDefault(ResidentPair.Create(residentA, residentB));
    }

    /// <summary>
    /// Records one encounter. Scores are deterministic and bounded; no random source is used.
    /// Mood values are expected to be normalized to [0,100].
    /// </summary>
    public void RecordEncounter(int residentA, int residentB, long tick, float stressA, float hungerA,
        float fatigueA, bool mentalBreakA, float stressB, float hungerB, float fatigueB, bool mentalBreakB)
    {
        if (residentA <= 0 || residentB <= 0 || residentA == residentB || tick < 0) return;
        if (!ValidNeed(stressA) || !ValidNeed(hungerA) || !ValidNeed(fatigueA) ||
            !ValidNeed(stressB) || !ValidNeed(hungerB) || !ValidNeed(fatigueB)) return;

        var pair = ResidentPair.Create(residentA, residentB);
        bool firstMeeting = !_relationships.TryGetValue(pair, out var relation);
        if (firstMeeting)
        {
            relation = new SocialRelationship(pair.ResidentA, pair.ResidentB);
            _relationships.Add(pair, relation);
            Enqueue(SocialEventKind.FirstMeeting, pair, tick, 0, 0, "Residents encountered one another for the first time.");
        }

        int previousAffinity = relation!.Affinity;
        int previousTension = relation.Tension;
        int previousTrust = relation.Trust;
        int pressureA = (int)MathF.Round((stressA + hungerA * 0.5f + fatigueA * 0.25f) / 35f);
        int pressureB = (int)MathF.Round((stressB + hungerB * 0.5f + fatigueB * 0.25f) / 35f);
        int delta = (mentalBreakA || mentalBreakB) ? -5 : pressureA + pressureB >= 5 ? -3 : pressureA + pressureB <= 1 ? 2 : 0;
        relation.Affinity = Math.Clamp(relation.Affinity + delta, MinimumScore, MaximumScore);
        relation.Tension = Math.Clamp(relation.Tension + (delta < 0 ? -delta : delta == 0 ? 0 : -1), 0, MaximumScore);
        int trustDelta = mentalBreakA || mentalBreakB ? -5 : delta < 0 ? -3 : delta > 0 ? 2 : 0;
        relation.Trust = Math.Clamp(relation.Trust + trustDelta, MinimumScore, MaximumScore);
        relation.EncounterCount = checked(relation.EncounterCount + 1);
        relation.LastEncounterTick = tick;
        relation.AddHistory(new SocialEncounterHistory(tick, delta, relation.Affinity, relation.Tension));

        if (previousAffinity < 60 && relation.Affinity >= 60)
            Enqueue(SocialEventKind.FriendshipMilestone, pair, tick, previousAffinity, relation.Affinity, "Affinity crossed the friendship threshold.");
        else if (previousAffinity > -60 && relation.Affinity <= -60)
            Enqueue(SocialEventKind.ConflictMilestone, pair, tick, previousAffinity, relation.Affinity, "Affinity crossed the conflict threshold.");
        if (previousTension < 60 && relation.Tension >= 60)
            Enqueue(SocialEventKind.TensionMilestone, pair, tick, previousTension, relation.Tension, "Interpersonal tension crossed its threshold.");
        if (previousTrust > -50 && relation.Trust <= -50)
            Enqueue(SocialEventKind.TrustMilestone, pair, tick, previousTrust, relation.Trust, "Trust crossed the breakdown threshold.");
        Enqueue(SocialEventKind.EncounterResolved, pair, tick, previousAffinity, relation.Affinity,
            delta < 0 ? "A strained encounter reinforced negative feelings." : delta > 0 ? "A calm encounter improved familiarity." : "The encounter left the relationship unchanged.");
    }

    public IReadOnlyList<SocialSimulationEvent> DrainEvents(int maximum = 1024)
    {
        if (maximum < 0) throw new ArgumentOutOfRangeException(nameof(maximum));
        var result = new List<SocialSimulationEvent>(Math.Min(maximum, _events.Count));
        while (result.Count < maximum && _events.Count > 0) result.Add(_events.Dequeue());
        return result;
    }

    public SocialNetworkSnapshot CaptureSnapshot() => new()
    {
        NextEventId = _nextEventId,
        Relationships = _relationships.Values.Select(x => x.ToSnapshot()).ToList(),
        PendingEvents = _events.ToList()
    };

    public static SocialRelationshipNetwork Restore(SocialNetworkSnapshot snapshot, IReadOnlySet<int> residentIds)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(residentIds);
        if (snapshot.NextEventId < 1 || snapshot.Relationships is null || snapshot.PendingEvents is null)
            throw new InvalidDataException("Social save is missing required state.");
        if (snapshot.Relationships.Count > 250_000 || snapshot.PendingEvents.Count > MaximumPendingEvents)
            throw new InvalidDataException("Social save exceeds supported collection bounds.");
        var network = new SocialRelationshipNetwork { _nextEventId = snapshot.NextEventId };
        foreach (var state in snapshot.Relationships)
        {
            if (state.ResidentA <= 0 || state.ResidentB <= 0 || state.ResidentA == state.ResidentB ||
                !residentIds.Contains(state.ResidentA) || !residentIds.Contains(state.ResidentB) ||
                state.Affinity is < MinimumScore or > MaximumScore || state.Trust is < MinimumScore or > MaximumScore || state.Tension is < 0 or > MaximumScore ||
                state.EncounterCount < 1 || state.LastEncounterTick < 0 || state.History is null ||
                state.History.Count > MaximumHistoryPerPair ||
                state.History.Any(h => h.Tick < 0 || h.Affinity is < MinimumScore or > MaximumScore || h.Tension is < 0 or > MaximumScore))
                throw new InvalidDataException("Social save contains an invalid relationship.");
            var pair = ResidentPair.Create(state.ResidentA, state.ResidentB);
            if (network._relationships.ContainsKey(pair)) throw new InvalidDataException("Social save contains a duplicate resident pair.");
            network._relationships.Add(pair, SocialRelationship.FromSnapshot(state));
        }
        foreach (var item in snapshot.PendingEvents)
        {
            if (item.Id <= 0 || item.Tick < 0 || !Enum.IsDefined(item.Kind) ||
                !residentIds.Contains(item.ResidentA) || !residentIds.Contains(item.ResidentB) ||
                item.ResidentA == item.ResidentB || item.Message is null)
                throw new InvalidDataException("Social save contains an invalid pending event.");
            network._events.Enqueue(item);
        }
        if (snapshot.PendingEvents.Any(x => x.Id >= snapshot.NextEventId))
            throw new InvalidDataException("Social event sequence is inconsistent.");
        return network;
    }

    private void Enqueue(SocialEventKind kind, ResidentPair pair, long tick, int previous, int current, string message)
    {
        if (_events.Count >= MaximumPendingEvents) _events.Dequeue();
        _events.Enqueue(new SocialSimulationEvent(_nextEventId++, tick, kind, pair.ResidentA, pair.ResidentB, previous, current, message));
    }

    private static bool ValidNeed(float value) => float.IsFinite(value) && value is >= 0 and <= 100;

    private readonly record struct ResidentPair(int ResidentA, int ResidentB) : IComparable<ResidentPair>
    {
        public static ResidentPair Create(int a, int b) => a < b ? new(a, b) : new(b, a);
        public int CompareTo(ResidentPair other)
        {
            int first = ResidentA.CompareTo(other.ResidentA);
            return first != 0 ? first : ResidentB.CompareTo(other.ResidentB);
        }
    }
}

internal sealed class SocialRelationship(int residentA, int residentB)
{
    private readonly Queue<SocialEncounterHistory> _history = new();
    public int ResidentA { get; } = Math.Min(residentA, residentB);
    public int ResidentB { get; } = Math.Max(residentA, residentB);
    public int Affinity { get; internal set; }
    public int Tension { get; internal set; }
    public int Trust { get; internal set; }
    public int EncounterCount { get; internal set; }
    public long LastEncounterTick { get; internal set; }
    public IReadOnlyCollection<SocialEncounterHistory> History => _history.ToArray();
    internal void AddHistory(SocialEncounterHistory item)
    {
        while (_history.Count >= SocialRelationshipNetwork.MaximumHistoryPerPair) _history.Dequeue();
        _history.Enqueue(item);
    }
    internal SocialRelationshipSnapshot ToSnapshot() => new()
    {
        ResidentA = ResidentA, ResidentB = ResidentB, Affinity = Affinity, Tension = Tension, Trust = Trust,
        EncounterCount = EncounterCount, LastEncounterTick = LastEncounterTick, History = _history.ToList()
    };
    internal static SocialRelationship FromSnapshot(SocialRelationshipSnapshot state)
    {
        var result = new SocialRelationship(state.ResidentA, state.ResidentB)
        {
            Affinity = state.Affinity, Tension = state.Tension, Trust = state.Trust, EncounterCount = state.EncounterCount,
            LastEncounterTick = state.LastEncounterTick
        };
        foreach (var history in state.History) result._history.Enqueue(history);
        return result;
    }
}

internal enum SocialEventKind { FirstMeeting, EncounterResolved, FriendshipMilestone, ConflictMilestone, TensionMilestone, TrustMilestone }
internal sealed record SocialEncounterHistory(long Tick, int AffinityDelta, int Affinity, int Tension);
internal sealed record SocialSimulationEvent(long Id, long Tick, SocialEventKind Kind, int ResidentA, int ResidentB, int PreviousScore, int CurrentScore, string Message);
internal sealed class SocialNetworkSnapshot
{
    public long NextEventId { get; set; } = 1;
    public List<SocialRelationshipSnapshot> Relationships { get; set; } = [];
    public List<SocialSimulationEvent> PendingEvents { get; set; } = [];
}
internal sealed class SocialRelationshipSnapshot
{
    public int ResidentA { get; set; }
    public int ResidentB { get; set; }
    public int Affinity { get; set; }
    public int Tension { get; set; }
    public int Trust { get; set; }
    public int EncounterCount { get; set; }
    public long LastEncounterTick { get; set; }
    public List<SocialEncounterHistory> History { get; set; } = [];
}
