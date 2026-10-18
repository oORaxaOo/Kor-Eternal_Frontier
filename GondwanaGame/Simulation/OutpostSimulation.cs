namespace KorTheBlight.Simulation;

/// <summary>A small fixed-step settlement scenario, independent of the renderer and SDL.</summary>
internal sealed class OutpostSimulation
{
    public const int Width = 13, Height = 9;
    public const float TileWidth = 54, TileHeight = 27;
    public const int BaseX = 6, BaseY = 4;
    private readonly byte[,] _blight = new byte[Width, Height];
    private readonly bool[,] _blocked = new bool[Width, Height];
    private readonly List<Purifier> _purifiers = [];
    private readonly List<(int X, int Y)> _route = [];
    private readonly ScenarioBalance _balance;
    private double _spreadClock, _workClock, _damageClock, _cleanseClock;
    private int _spreadTurns;

    public float WorkerX { get; private set; } = 3.5f;
    public float WorkerY { get; private set; } = 4f;
    public float TargetX { get; private set; } = 3.5f;
    public float TargetY { get; private set; } = 4f;
    public bool Selected { get; set; }
    public int Alloy { get; private set; }
    public int Supplies { get; private set; }
    public float BaseHealth { get; private set; } = 100;
    public int CompletedPurifiers => _purifiers.Count(p => p.Progress >= 1f);
    public bool Won => CompletedPurifiers >= 2;
    public bool Lost => BaseHealth <= 0;
    public string Message { get; private set; } = "Lilith: Salvage is to the west. Secure the red growth.";
    public IReadOnlyList<Purifier> Purifiers => _purifiers;
    public byte BlightAt(int x, int y) => InBounds(x, y) ? _blight[x, y] : (byte)0;
    public bool BlockedAt(int x, int y) => !InBounds(x, y) || _blocked[x, y] || (x == BaseX && y == BaseY) || _purifiers.Any(p => (int)p.X == x && (int)p.Y == y);
    public void SetMessage(string message) => Message = message;

    public OutpostSimulation(ScenarioBalance? balance = null)
    {
        _balance = balance ?? new ScenarioBalance();
        Alloy = _balance.StartingAlloy;
        Supplies = _balance.StartingSupplies;
        // Seeded, fixed starter layout; deterministic updates need no cosmetic RNG.
        _blight[0, 7] = 3; _blight[1, 7] = 2; _blight[1, 8] = 2;
        _blocked[4, 1] = true; _blocked[9, 6] = true; _blocked[11, 1] = true;
    }

    public static (float X, float Y) TileCenter(int x, int y, float cameraX = 0, float cameraY = 0, float zoom = 1)
    {
        float cx = 550 + cameraX, cy = 350 + cameraY;
        return (cx + (x - y) * TileWidth * zoom / 2f, cy + (x + y - 8) * TileHeight * zoom / 2f);
    }

    public static (float X, float Y) ScreenToTile(float sx, float sy, float cameraX = 0, float cameraY = 0, float zoom = 1)
    {
        float dx = (sx - 550 - cameraX) / (TileWidth * zoom / 2f);
        float dy = (sy - 350 - cameraY) / (TileHeight * zoom / 2f);
        return ((dx + dy) / 2f + 4f, (dy - dx) / 2f + 4f);
    }

    public bool SelectWorker(float sx, float sy, float cameraX = 0, float cameraY = 0, float zoom = 1)
    {
        var p = WorkerScreen(cameraX, cameraY, zoom);
        Selected = MathF.Sqrt(MathF.Pow(sx - p.X, 2) + MathF.Pow(sy - p.Y, 2)) <= 24;
        if (Selected) Message = "Worker selected. Right-click a deposit to gather, or open ground to move.";
        return Selected;
    }

    public (float X, float Y) WorkerScreen(float cameraX = 0, float cameraY = 0, float zoom = 1)
    {
        float cx = 550 + cameraX, cy = 350 + cameraY;
        return (cx + (WorkerX - WorkerY) * TileWidth * zoom / 2f, cy + (WorkerX + WorkerY - 8) * TileHeight * zoom / 2f);
    }

