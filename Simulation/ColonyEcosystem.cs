namespace KorTheBlight.Simulation;

/// <summary>Logical tile categories used by the high-throughput storage grid.</summary>
internal enum GridTileKind { Ground, Wall, Stockpile }

/// <summary>The four states are intentionally presentation-independent.</summary>
internal enum CitizenState { Idle, Working, MovingToStorage, MovingToJob, MentalBreak }
internal enum SimulationNeedKind { Hunger = 1, Fatigue = 2 }
internal enum SimulationHazardKind { ThermalExposure = 1 }

/// <summary>
/// Compact tile data. Coordinates are encoded by the array index rather than repeated per tile.
/// Each tile holds at most one resource type and one bounded stack.
/// </summary>
internal struct GridTile
{
    public GridTileKind Kind;
    public int ZoneId;
    public ResourceKind? Resource;
    public int Amount;
    public int MaxStackSize;
}

/// <summary>A resource-specific, four-neighbour-connected storage zone.</summary>
internal sealed class StockpileZone(int id, ResourceKind resource, int[] tileIndices)
{
    public int Id { get; } = id;
    public ResourceKind Resource { get; } = resource;
    public IReadOnlyList<int> TileIndices { get; } = Array.AsReadOnly(tileIndices);
}

internal readonly record struct StorageReservation(int Id, ResourceKind Resource, int Amount);

/// <summary>
/// Owns the logical tile grid and bounded resource stacks. No engine objects are created per tile.
/// Reservations prevent two producers from claiming the same free storage space.
/// </summary>
internal sealed class GridManager
{
    public const int DefaultMaxStackSize = 20;

    private readonly int _width;
    private readonly int _height;
    private readonly GridTile[] _tiles;
    private readonly List<StockpileZone> _zones = [];
    private readonly Dictionary<int, StorageReservation> _reservations = [];
    private readonly Dictionary<ResourceKind, int> _reservedByResource = [];
    private const int MaxCachedFlowFields = 128;
    private readonly Dictionary<int, int[]> _flowFieldsByTarget = [];
    private readonly Queue<int> _flowFieldOrder = new();
    private int _nextZoneId = 1;

    internal event Action<ResourceKind, int, int>? StockChanged;

    internal int FlowFieldBuilds { get; private set; }
    internal int FlowFieldCacheHits { get; private set; }
    internal int CachedFlowFieldCount => _flowFieldsByTarget.Count;
    private int _nextReservationId = 1;

    public int Width => _width;
    public int Height => _height;
    public IReadOnlyList<StockpileZone> Zones => _zones.AsReadOnly();

