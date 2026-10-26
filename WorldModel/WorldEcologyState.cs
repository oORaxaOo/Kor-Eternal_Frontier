namespace KorTheBlight.WorldModel;

internal enum WildlifeSpecies { LonghornGrazer, AshbackGrazer, DuskStalker }
internal enum WorldEventKind { WeatherShift, HerdMigration, BlightSurge, RareSighting, Drought }
internal enum WorldSeason { Spring, Summer, Autumn, Winter }
internal enum WorldWeather { Clear, Rain, Storm, Heatwave, Frost, Snow, Drought }

internal sealed class WildlifeHerdState
{
    public long Id { get; set; }
    public WildlifeSpecies Species { get; set; }
    public int Population { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double TargetX { get; set; }
    public double TargetY { get; set; }
    public double Speed { get; set; }
    public double NextMigrationAt { get; set; }
    public double NextPopulationChangeAt { get; set; }
    public double Forage { get; set; } = 1d;
}

internal sealed class BlightHotspotState
{
    public long Id { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Radius { get; set; }
    public double Intensity { get; set; }
    public double NextSpreadAt { get; set; }
    public bool IsActive { get; set; } = true;
}

internal sealed class WorldEventState
{
    public long Id { get; set; }
    public WorldEventKind Kind { get; set; }
    public double OccurredAt { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
}

/// <summary>
/// Low-cost background ecology: herds migrate, populations react to habitat pressure, Blight
/// hotspots expand/spread, and a deterministic event stream gives the strategic world a history.
/// The simulation tracks regional groups rather than individual animals.
/// </summary>
internal sealed class WorldEcologyState
{
    public const double SeasonDurationSeconds = 3_600d;
    public const double WeatherIntervalSeconds = 300d;

    public WorldSeason CurrentSeason { get; set; } = WorldSeason.Spring;
    public WorldWeather CurrentWeather { get; set; } = WorldWeather.Clear;
    public double FoodGrowthMultiplier => SeasonFoodMultiplier(CurrentSeason) * WeatherFoodMultiplier(CurrentWeather);

    public long NextHerdId { get; set; } = 1;
    public long NextHotspotId { get; set; } = 1;
    public long NextEventId { get; set; } = 1;
    public double NextWorldEventAt { get; set; } = 900d;
    public List<WildlifeHerdState> Herds { get; set; } = [];
    public List<BlightHotspotState> Hotspots { get; set; } = [];
    public List<WorldEventState> RecentEvents { get; set; } = [];

    public void Initialize(int seed)
    {
        if (Herds.Count != 0 || Hotspots.Count != 0) return;
        var random = new Random(seed ^ 0x4B4F52);
        AddHerd(WildlifeSpecies.LonghornGrazer, 34, 35_000, 39_000, random, 0);
        AddHerd(WildlifeSpecies.AshbackGrazer, 21, 73_000, 69_000, random, 0);
        AddHerd(WildlifeSpecies.DuskStalker, 8, 29_000, 66_000, random, 0);
        AddHotspot(64_000, 46_500, 3_500, .52, 500);
        NextWorldEventAt = 900 + random.Next(0, 300);
        SynchronizeClimate(0, seed);
    }

