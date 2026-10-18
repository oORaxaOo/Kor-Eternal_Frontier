using System;
using SkiaSharp;
using KorTheBlight.Simulation;
using System.Linq;

namespace KorTheBlight.Rendering;

internal sealed class OutpostRenderer : IDisposable
{
    private readonly SKPaint _tileFill = new() { IsAntialias = false };
    private readonly SKPaint _tileBorder = new() { IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
    private readonly SKPaint _unitPaint = new() { IsAntialias = true };
    private readonly SKPaint _text = new() { IsAntialias = true, Color = new SKColor(235, 226, 207) };
    private readonly SKFont _font = new(SKTypeface.FromFamilyName("sans-serif"), 14);

    public void Draw(SKCanvas c, OutpostSimulation sim, float cameraX, float cameraY, float zoom, bool buildMode)
    {
        // background
        c.Clear(new SKColor(12, 15, 17));

        // draw tiles as simple orthographic squares
        for (int y = 0; y < OutpostSimulation.Height; y++)
        for (int x = 0; x < OutpostSimulation.Width; x++)
        {
            var center = OutpostSimulation.TileCenter(x, y, cameraX, cameraY, zoom);
            float half = OutpostSimulation.TileWidth * zoom / 2f;
            var rect = new SKRect(center.X - half, center.Y - half, center.X + half, center.Y + half);

            if (sim.BlockedAt(x, y)) _tileFill.Color = new SKColor(55, 60, 62);
            else if (sim.BlightAt(x, y) > 0) _tileFill.Color = new SKColor(110, 20, 30);
            else _tileFill.Color = new SKColor(30, 36, 32);

            c.DrawRect(rect, _tileFill);
            _tileBorder.Color = new SKColor(20, 25, 26);
            c.DrawRect(rect, _tileBorder);
        }

        // draw resources: simple markers
        DrawNode(c, 2, 4, SKColors.Orange, cameraX, cameraY, zoom);
        DrawNode(c, 10, 3, SKColors.Green, cameraX, cameraY, zoom);

        // draw purifiers with progress
        foreach (var p in sim.Purifiers)
        {
            var pos = OutpostSimulation.TileCenter((int)p.X, (int)p.Y, cameraX, cameraY, zoom);
            float size = OutpostSimulation.TileWidth * zoom * 0.6f;
            var r = new SKRect(pos.X - size/2, pos.Y - size/2, pos.X + size/2, pos.Y + size/2);
            _unitPaint.Color = p.Progress >= 1f ? new SKColor(200, 180, 90) : new SKColor(120, 40, 60);
            c.DrawRect(r, _unitPaint);
            if (p.Progress < 1f)
            {
                var bar = new SKRect(r.Left, r.Bottom + 4, r.Left + (r.Width * Math.Clamp(p.Progress, 0, 1)), r.Bottom + 10);
                _unitPaint.Color = new SKColor(180, 160, 90);
                c.DrawRect(bar, _unitPaint);
            }
        }

        // draw hostiles as red triangles
        foreach (var h in sim.Hostiles)
        {
            var p = OutpostSimulation.WorldScreen(h.X, h.Y, cameraX, cameraY, zoom);
            DrawTriangle(c, p.X, p.Y, OutpostSimulation.TileWidth * zoom * 0.4f, new SKColor(180, 30, 40));
        }

        // draw guardian as square
        var g = sim.GuardianScreen(cameraX, cameraY, zoom);
        _unitPaint.Color = new SKColor(160, 200, 215);
        float gs = OutpostSimulation.TileWidth * zoom * 0.45f;
        c.DrawRect(new SKRect(g.X - gs/2, g.Y - gs/2, g.X + gs/2, g.Y + gs/2), _unitPaint);

        // draw worker as circle
        var w = sim.WorkerScreen(cameraX, cameraY, zoom);
        _unitPaint.Color = new SKColor(220, 210, 180);
        float ws = OutpostSimulation.TileWidth * zoom * 0.35f;
        c.DrawCircle(w.X, w.Y, ws/2, _unitPaint);

        // HUD: simple top bar
        DrawHud(c, sim, buildMode);
    }

    private void DrawNode(SKCanvas c, int x, int y, SKColor color, float cameraX, float cameraY, float zoom)
    {
        var p = OutpostSimulation.TileCenter(x, y, cameraX, cameraY, zoom);
        _unitPaint.Color = color;
        float s = OutpostSimulation.TileWidth * zoom * 0.5f;
        c.DrawCircle(p.X, p.Y - s*0.15f, s*0.35f, _unitPaint);
    }

    private void DrawTriangle(SKCanvas c, float x, float y, float size, SKColor color)
    {
        using var path = new SKPath();
        path.MoveTo(x, y - size/2);
        path.LineTo(x - size/2, y + size/2);
        path.LineTo(x + size/2, y + size/2);
        path.Close();
        _unitPaint.Color = color; c.DrawPath(path, _unitPaint);
    }

    private void DrawHud(SKCanvas c, OutpostSimulation sim, bool buildMode)
    {
        // top bar background
        var bar = new SKRect(0, 0, 1100, 80);
        _tileFill.Color = new SKColor(8, 10, 12, 230);
        c.DrawRect(bar, _tileFill);
        _text.Color = new SKColor(230, 224, 210);
        c.DrawText($"ALLOY: {sim.Alloy}", 20, 32, _font, _text);
        c.DrawText($"SUPPLIES: {sim.Supplies}", 180, 32, _font, _text);
        c.DrawText($"PURIFIERS: {sim.CompletedPurifiers}/2", 380, 32, _font, _text);
        c.DrawText(sim.Message, 20, 60, _font, _text);
        if (buildMode)
        {
            c.DrawText("BUILD MODE - click to place", 820, 32, _font, _text);
        }
    }

    public void Dispose()
    {
        _tileFill.Dispose(); _tileBorder.Dispose(); _unitPaint.Dispose(); _text.Dispose(); _font.Dispose();
    }
}
