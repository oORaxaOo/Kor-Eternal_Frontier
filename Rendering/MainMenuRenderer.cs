using SkiaSharp;

namespace KorTheBlight.Rendering;

/// <summary>
/// Start screen for KOR Eternal Frontier. All geometry stays in the game's 1100x720 logical
/// coordinate space so the existing mouse hit targets remain aligned with the visible controls.
/// </summary>
internal sealed class MainMenuRenderer : IDisposable
{
    private static readonly SKColor Background = new(9, 13, 14);
    private static readonly SKColor Panel = new(19, 25, 26);
    private static readonly SKColor PanelRaised = new(27, 35, 35);
    private static readonly SKColor Crimson = new(192, 43, 56);
    private static readonly SKColor Gold = new(216, 177, 91);
    private static readonly SKColor TextColor = new(235, 237, 229);
    private static readonly SKColor Muted = new(147, 160, 153);
    private static readonly SKColor Border = new(62, 76, 71);

    private readonly SKPaint _fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _line = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
    private readonly SKPaint _text = new() { IsAntialias = true, Color = TextColor };
    private readonly SKFont _wordmark = new(SKTypeface.FromFamilyName("sans-serif", SKFontStyle.Bold), 46);
    private readonly SKFont _title = new(SKTypeface.FromFamilyName("sans-serif", SKFontStyle.Bold), 24);
    private readonly SKFont _button = new(SKTypeface.FromFamilyName("sans-serif", SKFontStyle.Bold), 15);
    private readonly SKFont _body = new(SKTypeface.FromFamilyName("sans-serif"), 13);
    private readonly SKFont _small = new(SKTypeface.FromFamilyName("sans-serif"), 11);

