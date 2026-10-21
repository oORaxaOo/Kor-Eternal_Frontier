using System.Text.Json.Nodes;
using KorTheBlight.WorldModel;

namespace KorTheBlight.Tests;

internal static class RegionalWorldMapChecks
{
    public static void Run()
    {
        var first = RegionalWorldMap.Generate(31415);
        var repeat = RegionalWorldMap.Generate(31415);
        var otherSeed = RegionalWorldMap.Generate(31416);

        Check(first.Regions.Count == RegionalWorldMap.Columns * RegionalWorldMap.Rows,
            "regional generation must cover the full strategic world");
        Check(first.Regions.Select(Signature).SequenceEqual(repeat.Regions.Select(Signature)),
            "the same world seed must generate identical region terrain and local-map seeds");
        Check(first.Regions.Where((region, index) => Signature(region) != Signature(otherSeed.Regions[index])).Any(),
            "different world seeds must produce a distinct regional world");
        Check(first.Regions.Select(region => region.LocalMapSeed).Distinct().Count() > 390,
            "regions must receive independent deterministic local-map seeds");

        var northwest = first.RegionAt(0, 0);
        var southeast = first.RegionAt(ContinuousWorldState.WorldExtent, ContinuousWorldState.WorldExtent);
        Check(northwest.X == 0 && northwest.Y == 0 &&
              southeast.X == RegionalWorldMap.Columns - 1 && southeast.Y == RegionalWorldMap.Rows - 1,
            "world-coordinate lookup must handle map edges without indexing outside the region grid");
        CheckThrows(() => first.RegionAt(-1, 0),
            "regional lookup must reject coordinates outside the world");

        var world = ContinuousWorldState.Generate(31415);
        string json = ContinuousWorldState.Serialize(world);
        var restored = ContinuousWorldState.Deserialize(json);
        Check(restored.RegionalMap.Regions.Select(Signature).SequenceEqual(world.RegionalMap.Regions.Select(Signature)),
            "session serialization must preserve generated regional terrain and local-map seeds");

        var legacyJson = JsonNode.Parse(json)!.AsObject();
        legacyJson.Remove("RegionalMap");
        var legacyRestored = ContinuousWorldState.Deserialize(legacyJson.ToJsonString());
        Check(legacyRestored.RegionalMap.Seed == world.Seed &&
              legacyRestored.RegionalMap.Regions.Select(Signature).SequenceEqual(world.RegionalMap.Regions.Select(Signature)),
            "older continuous-world saves without regional data must regenerate it from their existing seed");

        var invalid = RegionalWorldMap.Generate(7);
        invalid.Regions[1].X = invalid.Regions[0].X;
        invalid.Regions[1].Y = invalid.Regions[0].Y;
        CheckThrows(() => invalid.Validate(),
            "duplicate region coordinates must be rejected during save validation");
    }

    private static string Signature(WorldRegionState region)
        => $"{region.X}:{region.Y}:{region.Biome}:{region.Elevation:R}:{region.Moisture:R}:{region.Temperature:R}:{region.LocalMapSeed}";

    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException($"Regional world map check failed: {reason}");
    }

    private static void CheckThrows(Action action, string reason)
    {
        try { action(); }
        catch (Exception error) when (error is ArgumentOutOfRangeException or InvalidDataException) { return; }
        throw new InvalidOperationException($"Regional world map check failed: {reason}");
    }
}