    public void Step(double deltaSeconds, double worldClock, int seed)
    {
        if (deltaSeconds <= 0) return;
        SynchronizeClimate(worldClock, seed);
        foreach (var herd in Herds)
        {
            double seasonalForageRate = CurrentSeason switch
            {
                WorldSeason.Spring => .00010,
                WorldSeason.Summer => .00013,
                WorldSeason.Autumn => .00004,
                WorldSeason.Winter => -.00004,
                _ => 0d
            };
            double weatherForageRate = CurrentWeather switch
            {
                WorldWeather.Rain => .00005,
                WorldWeather.Clear => .00001,
                WorldWeather.Storm => -.00002,
                WorldWeather.Heatwave => -.00007,
                WorldWeather.Frost => -.00008,
                WorldWeather.Snow => -.00005,
                WorldWeather.Drought => -.00012,
                _ => 0d
            };
            double pressureLoss = IsDangerousAt(herd.X, herd.Y) ? .0003 : 0d;
            herd.Forage = Math.Clamp(herd.Forage +
                deltaSeconds * (seasonalForageRate + weatherForageRate - pressureLoss), 0, 1);
            if (worldClock >= herd.NextMigrationAt || Distance(herd.X, herd.Y, herd.TargetX, herd.TargetY) < 100)
                ChooseMigrationTarget(herd, worldClock, seed);
            double dx = herd.TargetX - herd.X, dy = herd.TargetY - herd.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            double travel = herd.Speed * deltaSeconds;
            if (distance <= travel || distance < .001) { herd.X = herd.TargetX; herd.Y = herd.TargetY; }
            else { herd.X += dx / distance * travel; herd.Y += dy / distance * travel; }
            if (worldClock >= herd.NextPopulationChangeAt)
            {
                if (herd.Forage < .15 && herd.Population > 2)
                    herd.Population = Math.Max(2, herd.Population - 1);
                else if (herd.Forage > .8 && herd.Population < 80)
                    herd.Population++;
                herd.NextPopulationChangeAt = worldClock + 300 +
                    StableRoll(seed, herd.Id, (long)Math.Floor(worldClock / 300d), 300);
            }
        }

        foreach (var hotspot in Hotspots.Where(item => item.IsActive).ToArray())
        {
            hotspot.Intensity = Math.Min(1d, hotspot.Intensity + deltaSeconds * .0000025);
            hotspot.Radius = Math.Min(12_000d, hotspot.Radius + deltaSeconds * .08);
            if (worldClock >= hotspot.NextSpreadAt)
            {
                hotspot.NextSpreadAt = worldClock + 600 + StableRoll(seed, hotspot.Id, (long)worldClock, 600);
                if (hotspot.Intensity >= .58 && Hotspots.Count(item => item.IsActive) < 12 &&
                    StableRoll(seed, hotspot.Id, (long)worldClock, 100) < 38)
                {
                    double angle = StableRoll(seed, hotspot.Id, (long)worldClock + 1, 6283) / 1000d;
                    double distance = hotspot.Radius * (.45 + StableRoll(seed, hotspot.Id, (long)worldClock + 2, 500) / 1000d);
                    AddHotspot(
                        Math.Clamp(hotspot.X + Math.Cos(angle) * distance, 0, ContinuousWorldState.WorldExtent),
                        Math.Clamp(hotspot.Y + Math.Sin(angle) * distance, 0, ContinuousWorldState.WorldExtent),
                        1_200, .22, worldClock + 600);
                    AddEvent(WorldEventKind.BlightSurge, worldClock, "Blight bloom detected",
                        "A new Blight hotspot has emerged. Nearby routes and habitats are at risk.");
                }
            }
        }

        while (worldClock >= NextWorldEventAt)
        {
            EmitWorldEvent(worldClock, seed);
            NextWorldEventAt += 900 + StableRoll(seed, NextEventId, (long)NextWorldEventAt, 900);
        }
    }

    /// <summary>
    /// Reconstructs climate directly from the authoritative world clock and seed. This keeps
    /// season/weather deterministic across frame chunking, saves, and resumed sessions.
    /// </summary>
    public void SynchronizeClimate(double worldClock, int seed)
    {
        if (!double.IsFinite(worldClock) || worldClock < 0)
            throw new ArgumentOutOfRangeException(nameof(worldClock), "Climate time must be finite and non-negative.");

        long seasonIndex = (long)Math.Floor(worldClock / SeasonDurationSeconds);
        CurrentSeason = (WorldSeason)(seasonIndex % 4);
        long weatherSlot = (long)Math.Floor(worldClock / WeatherIntervalSeconds);
        CurrentWeather = WeatherForSlot(seed, weatherSlot);
    }

    private static WorldWeather WeatherForSlot(int seed, long slot)
    {
        long slotClock = slot * (long)WeatherIntervalSeconds;
        var season = (WorldSeason)(((long)Math.Floor(slotClock / SeasonDurationSeconds)) % 4);
        WorldWeather[] options = season switch
        {
            WorldSeason.Spring => [WorldWeather.Clear, WorldWeather.Rain, WorldWeather.Storm, WorldWeather.Drought],
            WorldSeason.Summer => [WorldWeather.Clear, WorldWeather.Rain, WorldWeather.Storm, WorldWeather.Heatwave, WorldWeather.Drought],
            WorldSeason.Autumn => [WorldWeather.Clear, WorldWeather.Rain, WorldWeather.Storm, WorldWeather.Drought],
            WorldSeason.Winter => [WorldWeather.Clear, WorldWeather.Storm, WorldWeather.Frost, WorldWeather.Snow],
            _ => [WorldWeather.Clear]
        };

        int choice = StableRoll(seed, 0x57454154484552L, slot, options.Length);
        if (slot > 0)
        {
            WorldWeather previous = WeatherForSlotRaw(seed, slot - 1);
            if (options[choice] == previous) choice = (choice + 1) % options.Length;
        }
        return options[choice];
    }