    public GridManager(int width, int height, int maxStackSize = DefaultMaxStackSize)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (maxStackSize <= 0) throw new ArgumentOutOfRangeException(nameof(maxStackSize));
        _width = width;
        _height = height;
        _tiles = new GridTile[checked(width * height)];
        for (int i = 0; i < _tiles.Length; i++)
            _tiles[i] = new GridTile { Kind = GridTileKind.Ground, MaxStackSize = maxStackSize };
    }

    public bool InBounds(CellPosition cell) =>
        cell.X >= 0 && cell.X < _width && cell.Y >= 0 && cell.Y < _height;

    public GridTile? TileAt(CellPosition cell) => InBounds(cell) ? _tiles[Index(cell)] : null;

    public bool SetWall(CellPosition cell, bool blocked = true)
    {
        if (!InBounds(cell)) return false;
        int index = Index(cell);
        ref var tile = ref _tiles[index];
        if (tile.Kind == GridTileKind.Stockpile || tile.Amount > 0 || tile.ZoneId != 0) return false;
        GridTileKind nextKind = blocked ? GridTileKind.Wall : GridTileKind.Ground;
        if (tile.Kind == nextKind) return true;
        tile.Kind = nextKind;
        InvalidateFlowFields();
        return true;
    }

    /// <summary>
    /// Defines a zone only when all cells are valid, unused ground and form one connected group.
    /// A tile belongs to one resource-specific zone so its stack can never mix resource types.
    /// </summary>
    public int DefineStockpileZone(ResourceKind resource, IReadOnlyCollection<CellPosition> cells, int? maxStackSize = null)
    {
        ArgumentNullException.ThrowIfNull(cells);
        if (maxStackSize is <= 0) throw new ArgumentOutOfRangeException(nameof(maxStackSize));
        if (!Enum.IsDefined(resource) || cells.Count == 0)
            throw new ArgumentException("A stockpile zone needs a valid resource and at least one tile.", nameof(cells));

        int[] indices = cells.Select(cell =>
        {
            if (!InBounds(cell)) throw new ArgumentOutOfRangeException(nameof(cells), "Stockpile tile is outside the grid.");
            return Index(cell);
        }).Distinct().ToArray();

        if (indices.Length != cells.Count)
            throw new ArgumentException("A stockpile zone cannot contain duplicate tiles.", nameof(cells));

        foreach (int index in indices)
        {
            var tile = _tiles[index];
            if (tile.Kind != GridTileKind.Ground || tile.ZoneId != 0 || tile.Amount != 0)
                throw new InvalidOperationException("Stockpile tiles must be unused ground and cannot overlap another zone.");
        }

        var allowed = indices.ToHashSet();
        var visited = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(indices[0]);
        visited.Add(indices[0]);
        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            foreach (int neighbour in Neighbours(current))
                if (allowed.Contains(neighbour) && visited.Add(neighbour))
                    queue.Enqueue(neighbour);
        }
        if (visited.Count != indices.Length)
            throw new ArgumentException("All stockpile tiles must be orthogonally connected.", nameof(cells));

        int zoneId = _nextZoneId++;
        foreach (int index in indices)
        {
            ref var tile = ref _tiles[index];
            tile.Kind = GridTileKind.Stockpile;
            tile.ZoneId = zoneId;
            if (maxStackSize.HasValue) tile.MaxStackSize = maxStackSize.Value;
        }
        _zones.Add(new StockpileZone(zoneId, resource, indices));
        return zoneId;
    }

    public int ResourceCount(ResourceKind resource) =>
        _zones.Where(zone => zone.Resource == resource)
            .Sum(zone => zone.TileIndices.Sum(index =>
                _tiles[index].Resource == resource ? _tiles[index].Amount : 0));

    public int ReservedCount(ResourceKind resource) =>
        _reservedByResource.TryGetValue(resource, out int amount) ? amount : 0;

    public int FreeCapacity(ResourceKind resource)
    {
        int physical = 0;
        foreach (var zone in _zones.Where(zone => zone.Resource == resource))
        foreach (int index in zone.TileIndices)
        {
            var tile = _tiles[index];
            physical += tile.Resource == resource
                ? Math.Max(0, tile.MaxStackSize - tile.Amount)
                : tile.Amount == 0 ? tile.MaxStackSize : 0;
        }
        return Math.Max(0, physical - ReservedCount(resource));
    }

    public bool CanStore(ResourceKind resource, int amount) =>
        amount >= 0 && FreeCapacity(resource) >= amount;

    public bool TryReserveSpace(ResourceKind resource, int amount, out StorageReservation reservation)
    {
        reservation = default;
        if (amount <= 0 || !CanStore(resource, amount)) return false;
        reservation = new StorageReservation(_nextReservationId++, resource, amount);
        _reservations.Add(reservation.Id, reservation);
        _reservedByResource[resource] = ReservedCount(resource) + amount;
        return true;
    }

    public bool CancelReservation(int reservationId)
    {
        if (!_reservations.Remove(reservationId, out var reservation)) return false;
        SetReserved(reservation.Resource, ReservedCount(reservation.Resource) - reservation.Amount);
        return true;
    }

    public bool CommitReservation(int reservationId)
    {
        if (!_reservations.TryGetValue(reservationId, out var reservation)) return false;
        SetReserved(reservation.Resource, ReservedCount(reservation.Resource) - reservation.Amount);
        if (!TryAddCore(reservation.Resource, reservation.Amount))
        {
            SetReserved(reservation.Resource, ReservedCount(reservation.Resource) + reservation.Amount);
            return false;
        }
        _reservations.Remove(reservationId);
        StockChanged?.Invoke(reservation.Resource, reservation.Amount, ResourceCount(reservation.Resource));
        return true;
    }

    public bool TryAdd(ResourceKind resource, int amount)
    {
        if (amount <= 0 || !CanStore(resource, amount) || !TryAddCore(resource, amount)) return false;
        StockChanged?.Invoke(resource, amount, ResourceCount(resource));
        return true;
    }

    public int TryTake(ResourceKind resource, int requested)
    {
        if (requested <= 0) return 0;
        int remaining = requested;
        foreach (var zone in _zones.Where(zone => zone.Resource == resource))
        foreach (int index in zone.TileIndices)
        {
            ref var tile = ref _tiles[index];
            if (tile.Resource != resource || tile.Amount <= 0) continue;
            int taken = Math.Min(remaining, tile.Amount);
            tile.Amount -= taken;
            remaining -= taken;
            if (tile.Amount == 0) tile.Resource = null;
            if (remaining == 0)
            {
                StockChanged?.Invoke(resource, -requested, ResourceCount(resource));
                return requested;
            }
        }
        int takenTotal = requested - remaining;
        if (takenTotal > 0) StockChanged?.Invoke(resource, -takenTotal, ResourceCount(resource));
        return takenTotal;
    }

    public CellPosition? FindNearestResource(ResourceKind resource, CellPosition origin)
    {
        CellPosition? best = null;
        int bestDistance = int.MaxValue;
        foreach (var zone in _zones.Where(zone => zone.Resource == resource))
        foreach (int index in zone.TileIndices)
        {
            var tile = _tiles[index];
            if (tile.Resource != resource || tile.Amount <= 0) continue;
            var cell = Position(index);
            int distance = Math.Abs(cell.X - origin.X) + Math.Abs(cell.Y - origin.Y);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = cell;
            }
        }
        return best;
    }

    /// <summary>
    /// Returns a deterministic four-neighbour path that excludes the starting tile.
    /// The first request for a destination builds a reverse distance/next-step field; subsequent
    /// residents sharing that destination reuse the field rather than running separate searches.
    /// </summary>
    public List<CellPosition>? FindPath(CellPosition start, CellPosition target)
    {
        if (!InBounds(start) || !InBounds(target) || IsBlocked(target)) return null;
        if (start == target) return [];

        int startIndex = Index(start), targetIndex = Index(target);
        if (!_flowFieldsByTarget.TryGetValue(targetIndex, out var nextStep))
        {
            nextStep = BuildFlowField(targetIndex);
            StoreFlowField(targetIndex, nextStep);
            FlowFieldBuilds++;
        }
        else
        {
            FlowFieldCacheHits++;
        }

        if (nextStep[startIndex] < 0) return null;
        var path = new List<CellPosition>();
        int cursor = startIndex;
        // A shortest-path field must monotonically approach the target. The guard makes corrupted
        // cache data fail closed instead of looping indefinitely.
        for (int steps = 0; cursor != targetIndex && steps < _tiles.Length; steps++)
        {
            cursor = nextStep[cursor];
            if (cursor < 0 || cursor >= _tiles.Length) return null;
            path.Add(Position(cursor));
        }
        return cursor == targetIndex ? path : null;
    }

    private int[] BuildFlowField(int targetIndex)
    {
        var nextStep = new int[_tiles.Length];
        Array.Fill(nextStep, -1);
        var queue = new Queue<int>();
        nextStep[targetIndex] = targetIndex;
        queue.Enqueue(targetIndex);

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            foreach (int neighbour in Neighbours(current))
            {
                if (nextStep[neighbour] != -1 || IsBlocked(Position(neighbour))) continue;
                nextStep[neighbour] = current;
                queue.Enqueue(neighbour);
            }
        }
        return nextStep;
    }

    private void StoreFlowField(int targetIndex, int[] nextStep)
    {
        if (_flowFieldsByTarget.Count >= MaxCachedFlowFields)
        {
            while (_flowFieldOrder.Count > 0)
            {
                int oldest = _flowFieldOrder.Dequeue();
                if (_flowFieldsByTarget.Remove(oldest)) break;
            }
        }
        _flowFieldsByTarget[targetIndex] = nextStep;
        _flowFieldOrder.Enqueue(targetIndex);
    }

    private void InvalidateFlowFields()
    {
        _flowFieldsByTarget.Clear();
        _flowFieldOrder.Clear();
    }

    private bool TryAddCore(ResourceKind resource, int amount)
    {
        if (amount <= 0) return false;
        int remaining = amount;
        foreach (var zone in _zones.Where(zone => zone.Resource == resource))
        foreach (int index in zone.TileIndices)
        {
            ref var tile = ref _tiles[index];
            if (tile.Resource != resource && tile.Amount != 0) continue;
            if (tile.Resource == null) tile.Resource = resource;
            int accepted = Math.Min(remaining, tile.MaxStackSize - tile.Amount);
            tile.Amount += accepted;
            remaining -= accepted;
            if (remaining == 0) return true;
        }
        return false;
    }

    private bool IsBlocked(CellPosition cell) => _tiles[Index(cell)].Kind == GridTileKind.Wall;
    private void SetReserved(ResourceKind resource, int amount)
    {
        if (amount <= 0) _reservedByResource.Remove(resource);
        else _reservedByResource[resource] = amount;
    }
    private int Index(CellPosition cell) => cell.Y * _width + cell.X;
    private CellPosition Position(int index) => new(index % _width, index / _width);
    private IEnumerable<int> Neighbours(int index)
    {
        int x = index % _width, y = index / _width;
        if (y > 0) yield return index - _width;
        if (x > 0) yield return index - 1;
        if (x + 1 < _width) yield return index + 1;
        if (y + 1 < _height) yield return index + _width;
    }
}

