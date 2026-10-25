namespace KorTheBlight.Rendering;

internal enum BuildPaletteAction
{
    None,
    Wall, Floor, Path, Door, Conduit, Dig, Remove, Cancel,
    SleepingPod, DiningTable, ComfortBench, Lamp,
    Workbench, Generator, StorageRack, Garden
}

/// <summary>
/// Canonical logical-space layout for the build palette. Drawing, hit-testing and tests share these bounds.
/// The palette stays inside the right-side panel at the game's 1100x720 logical resolution.
/// </summary>
internal static class BuildPaletteLayout
{
    public const float Left = 784f;
    public const float Right = 1086f;
    public const float CategoryTop = 82f;
    public const float CategoryHeight = 26f;
    public const float CategoryWidth = 57f;
    public const float CategoryGap = 4f;
    public const float ItemWidth = 145f;
    public const float ItemHeight = 42f;
    public const float ItemGap = 7f;
    public const float ItemColumnGap = 8f;
    public const float FirstRowTop = 124f;
    public const float SecondRowTop = 173f;
    public const int CategoryCount = 5;
    public const int ItemsPerRow = 2;
    public const int ItemCount = 8;

    private static readonly string[] CategoryLabels = ["STRUCT", "BUILD", "PROD", "STORE", "ZONES"];
    private static readonly BuildPaletteAction[][] Actions =
    [
        [BuildPaletteAction.Wall, BuildPaletteAction.Floor, BuildPaletteAction.Path, BuildPaletteAction.Door,
         BuildPaletteAction.Conduit, BuildPaletteAction.Dig, BuildPaletteAction.Remove, BuildPaletteAction.Cancel],
        [BuildPaletteAction.SleepingPod, BuildPaletteAction.DiningTable, BuildPaletteAction.ComfortBench, BuildPaletteAction.Lamp],
        [BuildPaletteAction.Workbench, BuildPaletteAction.Generator],
        [BuildPaletteAction.StorageRack],
        [BuildPaletteAction.Garden]
    ];

    public static string CategoryLabel(int category) =>
        category >= 0 && category < CategoryLabels.Length ? CategoryLabels[category] : string.Empty;

    public static BuildPaletteAction ActionAt(int category, int slot) =>
        category >= 0 && category < Actions.Length && slot >= 0 && slot < Actions[category].Length
            ? Actions[category][slot]
            : BuildPaletteAction.None;

    public static string ItemLabel(int category, int slot) => ActionAt(category, slot) switch
    {
        BuildPaletteAction.Wall => "WALL",
        BuildPaletteAction.Floor => "FLOOR",
        BuildPaletteAction.Path => "PATH",
        BuildPaletteAction.Door => "DOOR",
        BuildPaletteAction.Conduit => "COND",
        BuildPaletteAction.Dig => "DIG",
        BuildPaletteAction.Remove => "REMOVE",
        BuildPaletteAction.Cancel => "CANCEL",
        BuildPaletteAction.SleepingPod => "SLEEP POD",
        BuildPaletteAction.DiningTable => "TABLE",
        BuildPaletteAction.ComfortBench => "SEAT",
        BuildPaletteAction.Lamp => "LAMP",
        BuildPaletteAction.Workbench => "WORKBENCH",
        BuildPaletteAction.Generator => "GENERATOR",
        BuildPaletteAction.StorageRack => "STORAGE",
        BuildPaletteAction.Garden => "GARDEN PLOT",
        _ => "—"
    };

    public static int HitCategory(float x, float y)
    {
        if (y < CategoryTop || y >= CategoryTop + CategoryHeight || x < Left)
            return -1;

        int category = (int)((x - Left) / (CategoryWidth + CategoryGap));
        if (category < 0 || category >= CategoryCount)
            return -1;
        float itemLeft = Left + category * (CategoryWidth + CategoryGap);
        return x >= itemLeft && x < itemLeft + CategoryWidth ? category : -1;
    }

    public static int HitItem(float x, float y)
    {
        if (y < FirstRowTop) return -1;
        int row = (int)((y - FirstRowTop) / (ItemHeight + ItemGap));
        if (row < 0 || row >= ItemCount / ItemsPerRow) return -1;
        float rowTop = FirstRowTop + row * (ItemHeight + ItemGap);
        if (y >= rowTop + ItemHeight || x < Left || x >= Right) return -1;
        int column = (int)((x - Left) / (ItemWidth + ItemColumnGap));
        if (column < 0 || column >= ItemsPerRow) return -1;
        float itemLeft = Left + column * (ItemWidth + ItemColumnGap);
        if (x < itemLeft || x >= itemLeft + ItemWidth) return -1;
        return row * ItemsPerRow + column;
    }
}
