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
    private readonly SKFont _font = new(SKTypeface.FromFamilyName("Noto Sans"), 15);
    private readonly SKFont _small = new(SKTypeface.FromFamilyName("Noto Sans"), 12);
    private readonly SKFont _heading = new(SKTypeface.FromFamilyName("Noto Sans", SKFontStyle.Bold), 19);
    private readonly SKFont _title = new(SKTypeface.FromFamilyName("Noto Sans", SKFontStyle.Bold), 24);

    public void Draw(SKCanvas c, OutpostSimulation sim, float cameraX, float cameraY, float zoom, bool buildMode)
    {
        c.Clear(Charcoal);
        _fill.Color = new SKColor(23, 29, 29); c.DrawRect(new SKRect(0, 0, 1100, 720), _fill);
        for (int y = 0; y < OutpostSimulation.Height; y++) for (int x = 0; x < OutpostSimulation.Width; x++) DrawTile(c, sim, x, y, cameraX, cameraY, zoom);
        DrawNode(c, 2, 4, "A", new SKColor(180, 156, 110), cameraX, cameraY, zoom);
        DrawNode(c, 10, 3, "S", new SKColor(125, 169, 126), cameraX, cameraY, zoom);
        DrawBase(c, cameraX, cameraY, zoom);
        DrawPurifiers(c, sim, cameraX, cameraY, zoom);
        DrawHostiles(c, sim, cameraX, cameraY, zoom);
        DrawGuardian(c, sim, cameraX, cameraY, zoom);
        DrawWorker(c, sim, cameraX, cameraY, zoom);
        DrawHud(c, sim, buildMode);
        DrawTacticalSidebar(c, sim);
    }

    private void DrawTile(SKCanvas c, OutpostSimulation sim, int x, int y, float cameraX, float cameraY, float zoom)
    {
        var p = OutpostSimulation.TileCenter(x, y, cameraX, cameraY, zoom);
        float rx = OutpostSimulation.TileWidth * zoom / 2, ry = OutpostSimulation.TileHeight * zoom / 2;
        uint hash = unchecked((uint)(x * 73856093 ^ y * 19349663));
        int variation = (int)(hash % 6);
        string tile = sim.BlightAt(x, y) > 0 ? "ground-blight" : $"ground-{variation}";
        Artwork.Draw(c, tile, new SKRect(p.X - rx, p.Y - ry, p.X + rx, p.Y + ry));
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
        Artwork.Draw(c, "terrain-rocks", new SKRect(x - 27, y - 29, x + 27, y + 8));
    }

    private void DrawNode(SKCanvas c, int x, int y, string symbol, SKColor color, float cameraX, float cameraY, float zoom)
    {
        var p = OutpostSimulation.TileCenter(x, y, cameraX, cameraY, zoom);
        string art = symbol == "A" ? "alloy-outcrop" : "supply-cache";
        Artwork.Draw(c, art, new SKRect(p.X - 37 * zoom, p.Y - 54 * zoom, p.X + 37 * zoom, p.Y + 8 * zoom));
    }

    private void DrawBase(SKCanvas c, float cameraX, float cameraY, float zoom)
    {
        var p = OutpostSimulation.TileCenter(OutpostSimulation.BaseX, OutpostSimulation.BaseY, cameraX, cameraY, zoom);
        Artwork.Draw(c, "kor-command-habitat", new SKRect(p.X - 72 * zoom, p.Y - 84 * zoom, p.X + 72 * zoom, p.Y + 23 * zoom));
    }

    private void DrawPurifiers(SKCanvas c, OutpostSimulation sim, float cameraX, float cameraY, float zoom)
    {
        foreach (var building in sim.Purifiers)
        {
            var p = OutpostSimulation.TileCenter((int)building.X, (int)building.Y, cameraX, cameraY, zoom);
            if (building.Progress < 1)
            {
                _fill.Color = new SKColor(12, 16, 15, 165);
                c.DrawOval(new SKRect(p.X - 35 * zoom, p.Y - 3 * zoom, p.X + 35 * zoom, p.Y + 17 * zoom), _fill);
                Artwork.Draw(c, "kor-purifier", new SKRect(p.X - 40 * zoom, p.Y - 57 * zoom, p.X + 40 * zoom, p.Y + 18 * zoom));
                _fill.Color = new SKColor(5, 7, 7, 210); c.DrawRect(new SKRect(p.X - 25 * zoom, p.Y - 5 * zoom, p.X + 25 * zoom, p.Y + 1 * zoom), _fill);
                _fill.Color = Gold; c.DrawRect(new SKRect(p.X - 25 * zoom, p.Y - 5 * zoom, p.X + (50 * building.Progress - 25) * zoom, p.Y + 1 * zoom), _fill);
            }
            else Artwork.Draw(c, "kor-purifier", new SKRect(p.X - 42 * zoom, p.Y - 63 * zoom, p.X + 42 * zoom, p.Y + 16 * zoom));
        }
    }

    private void DrawWorker(SKCanvas c, OutpostSimulation sim, float cameraX, float cameraY, float zoom)
    {
        var p = sim.WorkerScreen(cameraX, cameraY, zoom);
        if (sim.WorkerSelected)
        {
            _line.Color = Gold; _line.StrokeWidth = Math.Max(2, zoom * 1.5f);
            c.DrawOval(new SKRect(p.X - 24 * zoom, p.Y - 7 * zoom, p.X + 24 * zoom, p.Y + 10 * zoom), _line);
            _line.StrokeWidth = 1;
            var target = OutpostSimulation.TileCenter((int)sim.TargetX, (int)sim.TargetY, cameraX, cameraY, zoom);
            Artwork.Draw(c, "alloy-outcrop", new SKRect(target.X - 23 * zoom, target.Y - 29 * zoom, target.X + 23 * zoom, target.Y + 5 * zoom));
        }
        Artwork.Draw(c, "kor-pioneer", new SKRect(p.X - 24 * zoom, p.Y - 42 * zoom, p.X + 24 * zoom, p.Y + 11 * zoom));
    }

    private void DrawGuardian(SKCanvas c, OutpostSimulation sim, float cameraX, float cameraY, float zoom)
    {
        var p = sim.GuardianScreen(cameraX, cameraY, zoom);
        if (sim.GuardianSelected)
        {
            _line.Color = Gold; _line.StrokeWidth = Math.Max(2, zoom * 1.5f);
            c.DrawOval(new SKRect(p.X - 32 * zoom, p.Y - 8 * zoom, p.X + 32 * zoom, p.Y + 12 * zoom), _line);
            _line.StrokeWidth = 1;
        }
        Artwork.Draw(c, "kor-guardian", new SKRect(p.X - 31 * zoom, p.Y - 48 * zoom, p.X + 31 * zoom, p.Y + 12 * zoom));
    }

    private void DrawHostiles(SKCanvas c, OutpostSimulation sim, float cameraX, float cameraY, float zoom)
    {
        foreach (var enemy in sim.Hostiles)
        {
            var p = OutpostSimulation.WorldScreen(enemy.X, enemy.Y, cameraX, cameraY, zoom);
            Artwork.Draw(c, "blight-stalker", new SKRect(p.X - 42 * zoom, p.Y - 43 * zoom, p.X + 42 * zoom, p.Y + 14 * zoom));
        }
    }

    private void DrawHud(SKCanvas c, OutpostSimulation sim, bool buildMode)
    {
        Artwork.Draw(c, "kor-hud-metal", new SKRect(0, 0, 1100, 80));
        _fill.Color = new SKColor(4, 6, 7, 100); c.DrawRect(new SKRect(0, 0, 1100, 80), _fill);
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

        // Textured stamped-steel command rails keep the battlefield UI grounded in Kor industrial hardware.
        Artwork.Draw(c, "kor-hud-metal", new SKRect(0, 635, 1100, 720));
        _fill.Color = new SKColor(4, 6, 7, 115); c.DrawRect(new SKRect(0, 635, 1100, 720), _fill);
        _line.Color = PanelEdge; c.DrawLine(0, 635, 1100, 635, _line);
        _fill.Color = Crimson; c.DrawRect(new SKRect(20, 651, 24, 702), _fill);
        c.DrawText("LILITH", 37, 669, SKTextAlign.Left, _heading, _text);
        c.DrawText(sim.Message, 37, 693, SKTextAlign.Left, _font, _subtle);
        _line.Color = PanelEdge; c.DrawLine(430, 649, 430, 706, _line);
        _fill.Color = Gold;
        c.DrawText("MISSION", 454, 666, SKTextAlign.Left, _font, _fill);
        c.DrawText(sim.Won ? "BLIGHT CONTAINED — SECTOR SECURE" : sim.Lost ? "COMMAND SHELTER LOST" : "DEPLOY 2 PURIFIERS TO CONTAIN THE BLIGHT", 454, 692, SKTextAlign.Left, _heading, _text);
        c.DrawText(sim.Won || sim.Lost ? "ENTER OR CLICK TO RETURN TO CAMPAIGN" : buildMode ? "BUILD MODE   /   CLICK TO PLACE   /   ESC CANCEL" : "RMB MOVE / GATHER   •   B BUILD   •   SPACE PAUSE", 454, 712, SKTextAlign.Left, _font, _subtle);
    }

    private void DrawTacticalSidebar(SKCanvas c, OutpostSimulation sim)
    {
        const float left = 874, right = 1100;
        Artwork.Draw(c, "kor-hud-metal", new SKRect(left, 80, right, 635));
        _fill.Color = new SKColor(8, 12, 14, 215); c.DrawRect(new SKRect(left, 80, right, 635), _fill);
        _line.Color = Gold; c.DrawLine(left + 1, 82, left + 1, 633, _line);
        c.DrawText("FIELD COMMAND", left + 15, 108, SKTextAlign.Left, _heading, _text);

        _fill.Color = new SKColor(5, 9, 11, 245); c.DrawRect(new SKRect(left + 12, 123, right - 12, 267), _fill);
        _line.Color = PanelEdge; c.DrawRect(new SKRect(left + 12, 123, right - 12, 267), _line);
        c.DrawText("SECTOR OVERVIEW", left + 20, 143, SKTextAlign.Left, _font, _subtle);
        const float mx = left + 20, my = 151, mw = 186, mh = 105;
        float cw = mw / OutpostSimulation.Width, ch = mh / OutpostSimulation.Height;
        for (int y = 0; y < OutpostSimulation.Height; y++)
        for (int x = 0; x < OutpostSimulation.Width; x++)
        {
            byte blight = sim.BlightAt(x, y);
            _fill.Color = blight > 0 ? new SKColor(126, 23, 34) : sim.BlockedAt(x, y) ? new SKColor(80, 81, 72) : new SKColor(45, 57, 49);
            c.DrawRect(new SKRect(mx + x * cw, my + y * ch, mx + (x + 1) * cw - 1, my + (y + 1) * ch - 1), _fill);
        }
        DrawMapContact(c, mx, my, mw, mh, OutpostSimulation.BaseX + .5f, OutpostSimulation.BaseY + .5f, new SKColor(231, 190, 102), 5);
        DrawMapContact(c, mx, my, mw, mh, sim.WorkerX, sim.WorkerY, new SKColor(220, 211, 174), 4);
        DrawMapContact(c, mx, my, mw, mh, sim.GuardianX, sim.GuardianY, Crimson, 5);
        foreach (var building in sim.Purifiers)
            DrawMapContact(c, mx, my, mw, mh, building.X + .5f, building.Y + .5f, new SKColor(110, 190, 145), 4);
        foreach (var enemy in sim.Hostiles)
            DrawMapContact(c, mx, my, mw, mh, enemy.X, enemy.Y, new SKColor(245, 70, 57), 4);

        _fill.Color = new SKColor(5, 9, 11, 245); c.DrawRect(new SKRect(left + 12, 278, right - 12, 386), _fill);
        _line.Color = PanelEdge; c.DrawRect(new SKRect(left + 12, 278, right - 12, 386), _line);
        c.DrawText("UNIT STATUS", left + 20, 298, SKTextAlign.Left, _font, _subtle);
        if (sim.Selected)
        {
            string name = sim.WorkerSelected ? "FIELD PIONEER" : "KOR GUARDIAN";
            string sprite = sim.WorkerSelected ? "kor-pioneer" : "kor-guardian";
            Artwork.Draw(c, sprite, new SKRect(left + 21, 302, left + 79, 373));
            c.DrawText(name, left + 88, 331, SKTextAlign.Left, _font, _text);
            c.DrawText("SELECTED KOR UNIT", left + 88, 354, SKTextAlign.Left, _small, _subtle);
        }
        else
        {
            Artwork.Draw(c, "kor-guardian", new SKRect(left + 21, 302, left + 72, 373));
            Artwork.Draw(c, "kor-pioneer", new SKRect(left + 75, 307, left + 121, 373));
            c.DrawText("2 KOR UNITS", left + 130, 331, SKTextAlign.Left, _small, _text);
            c.DrawText("LMB: SELECT", left + 130, 354, SKTextAlign.Left, _small, _subtle);
        }

        _text.Color = Gold; c.DrawText("MISSION STATUS", left + 16, 415, SKTextAlign.Left, _small, _text);
        _text.Color = new SKColor(235, 226, 207);
        c.DrawText("PURIFIERS", left + 16, 443, SKTextAlign.Left, _font, _text);
        c.DrawText($"{sim.CompletedPurifiers} / 2", right - 17, 443, SKTextAlign.Right, _font, _text);
        c.DrawText("SHELTER", left + 16, 469, SKTextAlign.Left, _font, _text);
        c.DrawText($"{(int)Math.Max(0, sim.BaseHealth)}%", right - 17, 469, SKTextAlign.Right, _font, _text);
        _subtle.Color = sim.Hostiles.Count > 0 ? new SKColor(231, 103, 82) : new SKColor(187, 186, 176);
        c.DrawText("BLIGHT", left + 16, 495, SKTextAlign.Left, _font, _subtle);
        c.DrawText($"{sim.Hostiles.Count} CONTACT{(sim.Hostiles.Count == 1 ? "" : "S")}", right - 17, 495, SKTextAlign.Right, _font, _subtle);
        _fill.Color = new SKColor(24, 31, 31); c.DrawRect(new SKRect(left + 16, 510, right - 16, 520), _fill);
        _fill.Color = sim.Lost ? Crimson : new SKColor(79, 157, 112);
        c.DrawRect(new SKRect(left + 16, 510, left + 16 + (right - left - 32) * Math.Clamp(sim.BaseHealth / 100f, 0, 1), 520), _fill);

        _text.Color = Gold; c.DrawText("COMMANDS", left + 16, 552, SKTextAlign.Left, _small, _text);
        _subtle.Color = new SKColor(187, 186, 176);
        c.DrawText("LMB   Select unit", left + 16, 574, SKTextAlign.Left, _small, _subtle);
        c.DrawText("RMB   Move / gather", left + 16, 591, SKTextAlign.Left, _small, _subtle);
        c.DrawText("B     Build  /  WASD pan", left + 16, 608, SKTextAlign.Left, _small, _subtle);
        c.DrawText("+/- Zoom  /  Space pause", left + 16, 625, SKTextAlign.Left, _small, _subtle);
        _text.Color = new SKColor(235, 226, 207);
        _subtle.Color = new SKColor(187, 186, 176);
    }

    private void DrawMapContact(SKCanvas c, float x, float y, float width, float height, float worldX, float worldY, SKColor color, float radius)
    {
        float px = x + Math.Clamp(worldX / OutpostSimulation.Width, 0, 1) * width;
        float py = y + Math.Clamp(worldY / OutpostSimulation.Height, 0, 1) * height;
        _fill.Color = new SKColor(4, 7, 8, 220); c.DrawCircle(px + 1, py + 2, radius + 2, _fill);
        _fill.Color = color; c.DrawCircle(px, py, radius, _fill);
        _line.Color = new SKColor(241, 230, 198, 210); c.DrawCircle(px, py, radius, _line);
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

    private static SKPath HelmetPath(float x, float y, float zoom)
    {
        var path = new SKPath();
        path.MoveTo(x - 7 * zoom, y + 1 * zoom); path.LineTo(x - 6 * zoom, y - 5 * zoom);
        path.LineTo(x - 2 * zoom, y - 8 * zoom); path.LineTo(x + 4 * zoom, y - 7 * zoom);
        path.LineTo(x + 7 * zoom, y - 2 * zoom); path.LineTo(x + 5 * zoom, y + 2 * zoom);
        path.LineTo(x - 7 * zoom, y + 1 * zoom); path.Close(); return path;
    }

    private static SKPath GuardianTorso(float x, float y, float zoom)
    {
        var path = new SKPath();
        path.MoveTo(x - 8 * zoom, y - 22 * zoom); path.LineTo(x + 8 * zoom, y - 22 * zoom);
        path.LineTo(x + 10 * zoom, y - 11 * zoom); path.LineTo(x + 6 * zoom, y - 7 * zoom);
        path.LineTo(x - 6 * zoom, y - 7 * zoom); path.LineTo(x - 10 * zoom, y - 11 * zoom);
        path.Close(); return path;
    }

    public void Dispose()
    {
        _fill.Dispose(); _line.Dispose(); _text.Dispose(); _subtle.Dispose(); _bold.Dispose();
        _font.Dispose(); _small.Dispose(); _heading.Dispose(); _title.Dispose();
    }
}