/// <summary>A lightweight simulated citizen. Hunger and fatigue are satisfaction values: 100 is rested/fed.</summary>
internal sealed class CitizenAgent(int id, string name, CellPosition position)
{
    public int Id { get; } = id;
    public string Name { get; } = name;
    public CellPosition Position { get; internal set; } = position;
    public float Hunger { get; internal set; } = 100f;
    public float Fatigue { get; internal set; } = 100f;
    public float Stress { get; internal set; }
    public float Mood => Math.Clamp((Hunger + Fatigue) * 0.5f - Stress * 0.5f, 0f, 100f);
    public float Happiness => Mood;
    public CitizenState State { get; internal set; } = CitizenState.Idle;
    public bool CanReceiveJob { get; internal set; } = true;
    public int? JobId { get; internal set; }
    public float WorkProgress { get; internal set; }
    public float MovementProgress { get; internal set; }
    public int OutputReservationId { get; internal set; }
    public List<CellPosition> Route { get; internal set; } = [];
    public int RouteIndex { get; internal set; }
}

/// <summary>
/// Immutable inspection result for player-facing diagnostics and debugging tools.
/// It explains current needs and work state without exposing mutable simulation objects.
/// </summary>
internal sealed record CitizenDiagnostic(
    int CitizenId,
    string Name,
    CellPosition Position,
    CitizenState State,
    float Hunger,
    float Fatigue,
    float Stress,
    float Mood,
    int? JobId,
    string? JobName,
    int AvailableFood,
    string PrimaryReason);

