using SkiaSharp;

namespace KorTheBlight.Rendering;

/// <summary>Native-feeling start screen rendered through the same Skia/Gondwana surface as gameplay.</summary>
internal sealed class MainMenuRenderer : IDisposable
{
    private static readonly SKColor Background = new(12, 15, 16);
    private static readonly SKColor Panel = new(24, 29, 30);
    private static readonly SKColor Crimson = new(190, 42, 55);
    private static readonly SKColor Gold = new(213, 174, 91);
    private static readonly SKColor TextColor = new(230, 232, 225);
    private static readonly SKColor Muted = new(139, 148, 143);
    private readonly SKPaint _fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _line = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
    private readonly SKPaint _text = new() { IsAntialias = true, Color = TextColor };
    private readonly SKFont _title = new(SKTypeface.FromFamilyName("sans-serif", SKFontStyle.Bold), 34);
    private readonly SKFont _subtitle = new(SKTypeface.FromFamilyName("sans-serif"), 13);
    private readonly SKFont _button = new(SKTypeface.FromFamilyName("sans-serif", SKFontStyle.Bold), 16);
    private readonly SKFont _small = new(SKTypeface.FromFamilyName("sans-serif"), 12);

    public void Draw(SKCanvas canvas, bool settings, bool startPaused, string message)
    {
        canvas.Clear(Background);
        _fill.Color = new SKColor(17, 22, 22);
        canvas.DrawRect(new SKRect(0, 0, 1100, 720), _fill);
        _line.Color = new SKColor(55, 63, 59);
        for (int i = -720; i < 1500; i += 52) canvas.DrawLine(i, 62, i + 600, 662, _line);
        _fill.Color = Panel;
        canvas.DrawRoundRect(new SKRect(300, 82, 800, 638), 10, 10, _fill);
        _line.Color = new SKColor(76, 53, 48);
        canvas.DrawRoundRect(new SKRect(300, 82, 800, 638), 10, 10, _line);
        _text.Color = Crimson;
        canvas.DrawText("KOR", 550, 145, SKTextAlign.Center, _title, _text);
        _text.Color = TextColor;
        canvas.DrawText("ETERNAL FRONTIER", 550, 181, SKTextAlign.Center, _button, _text);
        _text.Color = Muted;
        canvas.DrawText("A living world. A colony's future. Your command.", 550, 207, SKTextAlign.Center, _subtitle, _text);
        if (settings)
        {
            _text.Color = Gold;
            canvas.DrawText("EINSTELLUNGEN", 550, 247, SKTextAlign.Center, _button, _text);
            DrawButton(canvas, startPaused ? "Spielstart: pausiert" : "Spielstart: laufend", 370, 420, 360, 52, true);
            DrawButton(canvas, "Zurück zum Hauptmenü", 370, 490, 360, 52);
            _text.Color = Muted;
            canvas.DrawText("Die Einstellung gilt für neu gestartete Spiele.", 550, 578, SKTextAlign.Center, _small, _text);
        }
        else
        {
            DrawButton(canvas, "NEUES SPIEL", 370, 260, 360, 52, true);
            DrawButton(canvas, "SPIEL LADEN", 370, 326, 360, 52);
            DrawButton(canvas, "EINSTELLUNGEN", 370, 392, 360, 52);
            DrawButton(canvas, "BEENDEN", 370, 458, 360, 52);
            _text.Color = Muted;
            canvas.DrawText("Linksklick zum Auswählen  •  ESC/Schließen zum Beenden", 550, 578, SKTextAlign.Center, _small, _text);
        }
        if (!string.IsNullOrWhiteSpace(message))
        {
            _text.Color = new SKColor(235, 150, 125);
            canvas.DrawText(message.Length > 68 ? message[..65] + "..." : message, 550, 610, SKTextAlign.Center, _small, _text);
        }
        _text.Color = Gold;
        canvas.DrawText("KOR DYNASTY // FRONTIER COMMAND", 550, 668, SKTextAlign.Center, _small, _text);
    }

    private void DrawButton(SKCanvas canvas, string label, float x, float y, float width, float height, bool active = false)
    {
        _fill.Color = active ? new SKColor(72, 30, 36) : new SKColor(31, 39, 37);
        canvas.DrawRoundRect(new SKRect(x, y, x + width, y + height), 5, 5, _fill);
        _line.Color = active ? Gold : new SKColor(67, 78, 71);
        canvas.DrawRoundRect(new SKRect(x, y, x + width, y + height), 5, 5, _line);
        _text.Color = active ? Gold : TextColor;
        canvas.DrawText(label, x + width / 2, y + height / 2 + 6, SKTextAlign.Center, _button, _text);
    }

    public void Dispose()
    {
        _fill.Dispose(); _line.Dispose(); _text.Dispose();
        _title.Dispose(); _subtitle.Dispose(); _button.Dispose(); _small.Dispose();
    }
}
