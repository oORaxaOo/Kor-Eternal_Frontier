using KorTheBlight.Simulation;
using SkiaSharp;

namespace KorTheBlight.Rendering;

/// <summary>Draws the scenario in Gondwana's scene canvas; it never changes simulation state.</summary>
internal sealed class OutpostRenderer : IDisposable
{
    private static readonly SKColor Charcoal = new(22, 25, 27), Crimson = new(157, 36, 47), Gold = new(196, 158, 73);
    private readonly SKPaint _fill = new() { IsAntialias = true };
    private readonly SKPaint _line = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
    private readonly SKPaint _text = new() { IsAntialias = true, Color = new SKColor(235, 226, 207) };
    private readonly SKPaint _subtle = new() { IsAntialias = true, Color = new SKColor(187, 186, 176) };
    private readonly SKPaint _bold = new() { IsAntialias = true, Color = new SKColor(240, 232, 213) };
    private readonly SKFont _font = new(SKTypeface.FromFamilyName("sans-serif"), 15);
    private readonly SKFont _heading = new(SKTypeface.FromFamilyName("sans-serif", SKFontStyle.Bold), 19);
    private readonly SKFont _title = new(SKTypeface.FromFamilyName("sans-serif", SKFontStyle.Bold), 24);

    public void Draw(SKCanvas c, OutpostSimulation sim, float cameraX, float cameraY, float zoom, bool buildMode)
    {
        c.Clear(Charcoal);
        for (int y = 0; y < OutpostSimulation.Height; y++) for (int x = 0; x < OutpostSimulation.Width; x++) DrawTile(c, sim, x, y, cameraX, cameraY, zoom);
        DrawNode(c, 2, 4, "A", new SKColor(180, 156, 110), cameraX, cameraY, zoom);
        DrawNode(c, 10, 3, "S", new SKColor(125, 169, 126), cameraX, cameraY, zoom);
        DrawBase(c, cameraX, cameraY, zoom);
        DrawPurifiers(c, sim, cameraX, cameraY, zoom);
        DrawWorker(c, sim, cameraX, cameraY, zoom);
        DrawHud(c, sim, buildMode);
    }

    private void DrawTile(SKCanvas c, OutpostSimulation sim, int x, int y, float cameraX, float cameraY, float zoom)
    {
        var p = OutpostSimulation.TileCenter(x, y, cameraX, cameraY, zoom);
        float rx = OutpostSimulation.TileWidth * zoom / 2, ry = OutpostSimulation.TileHeight * zoom / 2;
        using var path = new SKPath();
        path.MoveTo(p.X, p.Y - ry); path.LineTo(p.X + rx, p.Y); path.LineTo(p.X, p.Y + ry); path.LineTo(p.X - rx, p.Y); path.Close();
        _fill.Color = sim.BlockedAt(x, y) && (x != OutpostSimulation.BaseX || y != OutpostSimulation.BaseY) ? new SKColor(59, 60, 57) : ((x + y) % 4 == 0 ? new SKColor(58, 62, 48) : new SKColor(42, 53, 48));
        c.DrawPath(path, _fill);
        _line.Color = new SKColor(84, 91, 77); c.DrawPath(path, _line);
        byte infection = sim.BlightAt(x, y);
        if (infection > 0)
        {
            _fill.Color = new SKColor(148, 22, 41, (byte)(65 + infection * 45));
            c.DrawPath(path, _fill);
            _fill.Color = new SKColor(176, 31, 49, 210);
            c.DrawCircle(p.X - rx * .12f, p.Y, ry * (.42f + infection * .06f), _fill);
            c.DrawCircle(p.X + rx * .22f, p.Y - ry * .15f, ry * .3f, _fill);
        }
        if (_blocked[x, y]) DrawRock(c, p.X, p.Y);
    }

    private readonly bool[,] _blocked = BuildRocks();
    private static bool[,] BuildRocks()
    {
        var rocks = new bool[OutpostSimulation.Width, OutpostSimulation.Height];
        rocks[4, 1] = rocks[9, 6] = rocks[11, 1] = true;
        return rocks;
    }

