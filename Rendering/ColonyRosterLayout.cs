namespace KorTheBlight.Rendering;

/// <summary>
/// Shared geometry for the compact, clickable colonist bar.
/// Drawing and input dispatch use the same bounds to prevent visual/hitbox drift.
/// </summary>
internal static class ColonyRosterLayout
{
    public const float Left = 155f;
    public const float Top = 8f;
    public const float ButtonWidth = 76f;
    public const float ButtonHeight = 27f;
    public const float Gap = 4f;
    public const int VisibleSlots = 3;
    public const float Step = ButtonWidth + Gap;

    public static int HitTest(float x, float y)
    {
        if (y < Top || y >= Top + ButtonHeight || x < Left)
            return -1;

        int slot = (int)((x - Left) / Step);
        if (slot < 0 || slot >= VisibleSlots)
            return -1;

        float slotLeft = Left + slot * Step;
        return x >= slotLeft && x < slotLeft + ButtonWidth ? slot : -1;
    }
}
