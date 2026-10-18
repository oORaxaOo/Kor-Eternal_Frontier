using System.Text.Json;

namespace KorTheBlight.Campaign;

internal enum WorldFactionKind { Kor, AshenCompact, FreeMarches, HelionLeague, VerdantCovenant, Blight, Neutral }
internal enum DiplomaticStatus { Hostile, Wary, Neutral, Friendly, NonAggression, TradePact }

internal sealed class WorldTerritory
{
    public int Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public WorldFactionKind Owner { get; set; }
    public int Development { get; set; }
    public int Supplies { get; set; }
    public int BlightPressure { get; set; }
    public bool Explored { get; set; }
    public bool HasSettlement { get; set; }
}

internal sealed class WorldMapState
{
    public int SchemaVersion { get; set; } = 1;
    public int Seed { get; set; }
    public int Turn { get; set; } = 1;
    public int EmpireSupplies { get; set; } = 400;
    public int EmpireAlloy { get; set; } = 220;
    public int SelectedTerritoryId { get; set; }
    public List<WorldTerritory> Territories { get; set; } = [];
    public Dictionary<string, int> Relations { get; set; } = new();
    public string LastEvent { get; set; } = "The frontier awaits.";
}

internal sealed class WorldMapSimulation
{
    public const int Width = 26;
    public const int Height = 18;
    private static readonly WorldFactionKind[] Factions =
    [
        WorldFactionKind.Kor, WorldFactionKind.AshenCompact, WorldFactionKind.FreeMarches,
        WorldFactionKind.HelionLeague, WorldFactionKind.VerdantCovenant, WorldFactionKind.Blight
    ];
    private readonly string _savePath;
    public WorldMapState State { get; private set; }
    public IReadOnlyList<WorldTerritory> Territories => State.Territories;
    public WorldTerritory Selected => State.Territories[Math.Clamp(State.SelectedTerritoryId, 0, State.Territories.Count - 1)];
    public string Message => State.LastEvent;
    public string SelectedOwnerName => FactionName(Selected.Owner);
    public bool CanClaimSelected => Selected.Owner == WorldFactionKind.Neutral && State.EmpireSupplies >= 40 && State.EmpireAlloy >= 25 && Neighbours(Selected.X, Selected.Y).Any(t => t.Owner == WorldFactionKind.Kor);

    public WorldMapSimulation(int seed)
    {
        string root = Environment.GetEnvironmentVariable("XDG_DATA_HOME") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        _savePath = Path.Combine(root, "kor-eternal-frontier", "world-map.json");
        State = File.Exists(_savePath) ? LoadState(File.ReadAllText(_savePath)) : Generate(seed);
        Validate(State);
    }

    public void SelectAt(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return;
        State.SelectedTerritoryId = y * Width + x;
        var t = Selected;
        State.LastEvent = $"{t.Owner}: {FactionName(t.Owner)} territory {t.X + 1},{t.Y + 1}; development {t.Development}; Blight pressure {t.BlightPressure}.";
    }

    public void SelectNext(int delta)
    {
        State.SelectedTerritoryId = (State.SelectedTerritoryId + delta + Territories.Count) % Territories.Count;
        var t = Selected;
        State.LastEvent = $"Selected {FactionName(t.Owner)} territory {t.X + 1},{t.Y + 1}.";
    }

    public DiplomaticStatus RelationTo(WorldFactionKind faction)
    {
        if (faction is WorldFactionKind.Kor or WorldFactionKind.Blight or WorldFactionKind.Neutral) return faction == WorldFactionKind.Blight ? DiplomaticStatus.Hostile : DiplomaticStatus.Friendly;
        int score = State.Relations.GetValueOrDefault(Key(WorldFactionKind.Kor, faction), 0);
        if (score >= 70) return DiplomaticStatus.TradePact;
        if (score >= 40) return DiplomaticStatus.NonAggression;
        if (score >= 15) return DiplomaticStatus.Friendly;
        if (score >= -14) return DiplomaticStatus.Neutral;
        if (score >= -44) return DiplomaticStatus.Wary;
        return DiplomaticStatus.Hostile;
    }