internal sealed class WorkplaceJob(int id, string name, CellPosition position, int workerCapacity,
    ResourceKind outputResource, int outputAmount, float workSeconds)
{
    private readonly HashSet<int> _workerIds = [];
    public int Id { get; } = id;
    public string Name { get; } = name;
    public CellPosition Position { get; } = position;
    public int WorkerCapacity { get; } = workerCapacity;
    public ResourceKind OutputResource { get; } = outputResource;
    public int OutputAmount { get; } = outputAmount;
    public float WorkSeconds { get; } = workSeconds;
    public int AssignedWorkers => _workerIds.Count;
    public int FreeWorkers => Math.Max(0, WorkerCapacity - _workerIds.Count);
    internal bool AddWorker(int citizenId) => FreeWorkers > 0 && _workerIds.Add(citizenId);
    internal void RemoveWorker(int citizenId) => _workerIds.Remove(citizenId);
}

/// <summary>Central workplace registry. Job assignment is deterministic and respects each workplace's capacity.</summary>
internal sealed class JobBroker
{
    private readonly List<WorkplaceJob> _jobs = [];
    private readonly Dictionary<int, WorkplaceJob> _jobsById = [];
    private readonly Dictionary<int, List<WorkplaceJob>> _jobsByTile = [];
    private readonly int _width;
    private readonly int _height;
    private int _nextJobId = 1;
    private int _availableCapacity;

    public IReadOnlyList<WorkplaceJob> Jobs => _jobs.AsReadOnly();
    internal event Action<int, int>? AvailabilityChanged;
    internal int AssignmentTileChecks { get; private set; }
    internal int JobCandidatesInspected { get; private set; }

    public JobBroker(int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        _width = width;
        _height = height;
    }

