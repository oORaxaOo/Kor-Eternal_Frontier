using System.Text.Json;
using KorTheBlight.Simulation;
using KorTheBlight.Campaign;
using KorTheBlight.Rendering;

namespace KorTheBlight.Tests;

internal static class ColonyChecks
{
    public static void Run()
    {
        var environment = new ColonySimulation();
        Check(environment.AddFluid(10, 10, FluidKind.Water, 7) == 7,
            "open tiles must accept fluid up to the seven-unit level cap");
        Check(environment.AddFluid(22, 6, FluidKind.Water, 1) == 0,
            "rock tiles must block fluid placement");
        Check(environment.AddFluid(10, 10, FluidKind.Magma, 1) == 0,
            "water and magma must not coexist in one tile in this first simulation slice");
        environment.Step(1f);
        Check(environment.TotalFluid(FluidKind.Water) == 7 &&
              environment.FluidLevelAt(10, 10, FluidKind.Water) < 7,
            "the deterministic fluid tick must redistribute water without creating or losing units");
        Check(environment.AddFluid(25, 7, FluidKind.Magma, 7) == 7,
            "magma must be placeable on open terrain with bounded levels");
        environment.Step(1f);
        Check(environment.TemperatureAt(25, 7) > 20f,
            "magma must heat its tile through the thermal simulation");

        static float NeighbourHeating(ArchitectureKind barrier)
        {
            var world = new ColonySimulation();
            world.TileAt(5, 5)!.TemperatureCelsius = 200f;
            world.TileAt(5, 5)!.Architecture = barrier;
            world.TileAt(6, 5)!.TemperatureCelsius = 20f;
            world.Step(1f);
            return world.TemperatureAt(6, 5);
        }

        float openHeating = NeighbourHeating(ArchitectureKind.None);
        float doorHeating = NeighbourHeating(ArchitectureKind.Door);
        float wallHeating = NeighbourHeating(ArchitectureKind.Wall);
        Check(openHeating > doorHeating && doorHeating > wallHeating && wallHeating > 20f,
            "open tiles must exchange heat fastest, doors partially insulate, and walls strongly reduce but do not eliminate heat transfer");

        var thermalHazard = new ColonySimulation();
        var thermalControl = new ColonySimulation();
        thermalHazard.TileAt(16, 12)!.TemperatureCelsius = 5_000f;
        for (int i = 0; i < 20; i++)
        {
            thermalHazard.Step(0.25f);
            thermalControl.Step(0.25f);
        }
        var exposedWorker = thermalHazard.Workers.Single(worker => worker.Id == 1);
        var exposedCitizen = thermalHazard.CitizenForWorker(1)!;
        Check(exposedWorker.ThermalExposure >= 15f,
            "sustained extreme tile temperature must accumulate resident thermal exposure");
        Check(exposedCitizen.Fatigue < thermalControl.CitizenForWorker(1)!.Fatigue,
            "thermal exposure must reduce rest satisfaction beyond ordinary fatigue drain");
        var hazardEvents = thermalHazard.DrainSimulationEvents();
        Check(hazardEvents.Any(item => item.Kind == SimulationEventKind.HazardChanged &&
              item.SubjectId == exposedCitizen.Id &&
              item.RelatedId == (long)SimulationHazardKind.ThermalExposure && item.Value > 0),
            "crossing the thermal danger threshold must emit a stable-ID hazard event");
        exposedWorker.Stress = 37f;
        var restoredHazard = ColonySaveService.Deserialize(ColonySaveService.Serialize(thermalHazard));
        Check(Math.Abs(restoredHazard.Workers.Single(worker => worker.Id == 1).ThermalExposure -
              exposedWorker.ThermalExposure) < 0.001f,
            "thermal exposure must survive a save/load round trip");
        Check(Math.Abs(restoredHazard.Workers.Single(worker => worker.Id == 1).Stress - 37f) < 0.001f,
            "resident stress must survive a save/load round trip");
        string environmentSave = ColonySaveService.Serialize(environment);
        var restoredEnvironment = ColonySaveService.Deserialize(environmentSave);
        Check(restoredEnvironment.TotalFluid(FluidKind.Water) == environment.TotalFluid(FluidKind.Water) &&
              restoredEnvironment.TotalFluid(FluidKind.Magma) == environment.TotalFluid(FluidKind.Magma) &&
              Math.Abs(restoredEnvironment.TemperatureAt(25, 7) - environment.TemperatureAt(25, 7)) < 0.001f,
            "fluid levels and tile temperatures must survive save/load");

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

        var reservationColony = new ColonySimulation();
        var reservationDeposit = reservationColony.Deposits.Single(d => d.Resource == ResourceKind.Wood);
        var firstGatherer = reservationColony.Workers[0];
        var secondGatherer = reservationColony.Workers[1];
        Check(reservationColony.SelectWorkerAt(firstGatherer.X, firstGatherer.Y),
            "the first worker should be selectable for a deposit reservation check");
        reservationColony.CommandSelectedTo(reservationDeposit.Cell.X + 0.5f, reservationDeposit.Cell.Y + 0.5f);
        Check(firstGatherer.Task?.Kind == ColonyTaskKind.Gather,
            "the first worker should claim the unreserved resource deposit");
        Check(reservationColony.SelectWorkerAt(secondGatherer.X, secondGatherer.Y),
            "the second worker should be selectable for a duplicate deposit order");
        reservationColony.CommandSelectedTo(reservationDeposit.Cell.X + 0.5f, reservationDeposit.Cell.Y + 0.5f);
        Check(secondGatherer.Task == null &&
              reservationColony.Message.Contains("already assigned", StringComparison.OrdinalIgnoreCase),
            "a finite resource deposit must not accept duplicate simultaneous gather assignments");

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
              !stageOneReason.Contains("Locked by research", StringComparison.Ordinal),
            "the next recipe tier should be technology-unlocked but still respect its actual material inputs");
        int woodBeforeRecipe = colony.ResourceCount(ResourceKind.Wood);
        Check(colony.QueueRecipeProduction(workbenchId, "recipe.sawmill.stage0"),
            "a catalogue recipe should be queueable after its real research prerequisite and material inputs are available");
        Check(colony.ResourceCount(ResourceKind.Wood) == woodBeforeRecipe - 2,
            "catalogue recipe inputs must be removed from the same live colony inventory");
        Check(!colony.QueueRecipeProduction(workbenchId, "recipe.metallurgy.stage0"),
            "a recipe requiring unavailable raw ore must be rejected instead of creating free materials");
        for (int i = 0; i < 1200; i++) colony.Step(0.1f);
        Check(colony.MaterialCount("material.sawmill.stage0") == 2,
            "worker labor must create the stage-specific stable-ID output from the selected recipe");
        Check(colony.ProductionOrders.Any(order => order.RecipeId == "recipe.sawmill.stage0" && order.Complete),
            "completed production orders must preserve their catalogue recipe identity");
        int legacyResourceTotal = Enum.GetValues<ResourceKind>().Sum(resource => colony.ResourceCount(resource));
        Check(colony.InventoryUsed == legacyResourceTotal + 2,
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

        Check(colony.DesignTileConstruction(19, 14, ArchitectureKind.Path), "a new path order should be accepted before saving");
        colony.Step(0.1f);
        string json = ColonySaveService.Serialize(colony);
        var restored = ColonySaveService.Deserialize(json);
        var legacySnapshot = colony.CaptureSnapshot();
        legacySnapshot.SchemaVersion = 1;
        legacySnapshot.Materials.Clear();
        legacySnapshot.ResearchedProjects.Clear();
        foreach (var order in legacySnapshot.ProductionOrders) order.RecipeId = null;
        string legacyJson = JsonSerializer.Serialize(legacySnapshot)
            .Replace("\"Materials\":[],", "", StringComparison.Ordinal)
            .Replace(",\"ResearchedProjects\":[]", "", StringComparison.Ordinal)
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
        Check(restored.MaterialCount("material.sawmill.stage0") == 2 &&
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
        Check(restored.Workers.Select(w => (w.Id, w.X, w.Y, w.Hunger, w.CarriedResource, w.CarriedAmount, w.Task?.Kind))
              .SequenceEqual(colony.Workers.Select(w => (w.Id, w.X, w.Y, w.Hunger, w.CarriedResource, w.CarriedAmount, w.Task?.Kind))),
            "save/load must preserve worker positions, needs, cargo, and task kinds");

        var needsColony = new ColonySimulation();
        var needsWorker = needsColony.Workers[0];
        var liveCitizen = needsColony.CitizenForWorker(needsWorker.Id);
        Check(liveCitizen != null, "each visible colonist must have a live citizen-needs agent");
        needsWorker.Hunger = 79f;
        int foodBeforeNeeds = needsColony.ResourceCount(ResourceKind.Food);
        for (int i = 0; i < 400; i++) needsColony.Step(0.1f);
        Check(needsColony.ResourceCount(ResourceKind.Food) < foodBeforeNeeds &&
              needsWorker.Hunger < 79f,
            "the live citizen system must move toward stockpiled food, consume it, and synchronize hunger back to the visible colonist");
        Check(liveCitizen!.Happiness >= 0 && liveCitizen.Happiness <= 100,
            "the live colony HUD must receive bounded citizen mood from the ecosystem");

        liveCitizen.Fatigue = 37.5f;
        string fatigueSave = ColonySaveService.Serialize(needsColony);
        var restoredNeedsColony = ColonySaveService.Deserialize(fatigueSave);
        var restoredCitizen = restoredNeedsColony.CitizenForWorker(needsWorker.Id);
        Check(restoredCitizen != null && MathF.Abs(restoredCitizen.Fatigue - 37.5f) < 0.001f,
            "save/load must preserve citizen fatigue instead of resetting population needs");

        var gardenColony = new ColonySimulation();
        int gardenFoodBefore = gardenColony.ResourceCount(ResourceKind.Food);
        int liveGardenId = gardenColony.PlaceObjectBlueprint(PlaceableKind.Garden, 24, 17);
        Check(liveGardenId > 0, "the live ecosystem integration fixture should accept a garden blueprint");
        for (int i = 0; i < 2500; i++) gardenColony.Step(0.1f);
        Check(gardenColony.Objects.Single(item => item.Id == liveGardenId).IsBuilt,
            "a garden must still use the existing construction scheduler before becoming operational");
        Check(gardenColony.ResourceCount(ResourceKind.Food) > gardenFoodBefore,
            "a completed garden must register with the live job broker and deliver reserved food output into colony inventory");

        var expandedStorageColony = new ColonySimulation();
        int expandedRackId = expandedStorageColony.PlaceObjectBlueprint(PlaceableKind.StorageRack, 23, 15);
        Check(expandedRackId > 0, "expanded-storage fixture should accept a rack blueprint");
        for (int i = 0; i < 1200; i++) expandedStorageColony.Step(0.1f);
        Check(expandedStorageColony.Objects.Single(item => item.Id == expandedRackId).IsBuilt,
            "the storage rack must finish construction before its grid capacity is enabled");
        Check(expandedStorageColony.InventoryCapacity ==
              ColonySimulation.BaseStockpileCapacity + ColonySimulation.StorageRackCapacity,
            "a completed storage rack must expand the colony's bounded inventory capacity");
        Check(expandedStorageColony.InventoryUsed <= expandedStorageColony.InventoryCapacity,
            "completed storage capacity must never leave the colony inventory over capacity");

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
