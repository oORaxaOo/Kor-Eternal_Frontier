namespace KorTheBlight.WorldModel;

/// <summary>Entity categories in continuous world space. This model deliberately has no tile/hex adjacency.</summary>
internal enum WorldEntityKind
{
    Settlement,
    ResourceSite,
    Caravan,
    Army,
    BlightFront
}

internal sealed class WorldEntityState
{
    public long Id { get; set; }
    public WorldEntityKind Kind { get; set; }
    public string Name { get; set; } = "Unnamed entity";
    public string Faction { get; set; } = "Neutral";
    public double X { get; set; }
    public double Y { get; set; }
    public double? DestinationX { get; set; }
    public double? DestinationY { get; set; }
    public double MovementSpeed { get; set; }
    public bool IsMoving => DestinationX.HasValue && DestinationY.HasValue;
}

/// <summary>
/// First continuous-space world layer. Positions and routes are expressed in world units rather
/// than linked map cells. Local settlement tiles remain a separate, zoomed-in gameplay detail.
/// </summary>
internal sealed class ContinuousWorldState
{
    public const double WorldExtent = 100_000d;

    public int SchemaVersion { get; set; } = 1;
    public int Seed { get; set; }
    public double ClockSeconds { get; set; }
    public long NextEntityId { get; set; } = 1;
    public List<WorldEntityState> Entities { get; set; } = [];

    public static ContinuousWorldState Generate(int seed)
    {
        var state = new ContinuousWorldState { Seed = seed };
        state.AddEntity(WorldEntityKind.Settlement, "Kor Hearthhold", "Kor", 50_000, 50_000);
        state.AddEntity(WorldEntityKind.Settlement, "Morrowstead", "Neutral", 54_200, 47_800);
        state.AddEntity(WorldEntityKind.Settlement, "Vesper Crossing", "Neutral", 45_700, 53_600);
        state.AddEntity(WorldEntityKind.Settlement, "Ironwood Enclave", "Neutral", 58_600, 54_100);
        state.AddEntity(WorldEntityKind.ResourceSite, "Westwood Deposit", "Neutral", 48_200, 51_900);
        state.AddEntity(WorldEntityKind.BlightFront, "Blight Expanse", "Blight", 64_000, 46_500);
        state.AddEntity(WorldEntityKind.Army, "Kor Vanguard", "Kor", 51_200, 50_800);
        state.AddEntity(WorldEntityKind.Caravan, "Hearthhold Caravan", "Kor", 49_100, 50_400);
        state.Validate();
        return state;
    }

