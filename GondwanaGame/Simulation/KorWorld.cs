namespace KorFrontier.Gondwana.Simulation;

public enum GroundKind { Meadow, Soil, Crystal, Blight }
public enum KorBuildKind { Solar, Extractor, Archive, Relay }
public enum KorUnitKind { Pioneer, Warden }

public sealed class KorUnit
{
    public int Id { get; init; }
    public KorUnitKind Kind { get; init; }
    public float X { get; set; }
    public float Y { get; set; }
    public float TargetX { get; set; }
    public float TargetY { get; set; }
    public bool Selected { get; set; }
    public float Work { get; set; }
}

public sealed class KorBuilding
{
    public int Id { get; init; }
    public KorBuildKind Kind { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public float Progress { get; set; }
    public bool Complete => Progress >= 1f;
}

/// <summary>
/// Deterministic, presentation-independent settlement simulation. Rendering and input stay
/// in the Gondwana host; this class owns rules so the simulation can be tested without a window.
/// </summary>
public sealed class KorWorld
{
    public const int Width = 40;
    public const int Height = 40;
    private readonly GroundKind[,] _ground = new GroundKind[Width, Height];
    private readonly Random _random;
    private int _nextId = 1;
    private float _economyClock;
    private float _blightClock;

    public IReadOnlyList<KorUnit> Units => _units;
    public IReadOnlyList<KorBuilding> Buildings => _buildings;
    private readonly List<KorUnit> _units = [];
    private readonly List<KorBuilding> _buildings = [];

    public int Supplies { get; private set; } = 520;
    public int Alloy { get; private set; } = 260;
    public int Energy { get; private set; } = 90;
    public int Knowledge { get; private set; } = 0;
    public int BlightTiles { get; private set; }
    public string Status { get; private set; } = "A new frontier. A place for every Kor.";

    public KorWorld(int seed = 7319)
    {
        _random = new Random(seed);
        GenerateTerrain();
        _buildings.Add(new KorBuilding { Id = _nextId++, Kind = KorBuildKind.Relay, X = 20, Y = 20, Progress = 1f });
        _units.Add(new KorUnit { Id = _nextId++, Kind = KorUnitKind.Pioneer, X = 19, Y = 20, TargetX = 19, TargetY = 20 });
        _units.Add(new KorUnit { Id = _nextId++, Kind = KorUnitKind.Pioneer, X = 20, Y = 21, TargetX = 20, TargetY = 21 });
        _units.Add(new KorUnit { Id = _nextId++, Kind = KorUnitKind.Warden, X = 21, Y = 20, TargetX = 21, TargetY = 20 });
    }

    public GroundKind GroundAt(int x, int y) =>
        x < 0 || y < 0 || x >= Width || y >= Height ? GroundKind.Soil : _ground[x, y];

    public void SelectAt(float x, float y, bool additive = false)
    {
        if (!additive)
            foreach (var unit in _units) unit.Selected = false;

        var nearest = _units
            .Where(u => Math.Abs(u.X - x) < 1.05f && Math.Abs(u.Y - y) < 1.05f)
            .OrderBy(u => Math.Abs(u.X - x) + Math.Abs(u.Y - y))
            .FirstOrDefault();

        if (nearest is not null)
            nearest.Selected = additive ? !nearest.Selected : true;
        Status = nearest is null ? "Select a Pioneer to expand the frontier." : $"{nearest.Kind} selected · ready for orders.";
    }

    public void OrderSelected(float x, float y)
    {
        int selected = 0;
        foreach (var unit in _units.Where(u => u.Selected))
        {
            unit.TargetX = Math.Clamp(x + (selected % 2) * 0.45f, 1, Width - 2);
            unit.TargetY = Math.Clamp(y + (selected / 2) * 0.45f, 1, Height - 2);
            selected++;
        }
        Status = selected == 0 ? "Select a Kor first." : $"Order received · {selected} Kor moving.";
    }

    public bool TryBuild(KorBuildKind kind, int x, int y)
    {
        x = Math.Clamp(x, 1, Width - 2);
        y = Math.Clamp(y, 1, Height - 2);
        if (_buildings.Any(b => Math.Abs(b.X - x) < 2 && Math.Abs(b.Y - y) < 2))
        {
            Status = "Too close to another structure. Choose open ground.";
            return false;
        }

        var (supplies, alloy, energy) = kind switch
        {
            KorBuildKind.Solar => (90, 20, 0),
            KorBuildKind.Extractor => (110, 45, 0),
            KorBuildKind.Archive => (120, 25, 10),
            _ => (80, 15, 0)
        };
        if (Supplies < supplies || Alloy < alloy || Energy < energy)
        {
            Status = "Not enough resources yet. Let the settlement gather supplies.";
            return false;
        }

        Supplies -= supplies;
        Alloy -= alloy;
        Energy -= energy;
        _buildings.Add(new KorBuilding { Id = _nextId++, Kind = kind, X = x, Y = y, Progress = 0f });
        Status = $"{kind} site placed · assign a Pioneer nearby to construct it.";
        return true;
    }