    private void DrawRock(SKCanvas c, float x, float y)
    {
        _fill.Color = new SKColor(86, 87, 78); c.DrawOval(new SKRect(x - 14, y - 20, x + 14, y + 5), _fill);
        _line.Color = new SKColor(128, 125, 105); c.DrawLine(x - 8, y - 12, x + 4, y - 17, _line);
    }

    private void DrawNode(SKCanvas c, int x, int y, string symbol, SKColor color, float cameraX, float cameraY, float zoom)
    {
        var p = OutpostSimulation.TileCenter(x, y, cameraX, cameraY, zoom);
        _fill.Color = new SKColor(17, 19, 18, 220); c.DrawCircle(p.X, p.Y - 9 * zoom, 12 * zoom, _fill);
        _fill.Color = color; c.DrawCircle(p.X, p.Y - 9 * zoom, 7 * zoom, _fill);
        c.DrawText(symbol, p.X - 5, p.Y - 4 * zoom, SKTextAlign.Left, _font, _bold);
    }

    private void DrawBase(SKCanvas c, float cameraX, float cameraY, float zoom)
    {
        var p = OutpostSimulation.TileCenter(OutpostSimulation.BaseX, OutpostSimulation.BaseY, cameraX, cameraY, zoom);
        _fill.Color = new SKColor(11, 13, 13, 140); c.DrawOval(new SKRect(p.X - 36 * zoom, p.Y - 7 * zoom, p.X + 36 * zoom, p.Y + 15 * zoom), _fill);
        _fill.Color = new SKColor(53, 54, 51); c.DrawRect(new SKRect(p.X - 25 * zoom, p.Y - 48 * zoom, p.X + 25 * zoom, p.Y + 2 * zoom), _fill);
        _fill.Color = Crimson; c.DrawRect(new SKRect(p.X - 21 * zoom, p.Y - 45 * zoom, p.X + 21 * zoom, p.Y - 35 * zoom), _fill);
        _fill.Color = new SKColor(31, 34, 33); c.DrawRect(new SKRect(p.X - 14 * zoom, p.Y - 30 * zoom, p.X + 14 * zoom, p.Y - 3 * zoom), _fill);
        _fill.Color = Gold; c.DrawCircle(p.X, p.Y - 39 * zoom, 4 * zoom, _fill);
    }

    private void DrawPurifiers(SKCanvas c, OutpostSimulation sim, float cameraX, float cameraY, float zoom)
    {
        foreach (var building in sim.Purifiers)
        {
            var p = OutpostSimulation.TileCenter((int)building.X, (int)building.Y, cameraX, cameraY, zoom);
            _fill.Color = new SKColor(12, 15, 15, 150); c.DrawOval(new SKRect(p.X - 22 * zoom, p.Y - 4 * zoom, p.X + 22 * zoom, p.Y + 10 * zoom), _fill);
            _fill.Color = building.Progress >= 1 ? new SKColor(83, 111, 102) : new SKColor(90, 87, 76);
            c.DrawRect(new SKRect(p.X - 14 * zoom, p.Y - 30 * zoom, p.X + 14 * zoom, p.Y + 2 * zoom), _fill);
            _fill.Color = building.Progress >= 1 ? Gold : Crimson;
            c.DrawRect(new SKRect(p.X - 4 * zoom, p.Y - 39 * zoom, p.X + 4 * zoom, p.Y - 28 * zoom), _fill);
            _line.Color = Gold; c.DrawRect(new SKRect(p.X - 16 * zoom, p.Y - 44 * zoom, p.X + 16 * zoom, p.Y - 40 * zoom), _line);
            _fill.Color = Gold; c.DrawRect(new SKRect(p.X - 16 * zoom, p.Y - 44 * zoom, p.X + (32 * building.Progress - 16) * zoom, p.Y - 40 * zoom), _fill);
        }
    }