    public WorldEntityState AddEntity(WorldEntityKind kind, string name, string faction, double x, double y)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Entity name cannot be empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(faction)) throw new ArgumentException("Faction cannot be empty.", nameof(faction));
        ValidatePosition(x, y);
        var entity = new WorldEntityState
        {
            Id = NextEntityId++, Kind = kind, Name = name, Faction = faction, X = x, Y = y
        };
        Entities.Add(entity);
        return entity;
    }

    public bool OrderMove(long entityId, double destinationX, double destinationY, double speed)
    {
        ValidatePosition(destinationX, destinationY);
        if (!double.IsFinite(speed) || speed <= 0)
            throw new ArgumentOutOfRangeException(nameof(speed), "Movement speed must be finite and positive.");
        var entity = Entities.FirstOrDefault(candidate => candidate.Id == entityId);
        if (entity == null) return false;
        entity.DestinationX = destinationX;
        entity.DestinationY = destinationY;
        entity.MovementSpeed = speed;
        if (Distance(entity.X, entity.Y, destinationX, destinationY) <= 0.000001)
        {
            entity.X = destinationX;
            entity.Y = destinationY;
            entity.DestinationX = null;
            entity.DestinationY = null;
            entity.MovementSpeed = 0;
        }
        return true;
    }

    public void Step(double deltaSeconds)
    {
        if (!double.IsFinite(deltaSeconds) || deltaSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds), "World time must be finite and non-negative.");
        if (deltaSeconds == 0) return;

        ClockSeconds += deltaSeconds;
        foreach (var entity in Entities.Where(candidate => candidate.IsMoving))
        {
            double targetX = entity.DestinationX!.Value;
            double targetY = entity.DestinationY!.Value;
            double dx = targetX - entity.X;
            double dy = targetY - entity.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            double travel = entity.MovementSpeed * deltaSeconds;
            if (distance <= travel || distance <= 0.000001)
            {
                entity.X = targetX;
                entity.Y = targetY;
                entity.DestinationX = null;
                entity.DestinationY = null;
                entity.MovementSpeed = 0;
                continue;
            }

            entity.X += dx / distance * travel;
            entity.Y += dy / distance * travel;
        }
    }

    /// <summary>Returns nearby entities by Euclidean distance, not by tile or hex adjacency.</summary>
    public IReadOnlyList<WorldEntityState> QueryRadius(double x, double y, double radius)
    {
        ValidatePosition(x, y);
        if (!double.IsFinite(radius) || radius < 0)
            throw new ArgumentOutOfRangeException(nameof(radius), "Search radius must be finite and non-negative.");
        double squaredRadius = radius * radius;
        return Entities
            .Where(entity => SquaredDistance(entity.X, entity.Y, x, y) <= squaredRadius)
            .OrderBy(entity => SquaredDistance(entity.X, entity.Y, x, y))
            .ThenBy(entity => entity.Id)
            .ToArray();
    }

    public void Validate()
    {
        if (SchemaVersion != 1)
            throw new InvalidDataException($"Unsupported continuous world schema version: {SchemaVersion}.");
        if (!double.IsFinite(ClockSeconds) || ClockSeconds < 0)
            throw new InvalidDataException("Continuous world clock is invalid.");
        if (Entities.Count > 100_000 || Entities.Select(entity => entity.Id).Distinct().Count() != Entities.Count)
            throw new InvalidDataException("Continuous world entity count or IDs are invalid.");
        foreach (var entity in Entities)
        {
            if (entity.Id <= 0 || !Enum.IsDefined(entity.Kind) ||
                string.IsNullOrWhiteSpace(entity.Name) || string.IsNullOrWhiteSpace(entity.Faction))
                throw new InvalidDataException("Continuous world contains an invalid entity identity.");
            try { ValidatePosition(entity.X, entity.Y); }
            catch (ArgumentOutOfRangeException error) { throw new InvalidDataException("Continuous world entity position is invalid.", error); }
            if (entity.DestinationX.HasValue != entity.DestinationY.HasValue)
                throw new InvalidDataException("Moving world entity has only one destination coordinate.");
            if (entity.IsMoving)
            {
                try { ValidatePosition(entity.DestinationX!.Value, entity.DestinationY!.Value); }
                catch (ArgumentOutOfRangeException error) { throw new InvalidDataException("Continuous world destination is invalid.", error); }
                if (!double.IsFinite(entity.MovementSpeed) || entity.MovementSpeed <= 0)
                    throw new InvalidDataException("Moving world entity has an invalid speed.");
            }
            else if (entity.MovementSpeed < 0 || !double.IsFinite(entity.MovementSpeed))
                throw new InvalidDataException("Stationary world entity has an invalid speed.");
        }
        if (NextEntityId <= Entities.Select(entity => entity.Id).DefaultIfEmpty(0).Max())
            throw new InvalidDataException("Continuous world next entity ID is not ahead of existing entities.");
    }

    public static string Serialize(ContinuousWorldState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.Validate();
        return System.Text.Json.JsonSerializer.Serialize(state, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    public static ContinuousWorldState Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new InvalidDataException("Continuous world save is empty.");
        var state = System.Text.Json.JsonSerializer.Deserialize<ContinuousWorldState>(json)
            ?? throw new InvalidDataException("Continuous world save is empty.");
        state.Validate();
        return state;
    }

    private static void ValidatePosition(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) ||
            x < 0 || y < 0 || x > WorldExtent || y > WorldExtent)
            throw new ArgumentOutOfRangeException(nameof(x), $"World coordinates must be inside 0..{WorldExtent:0}.");
    }

    private static double Distance(double ax, double ay, double bx, double by)
        => Math.Sqrt(SquaredDistance(ax, ay, bx, by));

    private static double SquaredDistance(double ax, double ay, double bx, double by)
    {
        double dx = ax - bx, dy = ay - by;
        return dx * dx + dy * dy;
    }
}