    public int RegisterWorkplace(string name, CellPosition position, int workerCapacity,
        ResourceKind outputResource, int outputAmount = 1, float workSeconds = 5f)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A workplace needs a name.", nameof(name));
        if (position.X < 0 || position.X >= _width || position.Y < 0 || position.Y >= _height)
            throw new ArgumentOutOfRangeException(nameof(position), "Workplace must be inside the colony grid.");
        if (workerCapacity <= 0 || outputAmount <= 0 || workSeconds <= 0 ||
            float.IsNaN(workSeconds) || float.IsInfinity(workSeconds))
            throw new ArgumentOutOfRangeException(nameof(workerCapacity), "Capacity, output and work duration must be positive.");

        int id = _nextJobId++;
        var job = new WorkplaceJob(id, name, position, workerCapacity, outputResource, outputAmount, workSeconds);
        _jobs.Add(job);
        _jobsById.Add(id, job);
        int tileIndex = Index(position);
        if (!_jobsByTile.TryGetValue(tileIndex, out var atTile))
            _jobsByTile[tileIndex] = atTile = [];
        atTile.Add(job);
        _availableCapacity = checked(_availableCapacity + workerCapacity);
        AvailabilityChanged?.Invoke(job.Id, job.FreeWorkers);
        return id;
    }

    public WorkplaceJob? FindJob(int jobId) =>
        _jobsById.TryGetValue(jobId, out var job) ? job : null;

    public bool RemoveWorkplace(int jobId)
    {
        var job = FindJob(jobId);
        if (job == null) return false;

        _availableCapacity -= job.FreeWorkers;
        _jobs.Remove(job);
        _jobsById.Remove(jobId);
        int tileIndex = Index(job.Position);
        if (_jobsByTile.TryGetValue(tileIndex, out var atTile))
        {
            atTile.RemoveAll(candidate => candidate.Id == jobId);
            if (atTile.Count == 0) _jobsByTile.Remove(tileIndex);
        }
        return true;
    }

    public WorkplaceJob? AssignNext(CitizenAgent citizen)
    {
        ArgumentNullException.ThrowIfNull(citizen);
        if (citizen.JobId.HasValue) return FindJob(citizen.JobId.Value);
        if (_availableCapacity <= 0) return null;

        CellPosition origin = citizen.Position;
        int maxDistance = _width + _height - 2;
        for (int distance = 0; distance <= maxDistance; distance++)
        {
            WorkplaceJob? bestInRing = null;
            int minX = Math.Max(0, origin.X - distance);
            int maxX = Math.Min(_width - 1, origin.X + distance);
            for (int x = minX; x <= maxX; x++)
            {
                int remainingY = distance - Math.Abs(x - origin.X);
                if (remainingY < 0) continue;

                int upperY = origin.Y - remainingY;
                if (upperY >= 0)
                    bestInRing = ConsiderTile(new CellPosition(x, upperY), bestInRing);

                int lowerY = origin.Y + remainingY;
                if (lowerY < _height && lowerY != upperY)
                    bestInRing = ConsiderTile(new CellPosition(x, lowerY), bestInRing);
            }

            // Search the entire ring before returning so stable job ID wins every equal-distance tie.
            if (bestInRing == null || !bestInRing.AddWorker(citizen.Id)) continue;
            _availableCapacity--;
            citizen.JobId = bestInRing.Id;
            return bestInRing;
        }
        return null;
    }

    public void Release(CitizenAgent citizen)
    {
        if (citizen.JobId is not int jobId) return;
        var job = FindJob(jobId);
        if (job != null)
        {
            int assignedBefore = job.AssignedWorkers;
            int freeBefore = job.FreeWorkers;
            job.RemoveWorker(citizen.Id);
            if (job.AssignedWorkers < assignedBefore)
            {
                _availableCapacity++;
                if (freeBefore == 0)
                    AvailabilityChanged?.Invoke(job.Id, job.FreeWorkers);
            }
        }
        citizen.JobId = null;
        citizen.WorkProgress = 0;
    }

    private WorkplaceJob? ConsiderTile(CellPosition position, WorkplaceJob? best)
    {
        AssignmentTileChecks++;
        if (!_jobsByTile.TryGetValue(Index(position), out var atTile)) return best;
        foreach (var candidate in atTile)
        {
            JobCandidatesInspected++;
            if (candidate.FreeWorkers > 0 && (best == null || candidate.Id < best.Id))
                best = candidate;
        }
        return best;
    }

    private int Index(CellPosition position) => position.Y * _width + position.X;
}
/// <summary>
/// Runs needs, movement, work, and storage as one deterministic simulation slice.
/// Production reserves storage before work begins, so finished output can never exceed capacity.
/// </summary>
internal sealed class ColonyEcosystem
{
    private readonly List<CitizenAgent> _citizens = [];
    private readonly IReadOnlyList<CitizenAgent> _citizenView;
    private int _nextCitizenId = 1;
    private readonly SimulationEventQueue _events = new();
    public GridManager Grid { get; }
    public JobBroker JobBroker { get; }
    // Cache the read-only wrapper: UI, save and simulation code may query this every frame.
    public IReadOnlyList<CitizenAgent> Citizens => _citizenView;
    public float ElapsedSeconds { get; private set; }
    public long SimulationTick { get; private set; }
    public IReadOnlyList<SimulationEvent> DrainEvents(int maxCount = 4096) => _events.DrainDue(SimulationTick, maxCount);
    public string LastEvent { get; private set; } = "Colony systems online.";