    private static WorldWeather WeatherForSlotRaw(int seed, long slot)
    {
        long slotClock = slot * (long)WeatherIntervalSeconds;
        var season = (WorldSeason)(((long)Math.Floor(slotClock / SeasonDurationSeconds)) % 4);
        WorldWeather[] options = season switch
        {
            WorldSeason.Spring => [WorldWeather.Clear, WorldWeather.Rain, WorldWeather.Storm, WorldWeather.Drought],
            WorldSeason.Summer => [WorldWeather.Clear, WorldWeather.Rain, WorldWeather.Storm, WorldWeather.Heatwave, WorldWeather.Drought],
            WorldSeason.Autumn => [WorldWeather.Clear, WorldWeather.Rain, WorldWeather.Storm, WorldWeather.Drought],
            WorldSeason.Winter => [WorldWeather.Clear, WorldWeather.Storm, WorldWeather.Frost, WorldWeather.Snow],
            _ => [WorldWeather.Clear]
        };
        return options[StableRoll(seed, 0x57454154484552L, slot, options.Length)];
    }

    private static double SeasonFoodMultiplier(WorldSeason season) => season switch
    {
        WorldSeason.Spring => 1.15d,
        WorldSeason.Summer => 1.35d,
        WorldSeason.Autumn => .90d,
        WorldSeason.Winter => .45d,
        _ => 1d
    };

    private static double WeatherFoodMultiplier(WorldWeather weather) => weather switch
    {
        WorldWeather.Clear => 1d,
        WorldWeather.Rain => 1.15d,
        WorldWeather.Storm => .65d,
        WorldWeather.Heatwave => .70d,
        WorldWeather.Frost => .45d,
        WorldWeather.Snow => .50d,
        WorldWeather.Drought => .35d,
        _ => 1d
    };

    public double EnvironmentalPressureAt(double x, double y)
    {
        double pressure = 0;
        foreach (var hotspot in Hotspots.Where(item => item.IsActive))
        {
            double distance = Distance(x, y, hotspot.X, hotspot.Y);
            if (distance >= hotspot.Radius) continue;
            pressure = Math.Max(pressure, hotspot.Intensity * (1 - distance / hotspot.Radius));
        }
        return Math.Clamp(pressure, 0, 1);
    }

    public bool IsDangerousAt(double x, double y) => EnvironmentalPressureAt(x, y) >= .55;

    private void AddHerd(WildlifeSpecies species, int population, double x, double y, Random random, double clock)
    {
        Herds.Add(new WildlifeHerdState
        {
            Id = NextHerdId++, Species = species, Population = population, X = x, Y = y,
            TargetX = x, TargetY = y, Speed = species == WildlifeSpecies.DuskStalker ? 95 : 55,
            NextMigrationAt = clock + 120 + random.Next(0, 180),
            NextPopulationChangeAt = clock + 300 + random.Next(0, 300),
            Forage = .65 + random.NextDouble() * .3
        });
    }

    private void AddHotspot(double x, double y, double radius, double intensity, double clock)
    {
        Hotspots.Add(new BlightHotspotState
        {
            Id = NextHotspotId++, X = x, Y = y, Radius = radius,
            Intensity = Math.Clamp(intensity, 0, 1), NextSpreadAt = clock
        });
    }

    private void ChooseMigrationTarget(WildlifeHerdState herd, double clock, int seed)
    {
        long roll = (long)(clock * 10) + herd.Id * 37;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            double angle = StableRoll(seed, herd.Id, roll + attempt, 6283) / 1000d;
            double distance = 2_500 + StableRoll(seed, herd.Id + attempt, roll + 19, 6_500);
            double x = Math.Clamp(herd.X + Math.Cos(angle) * distance, 500, ContinuousWorldState.WorldExtent - 500);
            double y = Math.Clamp(herd.Y + Math.Sin(angle) * distance, 500, ContinuousWorldState.WorldExtent - 500);
            if (!IsDangerousAt(x, y)) { herd.TargetX = x; herd.TargetY = y; break; }
        }
        herd.NextMigrationAt = clock + 240 + StableRoll(seed, herd.Id, roll + 31, 360);
    }

