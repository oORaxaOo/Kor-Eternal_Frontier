using System.Text.Json;
using KorTheBlight.Simulation;
using KorTheBlight.Campaign;
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
        Check(colony.InventoryUsed == 86 && colony.InventoryFreeCapacity == 34,
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

        var researchCampaign = new CampaignSimulation(WorldGenerationService.Generate(31415));
        researchCampaign.State.Knowledge = 500;
        researchCampaign.State.Energy = 500;
        const string machineToolsEra0 = "research.industry.machine_tools.era0";
        const string machineToolsEra1 = "research.industry.machine_tools.era1";
        const string logisticsEra0 = "research.logistics.field_operations.era0";
        Check(!colony.CanPlaceCatalogBuilding("building.workshop.tier0", out string lockedBuildingReason) &&
              lockedBuildingReason.Contains("Locked by research", StringComparison.Ordinal),
            "catalogue buildings must also respect their research prerequisite");
        Check(!colony.CanQueueRecipe("recipe.sawmill.stage0", out string lockedReason) &&
              lockedReason.Contains("Locked by research", StringComparison.Ordinal),
            "catalogue recipes must be locked until their technology prerequisite is researched");
        int knowledgeBeforeResearch = researchCampaign.State.Knowledge;
        int energyBeforeResearch = researchCampaign.State.Energy;
        Check(colony.CanResearchProject(machineToolsEra0, researchCampaign, out _),
            "an era-zero research project without prerequisites should be available when funded");
        Check(colony.ResearchProject(machineToolsEra0, researchCampaign),
            "research should spend strategic resources and persist a completed technology");
        var machineTools = ProgressionCatalog.Research.Single(item => item.Id == machineToolsEra0);
        Check(researchCampaign.State.Knowledge == knowledgeBeforeResearch - machineTools.CostKnowledge &&
              researchCampaign.State.Energy == energyBeforeResearch - machineTools.CostEnergy,
            "research must debit the exact Knowledge and Energy costs from the campaign");
        Check(!colony.CanResearchProject(machineToolsEra1, researchCampaign, out string prerequisiteReason) &&
              prerequisiteReason.Contains("Requires:", StringComparison.Ordinal),
            "higher-era research must be blocked until both its previous era and logistics prerequisite are complete");
        Check(colony.ResearchProject(logisticsEra0, researchCampaign),
            "the foundational logistics project should be researchable");
        Check(colony.ResearchProject(machineToolsEra1, researchCampaign),
            "higher-era research should unlock after all prerequisite technologies are completed");
        Check(colony.CanQueueRecipe("recipe.sawmill.stage0", out _),
            "researching Machine Tools Era I should unlock the stage-zero sawmill recipe");
        Check(!colony.CanQueueRecipe("recipe.sawmill.stage1", out string stageOneReason) &&
              stageOneReason.Contains("power capacity", StringComparison.Ordinal),
            "the next recipe tier should require local power capacity even after its research unlock is complete");
        int woodBeforeRecipe = colony.ResourceCount(ResourceKind.Wood);
        Check(colony.QueueRecipeProduction(workbenchId, "recipe.sawmill.stage0"),
            "a catalogue recipe should be queueable after its real research prerequisite and material inputs are available");
        Check(colony.QueueRecipeProduction(workbenchId, "recipe.sawmill.stage0"),
            "the same workstation should accept a second queued order without assigning concurrent labor");
        colony.Step(0.1f);
        Check(colony.WorkstationCapacity == 1 && colony.BusyWorkstations == 1 &&
              colony.ProductionOrders.Count(order => !order.Complete && order.Assigned) == 1,
            "one Workbench must run only one active production order at a time");
        Check(colony.ResourceCount(ResourceKind.Wood) == woodBeforeRecipe - 4,
            "catalogue recipe inputs must be removed from the same live colony inventory");
        Check(!colony.QueueRecipeProduction(workbenchId, "recipe.metallurgy.stage0"),
            "a recipe requiring unavailable raw ore must be rejected instead of creating free materials");
        for (int i = 0; i < 1200; i++) colony.Step(0.1f);
        Check(colony.MaterialCount("material.sawmill.stage0") == 4,
            "worker labor must create the stage-specific stable-ID output from the selected recipe");
        Check(colony.ProductionOrders.Any(order => order.RecipeId == "recipe.sawmill.stage0" && order.Complete),
            "completed production orders must preserve their catalogue recipe identity");
        int legacyResourceTotal = Enum.GetValues<ResourceKind>().Sum(resource => colony.ResourceCount(resource));
        Check(colony.InventoryUsed == legacyResourceTotal + 12,
            "stable-ID industry materials must count against the same physical storage capacity as legacy resources");

        var oreDeposit = colony.MaterialDeposits.Single(deposit => deposit.MaterialId == "ore");
        int oreDepositBefore = oreDeposit.Amount;
        int oreStockBefore = colony.MaterialCount("ore");
        Check(colony.SelectWorkerAt(worker.X, worker.Y), "a colonist should be selectable before raw-material extraction");
        colony.CommandSelectedTo(oreDeposit.Cell.X + 0.5f, oreDeposit.Cell.Y + 0.5f);
        for (int i = 0; i < 300 && worker.CarriedMaterialId == null; i++) colony.Step(0.1f);
        Check(worker.CarriedMaterialId == "ore" && worker.CarriedAmount == 5,
            "a raw-material extraction trip must place stable-ID cargo on the worker");
        string cargoSave = ColonySaveService.Serialize(colony);
        var cargoRestored = ColonySaveService.Deserialize(cargoSave);
        var restoredCargoWorker = cargoRestored.Workers.Single(item => item.Id == worker.Id);
        Check(restoredCargoWorker.CarriedMaterialId == "ore" && restoredCargoWorker.CarriedAmount == 5 &&
              restoredCargoWorker.Task?.Kind == ColonyTaskKind.Deliver,
            "save/load must preserve a worker carrying raw materials and its delivery task");
        for (int i = 0; i < 300; i++) colony.Step(0.1f);
        Check(colony.MaterialCount("ore") == oreStockBefore + 5,
            "workers must extract stable-ID raw materials and deliver them to the shared stockpile");
        Check(oreDeposit.Amount == oreDepositBefore - 5,
            "raw-material extraction must reduce the finite deposit by the same delivered amount");
        Check(worker.CarriedMaterialId == null && worker.CarriedAmount == 0,
            "a completed raw-material delivery must clear stable-ID cargo");

        int catalogWorkshopId = colony.PlaceCatalogBuildingBlueprint("building.workshop.tier0", 5, 4);
        Check(catalogWorkshopId > 0 && colony.Objects.Single(item => item.Id == catalogWorkshopId).CatalogBuildingId == "building.workshop.tier0",
            "a researched catalogue building should create a persistent footprint-based blueprint");
        Check(colony.DesignTileConstruction(19, 14, ArchitectureKind.Path), "a new path order should be accepted before saving");
        colony.Step(0.1f);
        string json = ColonySaveService.Serialize(colony);
        var restored = ColonySaveService.Deserialize(json);
        var legacySnapshot = colony.CaptureSnapshot();
        legacySnapshot.SchemaVersion = 1;
        legacySnapshot.Materials.Clear();
        legacySnapshot.ResearchedProjects.Clear();
        legacySnapshot.Objects.RemoveAll(item => item.CatalogBuildingId != null);
        var legacyObjectIds = legacySnapshot.Objects.Select(item => item.Id).ToHashSet();
        foreach (var workerSnapshot in legacySnapshot.Workers)
            if (workerSnapshot.Task?.Kind == ColonyTaskKind.BuildObject &&
                !legacyObjectIds.Contains(workerSnapshot.Task.TargetId))
                workerSnapshot.Task = null;
        foreach (var order in legacySnapshot.ProductionOrders) order.RecipeId = null;
        string legacyJson = JsonSerializer.Serialize(legacySnapshot)
            .Replace("\"Materials\":[],", "", StringComparison.Ordinal)
            .Replace(",\"ResearchedProjects\":[]", "", StringComparison.Ordinal)
            .Replace(",\"ProductionTargets\":[]", "", StringComparison.Ordinal)
            .Replace(",\"GatherTargets\":[]", "", StringComparison.Ordinal)
            .Replace("\"BuildingProductionProgressSeconds\":0,", "", StringComparison.Ordinal)
            .Replace("\"RecipeId\":null,", "", StringComparison.Ordinal);
        var migratedLegacySave = ColonySaveService.Deserialize(legacyJson);
        Check(migratedLegacySave.ResourceCount(ResourceKind.Wood) == colony.ResourceCount(ResourceKind.Wood) &&
              migratedLegacySave.MaterialCount("material.sawmill.stage0") == 0 &&
              !migratedLegacySave.IsResearchComplete(machineToolsEra0),
            "schema v1 saves without stable-ID materials, recipe IDs or a research ledger must remain loadable");
        Check(restored.Workers.Count == colony.Workers.Count, "save/load must preserve the colony population");
        Check(restored.ResourceCount(ResourceKind.Wood) == colony.ResourceCount(ResourceKind.Wood) &&
              restored.ResourceCount(ResourceKind.Components) == colony.ResourceCount(ResourceKind.Components),
            "save/load must preserve actual inventory balances");
        Check(restored.MaterialCount("material.sawmill.stage0") == 4 &&
              restored.ProductionOrders.Any(order => order.RecipeId == "recipe.sawmill.stage0" && order.Complete),
            "save/load must preserve stable-ID industry stock and queued recipe identities");
        Check(restored.IsResearchComplete(machineToolsEra0) && restored.IsResearchComplete(logisticsEra0) &&
              restored.IsResearchComplete(machineToolsEra1) &&
              restored.CanQueueRecipe("recipe.sawmill.stage0", out _),
            "save/load must preserve researched technologies and their recipe unlock effects");
        Check(restored.TileOrders.Count == colony.TileOrders.Count &&
              restored.TileOrders.Select(o => (o.Cell, o.Target, o.Status, o.WorkDone))
                  .SequenceEqual(colony.TileOrders.Select(o => (o.Cell, o.Target, o.Status, o.WorkDone))),
            "save/load must preserve each tile construction state and work progress");
        Check(restored.Deposits.Select(d => (d.Id, d.Amount)).SequenceEqual(colony.Deposits.Select(d => (d.Id, d.Amount))),
            "save/load must preserve finite legacy deposit quantities");
        Check(restored.MaterialDeposits.Select(d => (d.Id, d.MaterialId, d.Amount))
                .SequenceEqual(colony.MaterialDeposits.Select(d => (d.Id, d.MaterialId, d.Amount))),
            "save/load must preserve stable-ID raw-material sites and remaining quantities");
        Check(restored.MaterialCount("ore") == colony.MaterialCount("ore"),
            "save/load must preserve extracted raw materials in the shared inventory");
        Check(restored.Objects.Any(item => item.CatalogBuildingId == "building.workshop.tier0"),
            "save/load must preserve catalogue building identity and footprint state");
        Check(restored.Workers.Select(w => (w.Id, w.X, w.Y, w.Hunger, w.CarriedResource, w.CarriedAmount, w.Task?.Kind))
              .SequenceEqual(colony.Workers.Select(w => (w.Id, w.X, w.Y, w.Hunger, w.CarriedResource, w.CarriedAmount, w.Task?.Kind))),
            "save/load must preserve worker positions, needs, cargo, and task kinds");
        for (int i = 0; i < 1000; i++) colony.Step(0.1f);
        Check(colony.Objects.Single(item => item.Id == catalogWorkshopId).IsBuilt &&
              colony.MaterialCount("material.masonry.stage0") == 4,
            "catalogue building construction must consume its real stable-ID costs and complete worker labor");
        Check(colony.ResearchProject("research.energy.grid_stability.era0", researchCampaign),
            "the foundational energy technology should unlock power infrastructure");
        int powerBuildingId = colony.PlaceCatalogBuildingBlueprint("building.power.tier0", 10, 4);
        Check(powerBuildingId > 0, "a researched power building should be placeable from the catalogue");
        for (int i = 0; i < 1000; i++) colony.Step(0.1f);
        Check(colony.Objects.Single(item => item.Id == powerBuildingId).IsBuilt && colony.PowerCapacity >= 10,
            "completed catalogue power buildings must add real local production capacity");
        Check(colony.CanQueueRecipe("recipe.sawmill.stage1", out string poweredRecipeReason),
            $"an advanced recipe with available inputs and local power should be queueable ({poweredRecipeReason})");
        Check(colony.QueueRecipeProduction(workbenchId, "recipe.sawmill.stage1") && colony.PendingPowerDemand == 1,
            "advanced production must reserve power capacity while its order is pending");
        for (int i = 0; i < 1000; i++) colony.Step(0.1f);
        Check(colony.ProductionOrders.Any(order => order.RecipeId == "recipe.sawmill.stage1" && order.Complete) &&
              colony.PendingPowerDemand == 0,
            "powered workstation production must complete and release its reserved power capacity");

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
        int startingTotal = fullSnapshot.Inventory.Sum(stack => stack.Amount) + fullSnapshot.Materials.Sum(stack => stack.Amount);
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
        int partialStartingInventory = partialDeliverySnapshot.Inventory.Sum(stack => stack.Amount) +
            partialDeliverySnapshot.Materials.Sum(stack => stack.Amount);
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

        var plannerColony = new ColonySimulation();
        int plannerWorkbench = plannerColony.PlaceObjectBlueprint(PlaceableKind.Workbench, 13, 12);
        Check(plannerWorkbench > 0, "automatic production test should place a workbench blueprint");
        for (int i = 0; i < 700; i++) plannerColony.Step(0.1f);
        Check(plannerColony.Objects.Single(item => item.Id == plannerWorkbench).IsBuilt,
            "automatic production test should complete the workbench before planning");
        var plannerCampaign = new CampaignSimulation(WorldGenerationService.Generate(27182));
        plannerCampaign.State.Knowledge = 500;
        plannerCampaign.State.Energy = 500;
        Check(plannerColony.SetProductionTarget("recipe.sawmill.stage0", 5),
            "players should be able to set a valid automatic production target");
        plannerColony.Step(0.1f);
        Check(!plannerColony.ProductionOrders.Any(order => order.RecipeId == "recipe.sawmill.stage0"),
            "automatic production must not bypass a missing research unlock");
        Check(plannerColony.ResearchProject("research.industry.machine_tools.era0", plannerCampaign),
            "automatic production test should research the recipe prerequisite");
        for (int i = 0; i < 3; i++) plannerColony.Step(0.1f);
        Check(plannerColony.ProductionOrders.Count(order => order.RecipeId == "recipe.sawmill.stage0" && !order.Complete) == 3,
            "automatic production should queue only enough two-unit batches to cover a five-unit target");
        string plannerSave = ColonySaveService.Serialize(plannerColony);
        var plannerRestored = ColonySaveService.Deserialize(plannerSave);
        Check(plannerRestored.ProductionTarget("recipe.sawmill.stage0") == 5,
            "save/load must preserve automatic production targets");
        for (int i = 0; i < 5; i++) plannerRestored.Step(0.1f);
        Check(plannerRestored.ProductionOrders.Count(order => order.RecipeId == "recipe.sawmill.stage0") == 3,
            "pending output and persisted targets must prevent duplicate queue flooding after load");
        Check(plannerRestored.SetProductionTarget("recipe.sawmill.stage0", 0) &&
              plannerRestored.ProductionTarget("recipe.sawmill.stage0") == 0,
            "setting a target to zero should disable automatic production");

        var gatherPlannerColony = new ColonySimulation();
        Check(gatherPlannerColony.SetGatherTarget("ore", 10),
            "players should be able to set a stock target for an existing finite raw-material site");
        gatherPlannerColony.Step(0.1f);
        var oreSite = gatherPlannerColony.MaterialDeposits.Single(deposit => deposit.MaterialId == "ore");
        Check(gatherPlannerColony.Workers.Any(worker => worker.Task?.Kind == ColonyTaskKind.Gather &&
              worker.Task.MaterialDepositId == oreSite.Id),
            "automatic extraction should assign an idle colonist to a deposit while stock is below target");
        string gatherPlannerSave = ColonySaveService.Serialize(gatherPlannerColony);
        var gatherPlannerRestored = ColonySaveService.Deserialize(gatherPlannerSave);
        Check(gatherPlannerRestored.GatherTarget("ore") == 10 &&
              gatherPlannerRestored.Workers.Any(worker => worker.Task?.Kind == ColonyTaskKind.Gather &&
                  worker.Task.MaterialDepositId == oreSite.Id),
            "save/load must preserve extraction targets and the worker's assigned gathering task");
        Check(gatherPlannerRestored.SetGatherTarget("ore", 0) &&
              gatherPlannerRestored.GatherTarget("ore") == 0,
            "setting a raw-material target to zero should disable automatic extraction");

        var farmColony = new ColonySimulation();
        int gardenId = farmColony.PlaceObjectBlueprint(PlaceableKind.Garden, 13, 12);
        Check(gardenId > 0, "food-production test should place a garden blueprint");
        for (int i = 0; i < 700; i++) farmColony.Step(0.1f);
        Check(farmColony.Objects.Single(item => item.Id == gardenId).IsBuilt &&
              farmColony.FoodProducerCount == 1 && farmColony.FoodProductionPerCycle == 2 &&
              farmColony.FoodWaterDemandPerCycle == 1,
            "completed gardens should advertise their water-fed food-production effect");
        var farmSnapshot = farmColony.CaptureSnapshot();
        farmSnapshot.BuildingProductionProgressSeconds = 29.9f;
        farmSnapshot.Materials.Add(new MaterialStackSnapshot { MaterialId = "water", Amount = 2 });
        var wateredFarm = ColonySimulation.RestoreSnapshot(farmSnapshot);
        int foodBeforeFarmCycle = wateredFarm.ResourceCount(ResourceKind.Food);
        wateredFarm.Step(0.1f);
        Check(wateredFarm.ResourceCount(ResourceKind.Food) == foodBeforeFarmCycle + 2 &&
              wateredFarm.MaterialCount("water") == 1,
            "a completed garden should consume one stored water and produce two food each 30-second cycle");
        string farmSave = ColonySaveService.Serialize(wateredFarm);
        var restoredFarm = ColonySaveService.Deserialize(farmSave);
        Check(Math.Abs(restoredFarm.BuildingProductionProgressSeconds - wateredFarm.BuildingProductionProgressSeconds) < 0.001f &&
              restoredFarm.FoodProducerCount == 1,
            "save/load must preserve building-production timing and built food producers");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Colony check failed: {message}");
    }
}
