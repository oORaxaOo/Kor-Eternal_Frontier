using KorTheBlight.Simulation;

namespace KorTheBlight.Rendering;

/// <summary>Shared geometry for the fixed-width colony HUD header.</summary>
internal static class ColonyHudLayout
{
    public const float ResourceStart = 405f;
    public const float ResourceWidth = 76f;
    public const float ResourceStep = 82f;
    public const float SystemsButtonLeft = 855f;

    public static float ResourceX(int index)
    {
        if (index < 0 || index >= Enum.GetValues<ResourceKind>().Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        return ResourceStart + index * ResourceStep;
    }

    public static string ResourceLabel(ResourceKind resource) => resource switch
    {
        ResourceKind.Wood => "WOOD",
        ResourceKind.Stone => "STONE",
        ResourceKind.Food => "FOOD",
        ResourceKind.Alloy => "ALLOY",
        ResourceKind.Components => "COMP",
        _ => resource.ToString().ToUpperInvariant()
    };
}
