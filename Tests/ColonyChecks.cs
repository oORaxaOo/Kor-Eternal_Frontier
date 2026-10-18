using KorTheBlight.Simulation;
using KorTheBlight.Rendering;

namespace KorTheBlight.Tests;

internal static class ColonyChecks
{
    public static void Run()
    {
        var center = OrthogonalGridProjection.TileCenter(5, 3);
        var world = OrthogonalGridProjection.ScreenToWorld(center.X, center.Y);
        Check(MathF.Abs(world.X - 5.5f) < 0.001f && MathF.Abs(world.Y - 3.5f) < 0.001f,
            "screen projection must round-trip tile centers");
        var east = OrthogonalGridProjection.TileCenter(6, 3);
        Check(MathF.Abs(east.X - center.X - OrthogonalGridProjection.TileSize) < 0.001f &&
              MathF.Abs(east.Y - center.Y) < 0.001f,
            "orthogonal neighbors must be separated by one square tile");

        var colony = new ColonySimulation();
        Check(colony.Workers.Count == 3, "a new colony should contain three independent colonists");
        Check(colony.ResourceCount(ResourceKind.Wood) == 20, "initial wood should be explicit inventory");
        Check(colony.InventoryCapacity == ColonySimulation.BaseStockpileCapacity,
            "the starter stockpile should provide the colony's initial storage capacity");
        Check(colony.InventoryUsed == 78 && colony.InventoryFreeCapacity == 42,
            "inventory used and free capacity should reflect the actual starting resource stacks");
        var woodDeposit = colony.Deposits.Single(d => d.Resource == ResourceKind.Wood);
        int woodBefore = colony.ResourceCount(ResourceKind.Wood);
        int depositBefore = woodDeposit.Amount;
        var worker = colony.Workers[0];
        Check(colony.SelectWorkerAt(worker.X, worker.Y), "colonists should be selectable in logical world coordinates");
        colony.CommandSelectedTo(woodDeposit.Cell.X + 0.5f, woodDeposit.Cell.Y + 0.5f);
        for (int i = 0; i < 300; i++) colony.Step(0.1f);
        Check(colony.ResourceCount(ResourceKind.Wood) == woodBefore + 5,
            "a gathered resource stack must be delivered to inventory rather than appear at the deposit");
        Check(woodDeposit.Amount == depositBefore - 5, "gathering must subtract the same quantity from the source");
        Check(!worker.CarriedResource.HasValue, "a successful round trip must leave no ghost cargo");

        Check(colony.DesignTileConstruction(16, 10, ArchitectureKind.Wall),
            "a wall should create an individual tile construction order");
        Check(colony.DesignTileConstruction(17, 10, ArchitectureKind.Floor),
            "a neighboring floor should create its own independent tile order");
        Check(!colony.DesignTileConstruction(16, 10, ArchitectureKind.Wall),
            "the same tile must not accept duplicate architecture orders");
        Check(colony.TileOrders.Select(o => o.Cell).Distinct().Count() == 2,
            "tile architecture orders must remain one-per-cell");

        Check(colony.PlaceObjectBlueprint(PlaceableKind.Workbench, 15, 8) == 0,
            "object placement must reject a footprint overlapping an existing building");
        int workbenchId = colony.PlaceObjectBlueprint(PlaceableKind.Workbench, 13, 12);
        Check(workbenchId > 0, "a valid multi-tile object should create a separate blueprint");
        var workbench = colony.Objects.Single(o => o.Id == workbenchId);
        Check(workbench.Width == 2 && workbench.Height == 2 && !workbench.IsBuilt,
            "a placeable object must keep its footprint and remain unfinished after placement");

        int rackId = colony.PlaceObjectBlueprint(PlaceableKind.StorageRack, 22, 15);
        Check(rackId > 0, "a storage rack should be placeable as an independent object blueprint");
        var storageRack = colony.Objects.Single(o => o.Id == rackId);
        Check(!storageRack.IsBuilt && colony.InventoryCapacity == ColonySimulation.BaseStockpileCapacity,
            "a planned storage rack must not provide capacity before it is constructed");

        for (int i = 0; i < 1000; i++) colony.Step(0.1f);
        Check(colony.TileAt(16, 10)?.Architecture == ArchitectureKind.Wall,
            "a worker must complete wall construction on the designated tile");
        Check(colony.TileAt(17, 10)?.Architecture == ArchitectureKind.Floor,
            "a worker must complete floor construction as its own tile job");
        Check(colony.TileOrders.All(o => o.Status == ConstructionStatus.Complete),
            "tile orders should expose their final construction status");
        Check(workbench.IsBuilt, "an object blueprint must require and complete worker labor");
        Check(storageRack.IsBuilt, "a storage rack must require and complete worker labor");
        Check(colony.InventoryCapacity == ColonySimulation.BaseStockpileCapacity + ColonySimulation.StorageRackCapacity,
            "a completed storage rack must increase the colony's inventory capacity");

        int componentBefore = colony.ResourceCount(ResourceKind.Components);
        int alloyBefore = colony.ResourceCount(ResourceKind.Alloy);
        int woodBeforeProduction = colony.ResourceCount(ResourceKind.Wood);
        Check(colony.QueueComponentProduction(workbenchId), "a completed workbench should accept a funded production order");
        Check(colony.ResourceCount(ResourceKind.Alloy) == alloyBefore - 2 &&
              colony.ResourceCount(ResourceKind.Wood) == woodBeforeProduction - 1,
            "queued production must commit its real input materials");
        for (int i = 0; i < 300; i++) colony.Step(0.1f);
        Check(colony.ResourceCount(ResourceKind.Components) == componentBefore + 1,
            "a worker must finish the queued recipe and place the output in inventory");
        Check(!colony.QueueComponentProduction(999999), "production at a missing workstation must be rejected");

        int woodBeforeRecipe = colony.ResourceCount(ResourceKind.Wood);
        Check(colony.QueueRecipeProduction(workbenchId, "recipe.sawmill.stage0"),
            "a catalogue recipe should be queueable when its real inputs and workstation are available");
        Check(colony.ResourceCount(ResourceKind.Wood) == woodBeforeRecipe - 2,
            "catalogue recipe inputs must be removed from the same live colony inventory");
        Check(!colony.QueueRecipeProduction(workbenchId, "recipe.metallurgy.stage0"),
            "a recipe requiring unavailable raw ore must be rejected instead of creating free materials");
        for (int i = 0; i < 1200; i++) colony.Step(0.1f);
        Check(colony.MaterialCount("material.sawmill.stage0") == 2,
            "worker labor must create the stage-specific stable-ID output from the selected recipe");
        Check(colony.ProductionOrders.Any(order => order.RecipeId == "recipe.sawmill.stage0" && order.Complete),
            "completed production orders must preserve their catalogue recipe identity");

        Check(colony.DesignTileConstruction(19, 14, ArchitectureKind.Path), "a new path order should be accepted before saving");
        colony.Step(0.1f);
        string json = ColonySaveService.Serialize(colony);
        var restored = ColonySaveService.Deserialize(json);
        Check(restored.Workers.Count == colony.Workers.Count, "save/load must preserve the colony population");
        Check(restored.ResourceCount(ResourceKind.Wood) == colony.ResourceCount(ResourceKind.Wood) &&
              restored.ResourceCount(ResourceKind.Components) == colony.ResourceCount(ResourceKind.Components),
            "save/load must preserve actual inventory balances");
        Check(restored.MaterialCount("material.sawmill.stage0") == 2 &&
              restored.ProductionOrders.Any(order => order.RecipeId == "recipe.sawmill.stage0" && order.Complete),
            "save/load must preserve stable-ID industry stock and queued recipe identities");
        Check(restored.TileOrders.Count == colony.TileOrders.Count &&
              restored.TileOrders.Select(o => (o.Cell, o.Target, o.Status, o.WorkDone))
                  .SequenceEqual(colony.TileOrders.Select(o => (o.Cell, o.Target, o.Status, o.WorkDone))),
            "save/load must preserve each tile construction state and work progress");
        Check(restored.Deposits.Select(d => (d.Id, d.Amount)).SequenceEqual(colony.Deposits.Select(d => (d.Id, d.Amount))),
            "save/load must preserve finite deposit quantities");
        Check(restored.Workers.Select(w => (w.Id, w.X, w.Y, w.Hunger, w.CarriedResource, w.CarriedAmount, w.Task?.Kind))
              .SequenceEqual(colony.Workers.Select(w => (w.Id, w.X, w.Y, w.Hunger, w.CarriedResource, w.CarriedAmount, w.Task?.Kind))),
            "save/load must preserve worker positions, needs, cargo, and task kinds");

        string directory = Path.Combine(Path.GetTempPath(), "kor-save-check-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "colony.json");
        try
        {
            ColonySaveService.Save(colony, path);
            var loadedFromDisk = ColonySaveService.Load(path);
            Check(loadedFromDisk.ResourceCount(ResourceKind.Food) == colony.ResourceCount(ResourceKind.Food),
                "disk save/load must preserve inventory state");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }

        var validationColony = new ColonySimulation();
        Check(validationColony.DesignTileConstruction(5, 5, ArchitectureKind.Wall),
            "validation fixture should contain a tile construction order");

        var danglingReference = validationColony.CaptureSnapshot();
        danglingReference.Tiles.Single(tile => tile.X == 5 && tile.Y == 5).ConstructionOrderId = 999999;
        bool danglingReferenceRejected = false;
        try { ColonySimulation.RestoreSnapshot(danglingReference); }
        catch (InvalidDataException) { danglingReferenceRejected = true; }
        Check(danglingReferenceRejected,
            "save validation must reject a tile that references a missing construction order");

        var detachedOrder = validationColony.CaptureSnapshot();
        detachedOrder.Tiles.Single(tile => tile.X == 5 && tile.Y == 5).ConstructionOrderId = null;
        bool detachedOrderRejected = false;
        try { ColonySimulation.RestoreSnapshot(detachedOrder); }
        catch (InvalidDataException) { detachedOrderRejected = true; }
        Check(detachedOrderRejected,
            "save validation must reject a live construction order detached from its tile");

        var completedWithoutTile = validationColony.CaptureSnapshot();
        completedWithoutTile.TileOrders.Single(order => order.X == 5 && order.Y == 5).Status = ConstructionStatus.Complete;
        bool inconsistentCompletionRejected = false;
        try { ColonySimulation.RestoreSnapshot(completedWithoutTile); }
        catch (InvalidDataException) { inconsistentCompletionRejected = true; }
        Check(inconsistentCompletionRejected,
            "save validation must reject a completed construction order whose tile has not been completed");

        bool versionRejected = false;
        try { ColonySaveService.Deserialize("{\"SchemaVersion\":99}"); }
        catch (InvalidDataException) { versionRejected = true; }
        Check(versionRejected, "unsupported or incomplete save data must be rejected rather than loaded");

        var fullSnapshot = new ColonySimulation().CaptureSnapshot();
        int fullCapacity = ColonySimulation.BaseStockpileCapacity;
        int startingTotal = fullSnapshot.Inventory.Sum(stack => stack.Amount);
        fullSnapshot.Inventory.Single(stack => stack.Resource == ResourceKind.Food).Amount += fullCapacity - startingTotal;
        var fullColony = ColonySimulation.RestoreSnapshot(fullSnapshot);
        Check(fullColony.InventoryUsed == fullColony.InventoryCapacity && fullColony.InventoryFreeCapacity == 0,
            "a colony save may contain inventory exactly up to its available storage capacity");
        var fullWoodDeposit = fullColony.Deposits.Single(deposit => deposit.Resource == ResourceKind.Wood);
        int fullDepositBefore = fullWoodDeposit.Amount;
        var fullWorker = fullColony.Workers[0];
        Check(fullColony.SelectWorkerAt(fullWorker.X, fullWorker.Y),
            "worker selection should work in a colony whose storage is full");
        fullColony.CommandSelectedTo(fullWoodDeposit.Cell.X + 0.5f, fullWoodDeposit.Cell.Y + 0.5f);
        Check(fullWorker.Task == null && fullWoodDeposit.Amount == fullDepositBefore,
            "gathering must not begin when storage has no free capacity");

        var partialDeliverySnapshot = new ColonySimulation().CaptureSnapshot();
        int targetInventory = ColonySimulation.BaseStockpileCapacity - 2;
        int partialStartingInventory = partialDeliverySnapshot.Inventory.Sum(stack => stack.Amount);
        partialDeliverySnapshot.Inventory.Single(stack => stack.Resource == ResourceKind.Food).Amount +=
            targetInventory - partialStartingInventory;
        var cargoWorker = partialDeliverySnapshot.Workers[0];
        cargoWorker.X = 18.5f;
        cargoWorker.Y = 7.5f;
        cargoWorker.CarriedResource = ResourceKind.Wood;
        cargoWorker.CarriedAmount = 5;
        cargoWorker.Task = new WorkerTaskSnapshot
        {
            Kind = ColonyTaskKind.Deliver,
            TargetX = 18,
            TargetY = 7,
            Route = [],
            RouteIndex = 0
        };
        var partialDeliveryColony = ColonySimulation.RestoreSnapshot(partialDeliverySnapshot);
        var restoredCarrier = partialDeliveryColony.Workers.Single(worker => worker.Id == cargoWorker.Id);
        partialDeliveryColony.Step(0.1f);
        Check(partialDeliveryColony.InventoryUsed == partialDeliveryColony.InventoryCapacity &&
              restoredCarrier.CarriedResource == ResourceKind.Wood && restoredCarrier.CarriedAmount == 3,
            "delivery must transfer only available storage space and retain remaining cargo");
        partialDeliveryColony.Step(0.1f);
        Check(restoredCarrier.CarriedAmount == 3 && restoredCarrier.Task == null,
            "a worker with undelivered cargo must wait rather than take unrelated work while storage remains full");

        var overCapacity = fullColony.CaptureSnapshot();
        overCapacity.Inventory.Single(stack => stack.Resource == ResourceKind.Food).Amount++;
        bool excessInventoryRejected = false;
        try { ColonySimulation.RestoreSnapshot(overCapacity); }
        catch (InvalidDataException) { excessInventoryRejected = true; }
        Check(excessInventoryRejected,
            "save validation must reject inventory quantities above completed storage capacity");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Colony check failed: {message}");
    }
}
