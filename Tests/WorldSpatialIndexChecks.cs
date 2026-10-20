using KorTheBlight.WorldModel;

namespace KorTheBlight.Tests;

internal static class WorldSpatialIndexChecks
{
    public static void Run()
    {
        var world = ContinuousWorldState.Generate(seed: 81357);
        var indexed = world.QueryRadius(50_000, 50_000, 6_000);
        int initialBuilds = world.SpatialIndexRebuildCount;
        Check(initialBuilds == 1, "the first radius query should build the spatial index exactly once");
        world.Step(1);
        Check(world.SpatialIndexRebuildCount == initialBuilds,
            "stationary world ticks must not trigger a full spatial index rebuild");
        var expected = world.Entities
            .Where(entity =>
            {
                double dx = entity.X - 50_000, dy = entity.Y - 50_000;
                return dx * dx + dy * dy <= 6_000d * 6_000d;
            })
            .OrderBy(entity =>
            {
                double dx = entity.X - 50_000, dy = entity.Y - 50_000;
                return dx * dx + dy * dy;
            })
            .ThenBy(entity => entity.Id)
            .Select(entity => entity.Id)
            .ToArray();
        Check(indexed.Select(entity => entity.Id).SequenceEqual(expected),
            "spatial buckets must match a full scan and preserve distance/ID ordering");

        var moving = world.Entities.Single(entity => entity.Kind == WorldEntityKind.ResourceSite);
        double oldX = moving.X, oldY = moving.Y;
        Check(world.QueryRadius(oldX, oldY, 1).Any(entity => entity.Id == moving.Id),
            "a freshly built spatial index must include the resource site");
        int buildsBeforeMoveOrder = world.SpatialIndexRebuildCount;
        Check(world.OrderMove(moving.Id, oldX + 1_500, oldY + 1_500, 2_000),
            "world entities should accept a move order");
        Check(world.SpatialIndexRebuildCount == buildsBeforeMoveOrder,
            "a destination-only order must not rebuild the index before the entity moves");
        world.Step(2);
        Check(!world.QueryRadius(oldX, oldY, 1).Any(entity => entity.Id == moving.Id),
            "movement must invalidate the old spatial buckets");
        Check(world.QueryRadius(oldX + 1_500, oldY + 1_500, 1).Any(entity => entity.Id == moving.Id),
            "movement must make the entity visible in its new spatial bucket");
        Check(world.SpatialIndexRebuildCount == buildsBeforeMoveOrder + 1,
            "moving entities should cause exactly one lazy spatial index rebuild");

        var added = world.AddEntity(WorldEntityKind.Army, "Index invalidation probe", "Kor", 12_345, 23_456);
        Check(world.QueryRadius(12_345, 23_456, 0).Any(entity => entity.Id == added.Id),
            "adding an entity must invalidate and refresh the spatial index");

        var index = new WorldSpatialIndex(ContinuousWorldState.WorldExtent);
        index.Rebuild(world.Entities);
        Check(index.BucketCount > 0 && index.QueryCandidates(12_345, 23_456, 0).Any(entity => entity.Id == added.Id),
            "spatial buckets must include entities on their exact bucket coordinate");
    }

    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException($"World spatial index check failed: {reason}");
    }
}
