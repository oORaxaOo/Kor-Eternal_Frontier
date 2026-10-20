using KorTheBlight.WorldModel;

namespace KorTheBlight.Simulation;

internal enum TerrainKind { Ground, Rock, Water }
internal enum FluidKind { Water, Magma }
internal enum ArchitectureKind { None, Floor, Wall, Door, Path, Conduit }
internal enum PlaceableKind { Habitat, Stockpile, Workbench, SleepingPod, StorageRack, Generator, Garden }
internal enum ResourceKind { Wood, Stone, Food, Alloy, Components }
internal enum ConstructionStatus { Planned, WaitingForMaterials, UnderConstruction, Complete, Blocked }
internal enum ColonyTaskKind { Move, Gather, Deliver, BuildTile, BuildObject, Produce, Mine }
internal enum WorkOrderKind { TileConstruction, ObjectConstruction, Production, Excavation }

internal readonly record struct CellPosition(int X, int Y);
internal readonly record struct PathCacheKey(CellPosition Start, CellPosition Target);

internal sealed class TileCell
{
    public TerrainKind Terrain { get; internal set; } = TerrainKind.Ground;
    public ArchitectureKind Architecture { get; internal set; }
    public int? ConstructionOrderId { get; internal set; }
    public int WaterLevel { get; internal set; }
    public int MagmaLevel { get; internal set; }
    public float TemperatureCelsius { get; internal set; } = 20f;
}

internal sealed class TileConstructionOrder(int id, CellPosition cell, ArchitectureKind target, int workRequired)
{
    public int Id { get; } = id;
    public CellPosition Cell { get; } = cell;
    public ArchitectureKind Target { get; } = target;
    /// <summary>Player-defined importance from 1 (low) to 5 (critical).</summary>
    public int Priority { get; set; } = 3;
    public ConstructionStatus Status { get; internal set; } = ConstructionStatus.Planned;
    public float WorkDone { get; internal set; }
    public int WorkRequired { get; } = workRequired;
}

internal sealed class ExcavationOrder(int id, CellPosition cell, int workRequired)
{
    public int Id { get; } = id;
    public CellPosition Cell { get; } = cell;
    public int Priority { get; set; } = 3;
    public ConstructionStatus Status { get; internal set; } = ConstructionStatus.Planned;
    public float WorkDone { get; internal set; }
    public int WorkRequired { get; } = workRequired;
}

internal sealed class PlaceableObjectState(int id, PlaceableKind kind, CellPosition origin, int width, int height,
    int workRequired, ConstructionStatus status)
{
    public int Id { get; } = id;
    public PlaceableKind Kind { get; } = kind;
    public int Priority { get; set; } = 3;
    public CellPosition Origin { get; } = origin;
    public int Width { get; } = width;
    public int Height { get; } = height;
    public ConstructionStatus Status { get; internal set; } = status;
    public float WorkDone { get; internal set; }
    public int WorkRequired { get; } = workRequired;
    public bool IsBuilt => Status == ConstructionStatus.Complete;

    public bool Occupies(CellPosition cell) =>
        cell.X >= Origin.X && cell.X < Origin.X + Width &&
        cell.Y >= Origin.Y && cell.Y < Origin.Y + Height;
}

internal sealed class ResourceDeposit(int id, CellPosition cell, ResourceKind resource, int amount)
{
    public int Id { get; } = id;
    public CellPosition Cell { get; } = cell;
    public ResourceKind Resource { get; } = resource;
    public int Amount { get; internal set; } = amount;
}

internal sealed class ColonyWorker(int id, string name, float x, float y, float hunger)
{
    public int Id { get; } = id;
    public string Name { get; } = name;
    public float X { get; internal set; } = x;
    public float Y { get; internal set; } = y;
    public float Hunger { get; internal set; } = hunger;
    public float ThermalExposure { get; internal set; }
    public float Stress { get; internal set; }
    public bool Hungry => Hunger >= 80f;
    public ResourceKind? CarriedResource { get; internal set; }
    public int CarriedAmount { get; internal set; }
    public WorkerTask? Task { get; internal set; }
}

internal sealed class WorkerTask(ColonyTaskKind kind, CellPosition target, List<CellPosition> route)
{
    public ColonyTaskKind Kind { get; set; } = kind;
    public CellPosition Target { get; set; } = target;
    public List<CellPosition> Route { get; set; } = route;
    public int RouteIndex { get; set; }
    public int TargetId { get; set; }
    public float WorkDone { get; set; }
    public int DepositId { get; set; }
}

internal sealed class ProductionOrder(int id, int workstationId, float workRequired)
{
    public int Id { get; } = id;
    public int WorkstationId { get; } = workstationId;
    public float WorkRequired { get; } = workRequired;
    public int Priority { get; set; } = 3;
    public float WorkDone { get; internal set; }
    public bool Complete { get; internal set; }
    public bool Assigned { get; internal set; }
}

/// <summary>
/// Initial colony model. Construction is deliberately split into per-tile architecture orders
/// and independent footprint-based placeable objects. Worker movement, resource transfer,
/// construction and production operate on logical grid coordinates only.
/// </summary>
internal sealed class ColonySimulation
{
    public const int Width = 40;
    public const int Height = 26;
    public const int BaseStockpileCapacity = 120;
    public const int StorageRackCapacity = 80;
    private const int CarryCapacity = 5;
    private const int MaxCachedPaths = 8_192;
    private readonly Dictionary<PathCacheKey, List<CellPosition>> _pathCache = [];
    private readonly Queue<PathCacheKey> _pathCacheOrder = new();
    internal int PathfindingSearches { get; private set; }
    internal int PathCacheHits { get; private set; }
    internal int CachedPathCount => _pathCache.Count;
    private readonly TileCell[,] _tiles = new TileCell[Width, Height];
    private readonly List<TileConstructionOrder> _tileOrders = [];
    private readonly List<ExcavationOrder> _excavationOrders = [];
    private readonly List<PlaceableObjectState> _objects = [];
    private readonly List<ResourceDeposit> _deposits = [];
    private readonly List<ColonyWorker> _workers = [];
    private readonly List<ProductionOrder> _productionOrders = [];
    private readonly Dictionary<ResourceKind, int> _inventory = Enum.GetValues<ResourceKind>().ToDictionary(k => k, _ => 0);
    private ContinuousWorldState? _sharedInventoryWorld;
    private long? _sharedInventoryOwnerId;
    private ColonyEcosystem _ecosystem = null!;
    private readonly Dictionary<int, CitizenAgent> _citizensByWorker = [];
    private readonly Dictionary<int, int> _gardenJobsByObject = [];
    private SocialRelationshipNetwork _socialRelationships = new();
    private IncidentManager _incidentManager = new();
    private long _socialTick;
    private readonly HashSet<int> _storageZonesByObject = [];
    private int _nextOrderId = 1;
    private int _nextExcavationId = 1;
    private int _nextWorkerId = 4;
    private int _nextSpawnCellIndex = 12 * Width + 14;
    private int _nextObjectId = 1;
    private int _nextDepositId = 1;
    private int _nextProductionId = 1;
    private float _environmentAccumulatorSeconds;
    private readonly float[,] _temperatureBuffer = new float[Width, Height];
    private WorkOrderKind? _selectedWorkOrderKind;
    private int? _selectedWorkOrderId;

    public IReadOnlyList<ColonyWorker> Workers => _workers;
    public SocialRelationshipNetwork SocialRelationships => _socialRelationships;
    public IncidentManager Incidents => _incidentManager;
    public IReadOnlyList<TileConstructionOrder> TileOrders => _tileOrders;
    public IReadOnlyList<ExcavationOrder> ExcavationOrders => _excavationOrders;
    public IReadOnlyList<PlaceableObjectState> Objects => _objects;
    public IReadOnlyList<ResourceDeposit> Deposits => _deposits;
    public string Message { get; private set; } = "Colony ready. Design architecture per tile or place an object blueprint.";
    public void SetMessage(string message) => Message = message;
    public int? SelectedWorkerId { get; private set; }
    public int? SelectedWorkOrderId => _selectedWorkOrderId;
    public int SelectedWorkOrderPriority => GetSelectedWorkOrderPriority() ?? 3;
    public string SelectedWorkOrderSummary => GetSelectedWorkOrderSummary();
    public string SelectedWorkOrderDiagnostic => GetSelectedWorkOrderDiagnostic();
    public float ElapsedSeconds { get; private set; }
    public int CompletedTileOrders => _tileOrders.Count(x => x.Status == ConstructionStatus.Complete);
    public int CompletedObjects => _objects.Count(x => x.IsBuilt);
    public int PendingJobs => _excavationOrders.Count(x => x.Status is ConstructionStatus.Planned or ConstructionStatus.UnderConstruction)
        + _tileOrders.Count(x => x.Status is ConstructionStatus.Planned or ConstructionStatus.WaitingForMaterials or ConstructionStatus.UnderConstruction)
        + _objects.Count(x => x.Status is ConstructionStatus.Planned or ConstructionStatus.WaitingForMaterials or ConstructionStatus.UnderConstruction)
        + _productionOrders.Count(x => !x.Complete);
    public int InventoryUsed => _sharedInventoryWorld == null || !_sharedInventoryOwnerId.HasValue
        ? _inventory.Values.Sum()
        : (int)Math.Ceiling(_sharedInventoryWorld.InventoryTotalAt(_sharedInventoryOwnerId.Value));
    public int InventoryCapacity =>
        _objects.Where(item => item.Kind == PlaceableKind.Stockpile && item.IsBuilt).Sum(_ => BaseStockpileCapacity) +
        _objects.Where(item => item.Kind == PlaceableKind.StorageRack && item.IsBuilt).Sum(_ => StorageRackCapacity);
    public int InventoryFreeCapacity => Math.Max(0, InventoryCapacity - InventoryUsed);

