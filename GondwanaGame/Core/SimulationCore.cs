namespace KorTheBlight.Core;

public enum JobState
{
    Pending,
    Reserved,
    Active,
    Complete,
    Cancelled,
    Blocked
}

public enum JobType
{
    Gather,
    Build,
    Haul,
    Produce,
    Research
}

public sealed class ResourceInventory
{
    private readonly Dictionary<string, int> _stock = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _reserved = new(StringComparer.OrdinalIgnoreCase);

    public int Get(string resource)
    {
        if (string.IsNullOrWhiteSpace(resource)) return 0;
        return _stock.TryGetValue(resource, out var value) ? value : 0;
    }

    public int Reserved(string resource)
    {
        if (string.IsNullOrWhiteSpace(resource)) return 0;
        return _reserved.TryGetValue(resource, out var value) ? value : 0;
    }

    public void Set(string resource, int value)
    {
        if (string.IsNullOrWhiteSpace(resource)) return;
        if (value <= 0)
        {
            _stock.Remove(resource);
            _reserved.Remove(resource);
            return;
        }
        _stock[resource] = value;
    }

    public void Add(string resource, int amount)
    {
        if (string.IsNullOrWhiteSpace(resource) || amount <= 0) return;
        _stock[resource] = Get(resource) + amount;
    }

    public bool Reserve(string resource, int amount)
    {
        if (string.IsNullOrWhiteSpace(resource) || amount <= 0) return false;
        if (Get(resource) - Reserved(resource) < amount) return false;
        _reserved[resource] = Reserved(resource) + amount;
        return true;
    }

    public void Release(string resource, int amount)
    {
        if (string.IsNullOrWhiteSpace(resource) || amount <= 0) return;
        var current = Reserved(resource);
        if (current <= 0) return;
        var next = Math.Max(0, current - amount);
        if (next == 0) _reserved.Remove(resource);
        else _reserved[resource] = next;
    }

    public bool Consume(string resource, int amount)
    {
        if (string.IsNullOrWhiteSpace(resource) || amount <= 0) return false;
        if (Get(resource) < amount) return false;
        _stock[resource] = Get(resource) - amount;
        Release(resource, amount);
        return true;
    }

    public bool TryTransferTo(ResourceInventory destination, string resource, int amount)
    {
        if (destination is null || string.IsNullOrWhiteSpace(resource) || amount <= 0) return false;
        if (!Reserve(resource, amount)) return false;
        if (!Consume(resource, amount))
        {
            Release(resource, amount);
            return false;
        }
        destination.Add(resource, amount);
        return true;
    }
}

public sealed class Colonist
{
    public Colonist(string name, int x, int y)
    {
        Id = Guid.NewGuid().ToString("N");
        Name = name;
        Position = (x, y);
    }

    public string Id { get; }
    public string Name { get; }
    public (int X, int Y) Position { get; set; }
    public Dictionary<string, int> Skills { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string? CurrentJobId { get; set; }
    public ResourceInventory Inventory { get; } = new();
}

public sealed class Job
{
    public Job(JobType type, string description, int priority, string? requiredResource = null, int requiredAmount = 0)
    {
        Id = Guid.NewGuid().ToString("N");
        Type = type;
        Description = description;
        Priority = priority;
        RequiredResource = requiredResource;
        RequiredAmount = requiredAmount;
        State = JobState.Pending;
    }

    public string Id { get; }
    public JobType Type { get; }
    public string Description { get; }
    public int Priority { get; }
    public string? RequiredResource { get; }
    public int RequiredAmount { get; }
    public JobState State { get; set; }
    public string? AssignedWorkerId { get; set; }
    public int Progress { get; set; }
    public int TotalWork { get; set; } = 1;
}

public sealed class ColonyState
{
    public ResourceInventory Storage { get; } = new();
    public List<Colonist> Colonists { get; } = new();
    public List<Job> Jobs { get; } = new();
    public Dictionary<string, int> Research { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Colonist AddColonist(string name, int x, int y)
    {
        var colonist = new Colonist(name, x, y);
        Colonists.Add(colonist);
        return colonist;
    }

    public Job EnqueueJob(JobType type, string description, int priority, string? requiredResource = null, int requiredAmount = 0)
    {
        var job = new Job(type, description, priority, requiredResource, requiredAmount)
        {
            State = JobState.Pending
        };
        Jobs.Add(job);
        return job;
    }

    public bool TryAssignJob(Colonist colonist, Job job)
    {
        if (colonist is null || job is null) return false;
        if (job.State != JobState.Pending || !string.IsNullOrEmpty(colonist.CurrentJobId)) return false;
        if (!string.IsNullOrWhiteSpace(job.RequiredResource) && !Storage.Reserve(job.RequiredResource, job.RequiredAmount)) return false;
        job.State = JobState.Reserved;
        job.AssignedWorkerId = colonist.Id;
        colonist.CurrentJobId = job.Id;
        return true;
    }

    public void MarkCompleted(Job job)
    {
        if (job is null) return;
        job.State = JobState.Complete;
        if (!string.IsNullOrEmpty(job.AssignedWorkerId))
        {
            var worker = Colonists.FirstOrDefault(c => c.Id == job.AssignedWorkerId);
            if (worker != null) worker.CurrentJobId = null;
        }
        job.AssignedWorkerId = null;
    }

    public void CancelJob(Job job)
    {
        if (job is null) return;
        if (!string.IsNullOrWhiteSpace(job.RequiredResource)) Storage.Release(job.RequiredResource, job.RequiredAmount);
        job.State = JobState.Cancelled;
        if (!string.IsNullOrEmpty(job.AssignedWorkerId))
        {
            var worker = Colonists.FirstOrDefault(c => c.Id == job.AssignedWorkerId);
            if (worker != null) worker.CurrentJobId = null;
        }
        job.AssignedWorkerId = null;
    }
}
