using SkiaSharp;

namespace KorTheBlight.Rendering;

/// <summary>
/// Mouse-first command surface. Every keyboard shortcut has a discoverable menu equivalent.
/// Menu IDs: 0 closed, 1 Colony, 2 Build, 3 Empire, 4 Diplomacy, 5 Game,
/// 6 Architecture submenu, 7 Objects submenu, 8 Camera submenu, 9 Logistics submenu.
/// </summary>
internal sealed class MouseMenuRenderer : IDisposable
{
    public const float Top = 8, ButtonHeight = 40, MenuTop = 54, RowHeight = 31, MenuWidth = 242;
    private static readonly (string Label, float X, float Width, int Id)[] TopMenus =
    [
        ("Colony", 10, 100, 1), ("Build", 118, 100, 2), ("Empire", 226, 100, 3),
        ("Diplomacy", 334, 120, 4), ("Game", 462, 100, 5)
    ];
    private static readonly string[] ColonyItems = ["Strategic World Map", "Pause / Resume", "Camera controls  ›"];
    private static readonly string[] BuildItems = ["Architecture  ›", "Buildings & objects  ›", "Cancel current placement"];
    private static readonly string[] EmpireItems = ["Advance strategic turn", "Logistics  ›", "Claim selected frontier territory"];
    private static readonly string[] DiplomacyItems = ["Improve relations", "Offer non-aggression pact", "Propose trade agreement"];
    private static readonly string[] GameItems = ["Save session", "Load session", "Close menu"];
    private static readonly string[] ArchitectureItems = ["Wall", "Floor", "Path", "Door", "Conduit", "Back"];
    private static readonly string[] ObjectItems = ["Workbench", "Sleeping Pod", "Storage Rack", "Generator", "Garden", "Back"];
    private static readonly string[] CameraItems = ["Pan left", "Pan right", "Pan up", "Pan down", "Zoom in", "Zoom out", "Back"];
    private static readonly string[] LogisticsItems = ["Import strategic supplies", "Export colony resources", "Back"];
    private readonly SKPaint _fill = new() { IsAntialias = true };
    private readonly SKPaint _line = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
    private readonly SKFont _font = new(SKTypeface.FromFamilyName("sans-serif", SKFontStyle.Bold), 13);
    private readonly SKFont _small = new(SKTypeface.FromFamilyName("sans-serif"), 12);

    public static string[] ItemsFor(int menu) => menu switch
    {
        1 => ColonyItems, 2 => BuildItems, 3 => EmpireItems, 4 => DiplomacyItems, 5 => GameItems,
        6 => ArchitectureItems, 7 => ObjectItems, 8 => CameraItems, 9 => LogisticsItems, _ => []
    };

    public static float MenuX(int menu) => menu switch
    {
        1 => 10, 2 => 118, 3 => 226, 4 => 334, 5 => 462, 6 => 118, 7 => 118, 8 => 10, 9 => 226, _ => 10
    };

    public void Draw(SKCanvas canvas, int activeMenu, bool worldMapMode, ArchitectureKind architectureMode,
        PlaceableKind objectType, bool placingObject, bool paused)
    {
        _fill.Color = new SKColor(9, 12, 14, 245);
        canvas.DrawRect(new SKRect(0, 0, 1100, 58), _fill);
        DrawText(canvas, "KOR", 12, 39, _font, new SKColor(192, 42, 54));
        DrawText(canvas, worldMapMode ? "STRATEGIC COMMAND" : "COLONY COMMAND", 63, 38, _small, new SKColor(180, 190, 180));
        foreach (var item in TopMenus)
        {
            bool selected = activeMenu == item.Id;
            _fill.Color = selected ? new SKColor(104, 32, 43) : new SKColor(28, 35, 35);
            canvas.DrawRoundRect(new SKRect(item.X, Top, item.X + item.Width, Top + ButtonHeight), 5, 5, _fill);
            _line.Color = selected ? new SKColor(223, 174, 91) : new SKColor(67, 81, 75);
            canvas.DrawRoundRect(new SKRect(item.X, Top, item.X + item.Width, Top + ButtonHeight), 5, 5, _line);
            DrawText(canvas, item.Label, item.X + 12, Top + 25, _font, new SKColor(235, 231, 216));
        }

        if (activeMenu <= 0) return;
        string[] items = ItemsFor(activeMenu);
        float x = MenuX(activeMenu), width = MenuWidth;
        _fill.Color = new SKColor(17, 22, 23, 252);
        canvas.DrawRoundRect(new SKRect(x, MenuTop, x + width, MenuTop + items.Length * RowHeight + 10), 7, 7, _fill);
        _line.Color = new SKColor(110, 83, 62);
        canvas.DrawRoundRect(new SKRect(x, MenuTop, x + width, MenuTop + items.Length * RowHeight + 10), 7, 7, _line);
        for (int i = 0; i < items.Length; i++)
        {
            float y = MenuTop + 5 + i * RowHeight;
            _fill.Color = new SKColor(34, 42, 41);
            canvas.DrawRoundRect(new SKRect(x + 6, y, x + width - 6, y + RowHeight - 3), 4, 4, _fill);
            DrawText(canvas, items[i], x + 15, y + 19, _small, new SKColor(230, 229, 216));
        }

        // Persistent contextual hint: all core play can be completed with the mouse.
        string hint = worldMapMode
            ? "Mouse: select territory  •  menus: claim land, diplomacy, turns and saves"
            : $"Mouse: select / issue orders  •  Build menu: structures  •  { (paused ? "Paused" : "Simulation running") }";
        _fill.Color = new SKColor(12, 16, 16, 225);
        canvas.DrawRoundRect(new SKRect(18, 662, 1082, 708), 5, 5, _fill);
        DrawText(canvas, hint, 30, 690, _small, new SKColor(201, 207, 193));
        if (placingObject || architectureMode != ArchitectureKind.None)
        {
            string mode = placingObject ? $"Placement: {objectType}" : $"Construction: {architectureMode}";
            DrawText(canvas, mode, 800, 38, _small, new SKColor(223, 174, 91));
        }
    }

    private void DrawText(SKCanvas canvas, string value, float x, float y, SKFont font, SKColor color)
    {
        using var paint = new SKPaint { IsAntialias = true, Color = color };
        canvas.DrawText(value, x, y, SKTextAlign.Left, font, paint);
    }

    public void Dispose() { _fill.Dispose(); _line.Dispose(); _font.Dispose(); _small.Dispose(); }
}
