using System;
using System.Collections.Generic;
using System.Linq;

namespace KorTheBlight.Simulation;

/// <summary>A small fixed-step settlement scenario, independent of the renderer and SDL.
/// Updated to use an orthographic square tile mapping for top-down view.
/// </summary>
internal sealed class OutpostSimulation
{
    public const int Width = 13, Height = 9;
    // Square tile size in pixels at zoom == 1
    public const float TileSize = 48f;
    public const float TileWidth = TileSize, TileHeight = TileSize;
    private const float UI_OFFSET_Y = 80f; // top HUD height in the renderer layout

    public const int BaseX = 6, BaseY = 4;

    private readonly byte[,] _blight = new byte[Width, Height];
    private readonly bool[,] _blocked = new bool[Width, Height];
    private readonly List<Purifier> _purifiers = new();
    private readonly List<BlightEnemy> _hostiles = new();
    private readonly List<(int X, int Y)> _route = new();
    private readonly List<(int X, int Y)> _guardianRoute = new();
    private readonly ScenarioBalance _balance;
    private double _spreadClock, _workClock, _damageClock, _cleanseClock, _combatClock;
    private int _spreadTurns;
    private bool _guardianSelected;

    public float WorkerX { get; private set; } = 3.5f;
    public float WorkerY { get; private set; } = 4f;
    public float TargetX { get; private set; } = 3.5f;
    public float TargetY { get; private set; } = 4f;
    public float GuardianX { get; private set; } = 4.5f;
    public float GuardianY { get; private set; } = 3.5f;
    public float GuardianTargetX { get; private set; } = 4.5f;
    public float GuardianTargetY { get; private set; } = 3.5f;
    public bool Selected { get; private set; }
    public bool WorkerSelected => Selected && !_guardianSelected;
    public bool GuardianSelected => Selected && _guardianSelected;
    public IReadOnlyList<BlightEnemy> Hostiles => _hostiles;
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
        _hostiles.Add(new BlightEnemy(.5f, 7.5f));
    }

    // Map tile (x,y) to screen coordinates (center) using orthographic mapping.
    public static (float X, float Y) TileCenter(int x, int y, float cameraX = 0, float cameraY = 0, float zoom = 1)
    {
        float mapWidthPx = Width * TileWidth * zoom;
        float mapHeightPx = Height * TileHeight * zoom;
        // place map centered horizontally within the 1100 design width and below the HUD area
        float originX = 1100f / 2f - mapWidthPx / 2f + cameraX;
        float originY = UI_OFFSET_Y + cameraY; // render under top HUD
        float cx = originX + x * TileWidth * zoom + (TileWidth * zoom) / 2f;
        float cy = originY + y * TileHeight * zoom + (TileHeight * zoom) / 2f;
        return (cx, cy);
    }

    public static (float X, float Y) ScreenToTile(float sx, float sy, float cameraX = 0, float cameraY = 0, float zoom = 1)
    {
        float mapWidthPx = Width * TileWidth * zoom;
        float mapHeightPx = Height * TileHeight * zoom;
        float originX = 1100f / 2f - mapWidthPx / 2f + cameraX;
        float originY = UI_OFFSET_Y + cameraY;
        float dx = (sx - originX) / (TileWidth * zoom);
        float dy = (sy - originY) / (TileHeight * zoom);
        return (dx, dy);
    }

    public bool SelectWorker(float sx, float sy, float cameraX = 0, float cameraY = 0, float zoom = 1)
    {
        var p = WorkerScreen(cameraX, cameraY, zoom);
        Selected = MathF.Sqrt(MathF.Pow(sx - p.X, 2) + MathF.Pow(sy - p.Y, 2)) <= Math.Max(12f, TileSize * zoom * 0.5f);
        _guardianSelected = false;
        if (Selected) Message = "Worker selected. Right-click a deposit to gather or ground to move.";
        return Selected;
    }

    public bool SelectUnit(float sx, float sy, float cameraX = 0, float cameraY = 0, float zoom = 1)
    {
        var worker = WorkerScreen(cameraX, cameraY, zoom);
        var guardian = UnitScreen(GuardianX, GuardianY, cameraX, cameraY, zoom);
        float workerDistance = Distance(sx, sy, worker.X, worker.Y);
        float guardianDistance = Distance(sx, sy, guardian.X, guardian.Y);
        if (Math.Min(workerDistance, guardianDistance) > Math.Max(16f, TileSize * zoom * 0.6f))
        {
            Selected = false;
            return false;
        }
        _guardianSelected = guardianDistance < workerDistance;
        Selected = true;
        Message = _guardianSelected ? "Guardian selected. Right-click to move; it will engage nearby Blight." : "Worker selected. Right-click a deposit to gather or ground to move.";
        return true;
    }

    public (float X, float Y) WorkerScreen(float cameraX = 0, float cameraY = 0, float zoom = 1)
    {
        return UnitScreen(WorkerX, WorkerY, cameraX, cameraY, zoom);
    }

    public (float X, float Y) GuardianScreen(float cameraX = 0, float cameraY = 0, float zoom = 1) => UnitScreen(GuardianX, GuardianY, cameraX, cameraY, zoom);
    public static (float X, float Y) WorldScreen(float x, float y, float cameraX = 0, float cameraY = 0, float zoom = 1) => UnitScreen(x, y, cameraX, cameraY, zoom);

    private static (float X, float Y) UnitScreen(float x, float y, float cameraX, float cameraY, float zoom)
    {
        // x,y are world (tile) coordinates, may be fractional; map to center using same origin as TileCenter
        float mapWidthPx = Width * TileWidth * zoom;
        float originX = 1100f / 2f - mapWidthPx / 2f + cameraX;
        float originY = UI_OFFSET_Y + cameraY;
        float cx = originX + x * TileWidth * zoom;
        float cy = originY + y * TileHeight * zoom;
        return (cx, cy);
    }

    public void Command(float sx, float sy, float cameraX = 0, float cameraY = 0, float zoom = 1)
    {
        if (!Selected || Won || Lost) return;
        var tile = ScreenToTile(sx, sy, cameraX, cameraY, zoom);
        int x = (int)MathF.Round(tile.X), y = (int)MathF.Round(tile.Y);
        if (!InBounds(x, y) || BlockedAt(x, y)) { Message = "Lilith: That route is blocked."; return; }
        if (_guardianSelected)
        {
            if (SetGuardianRouteTo(x, y)) Message = "Guardian moving to the marked position.";
            return;
        }
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

        MoveGuardian(deltaSeconds);
        UpdateCombat(deltaSeconds);

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
                { purifier.Progress = Math.Min(1, purifier.Progress + _balance.PurifierWorkPerInterval); Message = purifier.Progress >= 1 ? "Purifier online. Local Blight is receding." : "Worker contributing to purifier construction."; }
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
        if (BlightAt((int)MathF.Floor(WorkerX), (int)MathF.Floor(WorkerY)) > 1) Message = "Lilith: Worker is exposed to the Blight.";
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
        if (!FindRoute(startX, startY, goalX, goalY, _route))
        {
            TargetX = WorkerX; TargetY = WorkerY;
            Message = "Lilith: No clear route to that location.";
            return false;
        }
        TargetX = goalX + .5f; TargetY = goalY + .5f;
        return true;
    }

    private bool SetGuardianRouteTo(int goalX, int goalY)
    {
        GuardianTargetX = goalX + .5f; GuardianTargetY = goalY + .5f;
        _guardianRoute.Clear();
        int startX = Math.Clamp((int)GuardianX, 0, Width - 1), startY = Math.Clamp((int)GuardianY, 0, Height - 1);
        if (FindRoute(startX, startY, goalX, goalY, _guardianRoute)) return true;
        GuardianTargetX = GuardianX; GuardianTargetY = GuardianY;
        Message = "Lilith: Guardian cannot reach that position.";
        return false;
    }

    private bool FindRoute(int startX, int startY, int goalX, int goalY, List<(int X, int Y)> route)
    {
        route.Clear();
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
            return false;
        var cursor = (X: goalX, Y: goalY);
        while (cursor.X != startX || cursor.Y != startY)
        {
            route.Add(cursor);
            cursor = previous[cursor.X, cursor.Y]!.Value;
        }
        route.Reverse();
        return true;
    }

    private void MoveGuardian(float deltaSeconds)
    {
        while (_guardianRoute.Count > 0)
        {
            var waypoint = _guardianRoute[0];
            float dx = waypoint.X + .5f - GuardianX, dy = waypoint.Y + .5f - GuardianY;
            float distance = MathF.Sqrt(dx * dx + dy * dy);
            if (distance <= .035f) { GuardianX = waypoint.X + .5f; GuardianY = waypoint.Y + .5f; _guardianRoute.RemoveAt(0); continue; }
            float move = Math.Min(distance, 2.5f * deltaSeconds);
            GuardianX += dx / distance * move; GuardianY += dy / distance * move;
            break;
        }
    }

    private void UpdateCombat(float deltaSeconds)
    {
        _combatClock += deltaSeconds;
        if (_combatClock >= .5)
        {
            _combatClock -= .5;
            var target = _hostiles.OrderBy(enemy => Distance(GuardianX, GuardianY, enemy.X, enemy.Y)).FirstOrDefault();
            if (target != null && Distance(GuardianX, GuardianY, target.X, target.Y) <= 3.2f)
            {
                target.HitPoints -= 24;
                if (target.HitPoints <= 0)
                {
                    _hostiles.Remove(target);
                    Message = "Guardian neutralised a Blight creature.";
                }
            }
        }

        foreach (var enemy in _hostiles)
        {
            float dx = (BaseX + .5f) - enemy.X, dy = (BaseY + .5f) - enemy.Y;
            float distance = MathF.Sqrt(dx * dx + dy * dy);
            if (distance > .85f)
            {
                float step = Math.Min(distance - .85f, .65f * deltaSeconds);
                enemy.X += dx / distance * step; enemy.Y += dy / distance * step;
            }
            else
            {
                BaseHealth -= 4f * deltaSeconds;
                Message = "Lilith: Blight creature attacking the command shelter!";
            }
        }
    }

    private static float Distance(float x1, float y1, float x2, float y2) => MathF.Sqrt(MathF.Pow(x1 - x2, 2) + MathF.Pow(y1 - y2, 2));
    private static bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    internal sealed class Purifier(float x, float y)
    {
        public float X { get; } = x;
        public float Y { get; } = y;
        public float Progress { get; set; }
    }

    internal sealed class BlightEnemy(float x, float y)
    {
        public float X { get; set; } = x;
        public float Y { get; set; } = y;
        public int HitPoints { get; set; } = 72;
    }
}
