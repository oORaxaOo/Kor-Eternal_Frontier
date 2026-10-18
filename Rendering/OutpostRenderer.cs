using KorTheBlight.Simulation;
using SkiaSharp;

namespace KorTheBlight.Rendering;

/// <summary>
/// Minimal symbol renderer for the first orthogonal top-down simulation slice.
/// It is presentation-only and never mutates simulation state.
/// </summary>
internal sealed class OutpostRenderer : IDisposable
{
    private static readonly SKColor Background = new(10, 13, 14);
    private static readonly SKColor Panel = new(19, 24, 25);
    private static readonly SKColor Border = new(64, 75, 72);
    private static readonly SKColor TextColor = new(230, 229, 216);
    private static readonly SKColor MutedColor = new(160, 173, 165);
    private static readonly SKColor Crimson = new(192, 42, 54);
    private static readonly SKColor Gold = new(216, 177, 89);
    private static readonly SKColor WorkerColor = new(216, 225, 204);
    private static readonly SKColor GuardianColor = new(100, 193, 178);

    private readonly SKPaint _fill = new() { IsAntialias = true };
    private readonly SKPaint _line = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
    private readonly SKPaint _text = new() { IsAntialias = true, Color = TextColor };
    private readonly SKPaint _muted = new() { IsAntialias = true, Color = MutedColor };
    private readonly SKFont _normalFont = new(SKTypeface.FromFamilyName("sans-serif"), 14);
    private readonly SKFont _smallFont = new(SKTypeface.FromFamilyName("sans-serif"), 12);
    private readonly SKFont _headingFont = new(SKTypeface.FromFamilyName("sans-serif", SKFontStyle.Bold), 18);
    private readonly SKFont _titleFont = new(SKTypeface.FromFamilyName("sans-serif", SKFontStyle.Bold), 23);

    public void Draw(SKCanvas canvas, OutpostSimulation simulation, float cameraX, float cameraY, float zoom, bool buildMode)
    {
        canvas.Clear(Background);
        DrawWorld(canvas, simulation, cameraX, cameraY, zoom);
        DrawResourceNode(canvas, 2, 4, "A", new SKColor(190, 166, 112), cameraX, cameraY, zoom);
        DrawResourceNode(canvas, 10, 3, "S", new SKColor(131, 174, 130), cameraX, cameraY, zoom);
        DrawBase(canvas, cameraX, cameraY, zoom);
        DrawPurifiers(canvas, simulation, cameraX, cameraY, zoom);
        DrawPeople(canvas, simulation, cameraX, cameraY, zoom);
        DrawHostiles(canvas, simulation, cameraX, cameraY, zoom);
        DrawHud(canvas, simulation, buildMode);
    }

    private void DrawWorld(SKCanvas canvas, OutpostSimulation simulation, float cameraX, float cameraY, float zoom)
    {
        float tileSize = OrthogonalGridProjection.TileSize * zoom;
        for (int y = 0; y < OutpostSimulation.Height; y++)
        for (int x = 0; x < OutpostSimulation.Width; x++)
        {
            var point = OrthogonalGridProjection.TileCenter(x, y, cameraX, cameraY, zoom);
            var rect = new SKRect(point.X - tileSize / 2f, point.Y - tileSize / 2f, point.X + tileSize / 2f, point.Y + tileSize / 2f);
            if (rect.Right < 0 || rect.Left > 770 || rect.Bottom < 62 || rect.Top > 650) continue;

            uint hash = unchecked((uint)(x * 73856093 ^ y * 19349663));
            byte shade = (byte)(28 + hash % 9);
            _fill.Color = new SKColor((byte)(shade - 3), (byte)(shade + 5), (byte)(shade + 2));
            canvas.DrawRect(rect, _fill);

            byte infection = simulation.BlightAt(x, y);
            if (infection > 0)
            {
                _fill.Color = new SKColor(133, 23, 39, (byte)Math.Clamp(55 + infection * 45, 0, 220));
                canvas.DrawRect(rect, _fill);
                _line.Color = new SKColor(199, 45, 60, 210);
                _line.StrokeWidth = 1.3f;
                canvas.DrawRect(Inset(rect, tileSize * 0.13f), _line);
            }

            _line.Color = new SKColor(67, 80, 75, 185);
            _line.StrokeWidth = Math.Max(0.6f, zoom * 0.8f);
            canvas.DrawRect(rect, _line);

            if (simulation.RockAt(x, y))
            {
                var rock = Inset(rect, tileSize * 0.19f);
                _fill.Color = new SKColor(88, 94, 87);
                canvas.DrawRect(rock, _fill);
                _line.Color = new SKColor(143, 146, 132);
                canvas.DrawRect(rock, _line);
            }
        }
    }