    public void Command(float sx, float sy, float cameraX = 0, float cameraY = 0, float zoom = 1)
    {
        if (!Selected || Won || Lost) return;
        var tile = ScreenToTile(sx, sy, cameraX, cameraY, zoom);
        int x = (int)MathF.Round(tile.X), y = (int)MathF.Round(tile.Y);
        if (!InBounds(x, y) || BlockedAt(x, y)) { Message = "Lilith: That route is blocked."; return; }
        if (!SetRouteTo(x, y)) return;
        if ((x == 2 && y == 4) || (x == 10 && y == 3))
            Message = x == 2 ? "Worker en route to alloy salvage." : "Worker en route to supply cache.";
        else Message = "Worker moving to the marked tile.";
    }

    public void PlacePurifier(float sx, float sy, float cameraX = 0, float cameraY = 0, float zoom = 1)
    {
        if (Won || Lost) return;
        var tile = ScreenToTile(sx, sy, cameraX, cameraY, zoom);
        int x = (int)MathF.Floor(tile.X), y = (int)MathF.Floor(tile.Y);
        if (!InBounds(x, y) || BlockedAt(x, y) || ((x == 2 && y == 4) || (x == 10 && y == 3)))
        { Message = "Lilith: Purifier placement is obstructed."; return; }
        if (Alloy < _balance.PurifierAlloyCost || Supplies < _balance.PurifierSuppliesCost)
        { Message = $"Lilith: A purifier costs {_balance.PurifierAlloyCost} alloy and {_balance.PurifierSuppliesCost} supplies."; return; }
        Alloy -= _balance.PurifierAlloyCost; Supplies -= _balance.PurifierSuppliesCost;
        _purifiers.Add(new Purifier(x + .5f, y + .5f));
        var workTiles = new[] { (x, y - 1), (x + 1, y), (x, y + 1), (x - 1, y) }
            .Where(tile => InBounds(tile.Item1, tile.Item2) && !BlockedAt(tile.Item1, tile.Item2)).ToArray();
        if (workTiles.Length == 0)
        {
            _purifiers.RemoveAt(_purifiers.Count - 1);
            Alloy += _balance.PurifierAlloyCost; Supplies += _balance.PurifierSuppliesCost;
            Message = "Lilith: No clear approach for the worker. Choose another site.";
            return;
        }
        var workTile = workTiles[0];
        if (!SetRouteTo(workTile.Item1, workTile.Item2))
        {
            _purifiers.RemoveAt(_purifiers.Count - 1);
            Alloy += _balance.PurifierAlloyCost; Supplies += _balance.PurifierSuppliesCost;
            Message = "Lilith: The worker cannot reach that construction site.";
            return;
        }
        Message = "Purifier foundation placed. Keep the worker nearby to build.";
    }

    public void Step(float deltaSeconds)
    {
        if (Won || Lost) return;
        while (_route.Count > 0)
        {
            var waypoint = _route[0];
            float dx = waypoint.X + .5f - WorkerX, dy = waypoint.Y + .5f - WorkerY;
            float distance = MathF.Sqrt(dx * dx + dy * dy);
            if (distance <= .035f) { WorkerX = waypoint.X + .5f; WorkerY = waypoint.Y + .5f; _route.RemoveAt(0); continue; }
            float move = Math.Min(distance, _balance.WorkerSpeed * deltaSeconds);
            WorkerX += dx / distance * move; WorkerY += dy / distance * move;
            break;
        }

        _workClock += deltaSeconds;
        if (_workClock >= _balance.WorkIntervalSeconds)
        {
            _workClock -= _balance.WorkIntervalSeconds;
            int wx = (int)WorkerX, wy = (int)WorkerY;
            if (MathF.Abs(WorkerX - TargetX) < .25f && MathF.Abs(WorkerY - TargetY) < .25f)
            {
                if (wx == 2 && wy == 4) { Alloy += _balance.AlloyPerGather; Message = $"Worker recovered {_balance.AlloyPerGather} alloy."; }
                else if (wx == 10 && wy == 3) { Supplies += _balance.SuppliesPerGather; Message = $"Worker recovered {_balance.SuppliesPerGather} supplies."; }
            }
            foreach (var purifier in _purifiers)
                if (purifier.Progress < 1 && Distance(WorkerX, WorkerY, purifier.X, purifier.Y) < 1.25f)
                { purifier.Progress = Math.Min(1, purifier.Progress + _balance.PurifierWorkPerInterval); Message = purifier.Progress >= 1 ? "Purifier online. Local Blight is receding." : "Worker constructing purifier."; }
        }

        _spreadClock += deltaSeconds;
        if (_spreadClock >= _balance.BlightSpreadSeconds)
        {
            _spreadClock -= _balance.BlightSpreadSeconds;
            _spreadTurns++;
            SpreadBlight();
        }
        _cleanseClock += deltaSeconds;
        if (_cleanseClock >= 1)
        {
            _cleanseClock -= 1;
            foreach (var purifier in _purifiers.Where(p => p.Progress >= 1))
            {
                int px = (int)purifier.X, py = (int)purifier.Y;
                for (int y = Math.Max(0, py - 2); y <= Math.Min(Height - 1, py + 2); y++)
                for (int x = Math.Max(0, px - 2); x <= Math.Min(Width - 1, px + 2); x++)
                    if (Distance(px, py, x, y) <= _balance.PurifierRange && _blight[x, y] > 0) _blight[x, y]--;
            }
        }
        if (_blight[BaseX, BaseY] > 0)
        {
            _damageClock += deltaSeconds;
            if (_damageClock >= 1) { _damageClock -= 1; BaseHealth -= _balance.ShelterDamagePerSecond; Message = "Lilith: The command shelter is under Blight pressure!"; }
        }
        else _damageClock = 0;
        if (BlightAt((int)WorkerX, (int)WorkerY) > 1) Message = "Lilith: Worker is exposed to the Blight.";
    }

