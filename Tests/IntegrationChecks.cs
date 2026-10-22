using System.Text.Json;
using KorTheBlight.Simulation;
using KorTheBlight.WorldModel;

namespace KorTheBlight.Tests;

internal static class IntegrationChecks
{
    public static void Run()
    {
        CheckSafetySystem();

        var world = new WorldSimulation(70241);
        var home = world.SpatialWorld.Entities.Single(entity =>
            entity.Kind == WorldEntityKind.Settlement && entity.Faction == "Kor");

        // Social and incident state must survive the canonical unified-session save.
        for (int tick = 1; tick <= 25; tick++)
            world.Colony.SocialRelationships.RecordEncounter(1, 2, tick, 100, 100, 100, true, 100, 100, 100, true);
        world.Colony.Incidents.Consume(world.Colony.SocialRelationships.DrainEvents());
        Check(world.Colony.SocialRelationships.Get(1, 2)?.Affinity <= -60 &&
              world.Colony.Incidents.Incidents.Any(item => item.Code == "resident_conflict"),
            "resident conflict milestones must produce an auditable incident");
        int wood = world.Colony.ResourceCount(ResourceKind.Wood);
        Check(wood == (int)world.SpatialWorld.InventoryAt(home.Id, WorldResourceKind.Wood),
            "local colony and strategic world must expose one shared resource balance");

        string json = GameSessionService.Serialize(world);
        using (var envelope = JsonDocument.Parse(json))
        {
            string colonyJson = envelope.RootElement.GetProperty("ColonyJson").GetString()
                ?? throw new InvalidDataException("Session did not contain colony state.");
            using var colony = JsonDocument.Parse(colonyJson);
            Check(colony.RootElement.GetProperty("InventoryOwnedByWorld").GetBoolean() &&
                  colony.RootElement.GetProperty("Inventory").GetArrayLength() == 0,
                "session persistence must not duplicate the canonical resource ledger in the colony snapshot");
        }

        var restored = GameSessionService.Deserialize(json);
        Check(restored.Colony.SocialRelationships.Get(2, 1)?.EncounterCount == 25 &&
              restored.Colony.Incidents.Incidents.Any(item => item.Code == "resident_conflict"),
            "unified session save/load must preserve relationship and incident history");
        var restoredHome = restored.ContinuousWorld.Entities.Single(entity => entity.Id == home.Id);
        Check(restored.Colony.ResourceCount(ResourceKind.Wood) == wood &&
              (int)restored.ContinuousWorld.InventoryAt(restoredHome.Id, WorldResourceKind.Wood) == wood,
            "session save/load must restore one inventory balance for both simulation views");

        // A built garden must produce through the citizen/job bridge while the world settlement
        // remains the only authoritative inventory owner.
        var productionWorld = new WorldSimulation(70242);
        var productionHome = productionWorld.SpatialWorld.Entities.Single(entity =>
            entity.Kind == WorldEntityKind.Settlement && entity.Faction == "Kor");
        int foodBeforeGarden = productionWorld.Colony.ResourceCount(ResourceKind.Food);
        int gardenId = productionWorld.Colony.PlaceObjectBlueprint(PlaceableKind.Garden, 24, 17);
        Check(gardenId > 0, "the unified world should accept a garden blueprint");
        for (int i = 0; i < 2500; i++) productionWorld.Step(0.1f);
        Check(productionWorld.Colony.Objects.Single(item => item.Id == gardenId).IsBuilt,
            "garden construction must progress under the unified world clock");
        int foodAfterGarden = productionWorld.Colony.ResourceCount(ResourceKind.Food);
        Check(foodAfterGarden > foodBeforeGarden,
            "citizen jobs must deliver garden output into the canonical settlement inventory");
        Check((int)productionWorld.SpatialWorld.InventoryAt(productionHome.Id, WorldResourceKind.Food) == foodAfterGarden &&
              productionWorld.Colony.InventoryUsed <= productionWorld.Colony.InventoryCapacity,
            "garden production must keep colony and world inventory consistent without exceeding storage");

        string directory = Path.Combine(Path.GetTempPath(), "kor-session-check-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "session.json");
        try
        {
            GameSessionService.Save(world, path);
            var fromDisk = GameSessionService.Load(path);
            Check(fromDisk.Colony.ResourceCount(ResourceKind.Wood) == wood &&
                  (int)fromDisk.ContinuousWorld.InventoryAt(home.Id, WorldResourceKind.Wood) == wood,
                "atomic disk save/load must preserve the canonical resource ledger");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static void CheckSafetySystem()
    {
        var safety = new ColonySafetySystem();
        var sensor = new SafetySensor(2, new CellPosition(4, 4), detectionRadius: 3,
            powerPerSecond: 2f, woodCost: 3, componentCost: 1);
        Check(safety.TryInstallSensor(sensor) && !safety.TryInstallSensor(sensor),
            "safety sensors must have unique stable IDs");

        var fire = new ColonyHazard(10, ColonyHazardKind.Fire, new CellPosition(6, 4), 75f);
        var detected = safety.Step(1f, availablePower: 2f, [fire]);
        Check(detected.Count == 1 && detected[0].SensorId == 2 &&
              detected[0].HazardId == 10 && detected[0].Kind == ColonyHazardKind.Fire,
            "a powered sensor must emit one alarm for an in-range hazard");
        Check(Math.Abs(safety.PowerUsedLastStep - 2f) < 0.001f,
            "powered sensors must account for their energy demand");
        Check(safety.Step(1f, availablePower: 2f, [fire]).Count == 0,
            "a persistent hazard must not spam duplicate alarms each tick");
        Check(safety.Step(1f, availablePower: 0f, [fire, new ColonyHazard(11,
                ColonyHazardKind.StructuralInstability, new CellPosition(4, 5), 40f)]).Count == 0,
            "a sensor without its required power must not report a new hazard");
        Check(safety.MaintainSensor(2, 10f) && sensor.Condition > 99f,
            "maintenance must restore sensor condition without exceeding its maximum");
        Check(safety.Step(1f, availablePower: 2f, [fire,
                new ColonyHazard(11, ColonyHazardKind.StructuralInstability, new CellPosition(4, 5), 40f)]).Count == 1,
            "a newly appearing hazard must produce a separate deterministic alarm");
        Check(safety.Alarms.Count == 2 && safety.Tick == 4,
            "the alarm history and fixed-step counter must remain consistent");
        Check(!safety.MaintainSensor(999, 10f) && safety.RemoveSensor(2) && safety.Sensors.Count == 0,
            "unknown sensor maintenance must fail safely and removal must clear the sensor");
    }

    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException($"Integration check failed: {reason}");
    }
}
