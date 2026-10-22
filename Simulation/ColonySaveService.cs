using System.Text.Json;
using System.Text.Json.Serialization;

namespace KorTheBlight.Simulation;

/// <summary>Versioned JSON persistence for a complete local colony simulation snapshot.</summary>
internal static class ColonySaveService
{
    private static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    public static string Serialize(ColonySimulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var snapshot = simulation.CaptureSnapshot();
        snapshot.Validate();
        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }

    public static ColonySimulation Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new InvalidDataException("Colony save is empty.");
        ColonySnapshot snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<ColonySnapshot>(json, JsonOptions)
                ?? throw new InvalidDataException("Colony save is empty.");
        }
        catch (JsonException error)
        {
            throw new InvalidDataException($"Colony save is not valid JSON: {error.Message}", error);
        }
        return ColonySimulation.RestoreSnapshot(snapshot);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = false };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

internal sealed class ColonySnapshot
{
    public int SchemaVersion { get; set; } = 9;
    public long SocialTick { get; set; }
    public SocialNetworkSnapshot? SocialNetwork { get; set; }
    public IncidentManagerSnapshot? IncidentManager { get; set; }
    public ColonyStorytellerSnapshot? Storyteller { get; set; }
    public WorkPriorityMatrixSnapshot? WorkPriorities { get; set; }
    public ColonyWealthSnapshot? Wealth { get; set; }
    public bool InventoryOwnedByWorld { get; set; }
    public int Width { get; set; } = ColonySimulation.Width;
    public int Height { get; set; } = ColonySimulation.Height;
    public float ElapsedSeconds { get; set; }
    public float EnvironmentAccumulatorSeconds { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? SelectedWorkerId { get; set; }
    public int NextOrderId { get; set; }
    public int NextExcavationId { get; set; } = 1;
    public int NextObjectId { get; set; }
    public int NextDepositId { get; set; }
    public int NextProductionId { get; set; }
    public List<TileSnapshot> Tiles { get; set; } = [];
    public List<TileOrderSnapshot> TileOrders { get; set; } = [];
    public List<ExcavationOrderSnapshot> ExcavationOrders { get; set; } = [];
    public List<PlaceableObjectSnapshot> Objects { get; set; } = [];
    public List<ResourceDepositSnapshot> Deposits { get; set; } = [];
    public List<ColonyWorkerSnapshot> Workers { get; set; } = [];
    public List<ResourceStackSnapshot> Inventory { get; set; } = [];
    public List<ProductionOrderSnapshot> ProductionOrders { get; set; } = [];

    public void Validate()
    {
        if (SchemaVersion < 2 || SchemaVersion > 9) throw new InvalidDataException($"Unsupported colony save schema version: {SchemaVersion}.");
        if (SocialTick < 0) throw new InvalidDataException("Colony save contains an invalid social simulation tick.");
        if (SchemaVersion >= 4 && (SocialNetwork is null || IncidentManager is null))
            throw new InvalidDataException("Current colony save is missing social or incident state.");
        if (SchemaVersion >= 7 && Storyteller is null)
            throw new InvalidDataException("Current colony save is missing storyteller state.");
        if (SchemaVersion >= 8 && WorkPriorities is null)
            throw new InvalidDataException("Current colony save is missing work-priority matrix state.");
        if (SchemaVersion >= 8 && Wealth is null)
            throw new InvalidDataException("Current colony save is missing wealth metrics.");
        if (Width != ColonySimulation.Width || Height != ColonySimulation.Height)
            throw new InvalidDataException($"Unsupported colony dimensions {Width}x{Height}.");
        if (!float.IsFinite(ElapsedSeconds) || ElapsedSeconds < 0)
            throw new InvalidDataException("Colony save contains an invalid simulation clock.");
        if (!float.IsFinite(EnvironmentAccumulatorSeconds) || EnvironmentAccumulatorSeconds < 0 || EnvironmentAccumulatorSeconds > 1_000_000f)
            throw new InvalidDataException("Colony save contains an invalid environment simulation accumulator.");
        if (Tiles is null || TileOrders is null || ExcavationOrders is null || Objects is null || Deposits is null ||
            Workers is null || Inventory is null || ProductionOrders is null)
            throw new InvalidDataException("Colony save is missing a required collection.");
        if (Tiles.Count != Width * Height)
            throw new InvalidDataException("Colony save has an incomplete tile grid.");

        var tileCoordinates = new HashSet<(int X, int Y)>();
        foreach (var tile in Tiles)
        {
            CheckCell(tile.X, tile.Y, "tile");
            if (!Enum.IsDefined(tile.Terrain) || !Enum.IsDefined(tile.Architecture))
                throw new InvalidDataException("Colony save contains an unknown terrain or architecture enum.");
            if (tile.WaterLevel is < 0 or > 7 || tile.MagmaLevel is < 0 or > 7 ||
                (tile.WaterLevel > 0 && tile.MagmaLevel > 0) ||
                !float.IsFinite(tile.TemperatureCelsius) || tile.TemperatureCelsius < -273.15f || tile.TemperatureCelsius > 5_000f)
                throw new InvalidDataException("Colony save contains invalid tile fluid levels or temperature.");
            if (!tileCoordinates.Add((tile.X, tile.Y)))
                throw new InvalidDataException("Colony save has duplicate tile coordinates.");
            if (tile.ConstructionOrderId is < 1)
                throw new InvalidDataException("Colony save has an invalid tile construction reference.");
            if (SchemaVersion >= 9 &&
                (!float.IsFinite(tile.Health) || tile.Health < 0f || tile.Health > 180f ||
                 (tile.Architecture == ArchitectureKind.None && tile.Health > 0f) ||
                 (tile.Architecture != ArchitectureKind.None &&
                    (tile.Health <= 0f || tile.Health > TileMaxHealth(tile.Architecture)))))
                throw new InvalidDataException("Colony save contains invalid structure integrity on a tile.");
        }

        var tileOrderIds = new HashSet<int>();
        var tileOrderCells = new HashSet<(int X, int Y)>();
        foreach (var order in TileOrders)
        {
            CheckId(order.Id, tileOrderIds, "tile construction order");
            CheckCell(order.X, order.Y, "tile construction order");
            if (!Enum.IsDefined(order.Target) || order.Target == ArchitectureKind.None || !Enum.IsDefined(order.Status))
                throw new InvalidDataException("Colony save contains an invalid tile construction state.");
            if (order.Priority is < 1 or > 5 || order.WorkRequired <= 0 || !float.IsFinite(order.WorkDone) || order.WorkDone < 0 ||
                (order.Status == ConstructionStatus.Complete && order.WorkDone < order.WorkRequired) ||
                (order.Status != ConstructionStatus.Complete && order.WorkDone >= order.WorkRequired))
                throw new InvalidDataException("Colony save contains invalid tile construction work values or progress.");
            if (!tileOrderCells.Add((order.X, order.Y)))
                throw new InvalidDataException("More than one construction order targets the same tile.");
        }

        var tilesByCell = Tiles.ToDictionary(tile => (tile.X, tile.Y));
        var tileOrdersById = TileOrders.ToDictionary(order => order.Id);
        foreach (var tile in Tiles)
        {
            if (!tile.ConstructionOrderId.HasValue) continue;
            if (!tileOrdersById.TryGetValue(tile.ConstructionOrderId.Value, out var linkedOrder))
                throw new InvalidDataException("Tile references a missing construction order.");
            if (linkedOrder.X != tile.X || linkedOrder.Y != tile.Y)
                throw new InvalidDataException("Tile construction reference points to an order for another tile.");
            if (linkedOrder.Status == ConstructionStatus.Complete || tile.Architecture != ArchitectureKind.None)
                throw new InvalidDataException("Tile has an active construction reference despite being completed.");
        }

        foreach (var order in TileOrders)
        {
            var tile = tilesByCell[(order.X, order.Y)];
            if (order.Status == ConstructionStatus.Complete)
            {
                if (tile.ConstructionOrderId.HasValue || tile.Architecture != order.Target ||
                    order.WorkDone < order.WorkRequired)
                    throw new InvalidDataException("Completed tile construction does not match the tile state or work progress.");
            }
            else if (tile.ConstructionOrderId != order.Id || tile.Architecture != ArchitectureKind.None ||
                     order.WorkDone >= order.WorkRequired)
            {
                throw new InvalidDataException("Unfinished tile construction is detached from its tile or has inconsistent progress.");
            }
        }

        var objectIds = new HashSet<int>();
        var occupied = new HashSet<(int X, int Y)>();
        foreach (var item in Objects)
        {
            CheckId(item.Id, objectIds, "placeable object");
            if (!Enum.IsDefined(item.Kind) || !Enum.IsDefined(item.Status) || item.Width <= 0 || item.Height <= 0 ||
                item.X < 0 || item.Y < 0 || item.X + item.Width > Width || item.Y + item.Height > Height)
                throw new InvalidDataException("Colony save contains an invalid placeable object footprint.");
            if (item.Priority is < 1 or > 5 || item.WorkRequired < 0 || !float.IsFinite(item.WorkDone) || item.WorkDone < 0 ||
                (item.Status != ConstructionStatus.Complete && item.WorkRequired == 0) ||
                (item.Status == ConstructionStatus.Complete && item.WorkDone < item.WorkRequired) ||
                (item.Status != ConstructionStatus.Complete && item.WorkDone >= item.WorkRequired))
                throw new InvalidDataException("Colony save contains invalid placeable object work values or progress.");
            if (SchemaVersion >= 9 &&
                (!float.IsFinite(item.Health) || item.Health < 0f || item.Health > ObjectMaxHealth(item.Kind) ||
                 (item.Status != ConstructionStatus.Complete && (item.Health != 0f || item.IsBlightInfected))))
                throw new InvalidDataException("Colony save contains invalid integrity or infection on a placeable object.");
            bool destroyedObject = SchemaVersion >= 9 &&
                item.Status == ConstructionStatus.Complete && item.Health <= 0f &&
                item.Kind is not (PlaceableKind.Habitat or PlaceableKind.Stockpile);
            if (destroyedObject) continue;
            for (int y = item.Y; y < item.Y + item.Height; y++)
            for (int x = item.X; x < item.X + item.Width; x++)
            {
                if (!occupied.Add((x, y)))
                    throw new InvalidDataException("Colony save contains overlapping placeable object footprints.");
                var tile = tilesByCell[(x, y)];
                if (tile.Terrain != TerrainKind.Ground || tile.Architecture == ArchitectureKind.Wall ||
                    tile.ConstructionOrderId.HasValue)
                    throw new InvalidDataException("Placeable object footprint conflicts with terrain, a wall, or tile construction.");
            }
        }

        var excavationIds = new HashSet<int>();
        var excavationCells = new HashSet<(int X, int Y)>();
        foreach (var order in ExcavationOrders)
        {
            CheckId(order.Id, excavationIds, "excavation order");
            CheckCell(order.X, order.Y, "excavation order");
            if (!Enum.IsDefined(order.Status) || order.Priority is < 1 or > 5 ||
                order.WorkRequired <= 0 || !float.IsFinite(order.WorkDone) || order.WorkDone < 0 ||
                (order.Status == ConstructionStatus.Complete && order.WorkDone < order.WorkRequired) ||
                (order.Status != ConstructionStatus.Complete && order.WorkDone >= order.WorkRequired))
                throw new InvalidDataException("Colony save contains invalid excavation order state or progress.");
            if (!excavationCells.Add((order.X, order.Y)))
                throw new InvalidDataException("More than one excavation order targets the same tile.");

            var tile = tilesByCell[(order.X, order.Y)];
            if (order.Status == ConstructionStatus.Complete)
            {
                if (tile.Terrain == TerrainKind.Rock)
                    throw new InvalidDataException("Completed excavation still contains solid rock.");
            }
            else if (tile.Terrain != TerrainKind.Rock || tile.Architecture != ArchitectureKind.None ||
                     tile.ConstructionOrderId.HasValue || occupied.Contains((order.X, order.Y)))
            {
                throw new InvalidDataException("Unfinished excavation is not on clear solid rock.");
            }
        }

        var depositIds = new HashSet<int>();
        foreach (var deposit in Deposits)
        {
            CheckId(deposit.Id, depositIds, "resource deposit");
            CheckCell(deposit.X, deposit.Y, "resource deposit");
            if (!Enum.IsDefined(deposit.Resource) || deposit.Amount < 0)
                throw new InvalidDataException("Colony save contains an invalid resource deposit.");
        }

        var workerIds = new HashSet<int>();
        foreach (var worker in Workers)
        {
            CheckId(worker.Id, workerIds, "colonist");
            if (string.IsNullOrWhiteSpace(worker.Name) ||
                !float.IsFinite(worker.X) || !float.IsFinite(worker.Y) ||
                worker.X < 0 || worker.Y < 0 || worker.X >= Width || worker.Y >= Height ||
                !float.IsFinite(worker.Hunger) || worker.Hunger is < 0 or > 100 ||
                !float.IsFinite(worker.CitizenFatigue) || worker.CitizenFatigue is < 0 or > 100 ||
                !float.IsFinite(worker.ThermalExposure) || worker.ThermalExposure is < 0 or > 100 ||
                !float.IsFinite(worker.Stress) || worker.Stress is < 0 or > 100 ||
                worker.CarriedAmount < 0)
                throw new InvalidDataException("Colony save contains an invalid colonist state.");
            if (worker.CarriedResource.HasValue != (worker.CarriedAmount > 0) ||
                (worker.CarriedResource.HasValue && !Enum.IsDefined(worker.CarriedResource.Value)))
                throw new InvalidDataException("Colony save contains an inconsistent colonist cargo state.");
            if (worker.Task is null) continue;
            if (!Enum.IsDefined(worker.Task.Kind) || !float.IsFinite(worker.Task.WorkDone) || worker.Task.WorkDone < 0 ||
                worker.Task.Route is null || worker.Task.RouteIndex < 0 || worker.Task.RouteIndex > worker.Task.Route.Count)
                throw new InvalidDataException("Colony save contains an invalid worker task.");
            CheckCell(worker.Task.TargetX, worker.Task.TargetY, "worker task target");
            foreach (var cell in worker.Task.Route) CheckCell(cell.X, cell.Y, "worker route");
            if (worker.Task.Kind == ColonyTaskKind.BuildTile && !tileOrderIds.Contains(worker.Task.TargetId))
                throw new InvalidDataException("Worker task references a missing tile construction order.");
            if (worker.Task.Kind == ColonyTaskKind.BuildObject && !objectIds.Contains(worker.Task.TargetId))
                throw new InvalidDataException("Worker task references a missing placeable object.");
            if (worker.Task.Kind == ColonyTaskKind.Mine && !excavationIds.Contains(worker.Task.TargetId))
                throw new InvalidDataException("Worker task references a missing excavation order.");
            if (worker.Task.Kind == ColonyTaskKind.Gather && !depositIds.Contains(worker.Task.DepositId))
                throw new InvalidDataException("Worker task references a missing resource deposit.");
        }

        var resourceKinds = Enum.GetValues<ResourceKind>().ToHashSet();
        if ((!InventoryOwnedByWorld && Inventory.Count != resourceKinds.Count) ||
            (InventoryOwnedByWorld && Inventory.Count != 0) ||
            Inventory.Any(stack => !resourceKinds.Contains(stack.Resource) || stack.Amount < 0) ||
            Inventory.Select(stack => stack.Resource).Distinct().Count() != Inventory.Count)
            throw new InvalidDataException("Colony inventory is incomplete, duplicated, or contains an invalid amount.");

        if (!InventoryOwnedByWorld)
        {
            int availableStorageCapacity =
                Objects.Where(item => item.Kind == PlaceableKind.Stockpile && item.Status == ConstructionStatus.Complete)
                    .Sum(_ => ColonySimulation.BaseStockpileCapacity) +
                Objects.Where(item => item.Kind == PlaceableKind.StorageRack && item.Status == ConstructionStatus.Complete)
                    .Sum(_ => ColonySimulation.StorageRackCapacity);
            if (Inventory.Sum(stack => stack.Amount) > availableStorageCapacity)
                throw new InvalidDataException("Colony save inventory exceeds available storage capacity.");
        }

        var productionIds = new HashSet<int>();
        foreach (var order in ProductionOrders)
        {
            CheckId(order.Id, productionIds, "production order");
            if (order.Priority is < 1 or > 5 || !objectIds.Contains(order.WorkstationId) || !float.IsFinite(order.WorkRequired) ||
                !float.IsFinite(order.WorkDone) || order.WorkRequired <= 0 || order.WorkDone < 0)
                throw new InvalidDataException("Colony save contains an invalid production order.");
        }
        foreach (var worker in Workers)
            if (worker.Task?.Kind == ColonyTaskKind.Produce && !productionIds.Contains(worker.Task.TargetId))
                throw new InvalidDataException("Worker task references a missing production order.");

        if (SchemaVersion >= 4)
        {
            var residents = workerIds.ToHashSet();
            _ = SocialRelationshipNetwork.Restore(SocialNetwork!, residents);
            _ = KorTheBlight.Simulation.IncidentManager.Restore(IncidentManager!, residents);
            if (SchemaVersion >= 7) _ = ColonyStoryteller.Restore(Storyteller!);
            if (SchemaVersion >= 8) _ = WorkPriorityMatrix.Restore(WorkPriorities, workerIds);
        }

        if (SelectedWorkerId.HasValue && !workerIds.Contains(SelectedWorkerId.Value))
            throw new InvalidDataException("Selected colonist is missing from the colony save.");
        if (NextOrderId <= tileOrderIds.DefaultIfEmpty(0).Max() ||
            NextExcavationId <= excavationIds.DefaultIfEmpty(0).Max() ||
            NextObjectId <= objectIds.DefaultIfEmpty(0).Max() ||
            NextDepositId <= depositIds.DefaultIfEmpty(0).Max() ||
            NextProductionId <= productionIds.DefaultIfEmpty(0).Max())
            throw new InvalidDataException("Colony save contains an invalid next-ID counter.");
    }

    private static float TileMaxHealth(ArchitectureKind kind) => kind switch
    {
        ArchitectureKind.Floor => 80f,
        ArchitectureKind.Path => 70f,
        ArchitectureKind.Wall => 180f,
        ArchitectureKind.Door => 100f,
        ArchitectureKind.Conduit => 65f,
        _ => 0f
    };

    private static float ObjectMaxHealth(PlaceableKind kind) => kind switch
    {
        PlaceableKind.Habitat => 500f,
        PlaceableKind.Stockpile => 450f,
        PlaceableKind.Workbench => 220f,
        PlaceableKind.SleepingPod => 130f,
        PlaceableKind.StorageRack => 150f,
        PlaceableKind.Generator => 280f,
        PlaceableKind.Garden => 100f,
        PlaceableKind.DiningTable => 110f,
        PlaceableKind.Lamp => 60f,
        PlaceableKind.ComfortBench => 100f,
        _ => 100f
    };

    private static void CheckCell(int x, int y, string label)
    {
        if (x < 0 || y < 0 || x >= ColonySimulation.Width || y >= ColonySimulation.Height)
            throw new InvalidDataException($"Colony save contains an out-of-bounds {label} coordinate.");
    }

    private static void CheckId(int id, HashSet<int> existing, string label)
    {
        if (id < 1 || !existing.Add(id))
            throw new InvalidDataException($"Colony save contains an invalid or duplicate {label} ID.");
    }
}

internal sealed class CellSnapshot { public int X { get; set; } public int Y { get; set; } }
internal sealed class TileSnapshot
{
    public int X { get; set; }
    public int Y { get; set; }
    public TerrainKind Terrain { get; set; }
    public ArchitectureKind Architecture { get; set; }
    public int? ConstructionOrderId { get; set; }
    public int WaterLevel { get; set; }
    public int MagmaLevel { get; set; }
    public float TemperatureCelsius { get; set; } = 20f;
    public float Health { get; set; }
    public bool IsBlightInfected { get; set; }
}
internal sealed class ExcavationOrderSnapshot
{
    public int Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public ConstructionStatus Status { get; set; }
    public float WorkDone { get; set; }
    public int WorkRequired { get; set; }
    public int Priority { get; set; } = 3;
}
internal sealed class TileOrderSnapshot
{
    public int Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public ArchitectureKind Target { get; set; }
    public ConstructionStatus Status { get; set; }
    public float WorkDone { get; set; }
    public int WorkRequired { get; set; }
    public int Priority { get; set; } = 3;
}
internal sealed class PlaceableObjectSnapshot
{
    public int Id { get; set; }
    public PlaceableKind Kind { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public ConstructionStatus Status { get; set; }
    public float WorkDone { get; set; }
    public int WorkRequired { get; set; }
    public int Priority { get; set; } = 3;
    public float Health { get; set; }
    public bool IsBlightInfected { get; set; }
}
internal sealed class ResourceDepositSnapshot
{
    public int Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public ResourceKind Resource { get; set; }
    public int Amount { get; set; }
}
internal sealed class ColonyWorkerSnapshot
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Faction { get; set; } = "Kor";
    public bool IsExternalVisitor { get; set; }
    public bool IsLeaving { get; set; }
    public Dictionary<ResourceKind, int> TradeCargo { get; set; } = [];
    public float X { get; set; }
    public float Y { get; set; }
    public float Hunger { get; set; }
    public float CitizenFatigue { get; set; } = 100f;
    public float ThermalExposure { get; set; }
    public float Stress { get; set; }
    public ResourceKind? CarriedResource { get; set; }
    public int CarriedAmount { get; set; }
    public WorkerTaskSnapshot? Task { get; set; }
}
internal sealed class WorkerTaskSnapshot
{
    public ColonyTaskKind Kind { get; set; }
    public int TargetX { get; set; }
    public int TargetY { get; set; }
    public List<CellSnapshot> Route { get; set; } = [];
    public int RouteIndex { get; set; }
    public int TargetId { get; set; }
    public float WorkDone { get; set; }
    public int DepositId { get; set; }
}
internal sealed class ResourceStackSnapshot
{
    public ResourceKind Resource { get; set; }
    public int Amount { get; set; }
}
internal sealed class ProductionOrderSnapshot
{
    public int Id { get; set; }
    public int WorkstationId { get; set; }
    public float WorkRequired { get; set; }
    public float WorkDone { get; set; }
    public bool Complete { get; set; }
    public bool Assigned { get; set; }
    public int Priority { get; set; } = 3;
}
