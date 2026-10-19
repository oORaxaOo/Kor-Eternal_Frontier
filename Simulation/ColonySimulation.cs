using KorTheBlight.WorldModel;

namespace KorTheBlight.Simulation;

internal enum TerrainKind { Ground, Rock, Water }
internal enum ArchitectureKind { None, Floor, Wall, Door, Path, Conduit }
internal enum PlaceableKind { Habitat, Stockpile, Workbench, SleepingPod, StorageRack, Generator, Garden }
internal enum ResourceKind { Wood, Stone, Food, Alloy, Components }
internal enum ConstructionStatus { Planned, WaitingForMaterials, UnderConstruction, Complete, Blocked }
internal enum ColonyTaskKind { Move, Gather, Deliver, BuildTile, BuildObject, Produce }

internal readonly record struct CellPosition(int X, int Y);

internal sealed class TileCell
{
    public TerrainKind Terrain { get; internal set; } = TerrainKind.Ground;
    public ArchitectureKind Architecture { get; internal set; }
    public int? ConstructionOrderId { get; internal set; }
}

internal sealed class TileConstructionOrder(int id, CellPosition cell, ArchitectureKind target, int workRequired)
{
    public int Id { get; } = id;
    public CellPosition Cell { get; } = cell;
    public ArchitectureKind Target { get; } = target;
    public ConstructionStatus Status { get; internal set; } = ConstructionStatus.Planned;
    public float WorkDone { get; internal set; }
    public int WorkRequired { get; } = workRequired;
}

