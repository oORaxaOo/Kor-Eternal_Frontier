using SkiaSharp;

namespace KorFrontier.Gondwana.Rendering;

/// <summary>Procedurally drawn original art: no borrowed game assets.</summary>
internal static class KorArt
{
    internal const int FrameSize = 64;
    internal const int Meadow = 0, Soil = 1, Crystal = 2, Blight = 3;
    internal const int Core = 4, Pioneer = 5, Warden = 6, Solar = 7, Extractor = 8, Archive = 9, Relay = 10;
    internal const int FrameCount = 11;

    internal static SKBitmap CreateAtlas()
    {
        var bitmap = new SKBitmap(FrameSize * FrameCount, FrameSize, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        DrawGround(canvas, Meadow, new SKColor(47, 77, 63), new SKColor(68, 105, 77));
        DrawGround(canvas, Soil, new SKColor(76, 62, 48), new SKColor(112, 82, 56));
        DrawGround(canvas, Crystal, new SKColor(40, 72, 79), new SKColor(78, 195, 187));
        DrawGround(canvas, Blight, new SKColor(50, 19, 34), new SKColor(169, 35, 68));
        DrawBuilding(canvas, Core, new SKColor(183, 35, 54), true);
        DrawUnit(canvas, Pioneer, new SKColor(228, 220, 194), new SKColor(207, 50, 65));
        DrawUnit(canvas, Warden, new SKColor(45, 49, 57), new SKColor(226, 174, 76));
        DrawBuilding(canvas, Solar, new SKColor(218, 163, 59), false);
        DrawBuilding(canvas, Extractor, new SKColor(86, 150, 151), false);
        DrawBuilding(canvas, Archive, new SKColor(152, 111, 194), false);
        DrawBuilding(canvas, Relay, new SKColor(192, 48, 63), false);
        canvas.Flush();
        return bitmap;
    }

    private static int Left(int frame) => frame * FrameSize;

    private static void DrawGround(SKCanvas canvas, int frame, SKColor baseColor, SKColor accent)
    {
        int x = Left(frame);
        using var fill = new SKPaint { IsAntialias = true, Color = baseColor, Style = SKPaintStyle.Fill };
        using var edge = new SKPaint { IsAntialias = true, Color = new SKColor(10, 17, 20, 190), StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke };
        using var detail = new SKPaint { IsAntialias = true, Color = accent, StrokeWidth = 2f, Style = SKPaintStyle.Stroke };
        var diamond = new SKPath();
        diamond.MoveTo(x + 32, 14); diamond.LineTo(x + 62, 32); diamond.LineTo(x + 32, 50); diamond.LineTo(x + 2, 32); diamond.Close();
        canvas.DrawPath(diamond, fill);
        canvas.DrawPath(diamond, edge);
        if (frame == Crystal)
        {
            using var crystal = new SKPaint { IsAntialias = true, Color = accent, Style = SKPaintStyle.Fill };
            var shard = new SKPath();
            shard.MoveTo(x + 32, 20); shard.LineTo(x + 40, 32); shard.LineTo(x + 32, 39); shard.LineTo(x + 25, 31); shard.Close();
            canvas.DrawPath(shard, crystal);
            canvas.DrawLine(x + 32, 22, x + 32, 37, edge);
        }
        else if (frame == Blight)
        {
            using var tendril = new SKPaint { IsAntialias = true, Color = accent, StrokeWidth = 2f, Style = SKPaintStyle.Stroke };
            canvas.DrawLine(x + 18, 33, x + 28, 27, tendril);
            canvas.DrawLine(x + 28, 27, x + 38, 36, tendril);
            canvas.DrawLine(x + 38, 36, x + 47, 30, tendril);
        }
        else
        {
            canvas.DrawLine(x + 17, 33, x + 24, 29, detail);
            canvas.DrawLine(x + 38, 37, x + 45, 32, detail);
        }
    }

    private static void DrawUnit(SKCanvas canvas, int frame, SKColor armor, SKColor accent)
    {
        int x = Left(frame);
        using var shadow = new SKPaint { IsAntialias = true, Color = new SKColor(0, 0, 0, 100), Style = SKPaintStyle.Fill };
        using var body = new SKPaint { IsAntialias = true, Color = armor, Style = SKPaintStyle.Fill };
        using var trim = new SKPaint { IsAntialias = true, Color = accent, Style = SKPaintStyle.Fill };
        using var line = new SKPaint { IsAntialias = true, Color = new SKColor(12, 15, 19), StrokeWidth = 2, Style = SKPaintStyle.Stroke };
        canvas.DrawOval(new SKRect(x + 14, 42, x + 50, 54), shadow);
        canvas.DrawRoundRect(new SKRect(x + 19, 28, x + 45, 48), 7, 7, body);
        canvas.DrawRoundRect(new SKRect(x + 23, 30, x + 41, 40), 4, 4, trim);
        canvas.DrawCircle(x + 32, 21, 9, body);
        canvas.DrawCircle(x + 32, 21, 9, line);
        canvas.DrawRect(new SKRect(x + 28, 18, x + 36, 23), trim);
        canvas.DrawRoundRect(new SKRect(x + 17, 39, x + 47, 48), 3, 3, line);
        canvas.DrawLine(x + 22, 47, x + 20, 54, line);
        canvas.DrawLine(x + 42, 47, x + 44, 54, line);
    }

    private static void DrawBuilding(SKCanvas canvas, int frame, SKColor accent, bool commandCore)
    {
        int x = Left(frame);
        using var shadow = new SKPaint { IsAntialias = true, Color = new SKColor(0, 0, 0, 105), Style = SKPaintStyle.Fill };
        using var body = new SKPaint { IsAntialias = true, Color = new SKColor(33, 38, 44), Style = SKPaintStyle.Fill };
        using var trim = new SKPaint { IsAntialias = true, Color = accent, Style = SKPaintStyle.Fill };
        using var gold = new SKPaint { IsAntialias = true, Color = new SKColor(230, 185, 91), Style = SKPaintStyle.Fill };
        using var edge = new SKPaint { IsAntialias = true, Color = new SKColor(9, 13, 18), StrokeWidth = 2, Style = SKPaintStyle.Stroke };
        canvas.DrawOval(new SKRect(x + 8, 42, x + 56, 55), shadow);
        var basePath = new SKPath();
        basePath.MoveTo(x + 11, 40); basePath.LineTo(x + 32, 28); basePath.LineTo(x + 53, 40); basePath.LineTo(x + 32, 51); basePath.Close();
        canvas.DrawPath(basePath, trim); canvas.DrawPath(basePath, edge);
        if (commandCore)
        {
            canvas.DrawRect(new SKRect(x + 23, 16, x + 41, 39), body);
            canvas.DrawRect(new SKRect(x + 27, 10, x + 37, 20), trim);
            canvas.DrawRect(new SKRect(x + 28, 24, x + 36, 31), gold);
            canvas.DrawLine(x + 20, 18, x + 44, 18, gold);
        }
        else
        {
            canvas.DrawRoundRect(new SKRect(x + 21, 25, x + 43, 41), 3, 3, body);
            canvas.DrawRoundRect(new SKRect(x + 21, 25, x + 43, 41), 3, 3, edge);
            canvas.DrawRect(new SKRect(x + 27, 17, x + 37, 27), trim);
            canvas.DrawLine(x + 25, 31, x + 39, 31, gold);
        }
    }
}