    private void DrawResourceNode(SKCanvas canvas, int x, int y, string label, SKColor color,
        float cameraX, float cameraY, float zoom)
    {
        var point = OrthogonalGridProjection.TileCenter(x, y, cameraX, cameraY, zoom);
        float size = 19f * zoom;
        var rect = new SKRect(point.X - size / 2, point.Y - size / 2, point.X + size / 2, point.Y + size / 2);
        _fill.Color = color;
        canvas.DrawRect(rect, _fill);
        _line.Color = new SKColor(12, 15, 14);
        _line.StrokeWidth = 1.5f;
        canvas.DrawRect(rect, _line);
        canvas.DrawText(label, point.X, point.Y + 4f * zoom, SKTextAlign.Center, _smallFont, _text);
    }

    private void DrawBase(SKCanvas canvas, float cameraX, float cameraY, float zoom)
    {
        var point = OrthogonalGridProjection.TileCenter(OutpostSimulation.BaseX, OutpostSimulation.BaseY, cameraX, cameraY, zoom);
        float size = OrthogonalGridProjection.TileSize * zoom * 0.82f;
        var rect = new SKRect(point.X - size / 2, point.Y - size / 2, point.X + size / 2, point.Y + size / 2);
        _fill.Color = new SKColor(86, 24, 31);
        canvas.DrawRect(rect, _fill);
        _line.Color = Crimson;
        _line.StrokeWidth = Math.Max(1f, zoom * 1.6f);
        canvas.DrawRect(rect, _line);
        canvas.DrawText("H", point.X, point.Y + 5f * zoom, SKTextAlign.Center, _headingFont, _text);
    }

    private void DrawPurifiers(SKCanvas canvas, OutpostSimulation simulation, float cameraX, float cameraY, float zoom)
    {
        foreach (var purifier in simulation.Purifiers)
        {
            var point = OrthogonalGridProjection.WorldToScreen(purifier.X, purifier.Y, cameraX, cameraY, zoom);
            float size = OrthogonalGridProjection.TileSize * zoom * 0.68f;
            var rect = new SKRect(point.X - size / 2, point.Y - size / 2, point.X + size / 2, point.Y + size / 2);
            _fill.Color = purifier.Progress >= 1f ? new SKColor(63, 74, 49) : new SKColor(40, 48, 44);
            canvas.DrawRect(rect, _fill);
            _line.Color = purifier.Progress >= 1f ? Gold : MutedColor;
            _line.StrokeWidth = Math.Max(1f, zoom * 1.4f);
            canvas.DrawRect(rect, _line);
            if (purifier.Progress < 1f)
            {
                var inner = Inset(rect, size * 0.3f);
                _line.Color = Gold;
                canvas.DrawRect(inner, _line);
            }
        }
    }

    private void DrawPeople(SKCanvas canvas, OutpostSimulation simulation, float cameraX, float cameraY, float zoom)
    {
        var worker = OrthogonalGridProjection.WorldToScreen(simulation.WorkerX, simulation.WorkerY, cameraX, cameraY, zoom);
        DrawPerson(canvas, worker.X, worker.Y, WorkerColor, simulation.WorkerSelected, zoom);

        var guardian = OrthogonalGridProjection.WorldToScreen(simulation.GuardianX, simulation.GuardianY, cameraX, cameraY, zoom);
        DrawPerson(canvas, guardian.X, guardian.Y, GuardianColor, simulation.GuardianSelected, zoom);
    }

    private void DrawPerson(SKCanvas canvas, float x, float y, SKColor color, bool selected, float zoom)
    {
        float radius = Math.Max(3.5f, 5f * zoom);
        _fill.Color = color;
        canvas.DrawCircle(x, y, radius, _fill);
        if (selected)
        {
            _line.Color = Gold;
            _line.StrokeWidth = Math.Max(1f, 1.7f * zoom);
            canvas.DrawCircle(x, y, radius + 4f * zoom, _line);
        }
    }

    private void DrawHostiles(SKCanvas canvas, OutpostSimulation simulation, float cameraX, float cameraY, float zoom)
    {
        foreach (var hostile in simulation.Hostiles)
        {
            var point = OrthogonalGridProjection.WorldToScreen(hostile.X, hostile.Y, cameraX, cameraY, zoom);
            float size = Math.Max(5f, 8f * zoom);
            using var path = new SKPath();
            path.MoveTo(point.X, point.Y - size);
            path.LineTo(point.X + size * 0.85f, point.Y + size * 0.7f);
            path.LineTo(point.X - size * 0.85f, point.Y + size * 0.7f);
            path.Close();
            _fill.Color = Crimson;
            canvas.DrawPath(path, _fill);
            _line.Color = new SKColor(255, 112, 112);
            _line.StrokeWidth = 1f;
            canvas.DrawPath(path, _line);
        }
    }

