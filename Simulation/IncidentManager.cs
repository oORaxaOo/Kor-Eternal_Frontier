namespace KorTheBlight.Simulation;

/// <summary>Turns deterministic simulation events into auditable incidents; it does not mutate resident state.</summary>
internal sealed class IncidentManager
{
    public const int MaximumIncidents = 2048;
    private readonly Queue<SimulationIncident> _incidents = new();
    private readonly Dictionary<IncidentKey, long> _lastTriggeredTick = new();
    private readonly Dictionary<SocialEventKind, IncidentRule> _rules = new()
    {
        [SocialEventKind.ConflictMilestone] = new("resident_conflict", 2, 300, "Residents have developed a persistent conflict."),
        [SocialEventKind.TensionMilestone] = new("social_tension", 1, 180, "Interpersonal tension is becoming a concern."),
        [SocialEventKind.FriendshipMilestone] = new("resident_bond", 1, 300, "A meaningful bond has formed between residents.")
    };
    private long _nextIncidentId = 1;

    public int Count => _incidents.Count;
    public IReadOnlyCollection<SimulationIncident> Incidents => _incidents.ToArray();

    public void Consume(IEnumerable<SocialSimulationEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        foreach (var item in events.OrderBy(x => x.Tick).ThenBy(x => x.Id))
        {
            if (!_rules.TryGetValue(item.Kind, out var rule)) continue;
            var key = new IncidentKey(rule.Code, Math.Min(item.ResidentA, item.ResidentB), Math.Max(item.ResidentA, item.ResidentB));
            if (_lastTriggeredTick.TryGetValue(key, out long previous) && item.Tick - previous < rule.CooldownTicks) continue;
            _lastTriggeredTick[key] = item.Tick;
            while (_incidents.Count >= MaximumIncidents) _incidents.Dequeue();
            _incidents.Enqueue(new SimulationIncident(_nextIncidentId++, item.Tick, rule.Code, rule.Severity,
                key.ResidentA, key.ResidentB, item.Message, rule.Severity >= 2 ? "high" : "normal"));
        }
    }

    public IncidentManagerSnapshot CaptureSnapshot() => new()
    {
        NextIncidentId = _nextIncidentId,
        Incidents = _incidents.ToList(),
        Cooldowns = _lastTriggeredTick.Select(x => new IncidentCooldownSnapshot
        { Code = x.Key.Code, ResidentA = x.Key.ResidentA, ResidentB = x.Key.ResidentB, LastTick = x.Value }).ToList()
    };

    public static IncidentManager Restore(IncidentManagerSnapshot snapshot, IReadOnlySet<int> residentIds)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.NextIncidentId < 1 || snapshot.Incidents is null || snapshot.Cooldowns is null ||
            snapshot.Incidents.Count > MaximumIncidents || snapshot.Cooldowns.Count > 250_000)
            throw new InvalidDataException("Incident save contains invalid collection sizes.");
        var manager = new IncidentManager { _nextIncidentId = snapshot.NextIncidentId };
        foreach (var item in snapshot.Incidents)
        {
            if (item.Id <= 0 || item.Tick < 0 || item.Severity is < 1 or > 3 ||
                !residentIds.Contains(item.ResidentA) || !residentIds.Contains(item.ResidentB) ||
                item.ResidentA == item.ResidentB || string.IsNullOrWhiteSpace(item.Code) || string.IsNullOrWhiteSpace(item.Message))
                throw new InvalidDataException("Incident save contains invalid incident data.");
            manager._incidents.Enqueue(item);
        }
        foreach (var item in snapshot.Cooldowns)
        {
            if (string.IsNullOrWhiteSpace(item.Code) || item.ResidentA <= 0 || item.ResidentB <= item.ResidentA ||
                !residentIds.Contains(item.ResidentA) || !residentIds.Contains(item.ResidentB) || item.LastTick < 0)
                throw new InvalidDataException("Incident save contains invalid cooldown data.");
            var key = new IncidentKey(item.Code, item.ResidentA, item.ResidentB);
            if (!manager._lastTriggeredTick.TryAdd(key, item.LastTick))
                throw new InvalidDataException("Incident save contains duplicate cooldown keys.");
        }
        if (snapshot.Incidents.Any(x => x.Id >= snapshot.NextIncidentId))
            throw new InvalidDataException("Incident ID sequence is inconsistent.");
        return manager;
    }

    private sealed record IncidentRule(string Code, int Severity, long CooldownTicks, string Description);
    private readonly record struct IncidentKey(string Code, int ResidentA, int ResidentB);
}
internal sealed record SimulationIncident(long Id, long Tick, string Code, int Severity, int ResidentA, int ResidentB, string Message, string Priority);
internal sealed class IncidentManagerSnapshot
{
    public long NextIncidentId { get; set; } = 1;
    public List<SimulationIncident> Incidents { get; set; } = [];
    public List<IncidentCooldownSnapshot> Cooldowns { get; set; } = [];
}
internal sealed class IncidentCooldownSnapshot
{
    public string Code { get; set; } = string.Empty;
    public int ResidentA { get; set; }
    public int ResidentB { get; set; }
    public long LastTick { get; set; }
}
