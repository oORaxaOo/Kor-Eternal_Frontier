using KorTheBlight.Simulation;
using SkiaSharp;

namespace KorTheBlight.Rendering;

/// <summary>Draws the scenario in Gondwana's scene canvas; it never changes simulation state.</summary>
internal sealed class OutpostRenderer : IDisposable
{
    private static readonly SKColor Charcoal = new(12, 15, 17), Crimson = new(164, 39, 48), Gold = new(217, 171, 79);
    private static readonly SKColor Panel = new(17, 22, 25), PanelEdge = new(70, 79, 78);
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
        _fill.Color = new SKColor(23, 29, 29); c.DrawRect(new SKRect(0, 0, 1100, 720), _fill);
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
        int variation = Math.Abs((x * 73 + y * 131 + x * y * 17) % 7);
        _fill.Color = sim.BlockedAt(x, y) && (x != OutpostSimulation.BaseX || y != OutpostSimulation.BaseY)
            ? new SKColor(47, 52, 51)
            : new SKColor((byte)(39 + variation * 2), (byte)(49 + variation * 2), (byte)(43 + variation));
        c.DrawPath(path, _fill);
        _line.Color = new SKColor(80, 88, 78, 170); c.DrawPath(path, _line);
        // Fine terrain marks break up the tile pattern without obscuring unit silhouettes.
        _fill.Color = new SKColor(137, 129, 99, 45);
        for (int i = 0; i < 4; i++)
        {
            float tx = p.X + (((x * 19 + y * 11 + i * 23) % 31) - 15) * zoom;
            float ty = p.Y + (((x * 7 + y * 17 + i * 13) % 13) - 6) * zoom;
            c.DrawCircle(tx, ty, (i % 2 == 0 ? 1.2f : 2f) * zoom, _fill);
        }
        byte infection = sim.BlightAt(x, y);
        if (infection > 0)
        {
            _fill.Color = new SKColor(123, 18, 34, (byte)(60 + infection * 42));
            c.DrawPath(path, _fill);
            _fill.Color = new SKColor(177, 35, 51, 190);
            c.DrawOval(new SKRect(p.X - rx * .7f, p.Y - ry * .48f, p.X + rx * .36f, p.Y + ry * .42f), _fill);
            _fill.Color = new SKColor(232, 88, 65, 105);
            c.DrawCircle(p.X + rx * .2f, p.Y - ry * .12f, ry * .23f, _fill);
            _line.Color = new SKColor(225, 84, 65, 145); _line.StrokeWidth = Math.Max(1, zoom);
            c.DrawLine(p.X - rx * .35f, p.Y + ry * .2f, p.X - rx * .08f, p.Y - ry * .28f, _line);
            c.DrawLine(p.X - rx * .08f, p.Y - ry * .28f, p.X + rx * .28f, p.Y - ry * .08f, _line);
            _line.StrokeWidth = 1;
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
        _fill.Color = new SKColor(21, 23, 22, 150); c.DrawOval(new SKRect(x - 17, y - 3, x + 17, y + 8), _fill);
        using var rock = new SKPath();
        rock.MoveTo(x - 15, y - 1); rock.LineTo(x - 11, y - 15); rock.LineTo(x - 2, y - 22);
        rock.LineTo(x + 10, y - 18); rock.LineTo(x + 16, y - 4); rock.Close();
        _fill.Color = new SKColor(78, 79, 74); c.DrawPath(rock, _fill);
        _line.Color = new SKColor(137, 131, 113); c.DrawLine(x - 10, y - 13, x - 2, y - 20, _line);
        _line.Color = new SKColor(47, 51, 49); c.DrawLine(x - 15, y - 1, x + 16, y - 4, _line);
    }