    private void EmitWorldEvent(double clock, int seed)
    {
        int roll = StableRoll(seed, NextEventId, (long)clock, 100);
        switch (roll % 5)
        {
            case 0:
                AddEvent(WorldEventKind.WeatherShift, clock, "Weather front moving in",
                    "Changing weather is reshaping forage and travel conditions across the region.");
                break;
            case 1:
                if (Herds.Count > 0)
                {
                    var herd = Herds[(int)(StableRoll(seed, NextEventId, 11, Herds.Count))];
                    herd.NextMigrationAt = Math.Min(herd.NextMigrationAt, clock);
                    AddEvent(WorldEventKind.HerdMigration, clock, $"{herd.Species} herd on the move",
                        $"A herd of {herd.Population} animals has begun migrating toward new grazing grounds.");
                }
                break;
            case 2:
                AddEvent(WorldEventKind.Drought, clock, "Regional forage decline",
                    "Dry conditions are reducing grazing quality. Herd populations may fall if conditions persist.");
                foreach (var herd in Herds) herd.Forage = Math.Max(0, herd.Forage - .12);
                break;
            case 3:
                AddEvent(WorldEventKind.RareSighting, clock, "Wildlife sighting reported",
                    "Scouts report unusual animal activity far from the settled roads.");
                break;
            default:
                var active = Hotspots.Where(item => item.IsActive).OrderByDescending(item => item.Intensity).FirstOrDefault();
                if (active != null) active.Intensity = Math.Min(1, active.Intensity + .08);
                AddEvent(WorldEventKind.BlightSurge, clock, "Blight activity intensifies",
                    "Blight pressure has increased. Exposed land may become unsafe for settlement.");
                break;
        }
    }

    private void AddEvent(WorldEventKind kind, double clock, string title, string description)
    {
        RecentEvents.Add(new WorldEventState { Id = NextEventId++, Kind = kind, OccurredAt = clock, Title = title, Description = description });
        if (RecentEvents.Count > 100) RecentEvents.RemoveAt(0);
    }

    private static int StableRoll(int seed, long a, long b, int max)
    {
        unchecked
        {
            uint value = (uint)seed;
            value = (value ^ (uint)a) * 16777619;
            value = (value ^ (uint)(a >> 32)) * 16777619;
            value = (value ^ (uint)b) * 16777619;
            value ^= value >> 13; value *= 1274126177; value ^= value >> 16;
            return (int)(value % (uint)Math.Max(1, max));
        }
    }

    public void Validate()
    {
        if (!Enum.IsDefined(CurrentSeason) || !Enum.IsDefined(CurrentWeather) ||
            Herds.Count > 10_000 || Hotspots.Count > 100 || RecentEvents.Count > 100 ||
            NextHerdId <= Herds.Select(item => item.Id).DefaultIfEmpty(0).Max() ||
            NextHotspotId <= Hotspots.Select(item => item.Id).DefaultIfEmpty(0).Max() ||
            NextEventId <= RecentEvents.Select(item => item.Id).DefaultIfEmpty(0).Max() ||
            !double.IsFinite(NextWorldEventAt) || NextWorldEventAt < 0)
            throw new InvalidDataException("World ecology counters are invalid.");
        foreach (var herd in Herds)
            if (herd.Id <= 0 || !Enum.IsDefined(herd.Species) || herd.Population < 0 ||
                !ValidPosition(herd.X, herd.Y) || !ValidPosition(herd.TargetX, herd.TargetY) ||
                !double.IsFinite(herd.Speed) || herd.Speed <= 0 ||
                !double.IsFinite(herd.Forage) || herd.Forage is < 0 or > 1 ||
                !double.IsFinite(herd.NextMigrationAt) || herd.NextMigrationAt < 0 ||
                !double.IsFinite(herd.NextPopulationChangeAt) || herd.NextPopulationChangeAt < 0)
                throw new InvalidDataException("Wildlife herd state is invalid.");
        foreach (var hotspot in Hotspots)
            if (hotspot.Id <= 0 || !ValidPosition(hotspot.X, hotspot.Y) ||
                !double.IsFinite(hotspot.Radius) || hotspot.Radius <= 0 ||
                !double.IsFinite(hotspot.Intensity) || hotspot.Intensity is < 0 or > 1 ||
                !double.IsFinite(hotspot.NextSpreadAt) || hotspot.NextSpreadAt < 0)
                throw new InvalidDataException("Blight hotspot state is invalid.");
        foreach (var item in RecentEvents)
            if (item.Id <= 0 || !Enum.IsDefined(item.Kind) || !double.IsFinite(item.OccurredAt) ||
                item.OccurredAt < 0 || string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.Description))
                throw new InvalidDataException("World event history is invalid.");
    }

    private static bool ValidPosition(double x, double y) =>
        double.IsFinite(x) && double.IsFinite(y) && x >= 0 && y >= 0 &&
        x <= ContinuousWorldState.WorldExtent && y <= ContinuousWorldState.WorldExtent;

    private static double Distance(double ax, double ay, double bx, double by)
    {
        double dx = ax - bx, dy = ay - by;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
