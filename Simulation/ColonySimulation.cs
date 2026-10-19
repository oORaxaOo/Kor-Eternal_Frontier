using KorTheBlight.Campaign;

namespace KorTheBlight.Simulation;

internal enum TerrainKind { Ground, Rock, Water }
internal enum ArchitectureKind { None, Floor, Wall, Door, Path, Conduit }
internal enum PlaceableKind { Habitat, Stockpile, Workbench, SleepingPod, StorageRack, Generator, Garden, IndustryBuilding }
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
    public string? CatalogBuildingId { get; internal set; }
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
internal sealed class MaterialDeposit(int id, CellPosition cell, string materialId, int amount)
{
    public int Id { get; } = id;
    public CellPosition Cell { get; } = cell;
    public string MaterialId { get; } = materialId;
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
    public string? CarriedMaterialId { get; internal set; }
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
    public int MaterialDepositId { get; set; }
}

internal sealed class ProductionOrder(int id, int workstationId, float workRequired, string? recipeId = null)
{
    public int Id { get; } = id;
    public int WorkstationId { get; } = workstationId;
    public float WorkRequired { get; } = workRequired;
    public string? RecipeId { get; } = recipeId;
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
    private readonly List<MaterialDeposit> _materialDeposits = [];
    private readonly List<ColonyWorker> _workers = [];
    private readonly List<ProductionOrder> _productionOrders = [];
    private readonly Dictionary<ResourceKind, int> _inventory = Enum.GetValues<ResourceKind>().ToDictionary(k => k, _ => 0);
    // Industry materials use catalogue-stable IDs; legacy five-resource APIs remain compatible.
    private readonly Dictionary<string, int> _industryInventory = new(StringComparer.Ordinal);
    private readonly HashSet<string> _researchedProjects = new(StringComparer.Ordinal);
    private int _nextOrderId = 1;
    private int _nextObjectId = 1;
    private int _nextDepositId = 1;
    private int _nextMaterialDepositId = 1;
    private int _nextProductionId = 1;

    public IReadOnlyList<ColonyWorker> Workers => _workers;
    public IReadOnlyList<TileConstructionOrder> TileOrders => _tileOrders;
    public IReadOnlyList<PlaceableObjectState> Objects => _objects;
    public IReadOnlyList<ResourceDeposit> Deposits => _deposits;
    public IReadOnlyList<MaterialDeposit> MaterialDeposits => _materialDeposits;
    public IReadOnlyList<ProductionOrder> ProductionOrders => _productionOrders;
    public IReadOnlyDictionary<string, int> IndustryInventory => _industryInventory;
    public string Message { get; private set; } = "Colony ready. Design architecture per tile or place an object blueprint.";
    public void SetMessage(string message) => Message = message;
    public int? SelectedWorkerId { get; private set; }
    public float ElapsedSeconds { get; private set; }
    public int CompletedTileOrders => _tileOrders.Count(x => x.Status == ConstructionStatus.Complete);
    public int CompletedObjects => _objects.Count(x => x.IsBuilt);
    public int PendingJobs => _tileOrders.Count(x => x.Status is ConstructionStatus.Planned or ConstructionStatus.WaitingForMaterials or ConstructionStatus.UnderConstruction)
        + _objects.Count(x => x.Status is ConstructionStatus.Planned or ConstructionStatus.WaitingForMaterials or ConstructionStatus.UnderConstruction)
        + _productionOrders.Count(x => !x.Complete);
    public int InventoryUsed => _inventory.Values.Sum() + _industryInventory.Values.Sum();
    public int InventoryCapacity =>
        _objects.Where(item => item.Kind == PlaceableKind.Stockpile && item.IsBuilt).Sum(_ => BaseStockpileCapacity) +
        _objects.Where(item => item.Kind == PlaceableKind.StorageRack && item.IsBuilt).Sum(_ => StorageRackCapacity) +
        _objects.Where(item => item.CatalogBuildingId != null && item.IsBuilt)
            .Sum(item => ProgressionCatalog.Buildings.FirstOrDefault(def => def.Id == item.CatalogBuildingId) is { Family: "storage" } def
                ? StorageRackCapacity * (def.Era + 1) : 0);
    public int InventoryFreeCapacity => Math.Max(0, InventoryCapacity - InventoryUsed);
    private int UnreservedInventoryFreeCapacity => Math.Max(0, InventoryFreeCapacity - PendingProductionOutputCapacity);

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
        _industryInventory["material.masonry.stage0"] = 8;

