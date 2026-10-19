using System.Text.Json;
using KorTheBlight.Simulation;
using KorTheBlight.WorldModel;

namespace KorTheBlight.Tests;

internal static class IntegrationChecks
{
    public static void Run()
    {
        var world = new WorldSimulation(70241);
        var home = world.SpatialWorld.Entities.Single(entity =>
            entity.Kind == WorldEntityKind.Settlement && entity.Faction == "Kor");
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
        var restoredHome = restored.ContinuousWorld.Entities.Single(entity => entity.Id == home.Id);
        Check(restored.Colony.ResourceCount(ResourceKind.Wood) == wood &&
              (int)restored.ContinuousWorld.InventoryAt(restoredHome.Id, WorldResourceKind.Wood) == wood,
            "session save/load must restore one inventory balance for both simulation views");

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

    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException($"Integration check failed: {reason}");
    }
}
