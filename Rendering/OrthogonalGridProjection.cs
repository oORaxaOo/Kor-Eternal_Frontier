namespace KorTheBlight.Rendering;

/// <summary>
/// Converts logical cell/world coordinates to an orthogonal screen projection.
/// Simulation state never stores pixels or camera-relative positions.
/// </summary>
internal static class OrthogonalGridProjection
{
    public const float TileSize = 48f;
    private const float ViewCenterX = 390f;
    private const float ViewCenterY = 360f;

    public static (float X, float Y) TileCenter(int x, int y, float cameraX = 0, float cameraY = 0, float zoom = 1)
        => WorldToScreen(x + 0.5f, y + 0.5f, cameraX, cameraY, zoom);

    public static (float X, float Y) WorldToScreen(float worldX, float worldY, float cameraX = 0, float cameraY = 0, float zoom = 1)
    {
        float scale = TileSize * Math.Clamp(zoom, 0.25f, 4f);
        return (
            ViewCenterX + cameraX + (worldX - 6.5f) * scale,
            ViewCenterY + cameraY + (worldY - 4.5f) * scale);
    }

    public static (float X, float Y) ScreenToWorld(float screenX, float screenY, float cameraX = 0, float cameraY = 0, float zoom = 1)
    {
        float scale = TileSize * Math.Clamp(zoom, 0.25f, 4f);
        return (
            (screenX - ViewCenterX - cameraX) / scale + 6.5f,
            (screenY - ViewCenterY - cameraY) / scale + 4.5f);
    }

    public static bool TryScreenToTile(float screenX, float screenY, out int tileX, out int tileY,
        float cameraX = 0, float cameraY = 0, float zoom = 1)
    {
        var world = ScreenToWorld(screenX, screenY, cameraX, cameraY, zoom);
        tileX = (int)MathF.Floor(world.X);
        tileY = (int)MathF.Floor(world.Y);
        return tileX >= 0 && tileY >= 0 && tileX < 13 && tileY < 9;
    }
}