    public void Draw(SKCanvas canvas, bool settings, bool startPaused, string message)
    {
        canvas.Clear(Background);

        // Restrained command-centre backdrop: readable first, decorative second.
        _fill.Color = new SKColor(13, 19, 19);
        canvas.DrawRect(new SKRect(0, 0, 1100, 720), _fill);
        _line.Color = new SKColor(29, 43, 39, 150);
        _line.StrokeWidth = 1;
        for (int x = -300; x < 1450; x += 54)
            canvas.DrawLine(x, 0, x + 340, 720, _line);
        _fill.Color = new SKColor(190, 42, 55, 16);
        canvas.DrawCircle(1050, 95, 250, _fill);

        // Left identity rail.
        _fill.Color = new SKColor(12, 17, 18, 245);
        canvas.DrawRect(new SKRect(0, 0, 280, 720), _fill);
        _fill.Color = Crimson;
        canvas.DrawRect(new SKRect(0, 0, 5, 720), _fill);
        _text.Color = Crimson;
        canvas.DrawText("KOR", 36, 86, SKTextAlign.Left, _wordmark, _text);
        _text.Color = TextColor;
        canvas.DrawText("ETERNAL FRONTIER", 39, 113, SKTextAlign.Left, _button, _text);
        _fill.Color = Gold;
        canvas.DrawRect(new SKRect(39, 134, 87, 137), _fill);
        _text.Color = Muted;
        canvas.DrawText("COLONY COMMAND", 39, 172, SKTextAlign.Left, _small, _text);
        canvas.DrawText("A living world.", 39, 213, SKTextAlign.Left, _title, _text);
        canvas.DrawText("A colony's future.", 39, 244, SKTextAlign.Left, _title, _text);
        _text.Color = Muted;
        canvas.DrawText("Your decisions shape the frontier.", 39, 275, SKTextAlign.Left, _body, _text);

        // A quiet, original map-like motif rather than a static screenshot or borrowed game art.
        _line.Color = new SKColor(71, 91, 78, 130);
        _line.StrokeWidth = 1.2f;
        var points = new SKPoint[]
        {
            new(40, 380), new(83, 356), new(121, 378), new(151, 344), new(194, 367),
            new(227, 351), new(246, 390), new(211, 422), new(174, 410), new(145, 447),
            new(104, 426), new(68, 446), new(40, 414), new(40, 380)
        };
        for (int i = 1; i < points.Length; i++)
            canvas.DrawLine(points[i - 1], points[i], _line);
        _fill.Color = Crimson;
        canvas.DrawCircle(151, 344, 4, _fill);
        _fill.Color = Gold;
        canvas.DrawCircle(104, 426, 3, _fill);
        _text.Color = Muted;
        canvas.DrawText("FRONTIER SECTOR 01", 39, 485, SKTextAlign.Left, _small, _text);
        _line.Color = Border;
        canvas.DrawLine(39, 510, 238, 510, _line);
        _text.Color = Gold;
        canvas.DrawText("ALLE FÜR ALLE", 39, 542, SKTextAlign.Left, _button, _text);
        _text.Color = Muted;
        canvas.DrawText("KOR DYNASTY  /  FIELD SYSTEMS", 39, 683, SKTextAlign.Left, _small, _text);

        // Main surface.
        _fill.Color = Panel;
        canvas.DrawRoundRect(new SKRect(312, 54, 1064, 650), 10, 10, _fill);
        _line.Color = Border;
        canvas.DrawRoundRect(new SKRect(312, 54, 1064, 650), 10, 10, _line);
        _fill.Color = Crimson;
        canvas.DrawRoundRect(new SKRect(312, 54, 1064, 59), 2, 2, _fill);

        if (settings)
        {
            _text.Color = Gold;
            canvas.DrawText("SYSTEM CONFIGURATION", 356, 108, SKTextAlign.Left, _small, _text);
            _text.Color = TextColor;
            canvas.DrawText("Einstellungen", 356, 145, SKTextAlign.Left, _title, _text);
            _text.Color = Muted;
            canvas.DrawText("Lege fest, wie eine neue Sitzung beginnen soll.", 356, 171, SKTextAlign.Left, _body, _text);
            DrawSettingRow(canvas, "Spielstart", startPaused ? "Pausiert" : "Laufend", 356, 225, 708, 68, startPaused);
            DrawButton(canvas, "SPIELSTART UMSCHALTEN", 370, 420, 360, 52, true);
            DrawButton(canvas, "ZURÜCK", 370, 490, 360, 52);
            _text.Color = Muted;
            canvas.DrawText("Diese Einstellung gilt für neu gestartete Spiele.", 356, 588, SKTextAlign.Left, _small, _text);
        }
        else
        {
            _text.Color = Gold;
            canvas.DrawText("COMMAND DECK  /  MAIN MENU", 356, 100, SKTextAlign.Left, _small, _text);
            _text.Color = TextColor;
            canvas.DrawText("Deine Frontier beginnt hier.", 356, 137, SKTextAlign.Left, _title, _text);
            _text.Color = Muted;
            canvas.DrawText("Wähle eine Sitzung oder passe deine Startoptionen an.", 356, 163, SKTextAlign.Left, _body, _text);

            DrawButton(canvas, "NEUES SPIEL", 370, 260, 360, 52, true, "01");
            DrawButton(canvas, "SPIEL LADEN", 370, 326, 360, 52, false, "02");
            DrawButton(canvas, "EINSTELLUNGEN", 370, 392, 360, 52, false, "03");
            DrawButton(canvas, "BEENDEN", 370, 458, 360, 52, false, "04");

            _fill.Color = PanelRaised;
            canvas.DrawRoundRect(new SKRect(770, 260, 1028, 510), 7, 7, _fill);
            _line.Color = Border;
            canvas.DrawRoundRect(new SKRect(770, 260, 1028, 510), 7, 7, _line);
            _text.Color = Gold;
            canvas.DrawText("FIELD STATUS", 790, 290, SKTextAlign.Left, _small, _text);
            _line.Color = Border;
            canvas.DrawLine(790, 305, 1008, 305, _line);
            _text.Color = TextColor;
            canvas.DrawText("Welt", 790, 334, SKTextAlign.Left, _body, _text);
            _text.Color = Muted;
            canvas.DrawText("Prozedural", 1008, 334, SKTextAlign.Right, _small, _text);
            _text.Color = TextColor;
            canvas.DrawText("Simulation", 790, 365, SKTextAlign.Left, _body, _text);
            _text.Color = Muted;
            canvas.DrawText("Echtzeit", 1008, 365, SKTextAlign.Right, _small, _text);
            _text.Color = TextColor;
            canvas.DrawText("Steuerung", 790, 396, SKTextAlign.Left, _body, _text);
            _text.Color = Muted;
            canvas.DrawText("Maus zuerst", 1008, 396, SKTextAlign.Right, _small, _text);
            _fill.Color = new SKColor(192, 43, 56, 55);
            canvas.DrawRoundRect(new SKRect(790, 431, 1008, 475), 5, 5, _fill);
            _text.Color = Crimson;
            canvas.DrawText("BUILD  /  FRONTIER", 899, 458, SKTextAlign.Center, _small, _text);
            _text.Color = Muted;
            canvas.DrawText("Linksklick zum Auswählen  •  Fenster schließen zum Beenden", 356, 588, SKTextAlign.Left, _small, _text);
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            _fill.Color = new SKColor(92, 31, 35, 240);
            canvas.DrawRoundRect(new SKRect(356, 610, 1028, 635), 4, 4, _fill);
            _text.Color = new SKColor(255, 190, 173);
            string visible = message.Length > 95 ? message[..92] + "..." : message;
            canvas.DrawText(visible, 368, 627, SKTextAlign.Left, _small, _text);
        }
    }