    public bool OfferNonAggression()
    {
        if (Selected.Owner is WorldFactionKind.Kor or WorldFactionKind.Blight or WorldFactionKind.Neutral) return false;
        string key = Key(WorldFactionKind.Kor, Selected.Owner);
        int score = State.Relations.GetValueOrDefault(key, 0);
        if (score < -25) { State.LastEvent = $"{FactionName(Selected.Owner)} rejected the pact. Relations are too poor."; return false; }
        State.Relations[key] = Math.Min(55, score + 35);
        State.LastEvent = $"{FactionName(Selected.Owner)} accepted a non-aggression pact with the Kor.";
        Save();
        return true;
    }

    public bool OfferTradePact()
    {
        if (Selected.Owner is WorldFactionKind.Kor or WorldFactionKind.Blight or WorldFactionKind.Neutral) return false;
        string key = Key(WorldFactionKind.Kor, Selected.Owner);
        int score = State.Relations.GetValueOrDefault(key, 0);
        if (score < 35) { State.LastEvent = $"{FactionName(Selected.Owner)} requires better relations before trade."; return false; }
        State.Relations[key] = Math.Min(100, score + 35);
        State.LastEvent = $"Trade pact established with {FactionName(Selected.Owner)}. Regional exchange efficiency improved.";
        Save();
        return true;
    }

    public bool ImproveRelations()
    {
        if (Selected.Owner is WorldFactionKind.Kor or WorldFactionKind.Blight) return false;
        string key = Key(WorldFactionKind.Kor, Selected.Owner);
        State.Relations[key] = Math.Min(100, State.Relations.GetValueOrDefault(key, 0) + 15);
        State.LastEvent = $"Diplomatic mission improved relations with {FactionName(Selected.Owner)}.";
        Save();
        return true;
    }

    public void EndTurn()
    {
        State.Turn++;
        foreach (var t in Territories.Where(t => t.Owner != WorldFactionKind.Blight && t.Owner != WorldFactionKind.Kor))
            t.Supplies += 1 + t.Development;
        SpreadBlight();
        foreach (var key in State.Relations.Keys.ToArray())
            if (State.Relations[key] < 0) State.Relations[key] = Math.Min(0, State.Relations[key] + 1);
        State.LastEvent = $"World turn {State.Turn}. The Blight advances where borders are weak; independent powers continue developing.";
        Save();
    }

    private void SpreadBlight()
    {
        var frontier = Territories.Where(t => t.Owner == WorldFactionKind.Blight)
            .SelectMany(t => Neighbours(t.X, t.Y))
            .Where(t => t.Owner != WorldFactionKind.Blight && t.Owner != WorldFactionKind.Kor)
            .OrderBy(t => t.BlightPressure).ThenBy(t => t.Id).FirstOrDefault();
        if (frontier == null) return;
        frontier.BlightPressure = Math.Min(3, frontier.BlightPressure + 1);
        if (frontier.BlightPressure >= 2)
        {
            frontier.Owner = WorldFactionKind.Blight;
            frontier.Explored = true;
            State.LastEvent = $"THE BLIGHT consumed territory {frontier.X + 1},{frontier.Y + 1}. The regional balance is shifting.";
        }
    }

    public IEnumerable<WorldTerritory> Neighbours(int x, int y)
    {
        foreach (var (dx, dy) in new[] { (1,0), (-1,0), (0,1), (0,-1) })
        {
            int nx = x + dx, ny = y + dy;
            if (nx >= 0 && nx < Width && ny >= 0 && ny < Height) yield return Territories[ny * Width + nx];
        }
    }