    private void DrawWorker(SKCanvas c, OutpostSimulation sim, float cameraX, float cameraY, float zoom)
    {
        var p = sim.WorkerScreen(cameraX, cameraY, zoom);
        if (sim.Selected)
        {
            _line.Color = Gold; _line.StrokeWidth = 2;
            c.DrawOval(new SKRect(p.X - 18 * zoom, p.Y - 5 * zoom, p.X + 18 * zoom, p.Y + 8 * zoom), _line);
            _line.StrokeWidth = 1;
            var target = OutpostSimulation.TileCenter((int)sim.TargetX, (int)sim.TargetY, cameraX, cameraY, zoom);
            _line.Color = new SKColor(235, 197, 113, 190); c.DrawCircle(target.X, target.Y, 8 * zoom, _line);
        }
        _fill.Color = new SKColor(18, 20, 19); c.DrawOval(new SKRect(p.X - 11 * zoom, p.Y - 3 * zoom, p.X + 11 * zoom, p.Y + 7 * zoom), _fill);
        _fill.Color = new SKColor(170, 170, 160); c.DrawRoundRect(new SKRect(p.X - 7 * zoom, p.Y - 22 * zoom, p.X + 7 * zoom, p.Y + 1 * zoom), 4, 4, _fill);
        _fill.Color = Gold; c.DrawCircle(p.X, p.Y - 24 * zoom, 6 * zoom, _fill);
        _fill.Color = new SKColor(65, 68, 67); c.DrawRect(new SKRect(p.X - 4 * zoom, p.Y - 27 * zoom, p.X + 4 * zoom, p.Y - 22 * zoom), _fill);
    }

    private void DrawHud(SKCanvas c, OutpostSimulation sim, bool buildMode)
    {
        _fill.Color = new SKColor(12, 14, 15, 238); c.DrawRect(new SKRect(0, 0, 1100, 74), _fill);
        _fill.Color = Crimson; c.DrawRect(new SKRect(0, 0, 7, 74), _fill);
        c.DrawText("KOR-ETERNAL FRONTIER", 25, 31, SKTextAlign.Left, _title, _text);
        c.DrawText("FRONTIER OUTPOST   /   DAY 01", 27, 55, SKTextAlign.Left, _font, _subtle);
        c.DrawText($"ALLOY  {sim.Alloy,4}", 570, 30, SKTextAlign.Left, _heading, _text);
        c.DrawText($"SUPPLIES  {sim.Supplies,4}", 740, 30, SKTextAlign.Left, _heading, _text);
        c.DrawText($"SHELTER  {(int)Math.Max(0, sim.BaseHealth)}%", 940, 30, SKTextAlign.Left, _heading, sim.BaseHealth < 50 ? _bold : _text);
        // Advisor and objective strip.
        _fill.Color = new SKColor(12, 14, 15, 238); c.DrawRect(new SKRect(0, 642, 1100, 720), _fill);
        _fill.Color = Crimson; c.DrawCircle(39, 681, 22, _fill);
        _fill.Color = new SKColor(225, 192, 139); c.DrawOval(new SKRect(31, 666, 47, 686), _fill);
        c.DrawText("LILITH", 72, 671, SKTextAlign.Left, _heading, _text);
        c.DrawText(sim.Message, 72, 695, SKTextAlign.Left, _font, _subtle);
        string action = sim.Won ? "CONTAINMENT ACHIEVED — OUTPOST SECURED" : sim.Lost ? "OUTPOST LOST — THE BLIGHT REACHED COMMAND" : buildMode ? "PLACEMENT MODE: LEFT-CLICK OPEN GROUND   |   ESC CANCELS" : "LMB SELECT WORKER   •   RMB MOVE / GATHER   •   B PLACE PURIFIER   •   WASD PAN   •   +/- ZOOM";
        _fill.Color = sim.Won ? new SKColor(111, 169, 124) : sim.Lost ? Crimson : Gold;
        c.DrawText(action, 470, 681, SKTextAlign.Left, _font, _fill);
        string objective = $"Contain the Blight: purifiers online {sim.CompletedPurifiers}/2";
        c.DrawText(objective, 470, 703, SKTextAlign.Left, _font, _subtle);
    }

    public void Dispose()
    {
        _fill.Dispose(); _line.Dispose(); _text.Dispose(); _subtle.Dispose(); _bold.Dispose();
        _font.Dispose(); _heading.Dispose(); _title.Dispose();
    }
}