    public void Update(float dt)
    {
        dt = Math.Clamp(dt, 0f, 0.05f);
        foreach (var unit in _units)
        {
            float dx = unit.TargetX - unit.X;
            float dy = unit.TargetY - unit.Y;
            float distance = MathF.Sqrt(dx * dx + dy * dy);
            if (distance > 0.025f)
            {
                float step = Math.Min(distance, dt * (unit.Kind == KorUnitKind.Warden ? 2.7f : 2.2f));
                unit.X += dx / distance * step;
                unit.Y += dy / distance * step;
            }
        }

        foreach (var building in _buildings.Where(b => !b.Complete))
        {
            var worker = _units.Where(u => u.Kind == KorUnitKind.Pioneer)
                .OrderBy(u => Math.Abs(u.X - building.X) + Math.Abs(u.Y - building.Y))
                .FirstOrDefault();
            if (worker is not null && Math.Abs(worker.X - building.X) + Math.Abs(worker.Y - building.Y) < 2.4f)
                building.Progress = Math.Min(1f, building.Progress + dt * 0.035f);
        }

        _economyClock += dt;
        if (_economyClock >= 1f)
        {
            _economyClock -= 1f;
            foreach (var building in _buildings.Where(b => b.Complete))
            {
                switch (building.Kind)
                {
                    case KorBuildKind.Solar: Energy += 2; break;
                    case KorBuildKind.Extractor: Alloy += 1; break;
                    case KorBuildKind.Archive: Knowledge += 1; break;
                    case KorBuildKind.Relay: Supplies += 1; break;
                }
            }
            Supplies = Math.Min(Supplies, 9999);
            Alloy = Math.Min(Alloy, 9999);
            Energy = Math.Min(Energy, 9999);
            Knowledge = Math.Min(Knowledge, 9999);
        }

        _blightClock += dt;
        if (_blightClock >= 10f)
        {
            _blightClock = 0f;
            SpreadBlight();
        }
    }

    private void GenerateTerrain()
    {
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            double roll = _random.NextDouble();
            _ground[x, y] = roll < 0.055 ? GroundKind.Crystal : roll < 0.15 ? GroundKind.Soil : GroundKind.Meadow;
        }

        // The Kor start is deliberately clear and readable.
        for (int y = 15; y <= 25; y++)
        for (int x = 15; x <= 25; x++)
            _ground[x, y] = GroundKind.Meadow;

        for (int i = 0; i < 8; i++)
        {
            int x = _random.Next(2, Width - 2), y = _random.Next(2, Height - 2);
            _ground[x, y] = GroundKind.Blight;
        }
        RecountBlight();
    }

    private void SpreadBlight()
    {
        var frontier = new List<(int X, int Y)>();
        for (int y = 1; y < Height - 1; y++)
        for (int x = 1; x < Width - 1; x++)
        {
            if (_ground[x, y] == GroundKind.Blight) continue;
            int neighbours = 0;
            if (_ground[x - 1, y] == GroundKind.Blight) neighbours++;
            if (_ground[x + 1, y] == GroundKind.Blight) neighbours++;
            if (_ground[x, y - 1] == GroundKind.Blight) neighbours++;
            if (_ground[x, y + 1] == GroundKind.Blight) neighbours++;
            if (neighbours > 0 && _random.NextDouble() < 0.08 * neighbours)
                frontier.Add((x, y));
        }

        foreach (var cell in frontier)
            if (!_buildings.Any(b => b.Complete && Math.Abs(b.X - cell.X) + Math.Abs(b.Y - cell.Y) < 2))
                _ground[cell.X, cell.Y] = GroundKind.Blight;

        RecountBlight();
        Status = BlightTiles > 45
            ? "The Blight is spreading. Expand carefully and establish a stable frontier."
            : "The frontier is stable · gather, build, and grow.";
    }

    private void RecountBlight()
    {
        BlightTiles = 0;
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
            if (_ground[x, y] == GroundKind.Blight) BlightTiles++;
    }
}
