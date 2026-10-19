using KorTheBlight.WorldModel;
using KorTheBlight.Rendering;

namespace KorTheBlight.Tests;

internal static class ContinuousWorldChecks
{
    public static void Run()
    {
        var world = ContinuousWorldState.Generate(70241);
        var projected = ContinuousWorldProjection.WorldToScreen(50_000, 50_000, 50_000, 50_000, .65f);
        var unprojected = ContinuousWorldProjection.ScreenToWorld(projected.X, projected.Y, 50_000, 50_000, .65f);
        Check(Math.Abs(unprojected.X - 50_000) < .01 && Math.Abs(unprojected.Y - 50_000) < .01,
            "continuous world projection must round-trip world coordinates");
        Check(ContinuousWorldProjection.Scale(.08f) < ContinuousWorldProjection.Scale(.65f),
            "strategic zoom must smoothly scale the continuous world representation");
        var home = world.Entities.Single(entity => entity.Faction == "Kor" && entity.Kind == WorldEntityKind.Settlement);
        var nearby = world.QueryRadius(home.X, home.Y, 1);
        Check(nearby.Count == 1 && nearby[0].Id == home.Id,
            "continuous spatial queries must use distance rather than hex adjacency");

        Check(world.OrderMove(home.Id, home.X + 20, home.Y, 10),
            "world entities must accept coordinate destinations");
        world.Step(1);
        Check(Math.Abs(home.X - 50_010) < 0.0001 && home.IsMoving,
            "entity movement must progress continuously according to speed and elapsed time");
        world.Step(1);
        Check(Math.Abs(home.X - 50_020) < 0.0001 && !home.IsMoving,
            "entity movement must stop exactly at its destination without overshoot");
        Check(world.ClockSeconds == 2,
            "continuous world state must maintain its own serializable simulation clock");

        var destination = world.Entities.First(entity => entity.Kind == WorldEntityKind.Settlement && entity.Faction == "Neutral");
        var caravan = world.Entities.Single(entity => entity.Kind == WorldEntityKind.Caravan);
        double woodBefore = world.Inventories.Values.Sum(inventory => inventory.GetValueOrDefault(WorldResourceKind.Wood));
        Check(world.CreateShipment(caravan.Id, home.Id, destination.Id, WorldResourceKind.Wood, 10, 1_000),
            "a stocked settlement must be able to dispatch a real cargo shipment");
        Check(Math.Abs(world.InventoryAt(home.Id, WorldResourceKind.Wood) - 10) < 0.0001 &&
              Math.Abs(world.InventoryAt(destination.Id, WorldResourceKind.Wood)) < 0.0001,
            "dispatch must reserve cargo at origin and must not teleport it to the destination");
        world.Step(1);
        Check(world.Shipments.Single().Status == WorldShipmentStatus.InTransit,
            "cargo must remain in transit until travel distance is covered");
        string inTransitJson = ContinuousWorldState.Serialize(world);
        var inTransitRestore = ContinuousWorldState.Deserialize(inTransitJson);
        Check(inTransitRestore.Shipments.Single().Status == WorldShipmentStatus.InTransit &&
              Math.Abs(inTransitRestore.InventoryAt(home.Id, WorldResourceKind.Wood) - 10) < 0.0001,
            "save/load must preserve in-flight cargo and the origin's already-debited inventory");
        world.Step(10);
        Check(world.Shipments.Single().Status == WorldShipmentStatus.Delivered &&
              Math.Abs(world.InventoryAt(destination.Id, WorldResourceKind.Wood) - 10) < 0.0001,
            "cargo must be delivered only after its caravan reaches the destination");
        double woodAfter = world.Inventories.Values.Sum(inventory => inventory.GetValueOrDefault(WorldResourceKind.Wood));
        Check(Math.Abs(woodBefore - woodAfter) < 0.0001,
            "world shipments must conserve resources across origin, cargo and destination");

        string json = ContinuousWorldState.Serialize(world);
        var restored = ContinuousWorldState.Deserialize(json);
        var restoredHome = restored.Entities.Single(entity => entity.Id == home.Id);
        Check(restored.ClockSeconds == world.ClockSeconds &&
              Math.Abs(restoredHome.X - home.X) < 0.0001 &&
              restored.Entities.Count == world.Entities.Count,
            "continuous world save/load must preserve clock, entities and positions");

        CheckThrows(() => world.OrderMove(home.Id, 50_030, 50_000, 0),
            "movement speed must be positive");
        CheckThrows(() => world.QueryRadius(50_000, 50_000, double.NaN),
            "invalid spatial query radii must be rejected");
    }

    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException($"Continuous world check failed: {reason}");
    }

    private static void CheckThrows(Action action, string reason)
    {
        try { action(); }
        catch (ArgumentOutOfRangeException) { return; }
        throw new InvalidOperationException($"Continuous world check failed: {reason}");
    }
}