    public static string FactionName(WorldFactionKind f) => f switch
    {
        WorldFactionKind.Kor => "The Kor",
        WorldFactionKind.AshenCompact => "Ashen Compact",
        WorldFactionKind.FreeMarches => "Free Marches",
        WorldFactionKind.HelionLeague => "Helion League",
        WorldFactionKind.VerdantCovenant => "Verdant Covenant",
        _ => "THE BLIGHT"
    };

    private static string Key(WorldFactionKind a, WorldFactionKind b) => $"{(int)a}:{(int)b}";
    private static WorldMapState Generate(int seed)
    {
        var rng = new Random(seed);
        var state = new WorldMapState { Seed = seed };
        var capitals = new (int x, int y)[] { (3, 8), (9, 3), (18, 4), (21, 12), (8, 14), (15, 9) };
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            int id = y * Width + x;
            var ranked = capitals.Select((p, i) => (i, distance: Math.Abs(p.x - x) + Math.Abs(p.y - y) + rng.NextDouble() * 3.5))
                .OrderBy(v => v.distance).ToArray();
            int ownerIndex = ranked[0].i;
            int capitalIndex = Array.FindIndex(capitals, p => p.x == x && p.y == y);
            if (capitalIndex >= 0) ownerIndex = capitalIndex;
            else if (ranked[0].distance - ranked[1].distance < 1.5 && rng.NextDouble() < .72) ownerIndex = -1;
            var owner = ownerIndex < 0 ? WorldFactionKind.Neutral : Factions[ownerIndex];
            bool settlement = capitalIndex >= 0;
            state.Territories.Add(new WorldTerritory { Id = id, X = x, Y = y, Owner = owner, Development = settlement ? 3 : rng.Next(0, 2), Supplies = rng.Next(4, 18), BlightPressure = owner == WorldFactionKind.Blight ? 2 : 0, Explored = owner == WorldFactionKind.Kor || owner == WorldFactionKind.Blight, HasSettlement = settlement });
        }
        // Ensure the Kor capital and a readable Blight front exist.
        foreach (var t in state.Territories.Where(t => t.Owner == WorldFactionKind.Kor)) t.Explored = true;\n        var korCapital = state.Territories.Single(t => t.X == capitals[0].x && t.Y == capitals[0].y);\n        var frontier = state.Territories.Single(t => t.X == capitals[0].x + 1 && t.Y == capitals[0].y);\n        frontier.Owner = WorldFactionKind.Neutral; frontier.HasSettlement = false; frontier.Development = 0;
        state.Relations[Key(WorldFactionKind.Kor, WorldFactionKind.AshenCompact)] = 12;
        state.Relations[Key(WorldFactionKind.Kor, WorldFactionKind.FreeMarches)] = 8;
        state.Relations[Key(WorldFactionKind.Kor, WorldFactionKind.HelionLeague)] = -8;
        state.Relations[Key(WorldFactionKind.Kor, WorldFactionKind.VerdantCovenant)] = 24;
        state.SelectedTerritoryId = 3 * Width + 9;
        return state;
    }

    private static WorldMapState LoadState(string json) => JsonSerializer.Deserialize<WorldMapState>(json)
        ?? throw new InvalidDataException("World map save is empty.");

    private static void Validate(WorldMapState state)
    {
        if (state.SchemaVersion != 1 || state.EmpireSupplies < 0 || state.EmpireAlloy < 0 || state.SelectedTerritoryId < 0 || state.SelectedTerritoryId >= Width * Height || state.Territories.Count != Width * Height ||
            state.Territories.Select(t => t.Id).Distinct().Count() != Width * Height ||
            state.Territories.Any(t => t.Id < 0 || t.Id >= Width * Height || t.X < 0 || t.X >= Width || t.Y < 0 || t.Y >= Height || t.BlightPressure is < 0 or > 3))
            throw new InvalidDataException("World map save contains invalid territory data.");
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_savePath)!);
        string tmp = _savePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(State, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, _savePath, true);
    }
}
