using KorTheBlight.WorldModel;

namespace KorTheBlight.Tests;

internal static class WorldSpatialIndexChecks
{
    public static void Run()
    {
        var world = ContinuousWorldState.Generate(seed: 81357);
        var indexed = world.QueryRadius(50_000, 50_000, 6_000);
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

        int initialRebuildCount = world.SpatialIndexRebuildCount;
        world.Step(1);
        Check(world.SpatialIndexRebuildCount == initialRebuildCount,
            "stationary world ticks must not rebuild the spatial index");

        var moving = world.Entities.Single(entity => entity.Kind == WorldEntityKind.ResourceSite);
        double oldX = moving.X, oldY = moving.Y;
        Check(world.QueryRadius(oldX, oldY, 1).Any(entity => entity.Id == moving.Id),
            "a freshly built spatial index must include the resource site");
        int rebuildsAfterInitialQuery = world.SpatialIndexRebuildCount;
        Check(world.OrderMove(moving.Id, oldX + 1_500, oldY + 1_500, 2_000),
            "world entities should accept a move order");
        world.Step(2);
        Check(!world.QueryRadius(oldX, oldY, 1).Any(entity => entity.Id == moving.Id),
            "movement must invalidate the old spatial buckets");
        Check(world.QueryRadius(oldX + 1_500, oldY + 1_500, 1).Any(entity => entity.Id == moving.Id),
            "movement must make the entity visible in its new spatial bucket");
        Check(world.SpatialIndexRebuildCount == rebuildsAfterInitialQuery,
            "moving entities across buckets must update the index incrementally without a full rebuild");

        Check(world.OrderMove(moving.Id, oldX + 1_550, oldY + 1_550, 100),
            "entities should accept a short move within their current bucket");
        world.Step(1);
        Check(world.QueryRadius(oldX + 1_550, oldY + 1_550, 1).Any(entity => entity.Id == moving.Id),
            "same-bucket movement must remain visible without changing bucket membership");
        Check(world.SpatialIndexRebuildCount == rebuildsAfterInitialQuery,
            "same-bucket movement must not trigger a full spatial-index rebuild");

        var added = world.AddEntity(WorldEntityKind.Army, "Index invalidation probe", "Kor", 12_345, 23_456);
        Check(world.QueryRadius(12_345, 23_456, 0).Any(entity => entity.Id == added.Id),
            "adding an entity must invalidate and refresh the spatial index");

        var index = new WorldSpatialIndex(ContinuousWorldState.WorldExtent);
        index.Rebuild(world.Entities);
        Check(index.BucketCount > 0 && index.QueryCandidates(12_345, 23_456, 0).Any(entity => entity.Id == added.Id),
            "spatial buckets must include entities on their exact bucket coordinate");

        var boundaryWorld = ContinuousWorldState.Generate(seed: 81358);
        var boundaryEntity = boundaryWorld.AddEntity(WorldEntityKind.Army, "Bucket boundary probe", "Kor", 1_000.0000001, 2_000);
        Check(boundaryWorld.QueryRadius(boundaryEntity.X, boundaryEntity.Y, 0).Any(entity => entity.Id == boundaryEntity.Id),
            "the boundary probe must enter the initial spatial index");
        Check(boundaryWorld.OrderMove(boundaryEntity.Id, 999.9999999, 2_000, 1),
            "near-zero movement orders must be accepted");
        Check(boundaryWorld.QueryRadius(999.9999999, 2_000, 0).Any(entity => entity.Id == boundaryEntity.Id) &&
              !boundaryWorld.QueryRadius(1_000.0000001, 2_000, 0).Any(entity => entity.Id == boundaryEntity.Id),
            "an immediate snap across a bucket boundary must update spatial membership");
    }

    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException($"World spatial index check failed: {reason}");
    }
}
