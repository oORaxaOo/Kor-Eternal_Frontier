namespace KorTheBlight.Simulation;

/// <summary>Metrics sampled from the authoritative colony state, independent of rendering.</summary>
internal readonly record struct StorytellerColonyState(
    int Population, int Food, int PendingJobs, float AverageStress, float AverageHunger, double Wealth = 0, int ThreatPoints = 0);

internal enum StorytellerMetric { FoodPerResident, AverageStress, JobBacklogPerResident, AverageHunger, ColonyWealth }
internal enum StorytellerComparison { Below, Above }

internal sealed record StorytellerRule(
    string Code, StorytellerMetric Metric, StorytellerComparison Comparison, float Threshold,
    int MinimumPopulation, long CooldownTicks, int Severity, string Message);

internal sealed record StorytellerEvent(long Id, long Tick, string Code, int Severity, string Message, int ThreatPoints = 0)
{
    public int ThreatUnits => ThreatPoints / 25;
}

/// <summary>
/// Deterministic, data-driven colony event evaluator. It observes the simulation and emits narrative
/// events only; it never changes resources, citizens, or jobs directly.
/// </summary>
internal sealed class ColonyStoryteller
{
    public const int MaximumEvents = 256;
    private readonly List<StorytellerRule> _rules;
    private readonly Queue<StorytellerEvent> _events = new();
    private readonly Dictionary<string, long> _lastTriggered = new(StringComparer.Ordinal);
    private readonly HashSet<string> _activeRules = new(StringComparer.Ordinal);
    private long _nextId = 1;

    public IReadOnlyCollection<StorytellerEvent> Events => _events.ToArray();

    public ColonyStoryteller(IEnumerable<StorytellerRule>? rules = null)
    {
        _rules = (rules ?? DefaultRules()).ToList();
        if (_rules.Any(rule => string.IsNullOrWhiteSpace(rule.Code) || !Enum.IsDefined(rule.Metric) ||
            !Enum.IsDefined(rule.Comparison) || !float.IsFinite(rule.Threshold) ||
            rule.MinimumPopulation < 0 || rule.CooldownTicks < 0 || rule.Severity is < 1 or > 3 ||
            string.IsNullOrWhiteSpace(rule.Message)) ||
            _rules.Select(rule => rule.Code).Distinct(StringComparer.Ordinal).Count() != _rules.Count)
            throw new ArgumentException("Storyteller rules must have unique codes and valid thresholds.", nameof(rules));
    }

    public IReadOnlyList<StorytellerEvent> Step(long tick, StorytellerColonyState state)
    {
        if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
        if (state.Population < 0 || state.Food < 0 || state.PendingJobs < 0 ||
            !float.IsFinite(state.AverageStress) || !float.IsFinite(state.AverageHunger) ||
            !double.IsFinite(state.Wealth) || state.Wealth < 0 || state.ThreatPoints is < 0 or > 250)
            throw new ArgumentOutOfRangeException(nameof(state), "Storyteller metrics must be finite and non-negative.");
        var emitted = new List<StorytellerEvent>();
        foreach (var rule in _rules)
        {
            bool active = state.Population >= rule.MinimumPopulation &&
                Compare(MetricValue(rule.Metric, state), rule.Comparison, rule.Threshold);
            if (!active) { _activeRules.Remove(rule.Code); continue; }
            if (_activeRules.Contains(rule.Code)) continue;
            if (_lastTriggered.TryGetValue(rule.Code, out long last) && tick - last < rule.CooldownTicks) continue;
            _activeRules.Add(rule.Code);

            int budget = rule.Severity >= 2 ? Math.Clamp(state.ThreatPoints, 0, 250) : 0;
            var item = new StorytellerEvent(_nextId++, tick, rule.Code, rule.Severity, rule.Message, budget);
            _lastTriggered[rule.Code] = tick;
            while (_events.Count >= MaximumEvents) _events.Dequeue();
            _events.Enqueue(item);
            emitted.Add(item);
        }
        return emitted;
    }

    public ColonyStorytellerSnapshot CaptureSnapshot() => new()
    {
        NextId = _nextId,
        Rules = _rules.ToList(),
        Events = _events.ToList(),
        LastTriggered = _lastTriggered.Select(pair => new StorytellerCooldownSnapshot { Code = pair.Key, Tick = pair.Value }).ToList(),
        ActiveRules = _activeRules.ToList()
    };