    public ColonySimulation()
    {
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
            _tiles[x, y] = new TileCell();

        // A small readable rock belt; the colony and the resource sites remain connected.
        foreach (var (x, y) in new[]
        {
            (22, 6), (23, 6), (24, 6), (22, 7), (23, 7), (24, 7),
            (25, 8), (26, 8), (25, 9), (26, 9), (31, 14), (32, 14), (31, 15)
        })
            _tiles[x, y].Terrain = TerrainKind.Rock;

        _inventory[ResourceKind.Wood] = 20;
        _inventory[ResourceKind.Stone] = 14;
        _inventory[ResourceKind.Food] = 32;
        _inventory[ResourceKind.Alloy] = 8;
        _inventory[ResourceKind.Components] = 4;

        AddBuiltObject(PlaceableKind.Habitat, new CellPosition(15, 8), 2, 2);
        AddBuiltObject(PlaceableKind.Stockpile, new CellPosition(18, 8), 3, 2);

        _workers.Add(new ColonyWorker(1, "Kor-01", 16.5f, 12.5f, 20f));
        _workers.Add(new ColonyWorker(2, "Kor-02", 17.5f, 12.5f, 30f));
        _workers.Add(new ColonyWorker(3, "Kor-03", 18.5f, 12.5f, 10f));

        AddDeposit(new CellPosition(10, 15), ResourceKind.Wood, 80);
        AddDeposit(new CellPosition(27, 12), ResourceKind.Stone, 90);
        AddDeposit(new CellPosition(14, 21), ResourceKind.Food, 100);
        AddDeposit(new CellPosition(34, 20), ResourceKind.Alloy, 45);
        InitializeEcosystem();
    }

    internal ColonySnapshot CaptureSnapshot()
    {
        var snapshot = new ColonySnapshot
        {
            SchemaVersion = 6,
            SocialTick = _socialTick,
            SocialNetwork = _socialRelationships.CaptureSnapshot(),
            IncidentManager = _incidentManager.CaptureSnapshot(),
            ElapsedSeconds = ElapsedSeconds,
            EnvironmentAccumulatorSeconds = _environmentAccumulatorSeconds,
            Message = Message,
            SelectedWorkerId = SelectedWorkerId,
            NextOrderId = _nextOrderId,
            NextExcavationId = _nextExcavationId,
            NextObjectId = _nextObjectId,
            NextDepositId = _nextDepositId,
            NextProductionId = _nextProductionId,
            Tiles = [],
            TileOrders = [],
            ExcavationOrders = [],
            Objects = [],
            Deposits = [],
            Workers = [],
            Inventory = [],
            ProductionOrders = []
        };

        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            var tile = _tiles[x, y];
            snapshot.Tiles.Add(new TileSnapshot
            {
                X = x, Y = y, Terrain = tile.Terrain, Architecture = tile.Architecture,
                ConstructionOrderId = tile.ConstructionOrderId,
                WaterLevel = tile.WaterLevel, MagmaLevel = tile.MagmaLevel,
                TemperatureCelsius = tile.TemperatureCelsius
            });
        }

