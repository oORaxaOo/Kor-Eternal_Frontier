namespace KorTheBlight.WorldModel;

internal enum WorldBiomeKind
{
    Plains,
    Forest,
    Desert,
    Tundra,
    Wetland,
    Mountain
}

internal sealed class WorldRegionState
{
    public int X { get; set; }
    public int Y { get; set; }
    public WorldBiomeKind Biome { get; set; }
    public float Elevation { get; set; }
    public float Moisture { get; set; }
    public float Temperature { get; set; }
    public int LocalMapSeed { get; set; }
}

/// <summary>
/// Deterministic regional layer above the local settlement grid. Each region owns a stable seed
/// that can be used to generate its detailed local map on demand.
/// </summary>
internal sealed class RegionalWorldMap
{
    public const int RegionSize = 5_000;
    public const int Columns = 20;
    public const int Rows = 20;
    public int Seed { get; set; }
    public List<WorldRegionState> Regions { get; set; } = [];

    public static RegionalWorldMap Generate(int seed)
    {
        var map = new RegionalWorldMap { Seed = seed };
        for (int y = 0; y < Rows; y++)
        for (int x = 0; x < Columns; x++)
        {
            float elevation = Sample(seed, x, y, 0x51ED270B);
            float moisture = Sample(seed, x, y, 0x2C1B3C6D);
            float latitude = Math.Abs(((y + 0.5f) / Rows) * 2f - 1f);
            float temperature = Math.Clamp(1f - latitude * 0.72f + (Sample(seed, x, y, 0x165667B1) - 0.5f) * 0.3f, 0f, 1f);
            WorldBiomeKind biome = elevation > 0.84f ? WorldBiomeKind.Mountain
                : temperature < 0.25f ? WorldBiomeKind.Tundra
                : moisture < 0.19f ? WorldBiomeKind.Desert
                : moisture > 0.79f ? WorldBiomeKind.Wetland
                : moisture > 0.52f ? WorldBiomeKind.Forest
                : WorldBiomeKind.Plains;

            map.Regions.Add(new WorldRegionState
            {
                X = x,
                Y = y,
                Biome = biome,
                Elevation = elevation,
                Moisture = moisture,
                Temperature = temperature,
                LocalMapSeed = (int)(Hash(seed, x, y, 0x6D2B79F5) & 0x7fffffff)
            });
        }

        map.Validate();
        return map;
    }

    public WorldRegionState RegionAt(double worldX, double worldY)
    {
        if (!double.IsFinite(worldX) || !double.IsFinite(worldY) ||
            worldX < 0 || worldY < 0 ||
            worldX > ContinuousWorldState.WorldExtent || worldY > ContinuousWorldState.WorldExtent)
            throw new ArgumentOutOfRangeException(nameof(worldX), "World coordinates must be inside the regional map bounds.");

        int x = Math.Min((int)(worldX / RegionSize), Columns - 1);
        int y = Math.Min((int)(worldY / RegionSize), Rows - 1);
        return Regions.Single(region => region.X == x && region.Y == y);
    }

    public void Validate()
    {
        if (Regions is null || Regions.Count != Columns * Rows)
            throw new InvalidDataException("Regional world map must contain exactly 400 regions.");

        var coordinates = new HashSet<(int X, int Y)>();
        foreach (var region in Regions)
        {
            if (region is null || region.X < 0 || region.X >= Columns || region.Y < 0 || region.Y >= Rows ||
                !coordinates.Add((region.X, region.Y)) || !Enum.IsDefined(region.Biome) ||
                !float.IsFinite(region.Elevation) || region.Elevation is < 0 or > 1 ||
                !float.IsFinite(region.Moisture) || region.Moisture is < 0 or > 1 ||
                !float.IsFinite(region.Temperature) || region.Temperature is < 0 or > 1 ||
                region.LocalMapSeed < 0)
                throw new InvalidDataException("Regional world map contains an invalid region.");
        }
    }

    private static float Sample(int seed, int x, int y, uint salt)
        => (Hash(seed, x, y, salt) & 0x00ffffff) / 16777215f;

    private static uint Hash(int seed, int x, int y, uint salt)
    {
        unchecked
        {
            uint value = (uint)seed ^ salt;
            value ^= (uint)(x + 1) * 0x9E3779B9;
            value = (value ^ (value >> 16)) * 0x7FEB352D;
            value ^= (uint)(y + 1) * 0x846CA68B;
            value = (value ^ (value >> 15)) * 0x2C1B3C6D;
            return value ^ (value >> 16);
        }
    }
}