    private void SpreadBlight()
    {
        var next = (byte[,])_blight.Clone();
        for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++)
        {
            if (_blight[x, y] > 1) next[x, y] = (byte)(_blight[x, y] - 1);
            if (_blight[x, y] == 0) continue;
            foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
            {
                if (!InBounds(nx, ny) || _blocked[nx, ny] || (nx == 2 && ny == 4) || (nx == 10 && ny == 3)) continue;
                if ((nx + 2 * ny + _spreadTurns) % _balance.BlightSpreadSkipEvery != 0) next[nx, ny] = Math.Max(next[nx, ny], (byte)1);
            }
        }
        Array.Copy(next, _blight, _blight.Length);
        if (_blight.Cast<byte>().Any(value => value > 0)) Message = "Lilith: The Blight is probing for new ground.";
    }

    private bool SetRouteTo(int goalX, int goalY)
    {
        _route.Clear();
        int startX = Math.Clamp((int)WorkerX, 0, Width - 1), startY = Math.Clamp((int)WorkerY, 0, Height - 1);
        var previous = new (int X, int Y)?[Width, Height];
        var visited = new bool[Width, Height];
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((startX, startY)); visited[startX, startY] = true;
        (int X, int Y)[] directions = [(0, -1), (1, 0), (0, 1), (-1, 0)];
        while (queue.Count > 0 && !visited[goalX, goalY])
        {
            var current = queue.Dequeue();
            foreach (var direction in directions)
            {
                int nx = current.X + direction.X, ny = current.Y + direction.Y;
                if (!InBounds(nx, ny) || visited[nx, ny] || BlockedAt(nx, ny)) continue;
                visited[nx, ny] = true; previous[nx, ny] = current; queue.Enqueue((nx, ny));
            }
        }
        if (!visited[goalX, goalY])
        {
            TargetX = WorkerX; TargetY = WorkerY;
            Message = "Lilith: No clear route to that location.";
            return false;
        }
        var cursor = (X: goalX, Y: goalY);
        while (cursor.X != startX || cursor.Y != startY)
        {
            _route.Add(cursor);
            cursor = previous[cursor.X, cursor.Y]!.Value;
        }
        _route.Reverse();
        TargetX = goalX + .5f; TargetY = goalY + .5f;
        return true;
    }

    private static float Distance(float x1, float y1, float x2, float y2) => MathF.Sqrt(MathF.Pow(x1 - x2, 2) + MathF.Pow(y1 - y2, 2));
    private static bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    internal sealed class Purifier(float x, float y)
    {
        public float X { get; } = x;
        public float Y { get; } = y;
        public float Progress { get; set; }
    }
}
