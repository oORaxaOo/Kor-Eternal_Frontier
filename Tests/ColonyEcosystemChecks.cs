using KorTheBlight.Simulation;

namespace KorTheBlight.Tests;

internal static class ColonyEcosystemChecks
{
    public static void Run()
    {

        var needs = new ColonyEcosystem(4, 4);
        var thresholdCitizen = needs.AddCitizen("Threshold probe", new CellPosition(0, 0));
        thresholdCitizen.Hunger = 30.1f;
        thresholdCitizen.Fatigue = 20.01f;
        needs.Step(1);
        var needEvents = needs.DrainEvents();
        Check(needs.SimulationTick == 1 && needEvents.Count == 2,
            "crossing hunger and fatigue thresholds must emit events in the current simulation tick");
        Check(needEvents[0].Kind == SimulationEventKind.NeedThresholdCrossed &&
              needEvents[0].SubjectId == thresholdCitizen.Id &&
              needEvents[0].RelatedId == (long)SimulationNeedKind.Hunger &&
              needEvents[0].Tick == needs.SimulationTick,
            "hunger threshold events must identify the citizen, need type and deterministic tick");
        Check(needEvents[1].RelatedId == (long)SimulationNeedKind.Fatigue &&
              needEvents[1].SubjectId == thresholdCitizen.Id,
            "fatigue threshold events must be distinguishable from hunger events");
        needs.Step(1);
        Check(needs.DrainEvents().Count == 0,
            "a citizen remaining below a threshold must not emit duplicate crossing events every tick");
        var hungryDiagnostic = needs.InspectCitizen(thresholdCitizen.Id);
        Check(hungryDiagnostic is not null &&
              hungryDiagnostic.CitizenId == thresholdCitizen.Id &&
              hungryDiagnostic.Name == "Threshold probe" &&
              hungryDiagnostic.Hunger < 30f &&
              hungryDiagnostic.AvailableFood == 0 &&
              hungryDiagnostic.PrimaryReason.Contains("no stored food", StringComparison.OrdinalIgnoreCase),
            "citizen inspection must explain critical hunger and missing food using live simulation state");
        Check(needs.InspectCitizen(9999) is null,
            "citizen inspection must return no result for an unknown citizen ID");
        var utilityDrivenNeeds = new ColonyEcosystem(4, 4);
        utilityDrivenNeeds.Grid.DefineStockpileZone(ResourceKind.Food,
            [new CellPosition(0, 0), new CellPosition(1, 0)]);
        Check(utilityDrivenNeeds.Grid.TryAdd(ResourceKind.Food, 2),
            "utility-driven need arbitration requires reachable stored food");
        var utilityCitizen = utilityDrivenNeeds.AddCitizen("Utility probe", new CellPosition(0, 1));
        utilityCitizen.Hunger = 10f;
        utilityCitizen.Fatigue = 5f;
        utilityCitizen.Personality.SetWeight(PawnNeedKind.Hunger, 3f);
        utilityDrivenNeeds.Step(0.1f);
        Check(utilityCitizen.State == CitizenState.MovingToStorage,
            "live citizen behavior must use personality-weighted utility scores to choose food over rest");
        Check(utilityCitizen.JobId is null && utilityDrivenNeeds.GridFor(ResourceKind.Wood).ReservedCount(ResourceKind.Wood) == 0,
            "interrupting for a utility-selected need must not strand a job or output reservation");

        var outputGridWorld = new ColonyEcosystem(8, 8);
        outputGridWorld.GridFor(ResourceKind.Food).DefineStockpileZone(ResourceKind.Food,
            [new CellPosition(0, 0), new CellPosition(1, 0)]);
        outputGridWorld.GridFor(ResourceKind.Wood).DefineStockpileZone(ResourceKind.Wood,
            [new CellPosition(0, 0), new CellPosition(1, 0)]);
        outputGridWorld.GridFor(ResourceKind.Food).TryAdd(ResourceKind.Food, 10);
        outputGridWorld.AddCitizen("Wood maker", new CellPosition(0, 0));
        outputGridWorld.JobBroker.RegisterWorkplace("Wood mill", new CellPosition(0, 0),
            workerCapacity: 1, outputResource: ResourceKind.Wood, outputAmount: 1, workSeconds: 0.1f);
        outputGridWorld.Step(0.1f);
        outputGridWorld.Step(0.1f);
        outputGridWorld.Step(0.2f);
        Check(outputGridWorld.GridFor(ResourceKind.Wood).ResourceCount(ResourceKind.Wood) == 1,
            "workplace output must be committed into the spatial grid for its actual resource type");

        var unifiedWork = new ColonyEcosystem(8, 8);
        unifiedWork.GridFor(ResourceKind.Food).DefineStockpileZone(ResourceKind.Food,
            [new CellPosition(0, 0), new CellPosition(1, 0)]);
        unifiedWork.GridFor(ResourceKind.Wood).DefineStockpileZone(ResourceKind.Wood,
            [new CellPosition(0, 0), new CellPosition(1, 0)]);
        unifiedWork.GridFor(ResourceKind.Food).TryAdd(ResourceKind.Food, 10);
        var externalWorker = unifiedWork.AddCitizen("External task worker", new CellPosition(0, 0));
        int workplaceId = unifiedWork.JobBroker.RegisterWorkplace("Garden", new CellPosition(0, 0),
            workerCapacity: 1, outputResource: ResourceKind.Wood, outputAmount: 1, workSeconds: 1f);
        unifiedWork.Step(0.1f);
        Check(externalWorker.JobId == workplaceId && externalWorker.OutputReservationId > 0,
            "a resident may reserve a workplace and output slot before starting work");
        Check(unifiedWork.SetExternalTaskState(externalWorker.Id, true) &&
              externalWorker.JobId is null && externalWorker.OutputReservationId == 0 &&
              unifiedWork.JobBroker.FindJob(workplaceId)!.AssignedWorkers == 0,
            "an external colony task must cancel the ecosystem job and release reserved output atomically");
        unifiedWork.Step(0.1f);
        Check(externalWorker.WorkProgress == 0f &&
              unifiedWork.GridFor(ResourceKind.Wood).ResourceCount(ResourceKind.Wood) == 0,
            "a resident must not keep working a second workplace while an external task owns the resident");

        var sharedCapacity = new ColonyEcosystem(8, 8);
        foreach (var resource in Enum.GetValues<ResourceKind>())
            sharedCapacity.GridFor(resource).DefineStockpileZone(resource,
                [new CellPosition(0, 0), new CellPosition(1, 0)]);
        sharedCapacity.SetExternalStorageBudget(inventoryUsed: 9, inventoryCapacity: 10);
        var budgetWorker = sharedCapacity.AddCitizen("Budget worker A", new CellPosition(0, 0));
        var secondBudgetWorker = sharedCapacity.AddCitizen("Budget worker B", new CellPosition(1, 0));
        sharedCapacity.JobBroker.RegisterWorkplace("First output", new CellPosition(0, 0),
            workerCapacity: 1, outputResource: ResourceKind.Wood, outputAmount: 1, workSeconds: 0.1f);
        sharedCapacity.JobBroker.RegisterWorkplace("Second output", new CellPosition(1, 0),
            workerCapacity: 1, outputResource: ResourceKind.Alloy, outputAmount: 1, workSeconds: 0.1f);
        sharedCapacity.Step(0.1f);
        Check(sharedCapacity.Citizens.Count(citizen => citizen.OutputReservationId > 0) == 1 &&
              new[] { budgetWorker, secondBudgetWorker }.Count(citizen => citizen.JobId.HasValue) == 1,
            "output reservations across different resource grids must share the same aggregate inventory budget");
        sharedCapacity.SetExternalStorageBudget(inventoryUsed: 10, inventoryCapacity: 10);
        Check(sharedCapacity.Citizens.All(citizen => citizen.OutputReservationId == 0),
            "external shipments filling the aggregate inventory must release all over-budget output reservations");

        var logisticsEvents = new ColonyEcosystem(4, 4);
        logisticsEvents.Grid.DefineStockpileZone(ResourceKind.Food,
            [new CellPosition(0, 0), new CellPosition(1, 0)]);
        int eventJobId = logisticsEvents.JobBroker.RegisterWorkplace("Event yard",
            new CellPosition(3, 3), workerCapacity: 1, outputResource: ResourceKind.Wood);
        var registeredEvents = logisticsEvents.DrainEvents();
        Check(registeredEvents.Count == 1 &&
              registeredEvents[0].Kind == SimulationEventKind.JobBecameAvailable &&
              registeredEvents[0].SubjectId == eventJobId && registeredEvents[0].RelatedId == 1,
            "registering a workplace must emit its available capacity");

        Check(logisticsEvents.Grid.TryAdd(ResourceKind.Food, 3),
            "stock-change events require a successful resource addition");
        var addedEvents = logisticsEvents.DrainEvents();
        Check(addedEvents.Count == 1 &&
              addedEvents[0].Kind == SimulationEventKind.StockChanged &&
              addedEvents[0].SubjectId == (long)ResourceKind.Food &&
              addedEvents[0].RelatedId == 3 && addedEvents[0].Value == 3,
            "resource additions must publish resource identity, new stock and positive delta");
        Check(logisticsEvents.Grid.TryTake(ResourceKind.Food, 1) == 1,
            "stock-change event fixture must withdraw the requested resource");
        var takenEvents = logisticsEvents.DrainEvents();
        Check(takenEvents.Count == 1 && takenEvents[0].Kind == SimulationEventKind.StockChanged &&
              takenEvents[0].RelatedId == 2 && takenEvents[0].Value == -1,
            "resource withdrawals must publish the remaining stock and negative delta");

        var eventCitizen = logisticsEvents.AddCitizen("Availability probe", new CellPosition(2, 2));
        Check(logisticsEvents.JobBroker.AssignNext(eventCitizen)?.Id == eventJobId,
            "availability event fixture must occupy the only workplace slot");
        logisticsEvents.JobBroker.Release(eventCitizen);
        var releasedEvents = logisticsEvents.DrainEvents();
        Check(releasedEvents.Count == 1 &&
              releasedEvents[0].Kind == SimulationEventKind.JobBecameAvailable &&
              releasedEvents[0].SubjectId == eventJobId && releasedEvents[0].RelatedId == 1,
            "releasing the final assigned worker must publish newly available workplace capacity");

        Check(logisticsEvents.JobBroker.AssignNext(eventCitizen)?.Id == eventJobId,
            "the workplace should accept a worker again before removal");
        Check(logisticsEvents.RemoveWorkplace(eventJobId) && eventCitizen.JobId == null &&
              logisticsEvents.DrainEvents().Count == 0,
            "removing a workplace must release its worker without publishing a stale availability event");


        var spatialJobs = new ColonyEcosystem(40, 26);
        int firstTieJob = spatialJobs.JobBroker.RegisterWorkplace("Stable first", new CellPosition(2, 1),
            workerCapacity: 1, outputResource: ResourceKind.Wood);
        spatialJobs.JobBroker.RegisterWorkplace("Stable second", new CellPosition(0, 1),
            workerCapacity: 1, outputResource: ResourceKind.Wood);
        for (int i = 0; i < 100; i++)
            spatialJobs.JobBroker.RegisterWorkplace($"Remote-{i}", new CellPosition(10 + i % 30, 10 + i / 30),
                workerCapacity: 1, outputResource: ResourceKind.Stone);
        var spatialResident = spatialJobs.AddCitizen("Spatial assignment probe", new CellPosition(1, 1));
        Check(spatialJobs.JobBroker.AssignNext(spatialResident)?.Id == firstTieJob,
            "nearest job assignment must preserve stable ID tie-breaking across different tiles");
        Check(spatialJobs.JobBroker.AssignmentTileChecks == 5 &&
              spatialJobs.JobBroker.JobCandidatesInspected == 2,
            "a nearby assignment should inspect only its Manhattan ring, not all registered workplaces");
        var grid = new GridManager(5, 5, maxStackSize: 10);
        int foodZone = grid.DefineStockpileZone(ResourceKind.Food,
            [new CellPosition(0, 0), new CellPosition(1, 0)]);
        Check(foodZone > 0, "a valid connected stockpile zone should be registered");
        Check(grid.TryAdd(ResourceKind.Food, 17), "food should be distributed over available tile stacks");
        Check(grid.ResourceCount(ResourceKind.Food) == 17 && grid.FreeCapacity(ResourceKind.Food) == 3,
            "resource totals and free space should reflect per-tile stack limits");
        Check(!grid.CanStore(ResourceKind.Food, 4) && grid.CanStore(ResourceKind.Food, 3),
            "capacity checks must reject overfilling while allowing the exact remaining capacity");

        Check(grid.TryReserveSpace(ResourceKind.Food, 3, out var reservation),
            "free stack space should be reservable by a future producer");
        Check(grid.FreeCapacity(ResourceKind.Food) == 0,
            "reserved storage must no longer be offered to competing producers");
        Check(grid.CommitReservation(reservation.Id) && grid.ResourceCount(ResourceKind.Food) == 20,
            "committing a reservation should add exactly the reserved quantity");
        Check(grid.TryTake(ResourceKind.Food, 7) == 7 && grid.ResourceCount(ResourceKind.Food) == 13,
            "withdrawal must never remove more resources than requested or available");
        Check(grid.TryAdd(ResourceKind.Food, 7) && grid.ResourceCount(ResourceKind.Food) == 20,
            "withdrawn space should become available for later deliveries");

        bool disconnectedRejected = false;
        try
        {
            grid.DefineStockpileZone(ResourceKind.Wood,
                [new CellPosition(3, 3), new CellPosition(4, 4)]);
        }
        catch (ArgumentException) { disconnectedRejected = true; }
        Check(disconnectedRejected, "a stockpile zone must be orthogonally connected");

        var colony = new ColonyEcosystem(8, 8);
        colony.Grid.DefineStockpileZone(ResourceKind.Food,
            [new CellPosition(0, 0), new CellPosition(0, 1)]);
        colony.GridFor(ResourceKind.Wood).DefineStockpileZone(ResourceKind.Wood,
            [new CellPosition(6, 6), new CellPosition(6, 7)]);
        Check(colony.Grid.TryAdd(ResourceKind.Food, 5),
            "the integrated colony should be able to seed a food stockpile");
        var citizen = colony.AddCitizen("Test-Kor", new CellPosition(1, 0));
        colony.AddCitizen("Second-Kor", new CellPosition(1, 1));
        int jobId = colony.JobBroker.RegisterWorkplace("Lumber yard", new CellPosition(5, 6),
            workerCapacity: 1, outputResource: ResourceKind.Wood, outputAmount: 2, workSeconds: 0.5f);

        colony.Step(0.1f);
        Check(citizen.JobId == jobId && citizen.State == CitizenState.MovingToJob,
            "an idle eligible citizen should reserve a job and move toward its workplace");
        Check(colony.GridFor(ResourceKind.Wood).ReservedCount(ResourceKind.Wood) == 2,
            "a worker must reserve output capacity before starting its job");
        Check(colony.RemoveWorkplace(jobId), "a registered workplace should be removable");
        Check(citizen.JobId == null && citizen.State == CitizenState.Idle &&
              colony.GridFor(ResourceKind.Wood).ReservedCount(ResourceKind.Wood) == 0,
            "removing a workplace must release its citizen and all reserved storage");

        int replacementJob = colony.JobBroker.RegisterWorkplace("Lumber yard", new CellPosition(5, 6),
            workerCapacity: 1, outputResource: ResourceKind.Wood, outputAmount: 2, workSeconds: 0.5f);
        for (int i = 0; i < 100; i++) colony.Step(0.1f);
        Check(colony.GridFor(ResourceKind.Wood).ResourceCount(ResourceKind.Wood) > 0 &&
              colony.GridFor(ResourceKind.Wood).ResourceCount(ResourceKind.Wood) <= 20,
            "work must produce real stored output without exceeding tile stack capacity");
        Check(colony.JobBroker.FindJob(replacementJob)?.AssignedWorkers <= 1,
            "workplace assignment must never exceed its worker capacity");
        Check(citizen.Happiness >= 0 && citizen.Happiness <= 100,
            "happiness should be the bounded average of citizen needs");

        var priorityColony = new ColonyEcosystem(8, 8);
        priorityColony.Grid.DefineStockpileZone(ResourceKind.Food,
            [new CellPosition(0, 0), new CellPosition(0, 1)]);
        priorityColony.GridFor(ResourceKind.Wood).DefineStockpileZone(ResourceKind.Wood,
            [new CellPosition(6, 6), new CellPosition(6, 7)]);
        Check(priorityColony.Grid.TryAdd(ResourceKind.Food, 2),
            "the priority fixture should have food available before work begins");
        var priorityCitizen = priorityColony.AddCitizen("Priority-Kor", new CellPosition(1, 0));
        int priorityJob = priorityColony.JobBroker.RegisterWorkplace("Priority lumber yard",
            new CellPosition(5, 6), workerCapacity: 1, outputResource: ResourceKind.Wood,
            outputAmount: 2, workSeconds: 5f);
        priorityColony.Step(0.1f);
        Check(priorityCitizen.JobId == priorityJob &&
              priorityColony.GridFor(ResourceKind.Wood).ReservedCount(ResourceKind.Wood) == 2,
            "a citizen should reserve workplace capacity and output storage before travelling to work");
        priorityCitizen.Hunger = 29f;
        priorityColony.Step(0.1f);
        Check(priorityCitizen.State == CitizenState.MovingToStorage &&
              priorityCitizen.JobId == null &&
              priorityColony.GridFor(ResourceKind.Wood).ReservedCount(ResourceKind.Wood) == 0,
            "food needs must preempt workplace travel and release both the job slot and its output reservation");
        Check(priorityColony.JobBroker.FindJob(priorityJob)?.AssignedWorkers == 0,
            "a citizen seeking food must not occupy workplace capacity while away from the job");

        var needsColony = new ColonyEcosystem(4, 4);
        needsColony.Grid.DefineStockpileZone(ResourceKind.Food,
            [new CellPosition(0, 0), new CellPosition(0, 1)]);
        needsColony.Grid.TryAdd(ResourceKind.Food, 2);
        var hungryCitizen = needsColony.AddCitizen("Hungry-Kor", new CellPosition(1, 0));
        hungryCitizen.Hunger = 25f;
        int foodBefore = needsColony.Grid.ResourceCount(ResourceKind.Food);
        for (int i = 0; i < 10; i++) needsColony.Step(0.1f);
        Check(needsColony.Grid.ResourceCount(ResourceKind.Food) == foodBefore - 1 &&
              hungryCitizen.Hunger > 30f,
            "a hungry citizen should physically reach food storage, consume one unit, and restore hunger");

        hungryCitizen.Fatigue = 10f;
        needsColony.Step(1f);
        Check(hungryCitizen.Fatigue > 10f,
            "an exhausted citizen should stop working and recover while idle");

        // Population-scale regression: 10,000 individually simulated citizens must keep
        // stable unique IDs and advance in one deterministic tick without a tiny population cap.
        var populationColony = new ColonyEcosystem(40, 26);
        var populationIds = new HashSet<int>();
        for (int i = 0; i < 10_000; i++)
        {
            var resident = populationColony.AddCitizen($"Resident-{i + 1}", new CellPosition(i % 40, (i / 40) % 26));
            Check(populationIds.Add(resident.Id), "every resident must receive a unique stable simulation ID");
        }
        Check(populationColony.Citizens.Count == 10_000,
            "the cached citizen view must expose all 10,000 residents without stale snapshots");
        populationColony.Step(0.05f);
        Check(populationColony.Citizens.All(resident => resident.Hunger < 100f),
            "all 10,000 residents should advance through the same population simulation tick");

        var recruitmentSimulation = new ColonySimulation();
        int recruited = recruitmentSimulation.RecruitColonists(25);
        Check(recruited == 25 && recruitmentSimulation.Workers.Count == 28,
            "the colony should recruit real individual residents in batches without a tiny population cap");
        Check(recruitmentSimulation.Workers.Select(worker => worker.Id).Distinct().Count() == 28 &&
              recruitmentSimulation.Workers.Select(worker => worker.Name).Distinct().Count() == 28,
            "recruited residents must receive unique IDs and names");
        Check(recruitmentSimulation.Workers.Skip(3).All(worker =>
                recruitmentSimulation.CitizenForWorker(worker.Id) != null),
            "every recruited resident must be connected to the needs and job simulation");
        string recruitmentSave = ColonySaveService.Serialize(recruitmentSimulation);
        var restoredRecruitment = ColonySaveService.Deserialize(recruitmentSave);
        Check(restoredRecruitment.Workers.Count == 28 &&
              restoredRecruitment.Workers.Select(worker => worker.Id).Distinct().Count() == 28,
            "recruited population and unique identities must survive save/load");
        int afterLoadRecruitment = restoredRecruitment.RecruitColonists(1);
        Check(afterLoadRecruitment == 1 && restoredRecruitment.Workers.Count == 29 &&
              restoredRecruitment.Workers.Select(worker => worker.Id).Distinct().Count() == 29,
            "recruitment after loading must continue the ID sequence without collisions");

        var pathCacheColony = new ColonySimulation();
        var pathStart = new CellPosition(2, 5);
        var pathTarget = new CellPosition(8, 5);
        Check(pathCacheColony.TryFindPathForDiagnostics(pathStart, pathTarget, out var firstRoute),
            "the path-cache fixture should find an open route");
        int originalRouteLength = firstRoute.Count;
        firstRoute.Clear();
        Check(pathCacheColony.TryFindPathForDiagnostics(pathStart, pathTarget, out var secondRoute) &&
              secondRoute.Count == originalRouteLength,
            "cached routes must be copied so callers cannot corrupt the cache");
        Check(pathCacheColony.PathfindingSearches == 1 && pathCacheColony.PathCacheHits == 1 &&
              pathCacheColony.CachedPathCount == 1,
            "repeating a route query should reuse the bounded path cache rather than rerun breadth-first search");

        var stressSimulation = new ColonyEcosystem(4, 4);
        var stressedCitizen = stressSimulation.AddCitizen("Stress probe", new CellPosition(0, 0));
        stressedCitizen.Hunger = 0f;
        stressedCitizen.Fatigue = 0f;
        stressSimulation.Step(60f);
        Check(stressedCitizen.State == CitizenState.MentalBreak && stressedCitizen.Stress >= 80f &&
              stressedCitizen.Mood < 10f,
            "sustained unmet needs must trigger a mental-break state and lower mood");
        var breakStarted = stressSimulation.DrainEvents().Single(item =>
            item.Kind == SimulationEventKind.MentalBreakStarted && item.SubjectId == stressedCitizen.Id);
        Check(breakStarted.Tick == stressSimulation.SimulationTick && breakStarted.Value >= 80f,
            "mental-break start events must carry stable identity, deterministic tick and stress severity");
        stressSimulation.Step(30f);
        Check(stressedCitizen.State == CitizenState.Idle && stressedCitizen.Stress <= 45f,
            "the mental-break state must recover below its lower hysteresis threshold");
        Check(stressSimulation.DrainEvents().Any(item =>
            item.Kind == SimulationEventKind.MentalBreakEnded && item.SubjectId == stressedCitizen.Id),
            "mental-break recovery must emit a deterministic end event");

        var flowGrid = new GridManager(6, 4);
        var flowTarget = new CellPosition(5, 0);
        Check(flowGrid.FindPath(new CellPosition(0, 0), flowTarget)?.Count == 5,
            "the first path to a shared destination should produce a shortest route");
        Check(flowGrid.FindPath(new CellPosition(0, 1), flowTarget)?.Count == 6 &&
              flowGrid.FlowFieldBuilds == 1 && flowGrid.FlowFieldCacheHits == 1 &&
              flowGrid.CachedFlowFieldCount == 1,
            "different residents sharing a destination should reuse one target-oriented flow field");
        flowGrid.SetWall(new CellPosition(3, 0));
        Check(flowGrid.FindPath(new CellPosition(0, 0), flowTarget)?.Count > 5 &&
              flowGrid.FlowFieldBuilds == 2 && flowGrid.CachedFlowFieldCount == 1,
            "changing walkability must invalidate stale flow fields and rebuild a valid detour");

        var wallGrid = new GridManager(3, 3);
        wallGrid.SetWall(new CellPosition(1, 0));
        wallGrid.SetWall(new CellPosition(0, 1));
        Check(wallGrid.FindPath(new CellPosition(0, 0), new CellPosition(1, 0)) == null,
            "pathfinding should reject a blocked destination tile");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Colony ecosystem check failed: {message}");
    }
}