    private void DrawNode(SKCanvas c, int x, int y, string symbol, SKColor color, float cameraX, float cameraY, float zoom)
    {
        var p = OutpostSimulation.TileCenter(x, y, cameraX, cameraY, zoom);
        _fill.Color = new SKColor(13, 16, 17, 220); c.DrawCircle(p.X, p.Y - 10 * zoom, 14 * zoom, _fill);
        _line.Color = new SKColor(201, 184, 139, 165); c.DrawCircle(p.X, p.Y - 10 * zoom, 11 * zoom, _line);
        _fill.Color = color; c.DrawCircle(p.X, p.Y - 10 * zoom, 7 * zoom, _fill);
        _fill.Color = new SKColor(244, 229, 195); c.DrawCircle(p.X - 2 * zoom, p.Y - 12 * zoom, 2 * zoom, _fill);
        c.DrawText(symbol, p.X - 4.5f * zoom, p.Y - 6 * zoom, SKTextAlign.Left, _font, _bold);
    }

    private void DrawBase(SKCanvas c, float cameraX, float cameraY, float zoom)
    {
        var p = OutpostSimulation.TileCenter(OutpostSimulation.BaseX, OutpostSimulation.BaseY, cameraX, cameraY, zoom);
        _fill.Color = new SKColor(6, 8, 8, 180); c.DrawOval(new SKRect(p.X - 43 * zoom, p.Y - 8 * zoom, p.X + 43 * zoom, p.Y + 15 * zoom), _fill);
        DrawPoly(c, _fill, new SKColor(45, 49, 48), zoom,
            (p.X - 29 * zoom, p.Y - 4 * zoom), (p.X - 22 * zoom, p.Y - 42 * zoom),
            (p.X - 12 * zoom, p.Y - 55 * zoom), (p.X + 21 * zoom, p.Y - 52 * zoom),
            (p.X + 31 * zoom, p.Y - 8 * zoom));
        DrawPoly(c, _fill, new SKColor(74, 77, 70), zoom,
            (p.X - 18 * zoom, p.Y - 42 * zoom), (p.X - 10 * zoom, p.Y - 51 * zoom),
            (p.X + 14 * zoom, p.Y - 49 * zoom), (p.X + 21 * zoom, p.Y - 40 * zoom));
        _fill.Color = Crimson; c.DrawRect(new SKRect(p.X - 25 * zoom, p.Y - 28 * zoom, p.X + 26 * zoom, p.Y - 23 * zoom), _fill);
        _fill.Color = new SKColor(22, 28, 29); c.DrawRoundRect(new SKRect(p.X - 12 * zoom, p.Y - 24 * zoom, p.X + 12 * zoom, p.Y - 5 * zoom), 3 * zoom, 3 * zoom, _fill);
        _fill.Color = new SKColor(230, 183, 89); c.DrawCircle(p.X, p.Y - 35 * zoom, 4.5f * zoom, _fill);
        _line.Color = new SKColor(151, 157, 143); _line.StrokeWidth = Math.Max(1, zoom);
        c.DrawLine(p.X - 20 * zoom, p.Y - 44 * zoom, p.X - 13 * zoom, p.Y - 50 * zoom, _line);
        c.DrawLine(p.X + 13 * zoom, p.Y - 50 * zoom, p.X + 20 * zoom, p.Y - 44 * zoom, _line);
        _line.StrokeWidth = 1;
    }