    public static ColonyStoryteller Restore(ColonyStorytellerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.NextId < 1 || snapshot.Events is null || snapshot.Events.Count > MaximumEvents ||
            snapshot.LastTriggered is null || snapshot.LastTriggered.Count > 1024 ||
            snapshot.Rules is null || snapshot.Rules.Count > 1024 ||
            snapshot.ActiveRules is null || snapshot.ActiveRules.Count > 1024)
            throw new InvalidDataException("Storyteller save contains invalid collection sizes.");
        var storyteller = new ColonyStoryteller(snapshot.Rules ?? throw new InvalidDataException("Storyteller save is missing rule definitions.")) { _nextId = snapshot.NextId };
        foreach (var item in snapshot.Events)
        {
            if (item.Id <= 0 || item.Tick < 0 || item.Severity is < 1 or > 3 ||
                string.IsNullOrWhiteSpace(item.Code) || string.IsNullOrWhiteSpace(item.Message))
                throw new InvalidDataException("Storyteller save contains an invalid event.");
            storyteller._events.Enqueue(item);
        }
        foreach (var item in snapshot.LastTriggered)
        {
            if (string.IsNullOrWhiteSpace(item.Code) || item.Tick < 0 ||
                !storyteller._lastTriggered.TryAdd(item.Code, item.Tick))
                throw new InvalidDataException("Storyteller save contains invalid cooldown state.");
        }
        foreach (var code in snapshot.ActiveRules)
        {
            if (string.IsNullOrWhiteSpace(code) || !storyteller._activeRules.Add(code))
                throw new InvalidDataException("Storyteller save contains invalid active-rule state.");
        }
        if (snapshot.Events.Any(item => item.Id >= snapshot.NextId))
            throw new InvalidDataException("Storyteller event ID sequence is inconsistent.");
        return storyteller;
    }

    private static bool Compare(float value, StorytellerComparison comparison, float threshold) =>
        comparison == StorytellerComparison.Below ? value < threshold : value > threshold;

    private static float MetricValue(StorytellerMetric metric, StorytellerColonyState state) => metric switch
    {
        StorytellerMetric.FoodPerResident => state.Population == 0 ? float.MaxValue : (float)state.Food / state.Population,
        StorytellerMetric.AverageStress => state.AverageStress,
        StorytellerMetric.JobBacklogPerResident => state.Population == 0 ? 0f : (float)state.PendingJobs / state.Population,
        StorytellerMetric.AverageHunger => state.AverageHunger,
        StorytellerMetric.ColonyWealth => (float)Math.Min(float.MaxValue, state.Wealth),
        _ => throw new ArgumentOutOfRangeException(nameof(metric))
    };

    private static IEnumerable<StorytellerRule> DefaultRules()
    {
        yield return new("food_shortage", StorytellerMetric.FoodPerResident, StorytellerComparison.Below, 2f, 1, 600,
            3, "Food reserves are dangerously low for the current population.");
        yield return new("colony_stress", StorytellerMetric.AverageStress, StorytellerComparison.Above, 60f, 2, 900,
            2, "Stress is spreading through the colony; consider rest and safer working conditions.");
        yield return new("work_backlog", StorytellerMetric.JobBacklogPerResident, StorytellerComparison.Above, 3f, 1, 600,
            1, "The work backlog is growing faster than the colony can comfortably manage.");
        yield return new("hunger_wave", StorytellerMetric.AverageHunger, StorytellerComparison.Above, 70f, 1, 600,
            2, "Many residents are going hungry; review food production and distribution.");
        yield return new("wealth_attracts_attention", StorytellerMetric.ColonyWealth, StorytellerComparison.Above, 5_000f, 1, 2_000,
            2, "The colony's growing wealth attracts outside attention; an incident budget has been allocated.");
    }
}

internal sealed class ColonyStorytellerSnapshot
{
    public long NextId { get; set; } = 1;
    public List<StorytellerRule> Rules { get; set; } = [];
    public List<StorytellerEvent> Events { get; set; } = [];
    public List<StorytellerCooldownSnapshot> LastTriggered { get; set; } = [];
    public List<string> ActiveRules { get; set; } = [];
}

internal sealed class StorytellerCooldownSnapshot
{
    public string Code { get; set; } = string.Empty;
    public long Tick { get; set; }
}
