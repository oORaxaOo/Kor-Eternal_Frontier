using KorTheBlight.Simulation;
using SkiaSharp;

namespace KorTheBlight.Rendering;

/// <summary>Draws the scenario in Gondwana's scene canvas; it never changes simulation state.</summary>
internal sealed class OutpostRenderer : IDisposable
{
    // Centralized wireframe palette: dark engineered surfaces, readable crimson contours,
    // muted terrain lines, and restrained gold for selection and critical information.
    private static readonly SKColor Charcoal = new(7, 10, 13);
    private static readonly SKColor Crimson = new(222, 54, 72);
    private static readonly SKColor Gold = new(226, 181, 91);
    private static readonly SKColor Panel = new(10, 15, 20);
    private static readonly SKColor PanelEdge = new(73, 91, 99);
    private static readonly SKColor TerrainWire = new(66, 100, 101, 205);
    private static readonly SKColor KorWire = new(230, 65, 82, 235);
    private static readonly SKColor BlightWire = new(245, 48, 92, 230);
    private static readonly SKColor SurfaceFill = new(11, 19, 24, 226);
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
        _fill.Color = new SKColor(8, 12, 16); c.DrawRect(new SKRect(0, 0, 1100, 720), _fill);
        DrawBackdropGrid(c);
        for (int y = 0; y < OutpostSimulation.Height; y++) for (int x = 0; x < OutpostSimulation.Width; x++) DrawTile(c, sim, x, y, cameraX, cameraY, zoom);
        DrawNode(c, 2, 4, "A", new SKColor(180, 156, 110), cameraX, cameraY, zoom);
        DrawNode(c, 10, 3, "S", new SKColor(125, 169, 126), cameraX, cameraY, zoom);
        DrawBase(c, cameraX, cameraY, zoom);
        DrawPurifiers(c, sim, cameraX, cameraY, zoom);
        DrawHostiles(c, sim, cameraX, cameraY, zoom);
        DrawGuardian(c, sim, cameraX, cameraY, zoom);
        DrawWorker(c, sim, cameraX, cameraY, zoom);
        DrawHud(c, sim, buildMode);
    }

    private void DrawBackdropGrid(SKCanvas c)
    {
        // A faint drafting grid continues into the dark negative space around the colony.
        _line.StrokeWidth = 1;
        _line.Color = new SKColor(44, 69, 78, 45);
        for (int x = 0; x <= 1100; x += 40) c.DrawLine(x, 80, x, 635, _line);
        for (int y = 80; y <= 635; y += 40) c.DrawLine(0, y, 1100, y, _line);
        _line.Color = new SKColor(102, 53, 64, 95);
        c.DrawLine(18, 96, 58, 96, _line); c.DrawLine(18, 96, 18, 136, _line);
        c.DrawLine(1082, 96, 1042, 96, _line); c.DrawLine(1082, 96, 1082, 136, _line);
    }

    private void DrawTile(SKCanvas c, OutpostSimulation sim, int x, int y, float cameraX, float cameraY, float zoom)
    {
        var p = OutpostSimulation.TileCenter(x, y, cameraX, cameraY, zoom);
        float rx = OutpostSimulation.TileWidth * zoom / 2, ry = OutpostSimulation.TileHeight * zoom / 2;
        using var path = new SKPath();
        path.MoveTo(p.X, p.Y - ry); path.LineTo(p.X + rx, p.Y); path.LineTo(p.X, p.Y + ry); path.LineTo(p.X - rx, p.Y); path.Close();
        int variation = Math.Abs((x * 73 + y * 131 + x * y * 17) % 7);
        bool blocked = sim.BlockedAt(x, y) && (x != OutpostSimulation.BaseX || y != OutpostSimulation.BaseY);
        _fill.Color = blocked
            ? new SKColor(15, 22, 27, 240)
            : new SKColor((byte)(9 + variation), (byte)(20 + variation), (byte)(25 + variation), 229);
        c.DrawPath(path, _fill);
        _line.Color = (x + y) % 4 == 0 ? new SKColor(95, 130, 133, 220) : TerrainWire;
        _line.StrokeWidth = Math.Max(.8f, zoom * .8f);
        c.DrawPath(path, _line);
        _line.StrokeWidth = 1;

        // Sparse survey marks make the terrain look engineered without drowning the scene in noise.
        if ((x * 3 + y * 5) % 4 == 0)
        {
            _line.Color = new SKColor(98, 127, 126, 95);
            c.DrawLine(p.X - rx * .24f, p.Y, p.X + rx * .24f, p.Y, _line);
            c.DrawLine(p.X, p.Y - ry * .24f, p.X, p.Y + ry * .24f, _line);
        }
        if ((x + y) % 7 == 0)
        {
            _line.Color = new SKColor(89, 116, 119, 110);
            c.DrawLine(p.X - rx * .38f, p.Y + ry * .12f, p.X - rx * .12f, p.Y + ry * .34f, _line);
        }

        byte infection = sim.BlightAt(x, y);
        if (infection > 0)
        {
            byte alpha = (byte)Math.Clamp(28 + infection * 34, 0, 150);
            _fill.Color = new SKColor(121, 12, 40, alpha);
            c.DrawPath(path, _fill);
            _line.Color = new SKColor(222, 34, 76, (byte)Math.Clamp(100 + infection * 35, 0, 225));
            _line.StrokeWidth = Math.Max(1.1f, zoom);
            // Alien branching growth follows the tile geometry instead of a simple red blob.
            c.DrawLine(p.X - rx * .55f, p.Y + ry * .2f, p.X - rx * .12f, p.Y - ry * .25f, _line);
            c.DrawLine(p.X - rx * .12f, p.Y - ry * .25f, p.X + rx * .12f, p.Y + ry * .02f, _line);
            c.DrawLine(p.X + rx * .12f, p.Y + ry * .02f, p.X + rx * .43f, p.Y - ry * .3f, _line);
            c.DrawLine(p.X - rx * .12f, p.Y - ry * .25f, p.X - rx * .37f, p.Y - ry * .48f, _line);
            c.DrawCircle(p.X + rx * .12f, p.Y + ry * .02f, Math.Max(1.2f, zoom * 2), _line);
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
        _fill.Color = new SKColor(2, 5, 8, 135); c.DrawOval(new SKRect(x - 18, y - 3, x + 18, y + 9), _fill);
        using var rock = new SKPath();
        rock.MoveTo(x - 15, y - 1); rock.LineTo(x - 11, y - 15); rock.LineTo(x - 2, y - 22);
        rock.LineTo(x + 10, y - 18); rock.LineTo(x + 16, y - 4); rock.Close();
        _fill.Color = new SKColor(15, 25, 30, 235); c.DrawPath(rock, _fill);
        _line.Color = new SKColor(119, 151, 154, 220); c.DrawPath(rock, _line);
        _line.Color = new SKColor(87, 121, 128, 170);
        c.DrawLine(x - 10, y - 13, x - 2, y - 20, _line);
        c.DrawLine(x - 2, y - 20, x + 10, y - 18, _line);
        c.DrawLine(x - 3, y - 5, x + 3, y - 14, _line);
        _line.Color = new SKColor(41, 63, 70, 220); c.DrawLine(x - 15, y - 1, x + 16, y - 4, _line);
    }

    private void DrawNode(SKCanvas c, int x, int y, string symbol, SKColor color, float cameraX, float cameraY, float zoom)
    {
        var p = OutpostSimulation.TileCenter(x, y, cameraX, cameraY, zoom);
        float cy = p.Y - 11 * zoom, radius = 13 * zoom;
        _fill.Color = new SKColor(4, 8, 12, 205); c.DrawCircle(p.X, cy, radius, _fill);
        _line.Color = new SKColor(198, 171, 110, 210); c.DrawCircle(p.X, cy, radius, _line);
        _line.Color = new SKColor(color.Red, color.Green, color.Blue, 220);
        using var crystal = new SKPath();
        crystal.MoveTo(p.X, cy - 8 * zoom); crystal.LineTo(p.X + 6 * zoom, cy - 1 * zoom);
        crystal.LineTo(p.X + 3 * zoom, cy + 7 * zoom); crystal.LineTo(p.X - 5 * zoom, cy + 5 * zoom);
        crystal.LineTo(p.X - 7 * zoom, cy - 2 * zoom); crystal.Close();
        _fill.Color = new SKColor(color.Red, color.Green, color.Blue, 45); c.DrawPath(crystal, _fill);
        _line.StrokeWidth = Math.Max(1, zoom); c.DrawPath(crystal, _line); _line.StrokeWidth = 1;
        _line.Color = new SKColor(239, 214, 156, 175);
        c.DrawLine(p.X - 4 * zoom, cy + 3 * zoom, p.X + 3 * zoom, cy - 4 * zoom, _line);
        c.DrawText(symbol, p.X - 4.5f * zoom, cy + 5 * zoom, SKTextAlign.Left, _font, _bold);
    }

    private void DrawBase(SKCanvas c, float cameraX, float cameraY, float zoom)
    {
        var p = OutpostSimulation.TileCenter(OutpostSimulation.BaseX, OutpostSimulation.BaseY, cameraX, cameraY, zoom);
        _fill.Color = new SKColor(0, 0, 0, 170);
        c.DrawOval(new SKRect(p.X - 39 * zoom, p.Y - 7 * zoom, p.X + 39 * zoom, p.Y + 16 * zoom), _fill);

        DrawWirePrism(c, p.X, p.Y + 1 * zoom, 30, 16, 48, zoom,
            new SKColor(15, 24, 30, 246), new SKColor(10, 17, 22, 246), KorWire);

        // The command hub receives an unmistakable crimson service band and a gold status beacon.
        _line.Color = new SKColor(246, 81, 91, 235); _line.StrokeWidth = Math.Max(1.4f, zoom);
        c.DrawLine(p.X - 23 * zoom, p.Y - 24 * zoom, p.X + 5 * zoom, p.Y - 11 * zoom, _line);
        c.DrawLine(p.X + 5 * zoom, p.Y - 11 * zoom, p.X + 23 * zoom, p.Y - 24 * zoom, _line);
        c.DrawLine(p.X - 24 * zoom, p.Y - 18 * zoom, p.X + 4 * zoom, p.Y - 5 * zoom, _line);
        c.DrawLine(p.X + 4 * zoom, p.Y - 5 * zoom, p.X + 23 * zoom, p.Y - 18 * zoom, _line);

        _fill.Color = new SKColor(6, 12, 17, 235);
        c.DrawRoundRect(new SKRect(p.X - 9 * zoom, p.Y - 20 * zoom, p.X + 9 * zoom, p.Y - 3 * zoom), 2 * zoom, 2 * zoom, _fill);
        _line.Color = new SKColor(115, 153, 164, 235);
        c.DrawRoundRect(new SKRect(p.X - 9 * zoom, p.Y - 20 * zoom, p.X + 9 * zoom, p.Y - 3 * zoom), 2 * zoom, 2 * zoom, _line);
        _line.Color = KorWire;
        c.DrawLine(p.X - 4 * zoom, p.Y - 16 * zoom, p.X + 4 * zoom, p.Y - 16 * zoom, _line);
        c.DrawLine(p.X - 4 * zoom, p.Y - 12 * zoom, p.X + 4 * zoom, p.Y - 12 * zoom, _line);

        _fill.Color = Gold; c.DrawCircle(p.X, p.Y - 48 * zoom, 3.8f * zoom, _fill);
        _line.Color = new SKColor(255, 211, 118, 220); c.DrawCircle(p.X, p.Y - 48 * zoom, 6 * zoom, _line);
        _line.Color = new SKColor(230, 65, 82, 165);
        c.DrawLine(p.X, p.Y - 42 * zoom, p.X, p.Y - 34 * zoom, _line);
    }

    private void DrawPurifiers(SKCanvas c, OutpostSimulation sim, float cameraX, float cameraY, float zoom)
    {
        foreach (var building in sim.Purifiers)
        {
            var p = OutpostSimulation.TileCenter((int)building.X, (int)building.Y, cameraX, cameraY, zoom);
            _fill.Color = new SKColor(0, 0, 0, 155);
            c.DrawOval(new SKRect(p.X - 24 * zoom, p.Y - 4 * zoom, p.X + 24 * zoom, p.Y + 10 * zoom), _fill);

            bool complete = building.Progress >= 1;
            SKColor edge = complete ? new SKColor(237, 188, 95, 235) : KorWire;
            DrawWirePrism(c, p.X, p.Y + 1 * zoom, 18, 11, 28, zoom,
                new SKColor(13, 23, 27, 245), new SKColor(9, 17, 21, 245), edge);

            // Split containment rings make the facility read as a machine, not a generic block.
            _line.Color = complete ? Gold : new SKColor(231, 64, 91, 230);
            _line.StrokeWidth = Math.Max(1.1f, zoom);
            c.DrawOval(new SKRect(p.X - 9 * zoom, p.Y - 38 * zoom, p.X + 9 * zoom, p.Y - 32 * zoom), _line);
            c.DrawLine(p.X - 8 * zoom, p.Y - 36 * zoom, p.X - 12 * zoom, p.Y - 43 * zoom, _line);
            c.DrawLine(p.X + 8 * zoom, p.Y - 36 * zoom, p.X + 12 * zoom, p.Y - 43 * zoom, _line);
            c.DrawLine(p.X, p.Y - 34 * zoom, p.X, p.Y - 48 * zoom, _line);
            _line.StrokeWidth = 1;
            _fill.Color = complete ? Gold : Crimson;
            c.DrawCircle(p.X, p.Y - 49 * zoom, 2.8f * zoom, _fill);
            _line.Color = new SKColor(218, 169, 90, 185);
            c.DrawRect(new SKRect(p.X - 18 * zoom, p.Y - 56 * zoom, p.X + 18 * zoom, p.Y - 52 * zoom), _line);
            _fill.Color = new SKColor(6, 11, 14, 230);
            c.DrawRect(new SKRect(p.X - 17 * zoom, p.Y - 55 * zoom, p.X + 17 * zoom, p.Y - 53 * zoom), _fill);
            _fill.Color = complete ? Gold : Crimson;
            c.DrawRect(new SKRect(p.X - 17 * zoom, p.Y - 55 * zoom, p.X + (-17 + 34 * Math.Clamp(building.Progress, 0, 1)) * zoom, p.Y - 53 * zoom), _fill);
        }
    }

    private void DrawWorker(SKCanvas c, OutpostSimulation sim, float cameraX, float cameraY, float zoom)
    {
        var p = sim.WorkerScreen(cameraX, cameraY, zoom);
        if (sim.WorkerSelected)
        {
            _line.Color = Gold; _line.StrokeWidth = 2;
            c.DrawOval(new SKRect(p.X - 18 * zoom, p.Y - 5 * zoom, p.X + 18 * zoom, p.Y + 8 * zoom), _line);
            _line.StrokeWidth = 1;
            var target = OutpostSimulation.TileCenter((int)sim.TargetX, (int)sim.TargetY, cameraX, cameraY, zoom);
            _line.Color = new SKColor(235, 197, 113, 190); c.DrawCircle(target.X, target.Y, 8 * zoom, _line);
        }
        _fill.Color = new SKColor(4, 6, 6, 190); c.DrawOval(new SKRect(p.X - 15 * zoom, p.Y - 3 * zoom, p.X + 15 * zoom, p.Y + 9 * zoom), _fill);
        _fill.Color = new SKColor(7, 15, 20, 238);
        c.DrawRoundRect(new SKRect(p.X - 13 * zoom, p.Y - 10 * zoom, p.X + 13 * zoom, p.Y + 4 * zoom), 5 * zoom, 5 * zoom, _fill);
        _line.Color = new SKColor(102, 143, 151, 220);
        c.DrawRoundRect(new SKRect(p.X - 13 * zoom, p.Y - 10 * zoom, p.X + 13 * zoom, p.Y + 4 * zoom), 5 * zoom, 5 * zoom, _line);
        _fill.Color = new SKColor(11, 22, 27, 245);
        c.DrawRoundRect(new SKRect(p.X - 8 * zoom, p.Y - 24 * zoom, p.X + 8 * zoom, p.Y - 3 * zoom), 4 * zoom, 4 * zoom, _fill);
        _line.Color = KorWire; c.DrawRoundRect(new SKRect(p.X - 8 * zoom, p.Y - 24 * zoom, p.X + 8 * zoom, p.Y - 3 * zoom), 4 * zoom, 4 * zoom, _line);
        _line.Color = Gold;
        c.DrawLine(p.X - 7 * zoom, p.Y - 7 * zoom, p.X - 12 * zoom, p.Y + 3 * zoom, _line);
        c.DrawLine(p.X + 7 * zoom, p.Y - 7 * zoom, p.X + 12 * zoom, p.Y + 3 * zoom, _line);
        c.DrawLine(p.X - 3 * zoom, p.Y - 22 * zoom, p.X - 3 * zoom, p.Y - 8 * zoom, _line);
        c.DrawLine(p.X + 3 * zoom, p.Y - 22 * zoom, p.X + 3 * zoom, p.Y - 8 * zoom, _line);
        _fill.Color = new SKColor(17, 26, 31, 245); c.DrawCircle(p.X, p.Y - 27 * zoom, 6 * zoom, _fill);
        _line.Color = new SKColor(235, 194, 109, 230); c.DrawCircle(p.X, p.Y - 27 * zoom, 6 * zoom, _line);
        _fill.Color = new SKColor(37, 40, 39); c.DrawRect(new SKRect(p.X - 5 * zoom, p.Y - 29 * zoom, p.X + 5 * zoom, p.Y - 24 * zoom), _fill);
        _line.Color = new SKColor(90, 167, 177, 230); c.DrawLine(p.X - 4 * zoom, p.Y - 27 * zoom, p.X + 4 * zoom, p.Y - 27 * zoom, _line);
    }

    private void DrawGuardian(SKCanvas c, OutpostSimulation sim, float cameraX, float cameraY, float zoom)
    {
        var p = sim.GuardianScreen(cameraX, cameraY, zoom);
        if (sim.GuardianSelected)
        {
            _line.Color = Gold; _line.StrokeWidth = 2;
            c.DrawOval(new SKRect(p.X - 21 * zoom, p.Y - 5 * zoom, p.X + 21 * zoom, p.Y + 10 * zoom), _line);
            _line.StrokeWidth = 1;
        }
        _fill.Color = new SKColor(4, 6, 6, 190); c.DrawOval(new SKRect(p.X - 19 * zoom, p.Y - 4 * zoom, p.X + 19 * zoom, p.Y + 10 * zoom), _fill);
        DrawPoly(c, _fill, new SKColor(8, 18, 24, 245), zoom,
            (p.X - 17 * zoom, p.Y - 5 * zoom), (p.X - 13 * zoom, p.Y - 18 * zoom),
            (p.X - 6 * zoom, p.Y - 22 * zoom), (p.X + 12 * zoom, p.Y - 18 * zoom), (p.X + 18 * zoom, p.Y - 5 * zoom));
        _line.Color = new SKColor(127, 157, 161, 235);
        DrawPoly(c, _line, _line.Color, zoom,
            (p.X - 17 * zoom, p.Y - 5 * zoom), (p.X - 13 * zoom, p.Y - 18 * zoom),
            (p.X - 6 * zoom, p.Y - 22 * zoom), (p.X + 12 * zoom, p.Y - 18 * zoom), (p.X + 18 * zoom, p.Y - 5 * zoom));
        _fill.Color = new SKColor(97, 22, 40, 245); c.DrawRoundRect(new SKRect(p.X - 10 * zoom, p.Y - 18 * zoom, p.X + 11 * zoom, p.Y - 8 * zoom), 3 * zoom, 3 * zoom, _fill);
        _line.Color = KorWire; c.DrawRoundRect(new SKRect(p.X - 10 * zoom, p.Y - 18 * zoom, p.X + 11 * zoom, p.Y - 8 * zoom), 3 * zoom, 3 * zoom, _line);
        _fill.Color = new SKColor(7, 17, 22, 245); c.DrawRect(new SKRect(p.X - 3 * zoom, p.Y - 25 * zoom, p.X + 4 * zoom, p.Y - 14 * zoom), _fill);
        _line.Color = new SKColor(118, 153, 163, 235); c.DrawRect(new SKRect(p.X - 3 * zoom, p.Y - 25 * zoom, p.X + 4 * zoom, p.Y - 14 * zoom), _line);
        _line.Color = Gold; c.DrawLine(p.X, p.Y - 23 * zoom, p.X, p.Y - 31 * zoom, _line);
        _fill.Color = Gold; c.DrawCircle(p.X, p.Y - 30 * zoom, 2 * zoom, _fill);
        _line.Color = new SKColor(249, 197, 111, 235); c.DrawCircle(p.X, p.Y - 30 * zoom, 4 * zoom, _line);
    }

    private void DrawHostiles(SKCanvas c, OutpostSimulation sim, float cameraX, float cameraY, float zoom)
    {
        foreach (var enemy in sim.Hostiles)
        {
            var p = OutpostSimulation.WorldScreen(enemy.X, enemy.Y, cameraX, cameraY, zoom);
            _fill.Color = new SKColor(5, 5, 7, 180); c.DrawOval(new SKRect(p.X - 16 * zoom, p.Y - 3 * zoom, p.X + 16 * zoom, p.Y + 9 * zoom), _fill);
            _fill.Color = new SKColor(32, 5, 22, 225); c.DrawOval(new SKRect(p.X - 11 * zoom, p.Y - 24 * zoom, p.X + 12 * zoom, p.Y + 2 * zoom), _fill);
            _line.Color = new SKColor(188, 35, 74, 235); c.DrawOval(new SKRect(p.X - 11 * zoom, p.Y - 24 * zoom, p.X + 12 * zoom, p.Y + 2 * zoom), _line);
            _fill.Color = new SKColor(75, 10, 39, 230); c.DrawOval(new SKRect(p.X - 8 * zoom, p.Y - 27 * zoom, p.X + 9 * zoom, p.Y - 9 * zoom), _fill);
            _line.Color = BlightWire; c.DrawOval(new SKRect(p.X - 8 * zoom, p.Y - 27 * zoom, p.X + 9 * zoom, p.Y - 9 * zoom), _line);
            _fill.Color = new SKColor(255, 100, 127); c.DrawCircle(p.X + 2 * zoom, p.Y - 20 * zoom, 2.2f * zoom, _fill);
            _line.Color = BlightWire; _line.StrokeWidth = Math.Max(1.1f, 1.6f * zoom);
            c.DrawLine(p.X - 8 * zoom, p.Y - 18 * zoom, p.X - 16 * zoom, p.Y - 24 * zoom, _line);
            c.DrawLine(p.X - 13 * zoom, p.Y - 21 * zoom, p.X - 14 * zoom, p.Y - 28 * zoom, _line);
            c.DrawLine(p.X + 8 * zoom, p.Y - 18 * zoom, p.X + 16 * zoom, p.Y - 25 * zoom, _line);
            c.DrawLine(p.X + 13 * zoom, p.Y - 22 * zoom, p.X + 18 * zoom, p.Y - 18 * zoom, _line);
            _line.StrokeWidth = 1;
        }
    }

    private void DrawHud(SKCanvas c, OutpostSimulation sim, bool buildMode)
    {
        _fill.Color = new SKColor(4, 8, 12, 242); c.DrawRect(new SKRect(0, 0, 1100, 80), _fill);
        _line.Color = new SKColor(86, 113, 120, 165); c.DrawLine(0, 79, 1100, 79, _line);
        _fill.Color = Crimson; c.DrawRect(new SKRect(0, 0, 4, 80), _fill);
        _line.Color = new SKColor(228, 61, 80, 180); c.DrawLine(5, 0, 5, 80, _line);
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
        _fill.Color = new SKColor(4, 8, 12, 246); c.DrawRect(new SKRect(0, 635, 1100, 720), _fill);
        _line.Color = PanelEdge; c.DrawLine(0, 635, 1100, 635, _line);
        _line.Color = new SKColor(177, 53, 70, 115); c.DrawLine(0, 638, 1100, 638, _line);
        _fill.Color = Crimson; c.DrawRect(new SKRect(20, 651, 24, 702), _fill);
        c.DrawText("LILITH", 37, 669, SKTextAlign.Left, _heading, _text);
        c.DrawText(sim.Message, 37, 693, SKTextAlign.Left, _font, _subtle);
        _line.Color = PanelEdge; c.DrawLine(430, 649, 430, 706, _line);
        _fill.Color = Gold;
        c.DrawText("MISSION", 454, 666, SKTextAlign.Left, _font, _fill);
        c.DrawText(sim.Won ? "BLIGHT CONTAINED — SECTOR SECURE" : sim.Lost ? "COMMAND SHELTER LOST" : "DEPLOY 2 PURIFIERS TO CONTAIN THE BLIGHT", 454, 692, SKTextAlign.Left, _heading, _text);
        c.DrawText(buildMode ? "BUILD MODE   /   LEFT CLICK: PLACE   /   ESC: CANCEL" : "LMB SELECT UNIT   •   RMB MOVE / GATHER   •   B BUILD   •   WASD PAN   •   +/- ZOOM", 454, 712, SKTextAlign.Left, _font, _subtle);
    }

    private void DrawResource(SKCanvas c, float x, string label, int value, SKColor color) => DrawResource(c, x, label, value.ToString(), color);

    private void DrawResource(SKCanvas c, float x, string label, string value, SKColor color)
    {
        _fill.Color = new SKColor(7, 13, 17, 238); c.DrawRoundRect(new SKRect(x, 17, x + 143, 62), 5, 5, _fill);
        _line.Color = PanelEdge; c.DrawRoundRect(new SKRect(x, 17, x + 143, 62), 5, 5, _line);
        _line.Color = new SKColor(color.Red, color.Green, color.Blue, 185);
        c.DrawLine(x + 8, 21, x + 8, 58, _line);
        _fill.Color = color; c.DrawRect(new SKRect(x + 10, 27, x + 13, 52), _fill);
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