    private void DrawPurifiers(SKCanvas c, OutpostSimulation sim, float cameraX, float cameraY, float zoom)
    {
        foreach (var building in sim.Purifiers)
        {
            var p = OutpostSimulation.TileCenter((int)building.X, (int)building.Y, cameraX, cameraY, zoom);
            _fill.Color = new SKColor(6, 8, 8, 170); c.DrawOval(new SKRect(p.X - 25 * zoom, p.Y - 4 * zoom, p.X + 25 * zoom, p.Y + 11 * zoom), _fill);
            SKColor body = building.Progress >= 1 ? new SKColor(65, 89, 83) : new SKColor(80, 69, 60);
            DrawPoly(c, _fill, body, zoom, (p.X - 19 * zoom, p.Y - 2 * zoom), (p.X - 14 * zoom, p.Y - 26 * zoom),
                (p.X - 6 * zoom, p.Y - 34 * zoom), (p.X + 11 * zoom, p.Y - 31 * zoom),
                (p.X + 17 * zoom, p.Y - 3 * zoom));
            _line.Color = new SKColor(150, 156, 137); c.DrawLine(p.X - 12 * zoom, p.Y - 25 * zoom, p.X + 9 * zoom, p.Y - 25 * zoom, _line);
            _fill.Color = new SKColor(29, 37, 36); c.DrawRect(new SKRect(p.X - 9 * zoom, p.Y - 20 * zoom, p.X + 9 * zoom, p.Y - 5 * zoom), _fill);
            _fill.Color = building.Progress >= 1 ? Gold : Crimson;
            DrawPoly(c, _fill, _fill.Color, zoom, (p.X - 5 * zoom, p.Y - 33 * zoom), (p.X, p.Y - 47 * zoom), (p.X + 5 * zoom, p.Y - 33 * zoom));
            _fill.Color = Gold; c.DrawCircle(p.X, p.Y - 37 * zoom, 2.5f * zoom, _fill);
            _line.Color = new SKColor(231, 188, 93, 190); c.DrawRect(new SKRect(p.X - 17 * zoom, p.Y - 52 * zoom, p.X + 17 * zoom, p.Y - 48 * zoom), _line);
            _fill.Color = Gold; c.DrawRect(new SKRect(p.X - 17 * zoom, p.Y - 52 * zoom, p.X + (34 * building.Progress - 17) * zoom, p.Y - 48 * zoom), _fill);
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
        _fill.Color = new SKColor(4, 6, 6, 190); c.DrawOval(new SKRect(p.X - 15 * zoom, p.Y - 3 * zoom, p.X + 15 * zoom, p.Y + 9 * zoom), _fill);
        _fill.Color = new SKColor(39, 44, 43); c.DrawRoundRect(new SKRect(p.X - 13 * zoom, p.Y - 10 * zoom, p.X + 13 * zoom, p.Y + 4 * zoom), 5 * zoom, 5 * zoom, _fill);
        _fill.Color = new SKColor(163, 164, 148); c.DrawRoundRect(new SKRect(p.X - 8 * zoom, p.Y - 24 * zoom, p.X + 8 * zoom, p.Y - 3 * zoom), 4 * zoom, 4 * zoom, _fill);
        _line.Color = new SKColor(230, 183, 89); c.DrawLine(p.X - 7 * zoom, p.Y - 7 * zoom, p.X - 12 * zoom, p.Y + 3 * zoom, _line);
        c.DrawLine(p.X + 7 * zoom, p.Y - 7 * zoom, p.X + 12 * zoom, p.Y + 3 * zoom, _line);
        _fill.Color = new SKColor(226, 183, 97); c.DrawCircle(p.X, p.Y - 27 * zoom, 6 * zoom, _fill);
        _fill.Color = new SKColor(37, 40, 39); c.DrawRect(new SKRect(p.X - 5 * zoom, p.Y - 29 * zoom, p.X + 5 * zoom, p.Y - 24 * zoom), _fill);
    }

    private void DrawHud(SKCanvas c, OutpostSimulation sim, bool buildMode)
    {
        _fill.Color = new SKColor(4, 6, 7, 235); c.DrawRect(new SKRect(0, 0, 1100, 80), _fill);
        _fill.Color = Crimson; c.DrawRect(new SKRect(0, 0, 6, 80), _fill);
        c.DrawText("KOR", 22, 35, SKTextAlign.Left, _title, _text);
        _line.Color = Gold; c.DrawLine(80, 16, 80, 64, _line);
        c.DrawText("ETERNAL FRONTIER", 96, 31, SKTextAlign.Left, _heading, _text);
        c.DrawText("FRONTIER COMMAND  /  SECTOR 01", 97, 53, SKTextAlign.Left, _font, _subtle);
        DrawResource(c, 392, "ALLOY", sim.Alloy, new SKColor(218, 163, 82));
        DrawResource(c, 545, "SUPPLIES", sim.Supplies, new SKColor(145, 184, 133));
        DrawResource(c, 709, "PURIFIERS", sim.CompletedPurifiers + "/2", Gold);
        _fill.Color = new SKColor(30, 37, 37); c.DrawRoundRect(new SKRect(892, 23, 1068, 54), 6, 6, _fill);
        _fill.Color = sim.BaseHealth < 50 ? Crimson : new SKColor(85, 145, 105);
        c.DrawRoundRect(new SKRect(895, 26, 895 + 170 * Math.Clamp(sim.BaseHealth, 0, 100) / 100f, 51), 4, 4, _fill);
        c.DrawText($"SHELTER  {(int)Math.Max(0, sim.BaseHealth)}%", 905, 45, SKTextAlign.Left, _font, _text);

        // C&C-inspired command footer with strong separation between world, orders and objective.
        _fill.Color = new SKColor(4, 6, 7, 245); c.DrawRect(new SKRect(0, 635, 1100, 720), _fill);
        _line.Color = PanelEdge; c.DrawLine(0, 635, 1100, 635, _line);
        _fill.Color = Crimson; c.DrawRect(new SKRect(20, 651, 24, 702), _fill);
        c.DrawText("LILITH", 37, 669, SKTextAlign.Left, _heading, _text);
        c.DrawText(sim.Message, 37, 693, SKTextAlign.Left, _font, _subtle);
        _line.Color = PanelEdge; c.DrawLine(430, 649, 430, 706, _line);
        _fill.Color = Gold;
        c.DrawText("MISSION", 454, 666, SKTextAlign.Left, _font, _fill);
        c.DrawText(sim.Won ? "BLIGHT CONTAINED — SECTOR SECURE" : sim.Lost ? "COMMAND SHELTER LOST" : "DEPLOY 2 PURIFIERS TO CONTAIN THE BLIGHT", 454, 692, SKTextAlign.Left, _heading, _text);
        c.DrawText(buildMode ? "BUILD MODE   /   LEFT CLICK: PLACE   /   ESC: CANCEL" : "LMB SELECT   •   RMB MOVE / GATHER   •   B BUILD   •   WASD PAN   •   +/- ZOOM", 454, 712, SKTextAlign.Left, _font, _subtle);
    }

    private void DrawResource(SKCanvas c, float x, string label, int value, SKColor color) => DrawResource(c, x, label, value.ToString(), color);

    private void DrawResource(SKCanvas c, float x, string label, string value, SKColor color)
    {
        _fill.Color = Panel; c.DrawRoundRect(new SKRect(x, 17, x + 143, 62), 5, 5, _fill);
        _line.Color = PanelEdge; c.DrawRoundRect(new SKRect(x, 17, x + 143, 62), 5, 5, _line);
        _fill.Color = color; c.DrawRect(new SKRect(x + 9, 27, x + 13, 52), _fill);
        c.DrawText(label, x + 21, 34, SKTextAlign.Left, _font, _subtle);
        c.DrawText(value, x + 21, 53, SKTextAlign.Left, _heading, _text);
    }

    private static void DrawPoly(SKCanvas c, SKPaint paint, SKColor color, float zoom, params (float X, float Y)[] points)
    {
        paint.Color = color;
        using var path = new SKPath();
        path.MoveTo(points[0].X, points[0].Y);
        for (int i = 1; i < points.Length; i++) path.LineTo(points[i].X, points[i].Y);
        path.Close(); c.DrawPath(path, paint);
    }

    public void Dispose()
    {
        _fill.Dispose(); _line.Dispose(); _text.Dispose(); _subtle.Dispose(); _bold.Dispose();
        _font.Dispose(); _heading.Dispose(); _title.Dispose();
    }
}
