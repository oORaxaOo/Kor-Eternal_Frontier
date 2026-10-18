using KorTheBlight.Campaign;
using SkiaSharp;

namespace KorTheBlight.Rendering;

internal sealed class WorldMapRenderer : IDisposable
{
    private static readonly SKColor Background = new(9, 12, 14);
    private static readonly SKColor Text = new(229, 230, 219);
    private static readonly SKColor Muted = new(151, 166, 160);
    private readonly SKPaint _fill = new() { IsAntialias = true };
    private readonly SKPaint _line = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
    private readonly SKFont _normal = new(SKTypeface.FromFamilyName("sans-serif"), 14);
    private readonly SKFont _small = new(SKTypeface.FromFamilyName("sans-serif"), 11);
    private readonly SKFont _title = new(SKTypeface.FromFamilyName("sans-serif", SKFontStyle.Bold), 24);
    private readonly SKFont _heading = new(SKTypeface.FromFamilyName("sans-serif", SKFontStyle.Bold), 16);

    public void Draw(SKCanvas canvas, WorldMapSimulation world)
    {
        canvas.Clear(Background);
        DrawText(canvas, "KOR ETERNAL FRONTIER  /  STRATEGIC WORLD", 24, 35, _title, Text);
        DrawText(canvas, $"WORLD TURN {world.State.Turn}   •   {WorldMapSimulation.Width * WorldMapSimulation.Height} TERRITORIES", 26, 59, _small, Muted);
        DrawText(canvas, $"TREASURY  {world.State.EmpireSupplies} SUPPLIES  /  {world.State.EmpireAlloy} ALLOY", 515, 59, _small, new SKColor(225, 196, 120));

        const float originX = 24, originY = 82, cell = 29;
        foreach (var t in world.Territories)
        {
            float x = originX + t.X * cell, y = originY + t.Y * cell;
            var rect = new SKRect(x, y, x + cell - 2, y + cell - 2);
            _fill.Color = t.Owner switch
            {
                WorldFactionKind.Kor => new SKColor(151, 35, 48),
                WorldFactionKind.AshenCompact => new SKColor(112, 95, 76),
                WorldFactionKind.FreeMarches => new SKColor(95, 117, 132),
                WorldFactionKind.HelionLeague => new SKColor(173, 143, 70),
                WorldFactionKind.VerdantCovenant => new SKColor(64, 115, 77),
                _ => new SKColor(157, 24, 38)
            };
            if (!t.Explored && t.Owner != WorldFactionKind.Kor) _fill.Color = new SKColor(35, 42, 43);
            canvas.DrawRect(rect, _fill);
            if (t.BlightPressure > 0 && t.Owner != WorldFactionKind.Blight)
            {
                _line.Color = new SKColor(245, 36, 58, (byte)(80 + t.BlightPressure * 45));
                _line.StrokeWidth = 2 + t.BlightPressure;
                canvas.DrawRect(Inset(rect, 3), _line);
            }
            if (t.HasSettlement)
            {
                _fill.Color = new SKColor(235, 224, 190);
                canvas.DrawCircle(rect.MidX, rect.MidY, 4, _fill);
            }
            if (t.Id == world.Selected.Id)
            {
                _line.Color = new SKColor(245, 220, 141);
                _line.StrokeWidth = 3;
                canvas.DrawRect(Inset(rect, -1), _line);
            }
        }

        float panelX = 795;
        _fill.Color = new SKColor(20, 26, 27);
        canvas.DrawRoundRect(new SKRect(panelX, 82, 1080, 650), 8, 8, _fill);
        _line.Color = new SKColor(67, 81, 75);
        canvas.DrawRoundRect(new SKRect(panelX, 82, 1080, 650), 8, 8, _line);

        var selected = world.Selected;
        DrawText(canvas, "TERRITORY INTELLIGENCE", panelX + 16, 112, _heading, Text);
        DrawText(canvas, WorldMapSimulation.FactionName(selected.Owner), panelX + 16, 143, _normal, Text);
        DrawText(canvas, $"Coordinates: {selected.X + 1}, {selected.Y + 1}", panelX + 16, 168, _small, Muted);
        DrawText(canvas, $"Development: {selected.Development}", panelX + 16, 190, _small, Muted);
        DrawText(canvas, $"Local supplies: {selected.Supplies}", panelX + 16, 212, _small, Muted);
        DrawText(canvas, $"Blight pressure: {selected.BlightPressure}/3", panelX + 16, 234, _small, selected.BlightPressure > 0 ? new SKColor(245, 80, 87) : Muted);
        DrawText(canvas, $"Diplomacy: {world.RelationTo(selected.Owner)}", panelX + 16, 273, _normal, new SKColor(225, 196, 120));
        DrawText(canvas, "FACTIONS", panelX + 16, 314, _heading, Text);
        int row = 0;
        foreach (var faction in Enum.GetValues<WorldFactionKind>())
        {
            _fill.Color = FactionColor(faction);
            canvas.DrawRect(new SKRect(panelX + 16, 331 + row * 25, panelX + 29, 344 + row * 25), _fill);
            DrawText(canvas, WorldMapSimulation.FactionName(faction), panelX + 38, 342 + row * 25, _small, Text);
            row++;
        }
        DrawText(canvas, "CONTROLS", panelX + 16, 500, _heading, Text);
        DrawText(canvas, "M   Return to colony / world map", panelX + 16, 523, _small, Muted);
        DrawText(canvas, "Click territory to inspect", panelX + 16, 544, _small, Muted);
        DrawText(canvas, "C   Claim adjacent frontier (40 S / 25 A)", panelX + 16, 565, _small, Muted);
        DrawText(canvas, "Y   Offer non-aggression pact", panelX + 16, 586, _small, Muted);
        DrawText(canvas, "G   Propose trade agreement", panelX + 16, 607, _small, Muted);
        DrawText(canvas, "R   Diplomatic mission   |   T   World turn", panelX + 16, 628, _small, Muted);
        _fill.Color = new SKColor(21, 27, 28);
        canvas.DrawRect(new SKRect(24, 614, 765, 660), _fill);
        DrawText(canvas, world.Message, 34, 642, _small, Text);
        DrawText(canvas, "THE BLIGHT IS A CONTINENTAL THREAT — not just a colony encounter.", 24, 690, _small, new SKColor(222, 78, 86));
    }

    private static SKColor FactionColor(WorldFactionKind f) => f switch
    {
        WorldFactionKind.Kor => new SKColor(151, 35, 48),
        WorldFactionKind.AshenCompact => new SKColor(112, 95, 76),
        WorldFactionKind.FreeMarches => new SKColor(95, 117, 132),
        WorldFactionKind.HelionLeague => new SKColor(173, 143, 70),
        WorldFactionKind.VerdantCovenant => new SKColor(64, 115, 77),
        _ => new SKColor(157, 24, 38)
    };
    private static SKRect Inset(SKRect r, float a) => new(r.Left - a, r.Top - a, r.Right + a, r.Bottom + a);
    private static void DrawText(SKCanvas canvas, string text, float x, float y, SKFont font, SKColor color)
    {
        using var paint = new SKPaint { IsAntialias = true, Color = color };
        canvas.DrawText(text, x, y, SKTextAlign.Left, font, paint);
    }
    public void Dispose() { _normal.Dispose(); _small.Dispose(); _title.Dispose(); _heading.Dispose(); _fill.Dispose(); _line.Dispose(); }
}
