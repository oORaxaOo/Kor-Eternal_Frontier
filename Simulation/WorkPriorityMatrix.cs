namespace KorTheBlight.Simulation;

internal enum WorkCategory { Mining, Construction, Production, Hauling, Gathering }

/// <summary>Per-colonist work priorities: 0 disables a category, 4 is highest.</summary>
internal sealed class WorkPriorityMatrix
{
    private readonly Dictionary<int, Dictionary<WorkCategory, int>> _priorities = [];

    public int GetPriority(int workerId, WorkCategory category)
    {
        if (workerId <= 0) return 0;
        if (_priorities.TryGetValue(workerId, out var row) && row.TryGetValue(category, out int value))
            return Math.Clamp(value, 0, 4);
        return category switch
        {
            WorkCategory.Hauling => 3,
            WorkCategory.Construction => 3,
            WorkCategory.Gathering => 2,
            WorkCategory.Production => 2,
            WorkCategory.Mining => 1,
            _ => 0
        };
    }

    public bool SetPriority(int workerId, WorkCategory category, int priority)
    {
        if (workerId <= 0 || !Enum.IsDefined(category) || priority is < 0 or > 4) return false;
        if (!_priorities.TryGetValue(workerId, out var row)) _priorities[workerId] = row = [];
        row[category] = priority;
        return true;
    }

    public IReadOnlyDictionary<WorkCategory, int> RowFor(int workerId) =>
        Enum.GetValues<WorkCategory>().ToDictionary(category => category, category => GetPriority(workerId, category));

    public WorkPriorityMatrixSnapshot CaptureSnapshot() => new()
    {
        Rows = _priorities.OrderBy(pair => pair.Key).Select(pair => new WorkPriorityRowSnapshot
        {
            WorkerId = pair.Key,
            Values = pair.Value.ToDictionary(item => item.Key, item => item.Value)
        }).ToList()
    };

    public static WorkPriorityMatrix Restore(WorkPriorityMatrixSnapshot? snapshot, IReadOnlySet<int> workerIds)
    {
        var result = new WorkPriorityMatrix();
        if (snapshot?.Rows == null) return result;
        foreach (var row in snapshot.Rows)
        {
            if (row.WorkerId <= 0 || !workerIds.Contains(row.WorkerId) || row.Values == null)
                throw new InvalidDataException("Work-priority save contains an invalid worker row.");
            foreach (var item in row.Values)
                if (!Enum.IsDefined(item.Key) || item.Value is < 0 or > 4 ||
                    !result.SetPriority(row.WorkerId, item.Key, item.Value))
                    throw new InvalidDataException("Work-priority save contains an invalid priority.");
        }
        return result;
    }
}

internal sealed class WorkPriorityMatrixSnapshot
{
    public List<WorkPriorityRowSnapshot> Rows { get; set; } = [];
}

internal sealed class WorkPriorityRowSnapshot
{
    public int WorkerId { get; set; }
    public Dictionary<WorkCategory, int> Values { get; set; } = [];
}
