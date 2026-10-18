using SkiaSharp;
using System.Text.Json;

namespace KorTheBlight.Rendering;

/// <summary>Cached original raster artwork. Simulation and hit-testing never depend on these images.</summary>
internal static class Artwork
{
    private static readonly Dictionary<string, SKBitmap> Images = new(StringComparer.Ordinal);
    private static Dictionary<string, (string File, int Width, int Height)>? _manifest;

    public static SKBitmap Get(string name)
    {
        if (Images.TryGetValue(name, out var image)) return image;
        _manifest ??= LoadManifest();
        if (!_manifest.TryGetValue(name, out var entry))
            throw new InvalidDataException($"Artwork '{name}' is not registered in Assets/Metadata/art_manifest.json.");
        string path = Path.Combine(AppContext.BaseDirectory, entry.File.Replace('/', Path.DirectorySeparatorChar));
        image = SKBitmap.Decode(path) ?? throw new InvalidDataException($"Could not decode artwork '{name}' at '{path}'. Check that the PNG is present and valid.");
        if (image.Width != entry.Width || image.Height != entry.Height)
        {
            image.Dispose();
            throw new InvalidDataException($"Artwork '{name}' dimensions are {image.Width}x{image.Height}; manifest expects {entry.Width}x{entry.Height}.");
        }
        Images.Add(name, image);
        return image;
    }

    private static Dictionary<string, (string File, int Width, int Height)> LoadManifest()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "Metadata", "art_manifest.json");
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var assets = document.RootElement.GetProperty("assets");
            var result = new Dictionary<string, (string File, int Width, int Height)>(StringComparer.Ordinal);
            foreach (var asset in assets.EnumerateArray())
            {
                string id = asset.GetProperty("assetId").GetString() ?? throw new InvalidDataException("Asset entry has an empty assetId.");
                var entry = (asset.GetProperty("file").GetString() ?? throw new InvalidDataException($"Asset '{id}' has no file."),
                    asset.GetProperty("width").GetInt32(), asset.GetProperty("height").GetInt32());
                if (!result.TryAdd(id, entry)) throw new InvalidDataException($"Duplicate art asset ID '{id}' in '{path}'.");
            }
            return result;
        }
        catch (Exception error) when (error is IOException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidDataException($"Could not load the game art manifest '{path}': {error.Message}", error);
        }
    }

    public static void Draw(SKCanvas canvas, string name, SKRect destination, SKPaint? paint = null) =>
        canvas.DrawBitmap(Get(name), destination, paint);

    public static int VerifyAll()
    {
        _manifest ??= LoadManifest();
        foreach (string id in _manifest.Keys) _ = Get(id);
        int count = Images.Count;
        Dispose();
        return count;
    }

    public static void Dispose()
    {
        foreach (var image in Images.Values) image.Dispose();
        Images.Clear();
        _manifest = null;
    }
}