    public ColonyEcosystem(int width, int height)
    {
        Grid = new GridManager(width, height);
        JobBroker = new JobBroker(width, height);
        _citizenView = _citizens.AsReadOnly();
        Grid.StockChanged += (resource, delta, total) =>
            _events.Enqueue(SimulationTick, SimulationEventKind.StockChanged, (long)resource, total, delta);
        JobBroker.AvailabilityChanged += (jobId, freeWorkers) =>
            _events.Enqueue(SimulationTick, SimulationEventKind.JobBecameAvailable, jobId, freeWorkers);
    }

    public void ReportHazard(int citizenId, SimulationHazardKind kind, float severity)
    {
        if (citizenId <= 0 || !_citizens.Any(citizen => citizen.Id == citizenId))
            throw new ArgumentOutOfRangeException(nameof(citizenId), "Hazard events must reference an existing citizen.");
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (!float.IsFinite(severity) || severity is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(severity));
        _events.Enqueue(SimulationTick, SimulationEventKind.HazardChanged, citizenId, (long)kind, severity);
    }

    public CitizenAgent AddCitizen(string name, CellPosition position)
    {
        if (!Grid.InBounds(position) || Grid.TileAt(position)?.Kind == GridTileKind.Wall)
            throw new ArgumentOutOfRangeException(nameof(position), "Citizen spawn must be on a walkable tile.");
        // Monotonic IDs keep bulk recruitment O(n), rather than rescanning the full population
        // for every new citizen (which would make creating thousands of residents O(n²)).
        var citizen = new CitizenAgent(_nextCitizenId++, name, position);
        _citizens.Add(citizen);
        return citizen;
    }

    /// <summary>
    /// Returns a read-only, actionable snapshot for the selected citizen.
    /// The reason is derived from the same live state that drives autonomous behavior.
    /// </summary>
    public CitizenDiagnostic? InspectCitizen(int citizenId)
    {
        var citizen = _citizens.FirstOrDefault(candidate => candidate.Id == citizenId);
        if (citizen is null) return null;

        var job = citizen.JobId is int jobId ? JobBroker.FindJob(jobId) : null;
        string reason = citizen.State switch
        {
            CitizenState.MentalBreak => "Overwhelmed: recovering from a mental break.",
            CitizenState.MovingToStorage => "Seeking food: travelling to a food stockpile.",
            CitizenState.MovingToJob => $"Travelling to workplace: {job?.Name ?? "assigned job"} .".Replace(" .", "."),
            CitizenState.Working => $"Working at {job?.Name ?? "assigned job"}.",
            _ when citizen.Hunger < 30f && Grid.ResourceCount(ResourceKind.Food) == 0 =>
                "Critical hunger: no stored food is available.",
            _ when citizen.Hunger < 30f => "Hungry: food is the highest-priority need.",
            _ when citizen.Fatigue < 20f => "Exhausted: resting before returning to work.",
            _ when citizen.Stress >= 80f => "Overwhelmed: stress prevents work.",
            _ when job is not null => $"Assigned to {job.Name}; waiting for the next work step.",
            _ => "Idle: no eligible workplace assignment is currently available."
        };

        return new CitizenDiagnostic(
            citizen.Id, citizen.Name, citizen.Position, citizen.State,
            citizen.Hunger, citizen.Fatigue, citizen.Stress, citizen.Mood,
            citizen.JobId, job?.Name, Grid.ResourceCount(ResourceKind.Food), reason);
    }

