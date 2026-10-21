namespace KorTheBlight.Simulation;

internal sealed record ColonyWealthSnapshot(long Tick, double StoredResourcesValue, double StructuresValue, double TotalWealth, int ThreatPoints);

/// <summary>Deterministic wealth sampling and bounded storyteller threat budget.</summary>
internal sealed class ColonyWealth
{
    public const long SampleIntervalTicks = 2_000;
    private static readonly IReadOnlyDictionary<ResourceKind, double> MarketValues = new Dictionary<ResourceKind, double>
    {
        [ResourceKind.Wood] = 2, [ResourceKind.Stone] = 1.5, [ResourceKind.Food] = 1,
        [ResourceKind.Alloy] = 8, [ResourceKind.Components] = 14
    };
    public ColonyWealthSnapshot Current { get; private set; } = new(0, 0, 0, 0, 0);

    public void Restore(ColonyWealthSnapshot? snapshot)
    {
        if (snapshot == null) return;
        if (snapshot.Tick < 0 || !double.IsFinite(snapshot.StoredResourcesValue) || snapshot.StoredResourcesValue < 0 ||
            !double.IsFinite(snapshot.StructuresValue) || snapshot.StructuresValue < 0 ||
            !double.IsFinite(snapshot.TotalWealth) || snapshot.TotalWealth < 0 || snapshot.ThreatPoints is < 0 or > 250)
            throw new InvalidDataException("Saved colony wealth metrics are invalid.");
        Current = snapshot;
    }

    public ColonyWealthSnapshot Sample(long tick, IReadOnlyDictionary<ResourceKind, int> inventory,
        IEnumerable<(PlaceableKind Kind, bool Built)> objects, IEnumerable<ArchitectureKind> architecture)
    {
        if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
        double stored = inventory.Sum(item => Math.Max(0, item.Value) * MarketValues[item.Key]);
        double structures = objects.Where(item => item.Built).Sum(item => item.Kind switch
        {
            PlaceableKind.Habitat => 180, PlaceableKind.Stockpile => 110, PlaceableKind.Generator => 240,
            PlaceableKind.Workbench => 90, PlaceableKind.StorageRack => 65, PlaceableKind.Garden => 75,
            PlaceableKind.SleepingPod => 55, PlaceableKind.DiningTable => 45, PlaceableKind.Lamp => 25,
            PlaceableKind.ComfortBench => 40, _ => 0
        }) + architecture.Sum(kind => kind switch
        {
            ArchitectureKind.Wall => 12, ArchitectureKind.Door => 18, ArchitectureKind.Floor => 5,
            ArchitectureKind.Path => 3, ArchitectureKind.Conduit => 9, _ => 0
        });
        double total = Math.Max(0, stored + structures);
        int threat = Math.Clamp((int)Math.Round(Math.Sqrt(total) * 1.6), 0, 250);
        Current = new ColonyWealthSnapshot(tick, stored, structures, total, threat);
        return Current;
    }
}