    private void DrawSettingRow(SKCanvas canvas, string label, string value, float x, float y, float width, float height, bool active)
    {
        _fill.Color = PanelRaised;
        canvas.DrawRoundRect(new SKRect(x, y, x + width, y + height), 6, 6, _fill);
        _line.Color = Border;
        canvas.DrawRoundRect(new SKRect(x, y, x + width, y + height), 6, 6, _line);
        _text.Color = TextColor;
        canvas.DrawText(label, x + 18, y + 28, SKTextAlign.Left, _button, _text);
        _text.Color = active ? Gold : Muted;
        canvas.DrawText(value, x + width - 18, y + 28, SKTextAlign.Right, _button, _text);
        _text.Color = Muted;
        canvas.DrawText("Per Klick umschaltbar", x + 18, y + 49, SKTextAlign.Left, _small, _text);
    }

    private void DrawButton(SKCanvas canvas, string label, float x, float y, float width, float height, bool active = false, string? index = null)
    {
        _fill.Color = active ? new SKColor(75, 31, 38) : PanelRaised;
        canvas.DrawRoundRect(new SKRect(x, y, x + width, y + height), 5, 5, _fill);
        _line.Color = active ? Gold : Border;
        _line.StrokeWidth = active ? 1.4f : 1f;
        canvas.DrawRoundRect(new SKRect(x, y, x + width, y + height), 5, 5, _line);
        if (active)
        {
            _fill.Color = Crimson;
            canvas.DrawRoundRect(new SKRect(x, y + 7, x + 4, y + height - 7), 2, 2, _fill);
        }
        _text.Color = active ? Gold : TextColor;
        float labelX = index is null ? x + width / 2 : x + 28;
        canvas.DrawText(label, labelX, y + height / 2 + 5, index is null ? SKTextAlign.Center : SKTextAlign.Left, _button, _text);
        if (index is not null)
        {
            _text.Color = Muted;
            canvas.DrawText(index, x + width - 20, y + height / 2 + 4, SKTextAlign.Right, _small, _text);
        }
    }

    public void Dispose()
    {
        _fill.Dispose();
        _line.Dispose();
        _text.Dispose();
        _wordmark.Dispose();
        _title.Dispose();
        _button.Dispose();
        _body.Dispose();
        _small.Dispose();
    }
}