    private void DrawHud(SKCanvas canvas, OutpostSimulation simulation, bool buildMode)
    {
        _fill.Color = Background;
        canvas.DrawRect(new SKRect(0, 0, 1100, 62), _fill);
        _fill.Color = Panel;
        canvas.DrawRect(new SKRect(770, 62, 1100, 650), _fill);
        canvas.DrawRect(new SKRect(0, 650, 1100, 720), _fill);
        _line.Color = Border;
        _line.StrokeWidth = 1;
        canvas.DrawLine(0, 62, 1100, 62, _line);
        canvas.DrawLine(770, 62, 770, 650, _line);
        canvas.DrawLine(0, 650, 1100, 650, _line);

        _text.Color = Crimson;
        canvas.DrawText("KOR ETERNAL FRONTIER", 22, 28, SKTextAlign.Left, _titleFont, _text);
        _muted.Color = MutedColor;
        canvas.DrawText("COLONY SIMULATION / ORTHOGONAL GRID", 23, 48, SKTextAlign.Left, _smallFont, _muted);
        _text.Color = TextColor;
        canvas.DrawText($"SUPPLIES  {simulation.Supplies}", 430, 27, SKTextAlign.Left, _normalFont, _text);
        canvas.DrawText($"ALLOY  {simulation.Alloy}", 600, 27, SKTextAlign.Left, _normalFont, _text);

        _text.Color = Gold;
        canvas.DrawText("OUTPOST STATUS", 792, 94, SKTextAlign.Left, _headingFont, _text);
        _text.Color = TextColor;
        canvas.DrawText($"Worker: {(simulation.WorkerSelected ? "selected" : "present")}", 792, 126, SKTextAlign.Left, _normalFont, _text);
        canvas.DrawText($"Guardian: {(simulation.GuardianSelected ? "selected" : "present")}", 792, 152, SKTextAlign.Left, _normalFont, _text);
        canvas.DrawText($"Purifiers: {simulation.CompletedPurifiers}/2", 792, 186, SKTextAlign.Left, _normalFont, _text);
        canvas.DrawText($"Base health: {Math.Max(0, (int)simulation.BaseHealth)}", 792, 212, SKTextAlign.Left, _normalFont, _text);

        int infectedTiles = 0;
        for (int y = 0; y < OutpostSimulation.Height; y++)
        for (int x = 0; x < OutpostSimulation.Width; x++)
            if (simulation.BlightAt(x, y) > 0) infectedTiles++;
        canvas.DrawText($"Blight tiles: {infectedTiles}", 792, 238, SKTextAlign.Left, _normalFont, _text);

        _text.Color = Gold;
        canvas.DrawText("CONTROLS", 792, 286, SKTextAlign.Left, _headingFont, _text);
        _text.Color = TextColor;
        canvas.DrawText("W A S D  - move camera", 792, 314, SKTextAlign.Left, _smallFont, _text);
        canvas.DrawText("+ / -  - zoom", 792, 336, SKTextAlign.Left, _smallFont, _text);
        canvas.DrawText("Left click - select", 792, 358, SKTextAlign.Left, _smallFont, _text);
        canvas.DrawText("Right click - command", 792, 380, SKTextAlign.Left, _smallFont, _text);
        canvas.DrawText("B - place purifier", 792, 402, SKTextAlign.Left, _smallFont, _text);
        canvas.DrawText("Esc - cancel", 792, 424, SKTextAlign.Left, _smallFont, _text);

        _text.Color = buildMode ? Gold : MutedColor;
        canvas.DrawText(buildMode ? "BUILD MODE: choose a clear tile." : "B enters purifier placement mode.", 22, 674, SKTextAlign.Left, _normalFont, _text);
        _text.Color = TextColor;
        canvas.DrawText(simulation.Message, 22, 700, SKTextAlign.Left, _smallFont, _text);
    }

    private static SKRect Inset(SKRect rect, float amount)
        => new(rect.Left + amount, rect.Top + amount, rect.Right - amount, rect.Bottom - amount);

    public void Dispose()
    {
        _fill.Dispose();
        _line.Dispose();
        _text.Dispose();
        _muted.Dispose();
        _normalFont.Dispose();
        _smallFont.Dispose();
        _headingFont.Dispose();
        _titleFont.Dispose();
    }
}