    /// <summary>Removing a workplace immediately releases every assigned citizen and storage reservation.</summary>
    public bool RemoveWorkplace(int jobId)
    {
        var job = JobBroker.FindJob(jobId);
        if (job == null || !JobBroker.RemoveWorkplace(jobId)) return false;
        // Remove the registry entry before releasing assigned citizens so their release cannot
        // publish a stale JobBecameAvailable event for a workplace that no longer exists.
        foreach (var citizen in _citizens.Where(citizen => citizen.JobId == jobId).ToArray())
        {
            ReleaseAssignment(citizen);
            citizen.State = CitizenState.Idle;
        }
        LastEvent = $"Workplace {job.Name} was removed; assigned citizens were released.";
        return true;
    }

    public void Step(float deltaSeconds)
    {
        if (deltaSeconds <= 0 || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds)) return;
        ElapsedSeconds += deltaSeconds;
        SimulationTick++;
        foreach (var citizen in _citizens)
        {
            float previousHunger = citizen.Hunger;
            citizen.Hunger = Math.Max(0, citizen.Hunger - deltaSeconds * 0.30f);
            if (previousHunger >= 30f && citizen.Hunger < 30f)
                _events.Enqueue(SimulationTick, SimulationEventKind.NeedThresholdCrossed, citizen.Id,
                    (long)SimulationNeedKind.Hunger, citizen.Hunger);
            // Rest satisfaction declines slowly at baseline and faster during travel/work.
            float fatigueDrain = citizen.State switch
            {
                CitizenState.Working => 0.24f,
                CitizenState.MovingToJob => 0.12f,
                _ => 0.04f
            };
            float previousFatigue = citizen.Fatigue;
            citizen.Fatigue = Math.Max(0, citizen.Fatigue - deltaSeconds * fatigueDrain);
            if (previousFatigue >= 20f && citizen.Fatigue < 20f)
                _events.Enqueue(SimulationTick, SimulationEventKind.NeedThresholdCrossed, citizen.Id,
                    (long)SimulationNeedKind.Fatigue, citizen.Fatigue);

            UpdateStress(citizen, deltaSeconds);
            if (citizen.State == CitizenState.MentalBreak)
            {
                citizen.Stress = Math.Max(0f, citizen.Stress - deltaSeconds * 2f);
                if (citizen.Stress <= 45f)
                {
                    citizen.State = CitizenState.Idle;
                    _events.Enqueue(SimulationTick, SimulationEventKind.MentalBreakEnded, citizen.Id, value: citizen.Stress);
                    LastEvent = $"{citizen.Name} has recovered from a mental break.";
                }
                else
                {
                    continue;
                }
            }

            // Food is the highest-priority need. A citizen releases the job and its reserved output
            // slot before travelling, so neither worker capacity nor storage can become stranded.
            if (citizen.Hunger < 30f && citizen.CanReceiveJob && citizen.State != CitizenState.MovingToStorage)
            {
                ReleaseAssignment(citizen);
                var food = Grid.FindNearestResource(ResourceKind.Food, citizen.Position);
                if (food.HasValue && TryStartMovement(citizen, food.Value, CitizenState.MovingToStorage))
                    LastEvent = $"{citizen.Name} is seeking food.";
                else
                    citizen.State = CitizenState.Idle;
            }

            if (citizen.State == CitizenState.MovingToStorage)
            {
                if (AdvanceMovement(citizen, deltaSeconds))
                {
                    int eaten = Grid.TryTake(ResourceKind.Food, 1);
                    if (eaten > 0)
                    {
                        citizen.Hunger = Math.Min(100f, citizen.Hunger + 65f);
                        LastEvent = $"{citizen.Name} ate from the stockpile.";
                    }
                    citizen.State = CitizenState.Idle;
                }
                continue;
            }

            if (citizen.Stress >= 80f)
            {
                ReleaseAssignment(citizen);
                citizen.State = CitizenState.MentalBreak;
                _events.Enqueue(SimulationTick, SimulationEventKind.MentalBreakStarted, citizen.Id, value: citizen.Stress);
                LastEvent = $"{citizen.Name} is overwhelmed and has stopped working.";
                continue;
            }

            if (citizen.Fatigue < 20f)
            {
                ReleaseAssignment(citizen);
                citizen.State = CitizenState.Idle;
            }

            if (citizen.State == CitizenState.MovingToJob)
            {
                if (AdvanceMovement(citizen, deltaSeconds))
                {
                    citizen.State = CitizenState.Working;
                    LastEvent = $"{citizen.Name} started work.";
                }
                continue;
            }

            if (citizen.State == CitizenState.Working)
            {
                Work(citizen, deltaSeconds);
                continue;
            }

            // Idle citizens recover before returning to the labour pool.
            if (citizen.Fatigue < 60f)
            {
                citizen.Fatigue = Math.Min(100f, citizen.Fatigue + deltaSeconds * 8f);
                continue;
            }
            if (citizen.Hunger < 30f || !citizen.CanReceiveJob) continue;

            var job = JobBroker.AssignNext(citizen);
            if (job == null) continue;
            if (!Grid.TryReserveSpace(job.OutputResource, job.OutputAmount, out var reservation))
            {
                JobBroker.Release(citizen);
                citizen.State = CitizenState.Idle;
                continue;
            }
            citizen.OutputReservationId = reservation.Id;
            if (!TryStartMovement(citizen, job.Position, CitizenState.MovingToJob))
            {
                ReleaseAssignment(citizen);
                citizen.State = CitizenState.Idle;
                LastEvent = $"No route to workplace {job.Name}.";
            }
        }
    }

    private static void UpdateStress(CitizenAgent citizen, float deltaSeconds)
    {
        float stressRate = 0f;
        if (citizen.Hunger < 30f) stressRate += (30f - citizen.Hunger) * 0.025f;
        if (citizen.Fatigue < 20f) stressRate += (20f - citizen.Fatigue) * 0.04f;

        if (stressRate > 0f)
            citizen.Stress = Math.Min(100f, citizen.Stress + stressRate * deltaSeconds);
        else if (citizen.Hunger > 70f && citizen.Fatigue > 70f)
            citizen.Stress = Math.Max(0f, citizen.Stress - deltaSeconds * 0.5f);
        else
            citizen.Stress = Math.Max(0f, citizen.Stress - deltaSeconds * 0.1f);
    }

    private void Work(CitizenAgent citizen, float deltaSeconds)
    {
        var job = citizen.JobId.HasValue ? JobBroker.FindJob(citizen.JobId.Value) : null;
        if (job == null)
        {
            ReleaseAssignment(citizen);
            citizen.State = CitizenState.Idle;
            return;
        }

        citizen.WorkProgress += deltaSeconds;
        if (citizen.WorkProgress < job.WorkSeconds) return;
        if (citizen.OutputReservationId == 0 || !Grid.CommitReservation(citizen.OutputReservationId))
        {
            LastEvent = $"Storage blocked output from {job.Name}.";
            return;
        }

        citizen.OutputReservationId = 0;
        citizen.WorkProgress = 0;
        LastEvent = $"{job.Name} produced {job.OutputAmount} {job.OutputResource}.";
        if (!Grid.TryReserveSpace(job.OutputResource, job.OutputAmount, out var nextReservation))
        {
            ReleaseAssignment(citizen);
            citizen.State = CitizenState.Idle;
            return;
        }
        citizen.OutputReservationId = nextReservation.Id;
    }

    private bool TryStartMovement(CitizenAgent citizen, CellPosition target, CitizenState state)
    {
        var route = Grid.FindPath(citizen.Position, target);
        if (route == null) return false;
        citizen.Route = route;
        citizen.RouteIndex = 0;
        citizen.MovementProgress = 0;
        citizen.State = state;
        return true;
    }

    private bool AdvanceMovement(CitizenAgent citizen, float deltaSeconds)
    {
        citizen.MovementProgress += deltaSeconds * 2f; // Two grid tiles per simulated second.
        while (citizen.MovementProgress >= 1f && citizen.RouteIndex < citizen.Route.Count)
        {
            citizen.Position = citizen.Route[citizen.RouteIndex++];
            citizen.MovementProgress -= 1f;
        }
        return citizen.RouteIndex >= citizen.Route.Count;
    }

    private void ReleaseAssignment(CitizenAgent citizen)
    {
        JobBroker.Release(citizen);
        if (citizen.OutputReservationId != 0)
        {
            Grid.CancelReservation(citizen.OutputReservationId);
            citizen.OutputReservationId = 0;
        }
        citizen.Route.Clear();
        citizen.RouteIndex = 0;
        citizen.MovementProgress = 0;
    }
}
