namespace KorTheBlight.Rendering;

/// <summary>Projects continuous world coordinates into the strategic view; coordinates never become cells.</summary>
internal static class ContinuousWorldProjection
{
    private const float ViewCenterX = 385f;
    private const float ViewCenterY = 356f;

    public static float Scale(float zoom) => Math.Clamp(zoom, 0.08f, 0.65f) * 0.012f;

    public static (float X, float Y) WorldToScreen(double worldX, double worldY, float cameraX, float cameraY, float zoom)
    {
        float scale = Scale(zoom);
        return (ViewCenterX + ((float)worldX - cameraX) * scale,
                ViewCenterY + ((float)worldY - cameraY) * scale);
    }

    public static (double X, double Y) ScreenToWorld(float screenX, float screenY, float cameraX, float cameraY, float zoom)
    {
        float scale = Scale(zoom);
        return (Math.Clamp(cameraX + (screenX - ViewCenterX) / scale, 0d, 100_000d),
                Math.Clamp(cameraY + (screenY - ViewCenterY) / scale, 0d, 100_000d));
    }
}