internal sealed class PlaceableObjectState(int id, PlaceableKind kind, CellPosition origin, int width, int height,
    int workRequired, ConstructionStatus status)
{
    public int Id { get; } = id;
    public PlaceableKind Kind { get; } = kind;
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
    private readonly TileCell[,] _tiles = new TileCell[Width, Height];
    private readonly List<TileConstructionOrder> _tileOrders = [];
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
    private readonly HashSet<int> _storageZonesByObject = [];
    private int _nextOrderId = 1;
    private int _nextObjectId = 1;
    private int _nextDepositId = 1;
    private int _nextProductionId = 1;

    public IReadOnlyList<ColonyWorker> Workers => _workers;
    public IReadOnlyList<TileConstructionOrder> TileOrders => _tileOrders;
    public IReadOnlyList<PlaceableObjectState> Objects => _objects;
    public IReadOnlyList<ResourceDeposit> Deposits => _deposits;
    public string Message { get; private set; } = "Colony ready. Design architecture per tile or place an object blueprint.";
    public void SetMessage(string message) => Message = message;
    public int? SelectedWorkerId { get; private set; }
    public float ElapsedSeconds { get; private set; }
    public int CompletedTileOrders => _tileOrders.Count(x => x.Status == ConstructionStatus.Complete);
    public int CompletedObjects => _objects.Count(x => x.IsBuilt);
    public int PendingJobs => _tileOrders.Count(x => x.Status is ConstructionStatus.Planned or ConstructionStatus.WaitingForMaterials or ConstructionStatus.UnderConstruction)
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
            SchemaVersion = 2,
            ElapsedSeconds = ElapsedSeconds,
            Message = Message,
            SelectedWorkerId = SelectedWorkerId,
            NextOrderId = _nextOrderId,
            NextObjectId = _nextObjectId,
            NextDepositId = _nextDepositId,
            NextProductionId = _nextProductionId,
            Tiles = [],
            TileOrders = [],
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
                ConstructionOrderId = tile.ConstructionOrderId
            });
        }

        snapshot.TileOrders.AddRange(_tileOrders.Select(order => new TileOrderSnapshot
        {
            Id = order.Id, X = order.Cell.X, Y = order.Cell.Y, Target = order.Target,
            Status = order.Status, WorkDone = order.WorkDone, WorkRequired = order.WorkRequired
        }));
        snapshot.Objects.AddRange(_objects.Select(item => new PlaceableObjectSnapshot
        {
            Id = item.Id, Kind = item.Kind, X = item.Origin.X, Y = item.Origin.Y,
            Width = item.Width, Height = item.Height, Status = item.Status,
            WorkDone = item.WorkDone, WorkRequired = item.WorkRequired
        }));
        snapshot.Deposits.AddRange(_deposits.Select(deposit => new ResourceDepositSnapshot
        {
            Id = deposit.Id, X = deposit.Cell.X, Y = deposit.Cell.Y,
            Resource = deposit.Resource, Amount = deposit.Amount
        }));
        snapshot.Workers.AddRange(_workers.Select(worker => new ColonyWorkerSnapshot
        {
            Id = worker.Id, Name = worker.Name, X = worker.X, Y = worker.Y, Hunger = worker.Hunger,
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
            WorkDone = order.WorkDone, Complete = order.Complete, Assigned = order.Assigned
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
        }

        simulation._tileOrders.Clear();
        simulation._tileOrders.AddRange(snapshot.TileOrders.Select(order =>
        {
            var restored = new TileConstructionOrder(order.Id, new CellPosition(order.X, order.Y), order.Target, order.WorkRequired)
            { Status = order.Status, WorkDone = order.WorkDone };
            return restored;
        }));

        simulation._objects.Clear();
        simulation._objects.AddRange(snapshot.Objects.Select(item => new PlaceableObjectState(
            item.Id, item.Kind, new CellPosition(item.X, item.Y), item.Width, item.Height,
            item.WorkRequired, item.Status) { WorkDone = item.WorkDone }));

        simulation._deposits.Clear();
        simulation._deposits.AddRange(snapshot.Deposits.Select(deposit => new ResourceDeposit(
            deposit.Id, new CellPosition(deposit.X, deposit.Y), deposit.Resource, deposit.Amount)));

        simulation._workers.Clear();
        simulation._workers.AddRange(snapshot.Workers.Select(worker =>
        {
            var restored = new ColonyWorker(worker.Id, worker.Name, worker.X, worker.Y, worker.Hunger)
            {
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

        if (!snapshot.InventoryOwnedByWorld)
        {
            simulation._inventory.Clear();
            foreach (var item in snapshot.Inventory) simulation._inventory.Add(item.Resource, item.Amount);
        }
        simulation._productionOrders.Clear();
        simulation._productionOrders.AddRange(snapshot.ProductionOrders.Select(order => new ProductionOrder(
            order.Id, order.WorkstationId, order.WorkRequired)
        {
            WorkDone = order.WorkDone, Complete = order.Complete, Assigned = order.Assigned
        }));

        simulation._nextOrderId = snapshot.NextOrderId;
        simulation._nextObjectId = snapshot.NextObjectId;
        simulation._nextDepositId = snapshot.NextDepositId;
        simulation._nextProductionId = snapshot.NextProductionId;
        simulation.ElapsedSeconds = snapshot.ElapsedSeconds;
        simulation.Message = snapshot.Message;
        simulation.SelectedWorkerId = snapshot.SelectedWorkerId;
        simulation.InitializeEcosystem();
        foreach (var workerState in snapshot.Workers)
        {
            var citizen = simulation.CitizenForWorker(workerState.Id);
            if (citizen != null) citizen.Fatigue = workerState.CitizenFatigue;
        }
        return simulation;
    }

    public int ResourceCount(ResourceKind resource)
    {
        double amount = InventoryAmount(resource);
        return (int)Math.Floor(amount + 1e-9);
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

    public TileCell? TileAt(int x, int y) => InBounds(x, y) ? _tiles[x, y] : null;

    /// <summary>Live citizen/job state from the shared colony ecosystem.</summary>
    public CitizenAgent? CitizenForWorker(int workerId) =>
        _citizensByWorker.TryGetValue(workerId, out var citizen) ? citizen : null;

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
            tile.ConstructionOrderId.HasValue || ObjectAt(x, y) != null)
        {
            Message = "Architecture cannot be designated on an occupied or unsuitable tile.";
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
                _tiles[tx, ty].ConstructionOrderId.HasValue || ObjectAt(tx, ty) != null)
            {
                Message = "The full object footprint must be clear ground.";
                return 0;
            }
        }

        var item = new PlaceableObjectState(_nextObjectId++, kind, origin, width, height, work, ConstructionStatus.Planned);
        _objects.Add(item);
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
        var worker = _workers.OrderBy(w => Distance(worldX, worldY, w.X, w.Y)).FirstOrDefault();
        if (worker == null || Distance(worldX, worldY, worker.X, worker.Y) > 0.65f)
        {
            SelectedWorkerId = null;
            return false;
        }
        SelectedWorkerId = worker.Id;
        Message = $"{worker.Name} selected.";
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
            (ColonyTaskKind.BuildTile or ColonyTaskKind.BuildObject or ColonyTaskKind.Produce or ColonyTaskKind.Deliver))
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
        StepEcosystem(deltaSeconds);
        AssignJobs();

        foreach (var worker in _workers)
        {
            if (worker.Task == null) continue;
            AdvanceWorker(worker, deltaSeconds);
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
                if (citizen.State is CitizenState.MovingToStorage or CitizenState.MovingToJob or CitizenState.Working)
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

            if (TryAssignTileBuild(worker)) continue;
            if (TryAssignObjectBuild(worker)) continue;
            if (TryAssignProduction(worker)) continue;
            TryAssignGatherForShortage(worker);
        }
    }

    private bool TryAssignTileBuild(ColonyWorker worker)
    {
        foreach (var order in _tileOrders.Where(o => o.Status is ConstructionStatus.Planned or ConstructionStatus.WaitingForMaterials))
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

    private bool TryAssignObjectBuild(ColonyWorker worker)
    {
        foreach (var item in _objects.Where(o => o.Status is ConstructionStatus.Planned or ConstructionStatus.WaitingForMaterials))
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

    private bool TryAssignProduction(ColonyWorker worker)
    {
        foreach (var order in _productionOrders.Where(o => !o.Complete && !o.Assigned))
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
        var required = new HashSet<ResourceKind>();
        foreach (var order in _tileOrders.Where(o => o.Status is ConstructionStatus.Planned or ConstructionStatus.WaitingForMaterials))
            foreach (var need in TileCost(order.Target))
                if (ResourceCount(need.Key) < need.Value) required.Add(need.Key);

        foreach (var item in _objects.Where(o => o.Status is ConstructionStatus.Planned or ConstructionStatus.WaitingForMaterials))
            foreach (var need in ObjectCost(item.Kind))
                if (ResourceCount(need.Key) < need.Value) required.Add(need.Key);

        var deposit = _deposits.Where(d => d.Amount > 0 && required.Contains(d.Resource))
            .OrderBy(d => Distance(worker.X, worker.Y, d.Cell.X + 0.5f, d.Cell.Y + 0.5f)).FirstOrDefault();
        if (deposit == null) return;
        if (!TryFindPath(worker, deposit.Cell, out var route)) return;
        worker.Task = new WorkerTask(ColonyTaskKind.Gather, deposit.Cell, route) { DepositId = deposit.Id };
        Message = $"{worker.Name} hauling is needed; gathering {deposit.Resource} from the nearby deposit.";
    }

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
        return true;
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