        AddBuiltObject(PlaceableKind.Habitat, new CellPosition(15, 8), 2, 2);
        AddBuiltObject(PlaceableKind.Stockpile, new CellPosition(18, 8), 3, 2);

        _workers.Add(new ColonyWorker(1, "Kor-01", 16.5f, 12.5f, 20f));
        _workers.Add(new ColonyWorker(2, "Kor-02", 17.5f, 12.5f, 30f));
        _workers.Add(new ColonyWorker(3, "Kor-03", 18.5f, 12.5f, 10f));

        AddDeposit(new CellPosition(10, 15), ResourceKind.Wood, 80);
        AddDeposit(new CellPosition(27, 12), ResourceKind.Stone, 90);
        AddDeposit(new CellPosition(14, 21), ResourceKind.Food, 100);
        AddDeposit(new CellPosition(34, 20), ResourceKind.Alloy, 45);
        AddMaterialDeposit(new CellPosition(25, 9), "ore", 100);
        AddMaterialDeposit(new CellPosition(32, 14), "rare_earth", 45);
        AddMaterialDeposit(new CellPosition(26, 8), "crystal", 35);
        AddMaterialDeposit(new CellPosition(30, 17), "reagents", 70);
        AddMaterialDeposit(new CellPosition(12, 20), "water", 100);
        AddMaterialDeposit(new CellPosition(11, 16), "biomass", 80);
        AddMaterialDeposit(new CellPosition(20, 18), "recycled_feedstock", 60);
    }

    internal ColonySnapshot CaptureSnapshot()
    {
        var snapshot = new ColonySnapshot
        {
            SchemaVersion = 5,
            ResearchedProjects = _researchedProjects.OrderBy(id => id, StringComparer.Ordinal).ToList(),
            ElapsedSeconds = ElapsedSeconds,
            Message = Message,
            SelectedWorkerId = SelectedWorkerId,
            NextOrderId = _nextOrderId,
            NextObjectId = _nextObjectId,
            NextDepositId = _nextDepositId,
            NextMaterialDepositId = _nextMaterialDepositId,
            NextProductionId = _nextProductionId,
            Tiles = [],
            TileOrders = [],
            Objects = [],
            Deposits = [],
            MaterialDeposits = [],
            Workers = [],
            Inventory = [],
            Materials = [],
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
            WorkDone = item.WorkDone, WorkRequired = item.WorkRequired, CatalogBuildingId = item.CatalogBuildingId
        }));
        snapshot.Deposits.AddRange(_deposits.Select(deposit => new ResourceDepositSnapshot
        {
            Id = deposit.Id, X = deposit.Cell.X, Y = deposit.Cell.Y,
            Resource = deposit.Resource, Amount = deposit.Amount
        }));
        snapshot.MaterialDeposits.AddRange(_materialDeposits.Select(deposit => new MaterialDepositSnapshot
        {
            Id = deposit.Id, X = deposit.Cell.X, Y = deposit.Cell.Y,
            MaterialId = deposit.MaterialId, Amount = deposit.Amount
        }));
        snapshot.Workers.AddRange(_workers.Select(worker => new ColonyWorkerSnapshot
        {
            Id = worker.Id, Name = worker.Name, X = worker.X, Y = worker.Y, Hunger = worker.Hunger,
            CarriedResource = worker.CarriedResource, CarriedMaterialId = worker.CarriedMaterialId,
            CarriedAmount = worker.CarriedAmount,
            Task = worker.Task == null ? null : new WorkerTaskSnapshot
            {
                Kind = worker.Task.Kind, TargetX = worker.Task.Target.X, TargetY = worker.Task.Target.Y,
                Route = worker.Task.Route.Select(cell => new CellSnapshot { X = cell.X, Y = cell.Y }).ToList(),
                RouteIndex = worker.Task.RouteIndex, TargetId = worker.Task.TargetId,
                WorkDone = worker.Task.WorkDone, DepositId = worker.Task.DepositId,
                MaterialDepositId = worker.Task.MaterialDepositId
            }
        }));
        snapshot.Inventory.AddRange(_inventory.Select(item => new ResourceStackSnapshot
        {
            Resource = item.Key, Amount = item.Value
        }));
        snapshot.Materials.AddRange(_industryInventory.Select(item => new MaterialStackSnapshot
        {
            MaterialId = item.Key, Amount = item.Value
        }));
        snapshot.ProductionOrders.AddRange(_productionOrders.Select(order => new ProductionOrderSnapshot
        {
            Id = order.Id, WorkstationId = order.WorkstationId, RecipeId = order.RecipeId,
            WorkRequired = order.WorkRequired, WorkDone = order.WorkDone,
            Complete = order.Complete, Assigned = order.Assigned
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
            item.WorkRequired, item.Status) { WorkDone = item.WorkDone, CatalogBuildingId = item.CatalogBuildingId }));

        simulation._deposits.Clear();
        simulation._deposits.AddRange(snapshot.Deposits.Select(deposit => new ResourceDeposit(
            deposit.Id, new CellPosition(deposit.X, deposit.Y), deposit.Resource, deposit.Amount)));
        simulation._materialDeposits.Clear();
        simulation._materialDeposits.AddRange(snapshot.MaterialDeposits.Select(deposit => new MaterialDeposit(
            deposit.Id, new CellPosition(deposit.X, deposit.Y), deposit.MaterialId, deposit.Amount)));

        simulation._workers.Clear();
        simulation._workers.AddRange(snapshot.Workers.Select(worker =>
        {
            var restored = new ColonyWorker(worker.Id, worker.Name, worker.X, worker.Y, worker.Hunger)
            {
                CarriedResource = worker.CarriedResource,
                CarriedMaterialId = worker.CarriedMaterialId,
                CarriedAmount = worker.CarriedAmount,
                Task = worker.Task == null ? null : new WorkerTask(worker.Task.Kind,
                    new CellPosition(worker.Task.TargetX, worker.Task.TargetY),
                    worker.Task.Route.Select(cell => new CellPosition(cell.X, cell.Y)).ToList())
                {
                    RouteIndex = worker.Task.RouteIndex,
                    TargetId = worker.Task.TargetId,
                    WorkDone = worker.Task.WorkDone,
                    DepositId = worker.Task.DepositId,
                    MaterialDepositId = worker.Task.MaterialDepositId
                }
            };
            return restored;
        }));

        simulation._inventory.Clear();
        foreach (var item in snapshot.Inventory) simulation._inventory.Add(item.Resource, item.Amount);
        simulation._industryInventory.Clear();
        foreach (var item in snapshot.Materials) simulation._industryInventory.Add(item.MaterialId, item.Amount);
        simulation._researchedProjects.Clear();
        foreach (var projectId in snapshot.ResearchedProjects) simulation._researchedProjects.Add(projectId);
        simulation._productionOrders.Clear();
        simulation._productionOrders.AddRange(snapshot.ProductionOrders.Select(order => new ProductionOrder(
            order.Id, order.WorkstationId, order.WorkRequired, order.RecipeId)
        {
            WorkDone = order.WorkDone, Complete = order.Complete, Assigned = order.Assigned
        }));

        simulation._nextOrderId = snapshot.NextOrderId;
        simulation._nextObjectId = snapshot.NextObjectId;
        simulation._nextDepositId = snapshot.NextDepositId;
        simulation._nextMaterialDepositId = snapshot.NextMaterialDepositId;
        simulation._nextProductionId = snapshot.NextProductionId;
        simulation.ElapsedSeconds = snapshot.ElapsedSeconds;
        simulation.Message = snapshot.Message;
        simulation.SelectedWorkerId = snapshot.SelectedWorkerId;
        return simulation;
    }

    public int ResourceCount(ResourceKind resource) => _inventory.TryGetValue(resource, out int amount) ? amount : 0;

    public int MaterialCount(string materialId)
    {
        if (TryMapLegacyMaterial(materialId, out var legacy)) return ResourceCount(legacy);
        return _industryInventory.TryGetValue(materialId, out int amount) ? amount : 0;
    }

    public bool IsResearchComplete(string researchId) => _researchedProjects.Contains(researchId);

    public bool CanResearchProject(string researchId, CampaignSimulation campaign, out string reason)
    {
        var project = ProgressionCatalog.Research.FirstOrDefault(item => item.Id == researchId);
        if (project == null)
        {
            reason = "Unknown research project.";
            return false;
        }
        if (_researchedProjects.Contains(project.Id))
        {
            reason = "Research already completed.";
            return false;
        }
        var missing = project.Prerequisites.Where(id => !_researchedProjects.Contains(id)).ToArray();
        if (missing.Length > 0)
        {
            var names = missing.Select(id => ProgressionCatalog.Research.FirstOrDefault(item => item.Id == id)?.Name ?? id);
            reason = "Requires: " + string.Join(", ", names) + ".";
            return false;
        }
        if (campaign.State.Knowledge < project.CostKnowledge || campaign.State.Energy < project.CostEnergy)
        {
            reason = $"Needs {project.CostKnowledge} Knowledge and {project.CostEnergy} Energy.";
            return false;
        }
        reason = "Ready to research.";
        return true;
    }

    public bool ResearchProject(string researchId, CampaignSimulation campaign)
    {
        if (!CanResearchProject(researchId, campaign, out string reason))
        {
            Message = reason;
            return false;
        }
        var project = ProgressionCatalog.Research.First(item => item.Id == researchId);
        campaign.State.Knowledge -= project.CostKnowledge;
        campaign.State.Energy -= project.CostEnergy;
        _researchedProjects.Add(project.Id);
        Message = $"Research completed: {project.Name}. Unlocks now apply to colony production.";
        campaign.State.LastEvent = Message;
        return true;
    }

    internal IReadOnlyList<ProgressionCatalog.ResearchDef> ManagementResearch(int page)
    {
        const int pageSize = 8;
        return ProgressionCatalog.Research.Where(project => !_researchedProjects.Contains(project.Id))
            .OrderBy(project => project.Era)
            .ThenBy(project => project.Id.StartsWith("research.industry.machine_tools.era", StringComparison.Ordinal) ? 0 :
                project.Id.StartsWith("research.logistics.field_operations.era", StringComparison.Ordinal) ? 1 : 2)
            .ThenBy(project => project.Discipline, StringComparer.Ordinal)
            .ThenBy(project => project.Name, StringComparer.Ordinal)
            .Skip(Math.Max(0, page) * pageSize).Take(pageSize).ToArray();
    }

    internal int ManagementResearchPageCount => Math.Max(1, (ProgressionCatalog.Research.Count - _researchedProjects.Count + 7) / 8);

    public bool CanQueueRecipe(string recipeId, out string reason)
    {
        var recipe = ProgressionCatalog.Recipes.FirstOrDefault(item => item.Id == recipeId);
        if (recipe == null)
        {
            reason = "Unknown production recipe.";
            return false;
        }
        if (!_researchedProjects.Contains(recipe.UnlockResearchId))
        {
            string projectName = ProgressionCatalog.Research.FirstOrDefault(item => item.Id == recipe.UnlockResearchId)?.Name ?? recipe.UnlockResearchId;
            reason = $"Locked by research: {projectName}.";
            return false;
        }

        foreach (var input in recipe.Inputs)
        {
            if (input.Value <= 0)
            {
                reason = "Recipe has an invalid input quantity.";
                return false;
            }
            int available = MaterialCount(input.Key);
            if (available < input.Value)
            {
                string name = ProgressionCatalog.Resources.FirstOrDefault(r => r.Id == input.Key)?.Name ?? input.Key;
                reason = $"Needs {input.Value} {name}; stock has {available}.";
                return false;
            }
        }

        int inputTotal = recipe.Inputs.Values.Sum();
        int outputTotal = recipe.Outputs.Values.Sum();
        int pendingReserved = PendingProductionOutputCapacity;
        if (InventoryFreeCapacity + inputTotal - pendingReserved < outputTotal)
        {
            reason = "Insufficient free storage for this recipe output.";
            return false;
        }

        reason = "Ready to queue.";
        return true;
    }

    internal IReadOnlyList<ProgressionCatalog.RecipeDef> ManagementRecipes
    {
        get
        {
            var recipes = ProgressionCatalog.Recipes;
            var readyFollowUps = recipes
                .Where(recipe => recipe.Stage > 0 && recipe.Inputs.All(input => MaterialCount(input.Key) >= input.Value))
                .OrderBy(recipe => recipe.Stage)
                .ThenBy(recipe => recipe.Industry, StringComparer.Ordinal)
                .Take(4);
            var readyStarters = recipes
                .Where(recipe => recipe.Stage == 0 && recipe.Inputs.All(input => MaterialCount(input.Key) >= input.Value))
                .Take(4);
            var unavailableStarters = recipes
                .Where(recipe => recipe.Stage == 0 && recipe.Inputs.Any(input => MaterialCount(input.Key) < input.Value))
                .Take(2);
            return readyFollowUps.Concat(readyStarters).Concat(unavailableStarters)
                .DistinctBy(recipe => recipe.Id).Take(8).ToArray();
        }
    }

    /// <summary>Accepts external deliveries without bypassing the colony's real storage limit.</summary>
    public int AddExternalResource(ResourceKind resource, int requested)
    {
        if (requested <= 0 || !Enum.IsDefined(resource)) return 0;
        int accepted = Math.Min(requested, UnreservedInventoryFreeCapacity);
        if (accepted <= 0) return 0;
        AddResource(resource, accepted);
        Message = $"External logistics delivered {accepted} {resource}.";
        return accepted;
    }

    /// <summary>Withdraws only resources actually present in colony inventory.</summary>
    public int RemoveExternalResource(ResourceKind resource, int requested)
    {
        if (requested <= 0 || !Enum.IsDefined(resource)) return 0;
        int removed = Math.Min(requested, ResourceCount(resource));
        if (removed <= 0) return 0;
        if (!TryTake(resource, removed)) return 0;
        Message = $"External logistics collected {removed} {resource}.";
        return removed;
    }

    public TileCell? TileAt(int x, int y) => InBounds(x, y) ? _tiles[x, y] : null;

    public ResourceDeposit? DepositAt(int x, int y) =>
        _deposits.FirstOrDefault(d => d.Cell.X == x && d.Cell.Y == y && d.Amount > 0);
    public MaterialDeposit? MaterialDepositAt(int x, int y) =>
        _materialDeposits.FirstOrDefault(d => d.Cell.X == x && d.Cell.Y == y && d.Amount > 0);

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

    public bool CanPlaceCatalogBuilding(string buildingId, out string reason)
    {
        var building = ProgressionCatalog.Buildings.FirstOrDefault(item => item.Id == buildingId);
        if (building == null)
        {
            reason = "Unknown catalogue building.";
            return false;
        }
        if (!_researchedProjects.Contains(building.UnlockResearchId))
        {
            string projectName = ProgressionCatalog.Research.FirstOrDefault(item => item.Id == building.UnlockResearchId)?.Name ?? building.UnlockResearchId;
            reason = $"Locked by research: {projectName}.";
            return false;
        }
        reason = "Ready to place.";
        return true;
    }

    internal IReadOnlyList<ProgressionCatalog.BuildingDef> ManagementBuildings(int page)
    {
        const int pageSize = 5;
        return ProgressionCatalog.Buildings.OrderBy(item => item.Era)
            .ThenBy(item => _researchedProjects.Contains(item.UnlockResearchId) ? 0 : 1)
            .ThenBy(item => item.Family, StringComparer.Ordinal)
            .Skip(Math.Max(0, page) * pageSize).Take(pageSize).ToArray();
    }

    internal int ManagementBuildingPageCount => (ProgressionCatalog.Buildings.Count + 4) / 5;

    public int PlaceCatalogBuildingBlueprint(string buildingId, int x, int y)
    {
        if (!CanPlaceCatalogBuilding(buildingId, out string reason))
        {
            Message = reason;
            return 0;
        }
        var building = ProgressionCatalog.Buildings.First(item => item.Id == buildingId);
        int width = Math.Max(1, building.Footprint / 2);
        int height = 2;
        var origin = new CellPosition(x, y);
        for (int ty = y; ty < y + height; ty++)
        for (int tx = x; tx < x + width; tx++)
        {
            if (!InBounds(tx, ty) || _tiles[tx, ty].Terrain != TerrainKind.Ground ||
                _tiles[tx, ty].Architecture == ArchitectureKind.Wall ||
                _tiles[tx, ty].ConstructionOrderId.HasValue || ObjectAt(tx, ty) != null)
            {
                Message = "The full catalogue-building footprint must be clear ground.";
                return 0;
            }
        }
        var item = new PlaceableObjectState(_nextObjectId++, PlaceableKind.IndustryBuilding, origin,
            width, height, building.Work, ConstructionStatus.Planned) { CatalogBuildingId = building.Id };
        _objects.Add(item);
        Message = $"{building.Name} blueprint placed at ({x}, {y}); materials and worker time are still required.";
        return item.Id;
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
        var inputs = new Dictionary<ResourceKind, int> { [ResourceKind.Alloy] = 2, [ResourceKind.Wood] = 1 };
        int inputTotal = inputs.Values.Sum();
        if (InventoryFreeCapacity + inputTotal - PendingProductionOutputCapacity < 1)
        {
            Message = "Component production has no reserved room for its finished output.";
            return false;
        }
        if (!TryConsume(inputs))
        {
            Message = "Component production needs 2 alloy and 1 wood in the inventory.";
            return false;
        }

        _productionOrders.Add(new ProductionOrder(_nextProductionId++, workstationId, 6f));
        Message = "Component production queued; 2 alloy and 1 wood are committed to the order.";
        return true;
    }

    public bool QueueRecipeProduction(int workstationId, string recipeId)
    {
        var station = _objects.FirstOrDefault(o => o.Id == workstationId && o.Kind == PlaceableKind.Workbench && o.IsBuilt);
        if (station == null)
        {
            Message = "Catalog recipes require a completed workbench.";
            return false;
        }
        var recipe = ProgressionCatalog.Recipes.FirstOrDefault(item => item.Id == recipeId);
        if (recipe == null)
        {
            Message = "That production recipe does not exist in the catalogue.";
            return false;
        }
        if (!CanQueueRecipe(recipeId, out string reason))
        {
            Message = reason;
            return false;
        }
        if (!TryConsumeMaterials(recipe.Inputs))
        {
            Message = "Production inputs changed before the order could reserve them.";
            return false;
        }

        _productionOrders.Add(new ProductionOrder(_nextProductionId++, workstationId, recipe.Work, recipe.Id));
        Message = $"{recipe.Name} queued at Workbench #{workstationId}; inputs are reserved.";
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
        if (worker.CarriedResource.HasValue || !string.IsNullOrWhiteSpace(worker.CarriedMaterialId))
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

        var deposit = DepositAt(x, y);
        if (deposit != null)
        {
            if (UnreservedInventoryFreeCapacity <= 0)
            {
                Message = "Colony storage is full or reserved for active production. Build storage or let production finish before gathering more resources.";
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

        var materialDeposit = MaterialDepositAt(x, y);
        if (materialDeposit != null)
        {
            if (UnreservedInventoryFreeCapacity <= 0)
            {
                Message = "Colony storage is full or reserved for active production.";
                return;
            }
            if (!TryFindPath(worker, materialDeposit.Cell, out var materialRoute))
            {
                Message = "No route to that raw-material deposit.";
                return;
            }
            worker.Task = new WorkerTask(ColonyTaskKind.Gather, materialDeposit.Cell, materialRoute)
                { MaterialDepositId = materialDeposit.Id };
            string materialName = ProgressionCatalog.Resources.FirstOrDefault(item => item.Id == materialDeposit.MaterialId)?.Name ?? materialDeposit.MaterialId;
            Message = $"{worker.Name} ordered to extract {materialName}. The material will be carried back to stockpile.";
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
        UpdateNeeds(deltaSeconds);
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

            if (worker.CarriedResource.HasValue || !string.IsNullOrWhiteSpace(worker.CarriedMaterialId))
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
            if (item.CatalogBuildingId == null && !TryGetFootprint(item.Kind, out _, out _, out _)) continue;
            var workCell = FindWorkCell(worker, item.Origin, item.Width, item.Height);
            if (!workCell.HasValue) { item.Status = ConstructionStatus.Blocked; continue; }
            var cost = ObjectMaterialCost(item);
            if (!TryConsumeMaterials(cost))
            {
                item.Status = ConstructionStatus.WaitingForMaterials;
                continue;
            }
            if (!TryFindPath(worker, workCell.Value, out var route))
            {
                AddMaterials(cost);
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
        if (UnreservedInventoryFreeCapacity <= 0) return;
        var required = new HashSet<ResourceKind>();
        foreach (var order in _tileOrders.Where(o => o.Status is ConstructionStatus.Planned or ConstructionStatus.WaitingForMaterials))
            foreach (var need in TileCost(order.Target))
                if (ResourceCount(need.Key) < need.Value) required.Add(need.Key);

        foreach (var item in _objects.Where(o => o.Status is ConstructionStatus.Planned or ConstructionStatus.WaitingForMaterials))
            foreach (var need in ObjectMaterialCost(item))
                if (MaterialCount(need.Key) < need.Value && TryMapLegacyMaterial(need.Key, out var legacy)) required.Add(legacy);

        var deposit = _deposits.Where(d => d.Amount > 0 && required.Contains(d.Resource))
            .OrderBy(d => Distance(worker.X, worker.Y, d.Cell.X + 0.5f, d.Cell.Y + 0.5f)).FirstOrDefault();
        if (deposit == null) return;
        if (!TryFindPath(worker, deposit.Cell, out var route)) return;
        worker.Task = new WorkerTask(ColonyTaskKind.Gather, deposit.Cell, route) { DepositId = deposit.Id };
        Message = $"{worker.Name} hauling is needed; gathering {deposit.Resource} from the nearby deposit.";
    }

    private void AssignDelivery(ColonyWorker worker)
    {
        if ((!worker.CarriedResource.HasValue && string.IsNullOrWhiteSpace(worker.CarriedMaterialId)) ||
            worker.CarriedAmount <= 0 || UnreservedInventoryFreeCapacity <= 0) return;
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
        if (task.MaterialDepositId > 0)
        {
            var materialDeposit = _materialDeposits.FirstOrDefault(d => d.Id == task.MaterialDepositId);
            if (materialDeposit == null || materialDeposit.Amount <= 0)
            {
                worker.Task = null;
                Message = "The raw-material deposit is exhausted.";
                return;
            }
            int materialAmount = Math.Min(CarryCapacity, materialDeposit.Amount);
            materialDeposit.Amount -= materialAmount;
            worker.CarriedResource = null;
            worker.CarriedMaterialId = materialDeposit.MaterialId;
            worker.CarriedAmount = materialAmount;
            worker.Task = null;
            AssignDelivery(worker);
            Message = $"{worker.Name} extracted {materialAmount} {materialDeposit.MaterialId}; returning it to stockpile.";
            return;
        }

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
        worker.CarriedMaterialId = null;
        worker.CarriedAmount = amount;
        worker.Task = null;
        AssignDelivery(worker);
        Message = $"{worker.Name} collected {amount} {deposit.Resource}; returning it to stockpile.";
    }

    private void CompleteDelivery(ColonyWorker worker)
    {
        if ((!worker.CarriedResource.HasValue && string.IsNullOrWhiteSpace(worker.CarriedMaterialId)) ||
            worker.CarriedAmount <= 0)
        {
            worker.Task = null;
            return;
        }

        int delivered = Math.Min(worker.CarriedAmount, UnreservedInventoryFreeCapacity);
        if (delivered > 0)
        {
            if (worker.CarriedResource.HasValue) AddResource(worker.CarriedResource.Value, delivered);
            else AddMaterial(worker.CarriedMaterialId!, delivered);
            worker.CarriedAmount -= delivered;
        }

        string deliveredName = worker.CarriedResource?.ToString() ??
            (ProgressionCatalog.Resources.FirstOrDefault(item => item.Id == worker.CarriedMaterialId)?.Name ?? worker.CarriedMaterialId ?? "raw materials");
        if (worker.CarriedAmount == 0)
        {
            worker.CarriedResource = null;
            worker.CarriedMaterialId = null;
            Message = $"{worker.Name} delivered {delivered} {deliveredName}.";
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
        string completedName = item.CatalogBuildingId == null ? item.Kind.ToString() :
            ProgressionCatalog.Buildings.FirstOrDefault(def => def.Id == item.CatalogBuildingId)?.Name ?? item.CatalogBuildingId;
        Message = $"{completedName} completed and is now operational.";
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
        int outputCapacity = order.RecipeId == null ? 1 :
            ProgressionCatalog.Recipes.FirstOrDefault(recipe => recipe.Id == order.RecipeId)?.Outputs.Values.Sum() ?? 0;
        if (order.WorkDone + deltaSeconds * workRate >= order.WorkRequired && InventoryFreeCapacity < outputCapacity)
        {
            Message = "Production is waiting for storage space before it can deliver the finished goods.";
            return;
        }
        order.WorkDone = Math.Min(order.WorkRequired, order.WorkDone + deltaSeconds * workRate);
        if (order.WorkDone < order.WorkRequired) return;
        order.Complete = true;
        order.Assigned = false;
        if (order.RecipeId == null)
            AddResource(ResourceKind.Components, 1);
        else
        {
            var recipe = ProgressionCatalog.Recipes.First(item => item.Id == order.RecipeId);
            foreach (var output in recipe.Outputs) AddMaterial(output.Key, output.Value);
        }
        worker.Task = null;
        Message = order.RecipeId == null
            ? "Workbench production completed: 1 component produced from committed materials."
            : $"Production completed: {ProgressionCatalog.Recipes.First(item => item.Id == order.RecipeId).Name}.";
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
        foreach (var (resource, amount) in costs) _inventory[resource] -= amount;
        return true;
    }

    private bool TryConsumeMaterials(IReadOnlyDictionary<string, int> costs)
    {
        if (costs.Any(item => item.Value <= 0 || MaterialCount(item.Key) < item.Value)) return false;
        foreach (var (materialId, amount) in costs)
        {
            if (TryMapLegacyMaterial(materialId, out var legacy))
                _inventory[legacy] -= amount;
            else
            {
                int remaining = MaterialCount(materialId) - amount;
                if (remaining == 0) _industryInventory.Remove(materialId);
                else _industryInventory[materialId] = remaining;
            }
        }
        return true;
    }

    private void AddMaterial(string materialId, int amount)
    {
        if (amount <= 0) return;
        if (!ProgressionCatalog.Resources.Any(item => item.Id == materialId))
            throw new InvalidOperationException($"Unknown catalogue material ID: {materialId}");
        if (TryMapLegacyMaterial(materialId, out var legacy))
            AddResource(legacy, amount);
        else
            _industryInventory[materialId] = checked(MaterialCount(materialId) + amount);
    }

    private int PendingProductionOutputCapacity => _productionOrders
        .Where(order => !order.Complete)
        .Sum(order => order.RecipeId == null ? 1 :
            ProgressionCatalog.Recipes.FirstOrDefault(recipe => recipe.Id == order.RecipeId)?.Outputs.Values.Sum() ?? 0);

    internal static bool IsLegacyMaterialId(string materialId) => TryMapLegacyMaterial(materialId, out _);

    private static bool TryMapLegacyMaterial(string materialId, out ResourceKind resource)
    {
        resource = materialId switch
        {
            "wood" => ResourceKind.Wood,
            "stone" => ResourceKind.Stone,
            "food" => ResourceKind.Food,
            "alloy" => ResourceKind.Alloy,
            "components" => ResourceKind.Components,
            _ => (ResourceKind)(-1)
        };
        return Enum.IsDefined(resource);
    }

    private bool TryTake(ResourceKind resource, int amount)
    {
        if (amount < 0 || ResourceCount(resource) < amount) return false;
        _inventory[resource] -= amount;
        return true;
    }

    private void AddResource(ResourceKind resource, int amount)
    {
        if (amount > 0) _inventory[resource] = checked(ResourceCount(resource) + amount);
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

    private void AddMaterialDeposit(CellPosition cell, string materialId, int amount)
    {
        if (!ProgressionCatalog.Resources.Any(item => item.Id == materialId) || IsLegacyMaterialId(materialId))
            throw new InvalidOperationException($"Unknown raw-material deposit ID: {materialId}");
        _tiles[cell.X, cell.Y].Terrain = TerrainKind.Ground;
        _materialDeposits.Add(new MaterialDeposit(_nextMaterialDepositId++, cell, materialId, amount));
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

    private Dictionary<string, int> ObjectMaterialCost(PlaceableObjectState item)
    {
        if (item.CatalogBuildingId != null)
            return ProgressionCatalog.Buildings.First(building => building.Id == item.CatalogBuildingId).Inputs
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        return ObjectCost(item.Kind).ToDictionary(pair => pair.Key switch
        {
            ResourceKind.Wood => "wood", ResourceKind.Stone => "stone", ResourceKind.Food => "food",
            ResourceKind.Alloy => "alloy", ResourceKind.Components => "components", _ => pair.Key.ToString().ToLowerInvariant()
        }, pair => pair.Value, StringComparer.Ordinal);
    }

    private void AddMaterials(IReadOnlyDictionary<string, int> materials)
    {
        foreach (var (materialId, amount) in materials) AddMaterial(materialId, amount);
    }

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