        snapshot.ExcavationOrders.AddRange(_excavationOrders.Select(order => new ExcavationOrderSnapshot
        {
            Id = order.Id, X = order.Cell.X, Y = order.Cell.Y, Status = order.Status,
            WorkDone = order.WorkDone, WorkRequired = order.WorkRequired, Priority = order.Priority
        }));
        snapshot.TileOrders.AddRange(_tileOrders.Select(order => new TileOrderSnapshot
        {
            Id = order.Id, X = order.Cell.X, Y = order.Cell.Y, Target = order.Target,
            Status = order.Status, WorkDone = order.WorkDone, WorkRequired = order.WorkRequired, Priority = order.Priority
        }));
        snapshot.Objects.AddRange(_objects.Select(item => new PlaceableObjectSnapshot
        {
            Id = item.Id, Kind = item.Kind, X = item.Origin.X, Y = item.Origin.Y,
            Width = item.Width, Height = item.Height, Status = item.Status,
            WorkDone = item.WorkDone, WorkRequired = item.WorkRequired, Priority = item.Priority
        }));
        snapshot.Deposits.AddRange(_deposits.Select(deposit => new ResourceDepositSnapshot
        {
            Id = deposit.Id, X = deposit.Cell.X, Y = deposit.Cell.Y,
            Resource = deposit.Resource, Amount = deposit.Amount
        }));
        snapshot.Workers.AddRange(_workers.Select(worker => new ColonyWorkerSnapshot
        {
            Id = worker.Id, Name = worker.Name, X = worker.X, Y = worker.Y, Hunger = worker.Hunger,
            ThermalExposure = worker.ThermalExposure,
            Stress = worker.Stress,
            CitizenFatigue = _citizensByWorker.TryGetValue(worker.Id, out var citizen) ? citizen.Fatigue : 100f,
            CarriedResource = worker.CarriedResource, CarriedAmount = worker.CarriedAmount,
            Task = worker.Task == null ? null : new WorkerTaskSnapshot
            {
                Kind = worker.Task.Kind, TargetX = worker.Task.Target.X, TargetY = worker.Task.Target.Y,
                Route = worker.Task.Route.Select(cell => new CellSnapshot { X = cell.X, Y = cell.Y }).ToList(),
                RouteIndex = worker.Task.RouteIndex, TargetId = worker.Task.TargetId,
                WorkDone = worker.Task.WorkDone, DepositId = worker.Task.DepositId
            }
        }));
        if (_sharedInventoryWorld == null)
            snapshot.Inventory.AddRange(_inventory.Select(item => new ResourceStackSnapshot
            {
                Resource = item.Key, Amount = item.Value
            }));
        else
            snapshot.InventoryOwnedByWorld = true;
        snapshot.ProductionOrders.AddRange(_productionOrders.Select(order => new ProductionOrderSnapshot
        {
            Id = order.Id, WorkstationId = order.WorkstationId, WorkRequired = order.WorkRequired,
            WorkDone = order.WorkDone, Complete = order.Complete, Assigned = order.Assigned, Priority = order.Priority
        }));
        return snapshot;
    }

    internal static ColonySimulation RestoreSnapshot(ColonySnapshot snapshot)
    {
        snapshot.Validate();
        var simulation = new ColonySimulation();
        foreach (var tileState in snapshot.Tiles)
        {
            var tile = simulation._tiles[tileState.X, tileState.Y];
            tile.Terrain = tileState.Terrain;
            tile.Architecture = tileState.Architecture;
            tile.ConstructionOrderId = tileState.ConstructionOrderId;
            tile.WaterLevel = tileState.WaterLevel;
            tile.MagmaLevel = tileState.MagmaLevel;
            tile.TemperatureCelsius = tileState.TemperatureCelsius;
        }

        simulation._excavationOrders.Clear();
        simulation._excavationOrders.AddRange(snapshot.ExcavationOrders.Select(order => new ExcavationOrder(
            order.Id, new CellPosition(order.X, order.Y), order.WorkRequired)
        { Status = order.Status, WorkDone = order.WorkDone, Priority = order.Priority }));

        simulation._tileOrders.Clear();
        simulation._tileOrders.AddRange(snapshot.TileOrders.Select(order =>
        {
            var restored = new TileConstructionOrder(order.Id, new CellPosition(order.X, order.Y), order.Target, order.WorkRequired)
            { Status = order.Status, WorkDone = order.WorkDone, Priority = order.Priority };
            return restored;
        }));

        simulation._objects.Clear();
        simulation._objects.AddRange(snapshot.Objects.Select(item => new PlaceableObjectState(
            item.Id, item.Kind, new CellPosition(item.X, item.Y), item.Width, item.Height,
            item.WorkRequired, item.Status) { WorkDone = item.WorkDone, Priority = item.Priority }));

        simulation._deposits.Clear();
        simulation._deposits.AddRange(snapshot.Deposits.Select(deposit => new ResourceDeposit(
            deposit.Id, new CellPosition(deposit.X, deposit.Y), deposit.Resource, deposit.Amount)));

        simulation._workers.Clear();
        simulation._workers.AddRange(snapshot.Workers.Select(worker =>
        {
            var restored = new ColonyWorker(worker.Id, worker.Name, worker.X, worker.Y, worker.Hunger)
            {
                ThermalExposure = worker.ThermalExposure,
                Stress = worker.Stress,
                CarriedResource = worker.CarriedResource,
                CarriedAmount = worker.CarriedAmount,
                Task = worker.Task == null ? null : new WorkerTask(worker.Task.Kind,
                    new CellPosition(worker.Task.TargetX, worker.Task.TargetY),
                    worker.Task.Route.Select(cell => new CellPosition(cell.X, cell.Y)).ToList())
                {
                    RouteIndex = worker.Task.RouteIndex,
                    TargetId = worker.Task.TargetId,
                    WorkDone = worker.Task.WorkDone,
                    DepositId = worker.Task.DepositId
                }
            };
            return restored;
        }));
        simulation._nextWorkerId = simulation._workers.Count == 0 ? 1 : simulation._workers.Max(worker => worker.Id) + 1;

        if (!snapshot.InventoryOwnedByWorld)
        {
            simulation._inventory.Clear();
            foreach (var item in snapshot.Inventory) simulation._inventory.Add(item.Resource, item.Amount);
        }
        simulation._productionOrders.Clear();
        simulation._productionOrders.AddRange(snapshot.ProductionOrders.Select(order => new ProductionOrder(
            order.Id, order.WorkstationId, order.WorkRequired)
        {
            WorkDone = order.WorkDone, Complete = order.Complete, Assigned = order.Assigned, Priority = order.Priority
        }));

        simulation._nextOrderId = snapshot.NextOrderId;
        simulation._nextExcavationId = snapshot.NextExcavationId;
        simulation._nextObjectId = snapshot.NextObjectId;
        simulation._nextDepositId = snapshot.NextDepositId;
        simulation._nextProductionId = snapshot.NextProductionId;
        simulation.ElapsedSeconds = snapshot.ElapsedSeconds;
        simulation._environmentAccumulatorSeconds = snapshot.EnvironmentAccumulatorSeconds;
        simulation.Message = snapshot.Message;
        simulation.SelectedWorkerId = snapshot.SelectedWorkerId;
        simulation.InitializeEcosystem();
        foreach (var workerState in snapshot.Workers)
        {
            var citizen = simulation.CitizenForWorker(workerState.Id);
            if (citizen != null) citizen.Fatigue = workerState.CitizenFatigue;
        }
        simulation._socialTick = snapshot.SocialTick;
        var residentIds = simulation._workers.Select(worker => worker.Id).ToHashSet();
        if (snapshot.SocialNetwork != null)
            simulation._socialRelationships = SocialRelationshipNetwork.Restore(snapshot.SocialNetwork, residentIds);
        if (snapshot.IncidentManager != null)
            simulation._incidentManager = IncidentManager.Restore(snapshot.IncidentManager, residentIds);
        return simulation;
    }

    public int ResourceCount(ResourceKind resource)
    {
        double amount = InventoryAmount(resource);
        return (int)Math.Floor(amount + 1e-9);
    }

    /// <summary>
    /// Human-readable construction preflight for the build HUD. Planning remains possible when
    /// stock is missing, but the job scheduler will not start physical work until every input
    /// material is available and atomically consumed.
    /// </summary>
    public string ConstructionCostStatus(ArchitectureKind architecture, PlaceableKind placeable, bool isObject)
    {
        if (!isObject && architecture == ArchitectureKind.None)
            return "Select a tile type or object to inspect its material cost.";

        var costs = isObject ? ObjectCost(placeable) : TileCost(architecture);
        if (costs.Count == 0) return "No material recipe is defined for this build item.";

        var parts = new List<string>();
        var missing = new List<string>();
        foreach (var (resource, required) in costs.OrderBy(pair => pair.Key))
        {
            int available = ResourceCount(resource);
            parts.Add($"{resource} {required}");
            if (available < required) missing.Add($"{resource} {required - available} short");
        }

        return missing.Count == 0
            ? $"COST: {string.Join(" + ", parts)}  |  MATERIALS READY"
            : $"COST: {string.Join(" + ", parts)}  |  WAITING: {string.Join(", ", missing)}";
    }

    /// <summary>
    /// Makes the settlement ledger the sole authority for colony resources. Standalone colony
    /// simulations retain an in-memory inventory for isolated tests and tooling.
    /// </summary>
    public void BindWorldInventory(ContinuousWorldState world, long settlementId)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.Entities.Any(entity => entity.Id == settlementId &&
                                          entity.Kind == WorldEntityKind.Settlement))
            throw new ArgumentException("The shared inventory owner must be a settlement in this world.", nameof(settlementId));

        _sharedInventoryWorld = world;
        _sharedInventoryOwnerId = settlementId;
        _inventory.Clear(); // No second resource balance remains once the canonical ledger is attached.
        world.SetInventoryCapacity(settlementId, InventoryCapacity);

        // The ecosystem grid is an operational buffer for citizen needs and garden jobs, not a
        // second authority. Reconcile it from the canonical settlement ledger at bind time.
        int ledgerFood = ResourceCount(ResourceKind.Food);
        int gridFood = _ecosystem.Grid.ResourceCount(ResourceKind.Food);
        if (ledgerFood > gridFood && !_ecosystem.Grid.TryAdd(ResourceKind.Food, ledgerFood - gridFood))
            throw new InvalidOperationException("The ecosystem food buffer cannot mirror the settlement ledger.");
        if (gridFood > ledgerFood) _ecosystem.Grid.TryTake(ResourceKind.Food, gridFood - ledgerFood);
    }

    private double InventoryAmount(ResourceKind resource)
    {
        if (_sharedInventoryWorld == null || !_sharedInventoryOwnerId.HasValue)
            return _inventory.GetValueOrDefault(resource);
        var worldResource = Enum.Parse<WorldResourceKind>(resource.ToString(), ignoreCase: false);
        return _sharedInventoryWorld.InventoryAt(_sharedInventoryOwnerId.Value, worldResource);
    }

    private void AdjustResource(ResourceKind resource, int delta)
    {
        if (_sharedInventoryWorld == null || !_sharedInventoryOwnerId.HasValue)
        {
            _inventory[resource] = checked(ResourceCount(resource) + delta);
            return;
        }

        var worldResource = Enum.Parse<WorldResourceKind>(resource.ToString(), ignoreCase: false);
        bool changed = delta > 0
            ? _sharedInventoryWorld.TryAddInventory(_sharedInventoryOwnerId.Value, worldResource, delta)
            : _sharedInventoryWorld.TryTakeInventory(_sharedInventoryOwnerId.Value, worldResource, -delta);
        if (!changed)
            throw new InvalidOperationException("The canonical resource ledger rejected an invalid or over-capacity adjustment.");
    }

    public int AddFluid(int x, int y, FluidKind kind, int amount)
    {
        if (!InBounds(x, y)) throw new ArgumentOutOfRangeException(nameof(x), "Fluid coordinates must be inside the colony grid.");
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Fluid amount must be positive.");

        var tile = _tiles[x, y];
        if (tile.Terrain == TerrainKind.Rock || tile.Architecture == ArchitectureKind.Wall)
            return 0;
        if (kind == FluidKind.Water)
        {
            if (tile.MagmaLevel > 0) return 0;
            int accepted = Math.Min(amount, 7 - tile.WaterLevel);
            tile.WaterLevel += accepted;
            return accepted;
        }

        if (tile.WaterLevel > 0) return 0;
        int acceptedMagma = Math.Min(amount, 7 - tile.MagmaLevel);
        tile.MagmaLevel += acceptedMagma;
        return acceptedMagma;
    }

    public int FluidLevelAt(int x, int y, FluidKind kind)
    {
        if (!InBounds(x, y)) throw new ArgumentOutOfRangeException(nameof(x));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        return kind == FluidKind.Water ? _tiles[x, y].WaterLevel : _tiles[x, y].MagmaLevel;
    }

    public float TemperatureAt(int x, int y)
    {
        if (!InBounds(x, y)) throw new ArgumentOutOfRangeException(nameof(x));
        return _tiles[x, y].TemperatureCelsius;
    }

    public int TotalFluid(FluidKind kind)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        int total = 0;
        foreach (var tile in _tiles)
            total += kind == FluidKind.Water ? tile.WaterLevel : tile.MagmaLevel;
        return total;
    }

    private void StepEnvironment(float deltaSeconds)
    {
        // Cap the backlog and the work performed by a single frame. Large elapsed times are
        // processed over subsequent steps rather than triggering an unbounded catch-up loop.
        _environmentAccumulatorSeconds = Math.Min(_environmentAccumulatorSeconds + deltaSeconds, 1_000_000f);
        int steps = 0;
        while (_environmentAccumulatorSeconds >= 1f && steps < 8)
        {
            StepFluidLevels(FluidKind.Water);
            StepFluidLevels(FluidKind.Magma);
            StepTemperature();
            _environmentAccumulatorSeconds -= 1f;
            steps++;
        }
    }

    private void StepFluidLevels(FluidKind kind)
    {
        int count = Width * Height;
        var current = new int[count];
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
            current[y * Width + x] = kind == FluidKind.Water ? _tiles[x, y].WaterLevel : _tiles[x, y].MagmaLevel;
        var next = (int[])current.Clone();

        for (int index = 0; index < count; index++)
        {
            int sourceLevel = current[index];
            if (sourceLevel <= 0) continue;
            int x = index % Width, y = index / Width;
            int bestLevel = sourceLevel - 1;
            int bestTarget = -1;
            ConsiderFluidNeighbour(x, y - 1, current, next, kind, ref bestLevel, ref bestTarget);
            ConsiderFluidNeighbour(x - 1, y, current, next, kind, ref bestLevel, ref bestTarget);
            ConsiderFluidNeighbour(x + 1, y, current, next, kind, ref bestLevel, ref bestTarget);
            ConsiderFluidNeighbour(x, y + 1, current, next, kind, ref bestLevel, ref bestTarget);
            if (bestTarget < 0 || next[bestTarget] >= 7) continue;
            next[index]--;
            next[bestTarget]++;
        }

        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            int level = next[y * Width + x];
            if (kind == FluidKind.Water) _tiles[x, y].WaterLevel = level;
            else _tiles[x, y].MagmaLevel = level;
        }
    }

    private void ConsiderFluidNeighbour(int x, int y, int[] current, int[] next, FluidKind kind,
        ref int bestLevel, ref int bestTarget)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return;
        var tile = _tiles[x, y];
        if (tile.Terrain == TerrainKind.Rock || tile.Architecture == ArchitectureKind.Wall) return;
        if (kind == FluidKind.Water ? tile.MagmaLevel > 0 : tile.WaterLevel > 0) return;
        int index = y * Width + x;
        int level = current[index];
        if (level >= bestLevel || next[index] >= 7) return;
        bestLevel = level;
        bestTarget = index;
    }

    private void StepTemperature()
    {
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            var tile = _tiles[x, y];
            float current = tile.TemperatureCelsius;
            float weightedDelta = 0;
            int neighbours = 0;
            AccumulateTemperatureExchange(x, y, x, y - 1, current, ref weightedDelta, ref neighbours);
            AccumulateTemperatureExchange(x, y, x - 1, y, current, ref weightedDelta, ref neighbours);
            AccumulateTemperatureExchange(x, y, x + 1, y, current, ref weightedDelta, ref neighbours);
            AccumulateTemperatureExchange(x, y, x, y + 1, current, ref weightedDelta, ref neighbours);

            float materialConductivity = tile.Architecture switch
            {
                ArchitectureKind.Wall => 0.025f,
                ArchitectureKind.Door => 0.07f,
                _ when tile.Terrain == TerrainKind.Rock => 0.045f,
                _ => 0.10f
            };
            // Edge conductance weights each neighbour's temperature delta. Dividing by neighbour
            // count keeps the update bounded and lets walls insulate without perfect heat isolation.
            float exchange = neighbours == 0 ? 0 : weightedDelta / neighbours;
            float next = current + exchange * materialConductivity;

            if (tile.MagmaLevel > 0)
            {
                float magmaTemperature = Math.Min(2_000f, 600f + tile.MagmaLevel * 100f);
                next += (magmaTemperature - next) * 0.25f;
            }
            else if (tile.WaterLevel > 0)
            {
                next += (20f - next) * 0.10f;
            }
            _temperatureBuffer[x, y] = Math.Clamp(next, -273.15f, 5_000f);
        }

        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
            _tiles[x, y].TemperatureCelsius = _temperatureBuffer[x, y];
    }

    private void AccumulateTemperatureExchange(int sourceX, int sourceY, int neighbourX, int neighbourY,
        float sourceTemperature, ref float weightedDelta, ref int neighbours)
    {
        if (neighbourX < 0 || neighbourX >= Width || neighbourY < 0 || neighbourY >= Height) return;
        var source = _tiles[sourceX, sourceY];
        var neighbour = _tiles[neighbourX, neighbourY];
        float edgeConductance = source.Architecture switch
        {
            ArchitectureKind.Wall => 0.08f,
            ArchitectureKind.Door => 0.45f,
            _ => 1f
        };
        edgeConductance = Math.Min(edgeConductance, neighbour.Architecture switch
        {
            ArchitectureKind.Wall => 0.08f,
            ArchitectureKind.Door => 0.45f,
            _ => 1f
        });
        weightedDelta += (neighbour.TemperatureCelsius - sourceTemperature) * edgeConductance;
        neighbours++;
    }

    public TileCell? TileAt(int x, int y) => InBounds(x, y) ? _tiles[x, y] : null;

    /// <summary>Live citizen/job state from the shared colony ecosystem.</summary>
    public CitizenAgent? CitizenForWorker(int workerId) =>
        _citizensByWorker.TryGetValue(workerId, out var citizen) ? citizen : null;

    internal IReadOnlyList<SimulationEvent> DrainSimulationEvents(int maxCount = 4096) =>
        _ecosystem.DrainEvents(maxCount);

    /// <summary>
    /// Recruits a batch of named, individually simulated residents. A batch is bounded to keep
    /// accidental UI calls from freezing a frame; the settlement itself has no small population cap.
    /// </summary>
    public int RecruitColonists(int count)
    {
        if (count is < 1 or > 1_000)
            throw new ArgumentOutOfRangeException(nameof(count), "Recruitment batches must contain between 1 and 1,000 colonists.");

        int recruited = 0;
        int mapArea = Width * Height;
        for (int requested = 0; requested < count; requested++)
        {
            CellPosition? spawn = null;
            // Rotate across the grid to spread new residents across available ground.
            // Residents may share a tile; population is not artificially capped by map area.
            for (int scanned = 0; scanned < mapArea; scanned++)
            {
                int index = _nextSpawnCellIndex;
                _nextSpawnCellIndex = (_nextSpawnCellIndex + 1) % mapArea;
                var candidate = new CellPosition(index % Width, index / Width);
                if (!IsWalkable(candidate)) continue;
                spawn = candidate;
                break;
            }
            if (!spawn.HasValue) break;

            int id = _nextWorkerId++;
            var cell = spawn.Value;
            var worker = new ColonyWorker(id, $"Kor-{id:0000}", cell.X + 0.5f, cell.Y + 0.5f, 0f);
            _workers.Add(worker);
            _citizensByWorker[id] = _ecosystem.AddCitizen(worker.Name, cell);
            recruited++;
        }

        Message = recruited == 0
            ? "Recruitment failed: no walkable ground is available."
            : $"Recruited {recruited} colonist{(recruited == 1 ? "" : "s")}. Population: {_workers.Count}.";
        return recruited;
    }

    private void InitializeEcosystem()
    {
        _ecosystem = new ColonyEcosystem(Width, Height);
        _citizensByWorker.Clear();
        _gardenJobsByObject.Clear();
        _storageZonesByObject.Clear();

        // The starter stockpile occupies six map tiles. The first runtime bridge maps its
        // pooled capacity to the grid's bounded stacks while the legacy construction scheduler
        // continues to own non-food materials during this migration slice.
        _ecosystem.Grid.DefineStockpileZone(ResourceKind.Food,
        [
            new CellPosition(18, 8), new CellPosition(19, 8), new CellPosition(20, 8),
            new CellPosition(18, 9), new CellPosition(19, 9), new CellPosition(20, 9)
        ]);
        RegisterBuiltStorageRacks();
        int initialFood = ResourceCount(ResourceKind.Food);
        if (initialFood > 0 && !_ecosystem.Grid.TryAdd(ResourceKind.Food, initialFood))
            throw new InvalidOperationException("Could not migrate starter food into grid storage.");

        foreach (var worker in _workers)
        {
            var cell = new CellPosition(Math.Clamp((int)worker.X, 0, Width - 1), Math.Clamp((int)worker.Y, 0, Height - 1));
            _citizensByWorker[worker.Id] = _ecosystem.AddCitizen(worker.Name, cell);
        }
    }

    private void RegisterBuiltStorageRacks()
    {
        foreach (var rack in _objects.Where(item => item.Kind == PlaceableKind.StorageRack && item.IsBuilt).OrderBy(item => item.Id))
        {
            if (_storageZonesByObject.Contains(rack.Id)) continue;
            int tileCount = checked(rack.Width * rack.Height);
            if (tileCount <= 0 || StorageRackCapacity % tileCount != 0)
                throw new InvalidOperationException("Storage rack capacity must divide evenly across its footprint.");
            var cells = new List<CellPosition>(tileCount);
            for (int y = rack.Origin.Y; y < rack.Origin.Y + rack.Height; y++)
            for (int x = rack.Origin.X; x < rack.Origin.X + rack.Width; x++)
                cells.Add(new CellPosition(x, y));
            _ecosystem.Grid.DefineStockpileZone(ResourceKind.Food, cells, StorageRackCapacity / tileCount);
            _storageZonesByObject.Add(rack.Id);
        }
    }

    private void StepEcosystem(float deltaSeconds)
    {
        RegisterBuiltStorageRacks();
        var grid = _ecosystem.Grid;
        int inventoryFood = ResourceCount(ResourceKind.Food);
        int gridFood = grid.ResourceCount(ResourceKind.Food);
        if (inventoryFood > gridFood)
        {
            if (!grid.TryAdd(ResourceKind.Food, inventoryFood - gridFood))
                throw new InvalidOperationException("Grid food storage could not accept the current colony inventory.");
        }
        else if (gridFood > inventoryFood)
        {
            grid.TryTake(ResourceKind.Food, gridFood - inventoryFood);
        }

        foreach (var worker in _workers)
        {
            if (!_citizensByWorker.TryGetValue(worker.Id, out var citizen)) continue;
            if (citizen.State == CitizenState.Idle || worker.Task != null)
                citizen.Position = new CellPosition(Math.Clamp((int)worker.X, 0, Width - 1), Math.Clamp((int)worker.Y, 0, Height - 1));
            citizen.CanReceiveJob = worker.Task == null;
            citizen.Hunger = Math.Clamp(100f - worker.Hunger, 0f, 100f);
            citizen.Stress = worker.Stress;
        }

        // Completed gardens become real food-producing workplaces. Their output is reserved
        // against the grid before work starts; pause/remove them when pooled legacy inventory
        // has no free room so this bridge cannot silently overfill the colony.
        foreach (var garden in _objects.Where(item => item.Kind == PlaceableKind.Garden && item.IsBuilt))
        {
            if (_gardenJobsByObject.ContainsKey(garden.Id)) continue;
            if (InventoryFreeCapacity <= grid.ReservedCount(ResourceKind.Food)) continue;
            int jobId = _ecosystem.JobBroker.RegisterWorkplace(
                $"Garden #{garden.Id}", garden.Origin, workerCapacity: 1,
                outputResource: ResourceKind.Food, outputAmount: 1, workSeconds: 8f);
            _gardenJobsByObject.Add(garden.Id, jobId);
        }
        foreach (var pair in _gardenJobsByObject.ToArray())
        {
            if (InventoryFreeCapacity > 0 && InventoryFreeCapacity >= grid.ReservedCount(ResourceKind.Food)) continue;
            _ecosystem.RemoveWorkplace(pair.Value);
            _gardenJobsByObject.Remove(pair.Key);
        }

        int beforeFood = grid.ResourceCount(ResourceKind.Food);
        _ecosystem.Step(deltaSeconds);
        int afterFood = grid.ResourceCount(ResourceKind.Food);
        if (afterFood < beforeFood)
        {
            int consumed = beforeFood - afterFood;
            AdjustResource(ResourceKind.Food, -consumed);
        }
        else if (afterFood > beforeFood)
        {
            int produced = afterFood - beforeFood;
            int accepted = Math.Min(produced, InventoryFreeCapacity);
            if (accepted > 0) AdjustResource(ResourceKind.Food, accepted);
            if (accepted < produced) grid.TryTake(ResourceKind.Food, produced - accepted);
        }

        foreach (var worker in _workers)
        {
            if (!_citizensByWorker.TryGetValue(worker.Id, out var citizen)) continue;
            worker.Hunger = Math.Clamp(100f - citizen.Hunger, 0f, 100f);
            worker.Stress = citizen.Stress;
            if (worker.Task == null)
            {
                worker.X = citizen.Position.X + 0.5f;
                worker.Y = citizen.Position.Y + 0.5f;
            }
        }
    }

    public ResourceDeposit? DepositAt(int x, int y) =>
        _deposits.FirstOrDefault(d => d.Cell.X == x && d.Cell.Y == y && d.Amount > 0);

    public PlaceableObjectState? ObjectAt(int x, int y) =>
        _objects.FirstOrDefault(o => o.Occupies(new CellPosition(x, y)));

    public TileConstructionOrder? TileOrderAt(int x, int y) =>
        _tileOrders.FirstOrDefault(o => o.Cell.X == x && o.Cell.Y == y &&
            o.Status is not (ConstructionStatus.Complete or ConstructionStatus.Blocked));

    public ExcavationOrder? ExcavationOrderAt(int x, int y) =>
        _excavationOrders.FirstOrDefault(o => o.Cell.X == x && o.Cell.Y == y && o.Status != ConstructionStatus.Complete);

    /// <summary>Selects an unfinished blueprint or queued production order by clicking its world tile.</summary>
    public bool SelectWorkOrderAt(float worldX, float worldY)
    {
        int x = (int)MathF.Floor(worldX), y = (int)MathF.Floor(worldY);
        _selectedWorkOrderKind = null;
        _selectedWorkOrderId = null;
        if (!InBounds(x, y)) return false;

        var cell = new CellPosition(x, y);
        var objectOrder = _objects
            .Where(item => item.Occupies(cell) && !item.IsBuilt)
            .OrderBy(item => item.Id)
            .FirstOrDefault();
        if (objectOrder != null)
        {
            SelectWorkOrder(WorkOrderKind.ObjectConstruction, objectOrder.Id);
            Message = $"Selected {objectOrder.Kind} blueprint. Priority {objectOrder.Priority}/5.";
            return true;
        }

        var excavationOrder = _excavationOrders
            .Where(order => order.Cell == cell && order.Status != ConstructionStatus.Complete)
            .OrderBy(order => order.Id)
            .FirstOrDefault();
        if (excavationOrder != null)
        {
            SelectWorkOrder(WorkOrderKind.Excavation, excavationOrder.Id);
            Message = $"Selected excavation at ({x}, {y}). Priority {excavationOrder.Priority}/5.";
            return true;
        }

        var tileOrder = _tileOrders
            .Where(order => order.Cell == cell && order.Status != ConstructionStatus.Complete)
            .OrderBy(order => order.Id)
            .FirstOrDefault();
        if (tileOrder != null)
        {
            SelectWorkOrder(WorkOrderKind.TileConstruction, tileOrder.Id);
            Message = $"Selected {tileOrder.Target} order. Priority {tileOrder.Priority}/5.";
            return true;
        }

        var builtObject = ObjectAt(x, y);
        if (builtObject?.Kind == PlaceableKind.Workbench && builtObject.IsBuilt)
        {
            var production = _productionOrders
                .Where(order => order.WorkstationId == builtObject.Id && !order.Complete)
                .OrderByDescending(order => order.Priority)
                .ThenBy(order => order.Id)
                .FirstOrDefault();
            if (production != null)
            {
                SelectWorkOrder(WorkOrderKind.Production, production.Id);
                Message = $"Selected component production order. Priority {production.Priority}/5.";
                return true;
            }
        }

        return false;
    }

    /// <summary>Adjusts the selected order priority. 5 is critical; 1 is low.</summary>
    public bool ChangeSelectedWorkOrderPriority(int delta)
    {
        if (delta == 0 || !_selectedWorkOrderId.HasValue || !_selectedWorkOrderKind.HasValue)
            return false;

        int? current = GetSelectedWorkOrderPriority();
        if (!current.HasValue) return false;
        int priority = Math.Clamp(current.Value + Math.Sign(delta), 1, 5);
        switch (_selectedWorkOrderKind.Value)
        {
            case WorkOrderKind.Excavation:
                var excavation = _excavationOrders.FirstOrDefault(order => order.Id == _selectedWorkOrderId.Value);
                if (excavation == null || excavation.Status is ConstructionStatus.Complete or ConstructionStatus.Blocked) return false;
                excavation.Priority = priority;
                break;
            case WorkOrderKind.TileConstruction:
                var tile = _tileOrders.FirstOrDefault(order => order.Id == _selectedWorkOrderId.Value);
                if (tile == null || tile.Status == ConstructionStatus.Complete) return false;
                tile.Priority = priority;
                break;
            case WorkOrderKind.ObjectConstruction:
                var item = _objects.FirstOrDefault(order => order.Id == _selectedWorkOrderId.Value);
                if (item == null || item.IsBuilt) return false;
                item.Priority = priority;
                break;
            case WorkOrderKind.Production:
                var production = _productionOrders.FirstOrDefault(order => order.Id == _selectedWorkOrderId.Value);
                if (production == null || production.Complete) return false;
                production.Priority = priority;
                break;
            default:
                return false;
        }

        Message = $"Work order priority changed to {priority}/5 (5 is highest).";
        return true;
    }

    private void SelectWorkOrder(WorkOrderKind kind, int id)
    {
        _selectedWorkOrderKind = kind;
        _selectedWorkOrderId = id;
    }

    private int? GetSelectedWorkOrderPriority() => _selectedWorkOrderKind switch
    {
        WorkOrderKind.Excavation => _excavationOrders.FirstOrDefault(order => order.Id == _selectedWorkOrderId)?.Priority,
        WorkOrderKind.TileConstruction => _tileOrders.FirstOrDefault(order => order.Id == _selectedWorkOrderId)?.Priority,
        WorkOrderKind.ObjectConstruction => _objects.FirstOrDefault(order => order.Id == _selectedWorkOrderId)?.Priority,
        WorkOrderKind.Production => _productionOrders.FirstOrDefault(order => order.Id == _selectedWorkOrderId)?.Priority,
        _ => null
    };

    private string GetSelectedWorkOrderSummary() => _selectedWorkOrderKind switch
    {
        WorkOrderKind.Excavation => _excavationOrders.FirstOrDefault(order => order.Id == _selectedWorkOrderId) is { } excavationOrder
            ? $"Excavate ({excavationOrder.Cell.X},{excavationOrder.Cell.Y}) / {excavationOrder.Status}"
            : "Order no longer exists",
        WorkOrderKind.TileConstruction => _tileOrders.FirstOrDefault(order => order.Id == _selectedWorkOrderId) is { } tileOrder
            ? $"{tileOrder.Target} ({tileOrder.Cell.X},{tileOrder.Cell.Y}) / {tileOrder.Status}"
            : "No work order selected",
        WorkOrderKind.ObjectConstruction => _objects.FirstOrDefault(item => item.Id == _selectedWorkOrderId) is { } objectOrder
            ? $"{objectOrder.Kind} ({objectOrder.Origin.X},{objectOrder.Origin.Y}) / {objectOrder.Status}"
            : "No work order selected",
        WorkOrderKind.Production => _productionOrders.FirstOrDefault(order => order.Id == _selectedWorkOrderId) is { } productionOrder
            ? $"Components at bench #{productionOrder.WorkstationId} / {(productionOrder.Complete ? "Complete" : productionOrder.Assigned ? "Assigned" : "Queued")}"
            : "No work order selected",
        _ => "Select a blueprint or workbench on the map"
    };

    private string GetSelectedWorkOrderDiagnostic() => _selectedWorkOrderKind switch
    {
        WorkOrderKind.Excavation => _excavationOrders.FirstOrDefault(order => order.Id == _selectedWorkOrderId) is { } excavationOrder
            ? GetExcavationDiagnostic(excavationOrder)
            : "Order no longer exists",
        WorkOrderKind.TileConstruction => _tileOrders.FirstOrDefault(order => order.Id == _selectedWorkOrderId) is { } tileOrder
            ? GetConstructionDiagnostic(tileOrder.Status, tileOrder.WorkDone, tileOrder.WorkRequired, TileCost(tileOrder.Target))
            : "Order no longer exists",
        WorkOrderKind.ObjectConstruction => _objects.FirstOrDefault(item => item.Id == _selectedWorkOrderId) is { } objectOrder
            ? GetConstructionDiagnostic(objectOrder.Status, objectOrder.WorkDone, objectOrder.WorkRequired, ObjectCost(objectOrder.Kind))
            : "Order no longer exists",
        WorkOrderKind.Production => _productionOrders.FirstOrDefault(order => order.Id == _selectedWorkOrderId) is { } productionOrder
            ? productionOrder.Complete
                ? "Production complete"
                : $"Inputs committed; work {Math.Min(100, (int)(productionOrder.WorkDone / Math.Max(1f, productionOrder.WorkRequired) * 100))}%"
            : "Order no longer exists",
        _ => "Select a blueprint or workbench on the map"
    };

    private static string GetExcavationDiagnostic(ExcavationOrder order) => order.Status switch
    {
        ConstructionStatus.Blocked => "BLOCKED: no accessible work tile found",
        ConstructionStatus.UnderConstruction => $"MINING IN PROGRESS: {Math.Min(100, (int)(order.WorkDone / Math.Max(1, order.WorkRequired) * 100))}%",
        ConstructionStatus.Complete => "Excavation complete; stone deposit created",
        _ => "Rock is accessible; awaiting miner assignment"
    };

    private string GetConstructionDiagnostic(ConstructionStatus status, float workDone, float workRequired,
        Dictionary<ResourceKind, int> costs)
    {
        if (status == ConstructionStatus.Blocked) return "BLOCKED: no accessible work tile found";
        if (status == ConstructionStatus.UnderConstruction)
            return $"WORK IN PROGRESS: {Math.Min(100, (int)(workDone / Math.Max(1, workRequired) * 100))}% | materials committed";
        if (status == ConstructionStatus.Complete) return "Construction complete";

        var missing = costs
            .OrderBy(pair => pair.Key)
            .Select(pair => (pair.Key, Required: pair.Value, Available: ResourceCount(pair.Key)))
            .Where(item => item.Available < item.Required)
            .Select(item => $"{item.Key} {item.Required - item.Available} short")
            .ToList();
        if (missing.Count > 0) return $"WAITING: {string.Join(", ", missing)}";
        return "Materials ready; awaiting worker assignment";
    }

    public bool DesignExcavation(int x, int y)
    {
        if (!InBounds(x, y))
        {
            Message = "That tile is outside the map.";
            return false;
        }

        var cell = new CellPosition(x, y);
        var tile = _tiles[x, y];
        if (tile.Terrain != TerrainKind.Rock || tile.Architecture != ArchitectureKind.None ||
            tile.ConstructionOrderId.HasValue || ObjectAt(x, y) != null || DepositAt(x, y) != null)
        {
            Message = "Excavation requires solid, unoccupied rock with no existing deposit.";
            return false;
        }
        if (_excavationOrders.Any(order => order.Cell == cell && order.Status != ConstructionStatus.Complete))
        {
            Message = "That rock tile already has an excavation order.";
            return false;
        }

        var order = new ExcavationOrder(_nextExcavationId++, cell, workRequired: 3);
        _excavationOrders.Add(order);
        Message = $"Excavation marked at ({x}, {y}); a worker must reach the rock and mine it before stone can be hauled.";
        return true;
    }

    public bool DesignTileConstruction(int x, int y, ArchitectureKind target)
    {
        if (target is ArchitectureKind.None)
        {
            Message = "Choose a tile architecture type first.";
            return false;
        }
        if (!InBounds(x, y))
        {
            Message = "That tile is outside the map.";
            return false;
        }

        var cell = new CellPosition(x, y);
        var tile = _tiles[x, y];
        if (tile.Terrain != TerrainKind.Ground || tile.Architecture != ArchitectureKind.None ||
            tile.ConstructionOrderId.HasValue || ObjectAt(x, y) != null || DepositAt(x, y) != null)
        {
            Message = "Architecture cannot be designated on an occupied tile, an active deposit, or unsuitable ground.";
            return false;
        }

        int work = target switch
        {
            ArchitectureKind.Floor => 2,
            ArchitectureKind.Path => 2,
            ArchitectureKind.Wall => 4,
            ArchitectureKind.Door => 3,
            ArchitectureKind.Conduit => 3,
            _ => 2
        };
        var order = new TileConstructionOrder(_nextOrderId++, cell, target, work);
        _tileOrders.Add(order);
        tile.ConstructionOrderId = order.Id;
        Message = $"{target} construction marked at ({x}, {y}). Each tile has its own job.";
        return true;
    }

    public int PlaceObjectBlueprint(PlaceableKind kind, int x, int y)
    {
        if (kind is PlaceableKind.Habitat or PlaceableKind.Stockpile)
        {
            Message = "Core habitat and stockpile are fixed starter structures in this slice.";
            return 0;
        }
        if (!TryGetFootprint(kind, out int width, out int height, out int work))
        {
            Message = "Unknown object type.";
            return 0;
        }

        var origin = new CellPosition(x, y);
        for (int ty = y; ty < y + height; ty++)
        for (int tx = x; tx < x + width; tx++)
        {
            if (!InBounds(tx, ty) || _tiles[tx, ty].Terrain != TerrainKind.Ground ||
                _tiles[tx, ty].Architecture is ArchitectureKind.Wall ||
                _tiles[tx, ty].ConstructionOrderId.HasValue || ObjectAt(tx, ty) != null || DepositAt(tx, ty) != null)
            {
                Message = "The full object footprint must be clear ground without active resource deposits.";
                return 0;
            }
        }

        var item = new PlaceableObjectState(_nextObjectId++, kind, origin, width, height, work, ConstructionStatus.Planned);
        _objects.Add(item);
        InvalidatePathCache();
        Message = $"{kind} blueprint placed at ({x}, {y}); materials and worker time are still required.";
        return item.Id;
    }

    public bool QueueComponentProduction(int workstationId)
    {
        var station = _objects.FirstOrDefault(o => o.Id == workstationId && o.Kind == PlaceableKind.Workbench && o.IsBuilt);
        if (station == null)
        {
            Message = "Component production requires a completed workbench.";
            return false;
        }
        if (!TryConsume(new Dictionary<ResourceKind, int> { [ResourceKind.Alloy] = 2, [ResourceKind.Wood] = 1 }))
        {
            Message = "Component production needs 2 alloy and 1 wood in the inventory.";
            return false;
        }

        _productionOrders.Add(new ProductionOrder(_nextProductionId++, workstationId, 6f));
        Message = "Component production queued; 2 alloy and 1 wood are committed to the order.";
        return true;
    }

    public bool SelectWorkerAt(float worldX, float worldY)
    {
        // A linear nearest-resident scan avoids sorting the entire population on every click.
        ColonyWorker? nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (var worker in _workers)
        {
            float distance = Distance(worldX, worldY, worker.X, worker.Y);
            if (distance >= nearestDistance) continue;
            nearest = worker;
            nearestDistance = distance;
        }
        if (nearest == null || nearestDistance > 0.65f)
        {
            SelectedWorkerId = null;
            return false;
        }
        SelectedWorkerId = nearest.Id;
        Message = $"{nearest.Name} selected.";
        return true;
    }

    public void CommandSelectedTo(float worldX, float worldY)
    {
        var worker = _workers.FirstOrDefault(w => w.Id == SelectedWorkerId);
        if (worker == null)
        {
            Message = "Select a colonist before issuing a command.";
            return;
        }

        int x = (int)MathF.Floor(worldX), y = (int)MathF.Floor(worldY);
        if (!InBounds(x, y))
        {
            Message = "That destination is outside the map.";
            return;
        }
        if (worker.CarriedResource.HasValue)
        {
            Message = $"{worker.Name} must deliver the carried material before taking another order.";
            return;
        }
        if (worker.Task != null && worker.Task.Kind is
            (ColonyTaskKind.BuildTile or ColonyTaskKind.BuildObject or ColonyTaskKind.Produce or ColonyTaskKind.Deliver or ColonyTaskKind.Mine))
        {
            Message = $"{worker.Name} is committed to a construction or production task and cannot abandon its material/work reservation.";
            return;
        }
        if (_citizensByWorker.TryGetValue(worker.Id, out var citizen) &&
            citizen.State is CitizenState.MovingToStorage or CitizenState.MovingToJob or CitizenState.Working)
        {
            Message = $"{worker.Name} is committed to a citizen need or workplace task and cannot take a conflicting order.";
            return;
        }

        var deposit = DepositAt(x, y);
        if (deposit != null)
        {
            if (IsDepositReserved(deposit.Id, worker.Id))
            {
                Message = $"Another worker is already assigned to {deposit.Resource} at ({x}, {y}).";
                return;
            }
            if (InventoryFreeCapacity <= 0)
            {
                Message = "Colony storage is full. Build or complete additional storage before gathering more resources.";
                return;
            }
            if (!TryFindPath(worker, deposit.Cell, out var route))
            {
                Message = "No route to that resource deposit.";
                return;
            }
            worker.Task = new WorkerTask(ColonyTaskKind.Gather, deposit.Cell, route) { DepositId = deposit.Id };
            Message = $"{worker.Name} ordered to gather {deposit.Resource}. The material will be carried back to stockpile.";
            return;
        }

        var target = new CellPosition(x, y);
        if (!TryFindPath(worker, target, out var movement))
        {
            Message = "No walkable route to the selected tile.";
            return;
        }
        worker.Task = new WorkerTask(ColonyTaskKind.Move, target, movement);
        Message = $"{worker.Name} moving to ({x}, {y}).";
    }

    public void Step(float deltaSeconds)
    {
        if (deltaSeconds <= 0 || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds)) return;
        ElapsedSeconds += deltaSeconds;
        StepEnvironment(deltaSeconds);
        StepEcosystem(deltaSeconds);
        StepEnvironmentalExposure(deltaSeconds);
        AssignJobs();

        foreach (var worker in _workers)
        {
            if (worker.Task == null) continue;
            if (_citizensByWorker.TryGetValue(worker.Id, out var citizen) && citizen.State == CitizenState.MentalBreak)
                continue;
            AdvanceWorker(worker, deltaSeconds);
        }
        StepSocialRelationships(deltaSeconds);
    }


    private void StepSocialRelationships(float deltaSeconds)
    {
        _socialTick += Math.Max(1, (long)Math.Round(deltaSeconds * 10f, MidpointRounding.AwayFromZero));
        // Bucket residents by occupied tile; only neighboring buckets are compared.
        var buckets = new SortedDictionary<(int X, int Y), List<ColonyWorker>>();
        foreach (var worker in _workers.OrderBy(w => w.Id))
        {
            var key = (Math.Clamp((int)worker.X, 0, Width - 1), Math.Clamp((int)worker.Y, 0, Height - 1));
            if (!buckets.TryGetValue(key, out var list)) buckets[key] = list = [];
            list.Add(worker);
        }

        var pairs = new SortedSet<(int A, int B)>();
        foreach (var bucket in buckets)
        {
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = bucket.Key.X + dx, ny = bucket.Key.Y + dy;
                if (!buckets.TryGetValue((nx, ny), out var neighbors)) continue;
                foreach (var a in bucket.Value)
                foreach (var b in neighbors)
                {
                    if (a.Id >= b.Id) continue;
                    float ddx = a.X - b.X, ddy = a.Y - b.Y;
                    if (ddx * ddx + ddy * ddy > 2.25f) continue;
                    pairs.Add((a.Id, b.Id));
                }
            }
        }

        var workersById = _workers.ToDictionary(w => w.Id);
        foreach (var pair in pairs)
        {
            var a = workersById[pair.A];
            var b = workersById[pair.B];
            var existing = _socialRelationships.Get(a.Id, b.Id);
            if (existing != null && _socialTick - existing.LastEncounterTick < 30) continue;
            var citizenA = _citizensByWorker.GetValueOrDefault(a.Id);
            var citizenB = _citizensByWorker.GetValueOrDefault(b.Id);
            _socialRelationships.RecordEncounter(a.Id, b.Id, _socialTick,
                a.Stress, a.Hunger, citizenA == null ? 100f : 100f - citizenA.Fatigue,
                citizenA?.State == CitizenState.MentalBreak,
                b.Stress, b.Hunger, citizenB == null ? 100f : 100f - citizenB.Fatigue,
                citizenB?.State == CitizenState.MentalBreak);
        }
        _incidentManager.Consume(_socialRelationships.DrainEvents());
    }

    private void StepEnvironmentalExposure(float deltaSeconds)
    {
        foreach (var worker in _workers)
        {
            int x = Math.Clamp((int)worker.X, 0, Width - 1);
            int y = Math.Clamp((int)worker.Y, 0, Height - 1);
            float temperature = _tiles[x, y].TemperatureCelsius;
            float exposureRate = temperature > 45f ? (temperature - 45f) / 25f
                : temperature < 0f ? -temperature / 20f : 0f;
            exposureRate = Math.Clamp(exposureRate, 0f, 4f);

            float previous = worker.ThermalExposure;
            worker.ThermalExposure = exposureRate > 0
                ? Math.Min(100f, previous + exposureRate * deltaSeconds)
                : Math.Max(0f, previous - deltaSeconds * 1.25f);

            if (!_citizensByWorker.TryGetValue(worker.Id, out var citizen)) continue;
            if (previous < 15f && worker.ThermalExposure >= 15f)
            {
                _ecosystem.ReportHazard(citizen.Id, SimulationHazardKind.ThermalExposure, worker.ThermalExposure);
                Message = $"{worker.Name} is suffering dangerous thermal exposure.";
            }
            else if (previous >= 15f && worker.ThermalExposure < 15f)
            {
                _ecosystem.ReportHazard(citizen.Id, SimulationHazardKind.ThermalExposure, 0f);
                Message = $"{worker.Name} has recovered from thermal exposure.";
            }

            // Thermal strain reduces rest satisfaction, which in turn makes the existing job
            // broker release exhausted workers instead of treating heat/cold as a cosmetic effect.
            if (worker.ThermalExposure > 15f)
                citizen.Fatigue = Math.Max(0f, citizen.Fatigue -
                    deltaSeconds * Math.Min(1.5f, (worker.ThermalExposure - 15f) * 0.025f));
        }
    }

    private void UpdateNeeds(float deltaSeconds)
    {
        foreach (var worker in _workers)
        {
            worker.Hunger = Math.Min(100f, worker.Hunger + deltaSeconds * 0.32f);
            if (worker.Hunger >= 80f && TryTake(ResourceKind.Food, 1))
            {
                worker.Hunger = 24f;
                Message = $"{worker.Name} ate from the colony stores.";
            }
        }
    }

    private void AssignJobs()
    {
        foreach (var worker in _workers)
        {
            if (worker.Task != null) continue;
            if (_citizensByWorker.TryGetValue(worker.Id, out var citizen))
            {
                if (citizen.State is CitizenState.MovingToStorage or CitizenState.MovingToJob or CitizenState.Working or CitizenState.MentalBreak)
                    continue;
                if (citizen.Hunger < 30f || citizen.Fatigue < 60f)
                    continue;
            }

            if (worker.CarriedResource.HasValue)
            {
                AssignDelivery(worker);
                // Carrying cargo is an outstanding logistics obligation. Never assign unrelated
                // work while the worker is waiting for storage capacity or a reachable drop-off.
                continue;
            }

            bool assigned = false;
            // Priority is evaluated across tile builds, object blueprints and production orders.
            // Within one priority level, task-type order remains stable and existing behavior is preserved.
            for (int priority = 5; priority >= 1 && !assigned; priority--)
                assigned = TryAssignExcavation(worker, priority) ||
                           TryAssignTileBuild(worker, priority) ||
                           TryAssignObjectBuild(worker, priority) ||
                           TryAssignProduction(worker, priority);
            if (assigned) continue;
            TryAssignGatherForShortage(worker);
        }
    }

    private bool TryAssignExcavation(ColonyWorker worker, int priority)
    {
        foreach (var order in _excavationOrders
                     .Where(order => (order.Status is ConstructionStatus.Planned or ConstructionStatus.UnderConstruction) &&
                                     order.Priority == priority)
                     .OrderBy(order => order.Id))
        {
            // A manually redirected miner may leave a valid excavation unfinished. Reassign it,
            // but never let two workers own the same mining order concurrently.
            if (_workers.Any(candidate => candidate.Id != worker.Id &&
                    candidate.Task?.Kind == ColonyTaskKind.Mine &&
                    candidate.Task.TargetId == order.Id))
                continue;

            var workCell = FindWorkCell(worker, order.Cell, 1, 1);
            if (!workCell.HasValue)
            {
                order.Status = ConstructionStatus.Blocked;
                continue;
            }
            if (!TryFindPath(worker, workCell.Value, out var route))
            {
                order.Status = ConstructionStatus.Blocked;
                continue;
            }

            order.Status = ConstructionStatus.UnderConstruction;
            worker.Task = new WorkerTask(ColonyTaskKind.Mine, workCell.Value, route) { TargetId = order.Id };
            Message = $"{worker.Name} assigned to excavate rock at ({order.Cell.X}, {order.Cell.Y}).";
            return true;
        }
        return false;
    }

    private bool TryAssignTileBuild(ColonyWorker worker, int priority)
    {
        foreach (var order in _tileOrders.Where(o => (o.Status is ConstructionStatus.Planned or ConstructionStatus.WaitingForMaterials) && o.Priority == priority).OrderBy(o => o.Id))
        {
            var workCell = FindWorkCell(worker, order.Cell, 1, 1);
            if (!workCell.HasValue) { order.Status = ConstructionStatus.Blocked; continue; }

            if (!TryConsume(TileCost(order.Target)))
            {
                order.Status = ConstructionStatus.WaitingForMaterials;
                continue;
            }

            if (!TryFindPath(worker, workCell.Value, out var route))
            {
                // Return materials if a route changed between selection and assignment.
                AddMany(TileCost(order.Target));
                order.Status = ConstructionStatus.WaitingForMaterials;
                continue;
            }

            order.Status = ConstructionStatus.UnderConstruction;
            worker.Task = new WorkerTask(ColonyTaskKind.BuildTile, workCell.Value, route) { TargetId = order.Id };
            Message = $"{worker.Name} assigned to build {order.Target} at ({order.Cell.X}, {order.Cell.Y}).";
            return true;
        }
        return false;
    }

    private bool TryAssignObjectBuild(ColonyWorker worker, int priority)
    {
        foreach (var item in _objects.Where(o => (o.Status is ConstructionStatus.Planned or ConstructionStatus.WaitingForMaterials) && o.Priority == priority).OrderBy(o => o.Id))
        {
            if (!TryGetFootprint(item.Kind, out _, out _, out _)) continue;
            var workCell = FindWorkCell(worker, item.Origin, item.Width, item.Height);
            if (!workCell.HasValue) { item.Status = ConstructionStatus.Blocked; continue; }
            var cost = ObjectCost(item.Kind);
            if (!TryConsume(cost))
            {
                item.Status = ConstructionStatus.WaitingForMaterials;
                continue;
            }
            if (!TryFindPath(worker, workCell.Value, out var route))
            {
                AddMany(cost);
                item.Status = ConstructionStatus.WaitingForMaterials;
                continue;
            }
            item.Status = ConstructionStatus.UnderConstruction;
            worker.Task = new WorkerTask(ColonyTaskKind.BuildObject, workCell.Value, route) { TargetId = item.Id };
            Message = $"{worker.Name} assigned to construct {item.Kind}.";
            return true;
        }
        return false;
    }

    private bool TryAssignProduction(ColonyWorker worker, int priority)
    {
        foreach (var order in _productionOrders.Where(o => !o.Complete && !o.Assigned && o.Priority == priority).OrderBy(o => o.Id))
        {
            var station = _objects.FirstOrDefault(o => o.Id == order.WorkstationId && o.IsBuilt);
            if (station == null) { order.Complete = true; continue; }
            var workCell = FindWorkCell(worker, station.Origin, station.Width, station.Height);
            if (!workCell.HasValue || !TryFindPath(worker, workCell.Value, out var route)) continue;

            order.Assigned = true;
            worker.Task = new WorkerTask(ColonyTaskKind.Produce, workCell.Value, route) { TargetId = order.Id };
            Message = $"{worker.Name} assigned to produce components.";
            return true;
        }
        return false;
    }

    private void TryAssignGatherForShortage(ColonyWorker worker)
    {
        if (InventoryFreeCapacity <= 0) return;

        // A shortage inherits the highest priority of any pending order that needs that resource.
        // Among resources at the same priority, prefer the nearest available finite deposit.
        var priorityByResource = new Dictionary<ResourceKind, int>();

        void IncludeShortages(int priority, Dictionary<ResourceKind, int> costs)
        {
            foreach (var need in costs)
            {
                if (ResourceCount(need.Key) >= need.Value) continue;
                if (!priorityByResource.TryGetValue(need.Key, out int knownPriority) || priority > knownPriority)
                    priorityByResource[need.Key] = priority;
            }
        }

        foreach (var order in _tileOrders
                     .Where(order => order.Status is ConstructionStatus.Planned or ConstructionStatus.WaitingForMaterials)
                     .OrderByDescending(order => order.Priority).ThenBy(order => order.Id))
            IncludeShortages(order.Priority, TileCost(order.Target));

        foreach (var item in _objects
                     .Where(item => item.Status is ConstructionStatus.Planned or ConstructionStatus.WaitingForMaterials)
                     .OrderByDescending(item => item.Priority).ThenBy(item => item.Id))
            IncludeShortages(item.Priority, ObjectCost(item.Kind));

        var deposit = _deposits
            .Where(d => d.Amount > 0 && priorityByResource.ContainsKey(d.Resource) &&
                        !IsDepositReserved(d.Id, worker.Id))
            .OrderByDescending(d => priorityByResource[d.Resource])
            .ThenBy(d => Distance(worker.X, worker.Y, d.Cell.X + 0.5f, d.Cell.Y + 0.5f))
            .ThenBy(d => d.Id)
            .FirstOrDefault();
        if (deposit == null) return;
        if (!TryFindPath(worker, deposit.Cell, out var route)) return;
        worker.Task = new WorkerTask(ColonyTaskKind.Gather, deposit.Cell, route) { DepositId = deposit.Id };
        Message = $"{worker.Name} hauling is needed; gathering {deposit.Resource} for a priority {priorityByResource[deposit.Resource]} order.";
    }

    private bool IsDepositReserved(int depositId, int exceptWorkerId) =>
        _workers.Any(candidate => candidate.Id != exceptWorkerId &&
            candidate.Task != null &&
            candidate.Task.Kind == ColonyTaskKind.Gather &&
            candidate.Task.DepositId == depositId);

    private void AssignDelivery(ColonyWorker worker)
    {
        if (!worker.CarriedResource.HasValue || worker.CarriedAmount <= 0 || InventoryFreeCapacity <= 0) return;
        var drop = FindStockpileDropoff();
        if (!drop.HasValue || !TryFindPath(worker, drop.Value, out var route)) return;
        worker.Task = new WorkerTask(ColonyTaskKind.Deliver, drop.Value, route);
    }

    private void AdvanceWorker(ColonyWorker worker, float deltaSeconds)
    {
        var task = worker.Task;
        if (task == null) return;

        while (task.RouteIndex < task.Route.Count)
        {
            var waypoint = task.Route[task.RouteIndex];
            float dx = waypoint.X + 0.5f - worker.X;
            float dy = waypoint.Y + 0.5f - worker.Y;
            float distance = MathF.Sqrt(dx * dx + dy * dy);
            if (distance <= 0.035f)
            {
                worker.X = waypoint.X + 0.5f;
                worker.Y = waypoint.Y + 0.5f;
                task.RouteIndex++;
                continue;
            }

            float speed = worker.Hungry ? 1f : 2f;
            float movement = Math.Min(distance, speed * deltaSeconds);
            worker.X += dx / distance * movement;
            worker.Y += dy / distance * movement;
            return;
        }

        switch (task.Kind)
        {
            case ColonyTaskKind.Move:
                worker.Task = null;
                break;
            case ColonyTaskKind.Gather:
                CompleteGather(worker, task);
                break;
            case ColonyTaskKind.Deliver:
                CompleteDelivery(worker);
                break;
            case ColonyTaskKind.BuildTile:
                WorkOnTile(worker, task, deltaSeconds);
                break;
            case ColonyTaskKind.BuildObject:
                WorkOnObject(worker, task, deltaSeconds);
                break;
            case ColonyTaskKind.Produce:
                WorkOnProduction(worker, task, deltaSeconds);
                break;
            case ColonyTaskKind.Mine:
                WorkOnExcavation(worker, task, deltaSeconds);
                break;
        }
    }

    private void CompleteGather(ColonyWorker worker, WorkerTask task)
    {
        var deposit = _deposits.FirstOrDefault(d => d.Id == task.DepositId);
        if (deposit == null || deposit.Amount <= 0)
        {
            worker.Task = null;
            Message = "The resource deposit is exhausted.";
            return;
        }

        int amount = Math.Min(CarryCapacity, deposit.Amount);
        deposit.Amount -= amount;
        worker.CarriedResource = deposit.Resource;
        worker.CarriedAmount = amount;
        worker.Task = null;
        AssignDelivery(worker);
        Message = $"{worker.Name} collected {amount} {deposit.Resource}; returning it to stockpile.";
    }

    private void CompleteDelivery(ColonyWorker worker)
    {
        if (!worker.CarriedResource.HasValue || worker.CarriedAmount <= 0)
        {
            worker.Task = null;
            return;
        }

        int delivered = Math.Min(worker.CarriedAmount, InventoryFreeCapacity);
        if (delivered > 0)
        {
            AddResource(worker.CarriedResource.Value, delivered);
            worker.CarriedAmount -= delivered;
        }

        if (worker.CarriedAmount == 0)
        {
            ResourceKind deliveredResource = worker.CarriedResource.Value;
            worker.CarriedResource = null;
            Message = $"{worker.Name} delivered {delivered} {deliveredResource}.";
        }
        else
        {
            Message = delivered == 0
                ? "Colony storage is full; carried resources are waiting for storage space."
                : $"{worker.Name} delivered {delivered}; {worker.CarriedAmount} remain carried until storage space is available.";
        }
        worker.Task = null;
    }

    private void WorkOnExcavation(ColonyWorker worker, WorkerTask task, float deltaSeconds)
    {
        var order = _excavationOrders.FirstOrDefault(candidate => candidate.Id == task.TargetId);
        if (order == null || order.Status != ConstructionStatus.UnderConstruction)
        {
            worker.Task = null;
            return;
        }

        order.WorkDone += deltaSeconds * (worker.Hungry ? 0.5f : 1f);
        if (order.WorkDone < order.WorkRequired) return;

        var tile = _tiles[order.Cell.X, order.Cell.Y];
        if (tile.Terrain != TerrainKind.Rock || tile.Architecture != ArchitectureKind.None)
        {
            order.Status = ConstructionStatus.Blocked;
            worker.Task = null;
            Message = $"Excavation at ({order.Cell.X}, {order.Cell.Y}) is blocked because the tile is no longer solid rock.";
            return;
        }

        // Mining consumes a finite rock tile and creates a physical stone deposit, not inventory.
        tile.Terrain = TerrainKind.Ground;
        InvalidatePathCache();
        AddDeposit(order.Cell, ResourceKind.Stone, amount: 3);
        order.Status = ConstructionStatus.Complete;
        worker.Task = null;
        Message = $"Excavation complete at ({order.Cell.X}, {order.Cell.Y}); 3 stone are ready to be hauled.";
    }

    private void WorkOnTile(ColonyWorker worker, WorkerTask task, float deltaSeconds)
    {
        var order = _tileOrders.FirstOrDefault(o => o.Id == task.TargetId);
        if (order == null || order.Status != ConstructionStatus.UnderConstruction)
        {
            worker.Task = null;
            return;
        }
        order.WorkDone += deltaSeconds * (worker.Hungry ? 0.5f : 1f);
        if (order.WorkDone < order.WorkRequired) return;

        var tile = _tiles[order.Cell.X, order.Cell.Y];
        tile.Architecture = order.Target;
        tile.ConstructionOrderId = null;
        InvalidatePathCache();
        order.Status = ConstructionStatus.Complete;
        worker.Task = null;
        Message = $"{order.Target} completed at ({order.Cell.X}, {order.Cell.Y}).";
    }

    private void WorkOnObject(ColonyWorker worker, WorkerTask task, float deltaSeconds)
    {
        var item = _objects.FirstOrDefault(o => o.Id == task.TargetId);
        if (item == null || item.Status != ConstructionStatus.UnderConstruction)
        {
            worker.Task = null;
            return;
        }
        item.WorkDone += deltaSeconds * (worker.Hungry ? 0.5f : 1f);
        if (item.WorkDone < item.WorkRequired) return;
        item.Status = ConstructionStatus.Complete;
        worker.Task = null;
        Message = $"{item.Kind} completed and is now operational.";
    }

    private void WorkOnProduction(ColonyWorker worker, WorkerTask task, float deltaSeconds)
    {
        var order = _productionOrders.FirstOrDefault(o => o.Id == task.TargetId);
        if (order == null || order.Complete)
        {
            worker.Task = null;
            return;
        }
        float workRate = worker.Hungry ? 0.5f : 1f;
        if (order.WorkDone + deltaSeconds * workRate >= order.WorkRequired && InventoryFreeCapacity < 1)
        {
            Message = "Production is waiting for storage space before it can deliver the completed component.";
            return;
        }
        order.WorkDone += deltaSeconds * workRate;
        if (order.WorkDone < order.WorkRequired) return;
        order.Complete = true;
        order.Assigned = false;
        AddResource(ResourceKind.Components, 1);
        worker.Task = null;
        Message = "Workbench production completed: 1 component produced from committed materials.";
    }

    private CellPosition? FindWorkCell(ColonyWorker worker, CellPosition origin, int width, int height)
    {
        var candidates = new List<CellPosition>();
        for (int x = origin.X; x < origin.X + width; x++)
        {
            candidates.Add(new CellPosition(x, origin.Y - 1));
            candidates.Add(new CellPosition(x, origin.Y + height));
        }
        for (int y = origin.Y; y < origin.Y + height; y++)
        {
            candidates.Add(new CellPosition(origin.X - 1, y));
            candidates.Add(new CellPosition(origin.X + width, y));
        }

        foreach (var candidate in candidates.Where(IsWalkable)
                     .OrderBy(c => Distance(worker.X, worker.Y, c.X + 0.5f, c.Y + 0.5f)))
            if (TryFindPath(worker, candidate, out _))
                return candidate;
        return null;
    }

    private CellPosition? FindStockpileDropoff()
    {
        var stockpile = _objects.FirstOrDefault(o => o.Kind == PlaceableKind.Stockpile && o.IsBuilt);
        if (stockpile == null) return null;
        var candidates = new[]
        {
            new CellPosition(stockpile.Origin.X - 1, stockpile.Origin.Y),
            new CellPosition(stockpile.Origin.X, stockpile.Origin.Y - 1),
            new CellPosition(stockpile.Origin.X + stockpile.Width, stockpile.Origin.Y),
            new CellPosition(stockpile.Origin.X, stockpile.Origin.Y + stockpile.Height)
        };
        foreach (var candidate in candidates)
            if (IsWalkable(candidate)) return candidate;
        return null;
    }

    private bool TryFindPath(ColonyWorker worker, CellPosition target, out List<CellPosition> path)
        => TryFindPath(new CellPosition((int)worker.X, (int)worker.Y), target, out path);

    private bool TryFindPath(CellPosition start, CellPosition target, out List<CellPosition> path)
    {
        path = [];
        if (!InBounds(start.X, start.Y) || !IsWalkable(target)) return false;

        var key = new PathCacheKey(start, target);
        if (_pathCache.TryGetValue(key, out var cachedPath))
        {
            PathCacheHits++;
            path = [.. cachedPath];
            return true;
        }

        PathfindingSearches++;
        var previous = new CellPosition?[Width, Height];
        var visited = new bool[Width, Height];
        var queue = new Queue<CellPosition>();
        queue.Enqueue(start);
        visited[start.X, start.Y] = true;

        CellPosition[] directions = [new(0, -1), new(1, 0), new(0, 1), new(-1, 0)];
        while (queue.Count > 0 && !visited[target.X, target.Y])
        {
            var current = queue.Dequeue();
            foreach (var direction in directions)
            {
                var next = new CellPosition(current.X + direction.X, current.Y + direction.Y);
                if (!InBounds(next.X, next.Y) || visited[next.X, next.Y] || !IsWalkable(next)) continue;
                visited[next.X, next.Y] = true;
                previous[next.X, next.Y] = current;
                queue.Enqueue(next);
            }
        }
        if (!visited[target.X, target.Y]) return false;

        var cursor = target;
        while (cursor != start)
        {
            path.Add(cursor);
            var prior = previous[cursor.X, cursor.Y];
            if (!prior.HasValue) return false;
            cursor = prior.Value;
        }
        path.Reverse();
        StoreCachedPath(key, path);
        return true;
    }

    /// <summary>Diagnostic route query used by deterministic scalability regression checks.</summary>
    internal bool TryFindPathForDiagnostics(CellPosition start, CellPosition target, out List<CellPosition> path)
        => TryFindPath(start, target, out path);

    private void StoreCachedPath(PathCacheKey key, List<CellPosition> path)
    {
        if (_pathCache.Count >= MaxCachedPaths)
        {
            while (_pathCacheOrder.Count > 0)
            {
                var oldest = _pathCacheOrder.Dequeue();
                if (_pathCache.Remove(oldest)) break;
            }
        }

        _pathCache[key] = [.. path];
        _pathCacheOrder.Enqueue(key);
    }

    private void InvalidatePathCache()
    {
        _pathCache.Clear();
        _pathCacheOrder.Clear();
    }

    private bool IsWalkable(CellPosition cell)
    {
        if (!InBounds(cell.X, cell.Y)) return false;
        var tile = _tiles[cell.X, cell.Y];
        if (tile.Terrain != TerrainKind.Ground || tile.Architecture == ArchitectureKind.Wall) return false;
        return ObjectAt(cell.X, cell.Y) == null;
    }

    private bool TryConsume(Dictionary<ResourceKind, int> costs)
    {
        if (costs.Any(x => x.Value < 0 || ResourceCount(x.Key) < x.Value)) return false;
        foreach (var (resource, amount) in costs) AdjustResource(resource, -amount);
        return true;
    }

    private bool TryTake(ResourceKind resource, int amount)
    {
        if (amount < 0 || ResourceCount(resource) < amount) return false;
        AdjustResource(resource, -amount);
        return true;
    }

    private void AddResource(ResourceKind resource, int amount)
    {
        if (amount > 0) AdjustResource(resource, amount);
    }

    private void AddMany(Dictionary<ResourceKind, int> costs)
    {
        foreach (var (resource, amount) in costs) AddResource(resource, amount);
    }

    private void AddBuiltObject(PlaceableKind kind, CellPosition origin, int width, int height)
        => _objects.Add(new PlaceableObjectState(_nextObjectId++, kind, origin, width, height, 0, ConstructionStatus.Complete));

    private void AddDeposit(CellPosition cell, ResourceKind resource, int amount)
    {
        _tiles[cell.X, cell.Y].Terrain = TerrainKind.Ground;
        _deposits.Add(new ResourceDeposit(_nextDepositId++, cell, resource, amount));
    }

    private static Dictionary<ResourceKind, int> TileCost(ArchitectureKind target) => target switch
    {
        ArchitectureKind.Floor => new() { [ResourceKind.Wood] = 1 },
        ArchitectureKind.Path => new() { [ResourceKind.Stone] = 1 },
        ArchitectureKind.Wall => new() { [ResourceKind.Wood] = 2, [ResourceKind.Stone] = 1 },
        ArchitectureKind.Door => new() { [ResourceKind.Wood] = 3 },
        ArchitectureKind.Conduit => new() { [ResourceKind.Alloy] = 1 },
        _ => new()
    };

    private static Dictionary<ResourceKind, int> ObjectCost(PlaceableKind kind) => kind switch
    {
        PlaceableKind.Workbench => new() { [ResourceKind.Wood] = 6, [ResourceKind.Stone] = 3 },
        PlaceableKind.SleepingPod => new() { [ResourceKind.Wood] = 4 },
        PlaceableKind.StorageRack => new() { [ResourceKind.Wood] = 4, [ResourceKind.Stone] = 2 },
        PlaceableKind.Generator => new() { [ResourceKind.Alloy] = 8, [ResourceKind.Components] = 3 },
        PlaceableKind.Garden => new() { [ResourceKind.Wood] = 2, [ResourceKind.Stone] = 1 },
        _ => new()
    };

    private static bool TryGetFootprint(PlaceableKind kind, out int width, out int height, out int work)
    {
        (width, height, work) = kind switch
        {
            PlaceableKind.Workbench => (2, 2, 8),
            PlaceableKind.SleepingPod => (2, 1, 5),
            PlaceableKind.StorageRack => (2, 1, 4),
            PlaceableKind.Generator => (2, 2, 10),
            PlaceableKind.Garden => (3, 3, 6),
            _ => (0, 0, 0)
        };
        return width > 0;
    }

    private bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
    private static float Distance(float x1, float y1, float x2, float y2)
        => MathF.Sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2));
}
