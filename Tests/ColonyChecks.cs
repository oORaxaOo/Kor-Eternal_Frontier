using KorTheBlight.Simulation;
using KorTheBlight.Rendering;
using KorTheBlight.WorldModel;

namespace KorTheBlight.Tests;

internal static class ColonyChecks
{
    public static void Run()
    {
        var tickClock = new TickManager();
        int tickCallbacks = 0;
        tickClock.Advance(1f, _ => tickCallbacks++);
        Check(tickCallbacks == 60 && tickClock.TickCount == 60,
            "the fixed simulation clock must emit exactly 60 ticks per simulated second");
        tickClock.SetSpeed(3f);
        int tripleSpeedTicks = tickClock.Advance(1f, _ => { });
        Check(tripleSpeedTicks == 180 && tickClock.TickCount == 240,
            "3x speed must advance three times as many 60 Hz ticks (180 ticks per real second)");
        Check(new SimulationTick(250, 1f / 60f).IsRare &&
              new SimulationTick(2_000, 1f / 60f).IsGlobal,
            "rare and global simulation schedules must use 250- and 2000-tick intervals");

        var toilTracker = new PawnJobTracker(ColonyTaskKind.BuildTile, new CellPosition(4, 7));
        Check(toilTracker.CurrentToil?.Kind == PawnToilKind.MoveTo &&
              toilTracker.Toils.Any(toil => toil.Kind == PawnToilKind.Work),
            "a pawn job must expose ordered movement and work toils");
        toilTracker.AdvanceToNextToil();
        Check(toilTracker.CurrentToil?.Kind == PawnToilKind.Work,
            "a pawn job must advance from movement into its work toil");
        toilTracker.Finish();
        Check(toilTracker.IsComplete, "a pawn job tracker must support deterministic completion");

        var thingGrid = new ThingGrid();
        Check(thingGrid.Register(new PawnThing(1, new CellPosition(1, 1))) &&
              thingGrid.Register(new ResourceThing(4, ResourceKind.Food, 5, new CellPosition(3, 1))),
            "the shared thing grid must register pawns and physical resources");
        Check(thingGrid.At(new CellPosition(1, 1)).Single().Kind == ThingKind.Pawn &&
              thingGrid.FindNearest(new CellPosition(0, 1), thing => thing.IsEdible)?.Id == "resource:4",
            "the thing grid must query a cell and find the nearest edible thing");
        Check(thingGrid.Move("pawn:1", new CellPosition(2, 1)) &&
              thingGrid.At(new CellPosition(2, 1)).Single().Id == "pawn:1" &&
              thingGrid.Remove("resource:4") && thingGrid.Count == 1,
            "the thing grid must keep its spatial index consistent after movement and removal");

        var weightedPath = AStarPathfinder.TryFindPath(5, 5, new CellPosition(0, 2),
            new CellPosition(4, 2), _ => true,
            cell => cell == new CellPosition(1, 2) ? 20 : 1, out var weightedRoute);
        Check(weightedPath && weightedRoute.Count > 4 && !weightedRoute.Contains(new CellPosition(1, 2)),
            "A* must prefer a longer low-cost route over an expensive direct cell");

        var previewValidation = new ColonySimulation();
        Check(previewValidation.TryGetPlacementFootprint(PlaceableKind.Garden, out int gardenWidth, out int gardenHeight) &&
              gardenWidth == 3 && gardenHeight == 3,
            "the placement preview must expose the actual garden footprint dimensions");
        bool diningTablePreviewValid = previewValidation.CanPlaceObjectAt(PlaceableKind.DiningTable, 6, 6);
        Check((previewValidation.PlaceObjectBlueprint(PlaceableKind.DiningTable, 6, 6) > 0) == diningTablePreviewValid,
            "placement preview validity must use the same footprint rules as actual blueprint placement");

        var objectInspection = new ColonySimulation();
        var inspectionCell = (from y in Enumerable.Range(1, ColonySimulation.Height - 2)
                              from x in Enumerable.Range(1, ColonySimulation.Width - 2)
                              where objectInspection.CanPlaceObjectAt(PlaceableKind.StorageRack, x, y)
                              select new CellPosition(x, y)).First();
        int inspectionRackId = objectInspection.PlaceObjectBlueprint(
            PlaceableKind.StorageRack, inspectionCell.X, inspectionCell.Y);
        var inspectionRack = objectInspection.Objects.Single(item => item.Id == inspectionRackId);
        inspectionRack.Status = ConstructionStatus.Complete;
        inspectionRack.Health = inspectionRack.MaxHealth;
        Check(objectInspection.SelectWorkOrderAt(inspectionCell.X + 0.5f, inspectionCell.Y + 0.5f) &&
              objectInspection.Message.Contains("Storage rack inspected", StringComparison.Ordinal) &&
              objectInspection.Message.Contains(ColonySimulation.StorageRackCapacity.ToString(), StringComparison.Ordinal),
            "clicking a completed storage rack must inspect it and expose its actual capacity");

        Check(objectInspection.SelectedObjectId == inspectionRackId &&
              objectInspection.SelectedObjectSummary.Contains("StorageRack", StringComparison.Ordinal) &&
              objectInspection.SelectedObjectDiagnostic.Contains(ColonySimulation.StorageRackCapacity.ToString(), StringComparison.Ordinal),
            "completed storage rack selection must populate the object inspector with model-backed details");

        var environment = new ColonySimulation();
        var furnitureChecks = new ColonySimulation();
        Check(furnitureChecks.PlaceObjectBlueprint(PlaceableKind.DiningTable, 6, 6) > 0, "dining table should accept a blueprint");
        Check(furnitureChecks.PlaceObjectBlueprint(PlaceableKind.Lamp, 10, 6) > 0, "lamp should accept a blueprint");
        Check(furnitureChecks.PlaceObjectBlueprint(PlaceableKind.ComfortBench, 12, 6) > 0, "comfort bench should accept a blueprint");
        var builderControls = new ColonySimulation();
        Check(builderControls.CanDesignTileConstruction(5, 5, ArchitectureKind.Floor),
            "the tile placement preview should recognize a clear tile as valid");
        Check(builderControls.DesignTileConstruction(5, 5, ArchitectureKind.Floor),
            "a clear tile should accept a construction blueprint");
        Check(!builderControls.CanDesignTileConstruction(5, 5, ArchitectureKind.Floor),
            "the tile placement preview should turn invalid after the tile is reserved");
        Check(builderControls.SelectWorkOrderAt(5.5f, 5.5f) && builderControls.CancelSelectedWorkOrder(),
            "a selected planned tile blueprint should be cancellable");
        Check(builderControls.TileOrderAt(5, 5) == null,
            "cancelling a tile blueprint must release the tile reservation");
        int cancellableObject = builderControls.PlaceObjectBlueprint(PlaceableKind.SleepingPod, 8, 8);
        Check(cancellableObject > 0 && builderControls.SelectWorkOrderAt(8.5f, 8.5f) &&
              builderControls.CancelSelectedWorkOrder() && builderControls.Objects.All(item => item.Id != cancellableObject),
            "a selected object blueprint should be removable before construction starts");
        Check(!builderControls.DemolishAt(15, 8),
            "the starter habitat must remain protected from accidental demolition");
        Check(!builderControls.DemolishAt(20, 12),
            "an empty tile should not be treated as a completed structure");
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
        // Freeze the test resident's colony tasks through the real deployment path so the
        // opening work schedule cannot legitimately send them away before exposure accumulates.
        Check(thermalHazard.PrepareWorkerForCombat(1),
            "a resident can be held in place to isolate the thermal exposure regression");
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

        string wallCost = colony.ConstructionCostStatus(ArchitectureKind.Wall, PlaceableKind.Workbench, isObject: false);
        Check(wallCost.Contains("Wood 2", StringComparison.Ordinal) &&
              wallCost.Contains("Stone 1", StringComparison.Ordinal) &&
              wallCost.Contains("MATERIALS READY", StringComparison.Ordinal),
            "the build HUD must expose the real wall recipe and report when all required materials are available");
        string generatorCost = colony.ConstructionCostStatus(ArchitectureKind.None, PlaceableKind.Generator, isObject: true);
        Check(generatorCost.Contains("Alloy 8", StringComparison.Ordinal) &&
              generatorCost.Contains("Components 3", StringComparison.Ordinal) &&
              generatorCost.Contains("MATERIALS READY", StringComparison.Ordinal),
            "the build HUD must expose multi-resource object recipes from the same authoritative cost catalog");
        Check(colony.ConstructionCostStatus(ArchitectureKind.None, PlaceableKind.Workbench, isObject: false)
                  .Contains("Select a tile type", StringComparison.Ordinal),
            "the build HUD must explain when no construction type is selected");

        // A higher-priority work order must be selected before an older low-priority order.
        var priorityColony = new ColonySimulation();
        Check(priorityColony.DesignTileConstruction(16, 10, ArchitectureKind.Wall),
            "priority test should queue a low-priority wall order");
        int lowPriorityOrderId = priorityColony.TileOrders.Single().Id;
        Check(priorityColony.DesignTileConstruction(17, 10, ArchitectureKind.Floor),
            "priority test should queue a second construction order");
        int highPriorityOrderId = priorityColony.TileOrders.Single(order => order.Cell == new CellPosition(17, 10)).Id;
        Check(priorityColony.SelectWorkOrderAt(17.5f, 10.5f) &&
              priorityColony.ChangeSelectedWorkOrderPriority(1) &&
              priorityColony.ChangeSelectedWorkOrderPriority(1) &&
              priorityColony.SelectedWorkOrderPriority == 5,
            "the player should select a blueprint and raise its priority to the critical cap");
        Check(priorityColony.SelectedWorkOrderDiagnostic.Contains("Materials ready", StringComparison.Ordinal),
            "the work order inspector should explain that a funded blueprint is ready and waiting for a worker");
        Check(priorityColony.ChangeSelectedWorkOrderPriority(1) &&
              priorityColony.SelectedWorkOrderPriority == 5,
            "work order priority must clamp at the critical maximum");
        Check(priorityColony.SelectWorkOrderAt(16.5f, 10.5f) &&
              priorityColony.ChangeSelectedWorkOrderPriority(-1) &&
              priorityColony.ChangeSelectedWorkOrderPriority(-1) &&
              priorityColony.SelectedWorkOrderPriority == 1,
            "the player should lower another order to the minimum priority");
        Check(priorityColony.SelectWorkOrderAt(17.5f, 10.5f),
            "the critical construction order should remain inspectable after changing another order");
        priorityColony.Step(0.1f);
        Check(priorityColony.Workers[0].Task?.Kind == ColonyTaskKind.BuildTile &&
              priorityColony.Workers[0].Task?.TargetId == highPriorityOrderId,
            "automatic scheduling must prefer a critical tile order over an earlier low-priority order");
        var restoredPriorityColony = ColonySaveService.Deserialize(ColonySaveService.Serialize(priorityColony));
        Check(restoredPriorityColony.TileOrders.Single(order => order.Id == highPriorityOrderId).Priority == 5 &&
              restoredPriorityColony.TileOrders.Single(order => order.Id == lowPriorityOrderId).Priority == 1,
            "work order priorities must survive versioned save/load");

        // Missing materials inherit the urgency of the most important order that needs them.
        var gatheringPriorityColony = new ColonySimulation();
        int firstGeneratorId = gatheringPriorityColony.PlaceObjectBlueprint(PlaceableKind.Generator, 3, 3);
        int criticalGeneratorId = gatheringPriorityColony.PlaceObjectBlueprint(PlaceableKind.Generator, 6, 3);
        Check(firstGeneratorId > 0 && criticalGeneratorId > 0,
            "material-priority test should queue two valid generator blueprints");
        foreach (int generatorId in new[] { firstGeneratorId, criticalGeneratorId })
        {
            var generator = gatheringPriorityColony.Objects.Single(item => item.Id == generatorId);
            Check(gatheringPriorityColony.SelectWorkOrderAt(generator.Origin.X + 0.5f, generator.Origin.Y + 0.5f) &&
                  gatheringPriorityColony.ChangeSelectedWorkOrderPriority(1) &&
                  gatheringPriorityColony.ChangeSelectedWorkOrderPriority(1),
                "both generators should be designated critical work orders");
        }
        for (int x = 2; x < 9; x++)
        {
            Check(gatheringPriorityColony.DesignTileConstruction(x, 12, ArchitectureKind.Door),
                "material-priority test should queue door construction");
            Check(gatheringPriorityColony.SelectWorkOrderAt(x + 0.5f, 12.5f) &&
                  gatheringPriorityColony.ChangeSelectedWorkOrderPriority(-1) &&
                  gatheringPriorityColony.ChangeSelectedWorkOrderPriority(-1),
                "door work orders should be marked low priority");
        }

        bool gatheredCriticalMaterial = false;
        for (int i = 0; i < 3000 && !gatheredCriticalMaterial; i++)
        {
            gatheringPriorityColony.Step(0.1f);
            gatheredCriticalMaterial = gatheringPriorityColony.Workers.Any(worker =>
                worker.Task?.Kind == ColonyTaskKind.Gather &&
                gatheringPriorityColony.Deposits.Any(deposit =>
                    deposit.Id == worker.Task.DepositId && deposit.Resource == ResourceKind.Alloy));
        }
        Check(gatheredCriticalMaterial,
            "automatic hauling must prioritize alloy for a priority-five generator over wood for priority-one doors, even when the wood deposit is closer");

        var miningColony = new ColonySimulation();
        int stoneBeforeMining = miningColony.ResourceCount(ResourceKind.Stone);
        Check(miningColony.DesignExcavation(22, 6),
            "the player should be able to designate an unoccupied rock tile for excavation");
        Check(!miningColony.DesignExcavation(22, 6),
            "a rock tile must not accept duplicate unfinished excavation orders");
        Check(!miningColony.DesignExcavation(16, 10),
            "excavation designation must reject ordinary ground");
        var miningOrder = miningColony.ExcavationOrders.Single();
        Check(miningColony.SelectWorkOrderAt(22.5f, 6.5f) &&
              miningColony.ChangeSelectedWorkOrderPriority(1) &&
              miningColony.ChangeSelectedWorkOrderPriority(1) &&
              miningColony.SelectedWorkOrderPriority == 5,
            "excavation should use the same selectable work-order priority system");

        var crossFamilyPriorityColony = new ColonySimulation();
        Check(crossFamilyPriorityColony.DesignTileConstruction(16, 10, ArchitectureKind.Wall) &&
              crossFamilyPriorityColony.DesignExcavation(22, 6),
            "cross-family priority test should queue both construction and mining");
        Check(crossFamilyPriorityColony.SelectWorkOrderAt(22.5f, 6.5f) &&
              crossFamilyPriorityColony.ChangeSelectedWorkOrderPriority(1) &&
              crossFamilyPriorityColony.ChangeSelectedWorkOrderPriority(1) &&
              crossFamilyPriorityColony.SelectedWorkOrderDiagnostic.Contains("Rock is accessible", StringComparison.Ordinal),
            "the excavation inspector should explain that solid rock is available for mining");
        crossFamilyPriorityColony.Step(0.1f);
        Check(crossFamilyPriorityColony.Workers[0].Task?.Kind == ColonyTaskKind.Mine,
            "a critical excavation must outrank a normal-priority construction order in the shared scheduler");

        var plannedMiningSave = ColonySaveService.Deserialize(ColonySaveService.Serialize(miningColony));
        Check(plannedMiningSave.ExcavationOrders.Single().Priority == 5 &&
              plannedMiningSave.TileAt(22, 6)?.Terrain == TerrainKind.Rock,
            "unstarted mining orders and the untouched rock tile should survive save/load");

        miningColony.Step(0.1f);
        Check(miningOrder.Status == ConstructionStatus.UnderConstruction &&
              miningColony.Workers.Any(worker => worker.Task?.Kind == ColonyTaskKind.Mine &&
                                                    worker.Task.TargetId == miningOrder.Id),
            "a worker should autonomously travel to and begin a designated excavation");
        var activeMiningSave = ColonySaveService.Deserialize(ColonySaveService.Serialize(miningColony));
        Check(activeMiningSave.ExcavationOrders.Single().Status == ConstructionStatus.UnderConstruction &&
              activeMiningSave.Workers.Any(worker => worker.Task?.Kind == ColonyTaskKind.Mine),
            "the active mining task and excavation progress state should survive save/load");

        for (int i = 0; i < 400 && miningOrder.Status != ConstructionStatus.Complete; i++)
            miningColony.Step(0.1f);
        var minedStone = miningColony.Deposits.SingleOrDefault(deposit =>
            deposit.Cell == new CellPosition(22, 6) && deposit.Resource == ResourceKind.Stone && deposit.Amount == 3);
        Check(miningOrder.Status == ConstructionStatus.Complete &&
              miningColony.TileAt(22, 6)?.Terrain == TerrainKind.Ground &&
              minedStone != null,
            "completed mining should remove the rock and create a finite, physical stone deposit");
        Check(miningColony.ResourceCount(ResourceKind.Stone) == stoneBeforeMining,
            "mining must not silently add extracted stone directly to colony inventory");
        var completedMiningSave = ColonySaveService.Deserialize(ColonySaveService.Serialize(miningColony));
        Check(completedMiningSave.ExcavationOrders.Single().Status == ConstructionStatus.Complete &&
              completedMiningSave.Deposits.Any(deposit => deposit.Cell == new CellPosition(22, 6) &&
                                                          deposit.Resource == ResourceKind.Stone && deposit.Amount == 3),
            "the mined terrain and uncollected stone deposit should persist through save/load");



        var woodDeposit = colony.Deposits.Single(d => d.Cell == new CellPosition(10, 15));
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
        var reservationDeposit = reservationColony.Deposits.Single(d => d.Cell == new CellPosition(10, 15));
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

        Check(colony.DesignTileConstruction(19, 14, ArchitectureKind.Path), "a new path order should be accepted before saving");
        colony.Step(0.1f);
        string json = ColonySaveService.Serialize(colony);
        var restored = ColonySaveService.Deserialize(json);
        Check(restored.Workers.Count == colony.Workers.Count, "save/load must preserve the colony population");
        Check(restored.ResourceCount(ResourceKind.Wood) == colony.ResourceCount(ResourceKind.Wood) &&
              restored.ResourceCount(ResourceKind.Components) == colony.ResourceCount(ResourceKind.Components),
            "save/load must preserve actual inventory balances");
        Check(restored.TileOrders.Count == colony.TileOrders.Count &&
              restored.TileOrders.Select(o => (o.Cell, o.Target, o.Status, o.WorkDone))
                  .SequenceEqual(colony.TileOrders.Select(o => (o.Cell, o.Target, o.Status, o.WorkDone))),
            "save/load must preserve each tile construction state and work progress");
        Check(restored.Deposits.Select(d => (d.Id, d.Amount)).SequenceEqual(colony.Deposits.Select(d => (d.Id, d.Amount))),
            "save/load must preserve finite deposit quantities");
        var actualWorkerState = restored.Workers.Select(w => (w.Id, w.X, w.Y, w.Hunger, w.CarriedResource, w.CarriedAmount, w.Task?.Kind)).ToArray();
        var expectedWorkerState = colony.Workers.Select(w => (w.Id, w.X, w.Y, w.Hunger, w.CarriedResource, w.CarriedAmount, w.Task?.Kind)).ToArray();
        Check(actualWorkerState.SequenceEqual(expectedWorkerState),
            $"save/load must preserve worker positions, needs, cargo, and task kinds. Expected [{string.Join("; ", expectedWorkerState.Select(w => $"#{w.Id} ({w.X:0.000},{w.Y:0.000}) hunger={w.Hunger:0.000} cargo={w.CarriedAmount} task={w.Item7}"))}], actual [{string.Join("; ", actualWorkerState.Select(w => $"#{w.Id} ({w.X:0.000},{w.Y:0.000}) hunger={w.Hunger:0.000} cargo={w.CarriedAmount} task={w.Item7}"))}]");

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

        var climateColony = new ColonySimulation(seed: 77);
        float ambientBeforeWeather = climateColony.TemperatureAt(5, 5);
        climateColony.ApplyWorldClimate(WorldSeason.Winter, WorldWeather.Frost);
        climateColony.Step(10f);
        climateColony.Step(10f);
        Check(climateColony.TemperatureAt(5, 5) < ambientBeforeWeather,
            "world seasons and weather must alter local colony temperatures through ambient heat exchange");

        var blightColony = new ColonySimulation(seed: 2137);
        var infectedHabitat = blightColony.Objects.Single(item => item.Kind == PlaceableKind.Habitat);
        infectedHabitat.IsBlightInfected = true;
        var magmaBarrier = blightColony.TileAt(14, 8)!;
        magmaBarrier.MagmaLevel = 1;
        for (int i = 0; i < 12; i++)
        {
            blightColony.Step(10f);
            blightColony.ApplyBlightInfectionTick(0.01f);
        }
        Check(Enumerable.Range(0, ColonySimulation.Width).Any(x =>
                Enumerable.Range(0, ColonySimulation.Height).Any(y =>
                    blightColony.TileAt(x, y) is { IsBlightInfected: true, Architecture: ArchitectureKind.None } tile &&
                    tile.Terrain == TerrainKind.Ground)),
            "local Blight must creep from infected structures onto bare organic ground");
        Check(!magmaBarrier.IsBlightInfected,
            "magma must block and cleanse local Blight growth");
        var blightSnapshot = blightColony.CaptureSnapshot();
        var restoredBlightColony = ColonySimulation.RestoreSnapshot(blightSnapshot);
        Check(Enumerable.Range(0, ColonySimulation.Width).Any(x =>
                Enumerable.Range(0, ColonySimulation.Height).Any(y =>
                    restoredBlightColony.TileAt(x, y)?.IsBlightInfected == true)),
            "organic Blight growth must persist through colony save/load");

        var fullSnapshot = new ColonySimulation().CaptureSnapshot();
        int fullCapacity = ColonySimulation.BaseStockpileCapacity;
        int startingTotal = fullSnapshot.Inventory.Sum(stack => stack.Amount);
        fullSnapshot.Inventory.Single(stack => stack.Resource == ResourceKind.Food).Amount += fullCapacity - startingTotal;
        var fullColony = ColonySimulation.RestoreSnapshot(fullSnapshot);
        Check(fullColony.InventoryUsed == fullColony.InventoryCapacity && fullColony.InventoryFreeCapacity == 0,
            "a colony save may contain inventory exactly up to its available storage capacity");
        var fullWoodDeposit = fullColony.Deposits.Single(deposit => deposit.Cell == new CellPosition(10, 15));
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
