namespace KorTheBlight.Simulation;

internal enum ThingKind { Pawn, Building, Resource, Architecture }

/// <summary>Common spatial identity for physical entities in the colony map.</summary>
internal abstract class Thing(string id, CellPosition position, ThingKind kind)
{
    public string Id { get; } = id;
    public CellPosition Position { get; internal set; } = position;
    public ThingKind Kind { get; } = kind;
    public virtual bool BlocksMovement => false;
    public virtual bool IsEdible => false;
    public virtual ResourceKind? Resource => null;
}
internal sealed class PawnThing(int workerId, CellPosition position) : Thing($"pawn:{workerId}", position, ThingKind.Pawn)
{ public int WorkerId { get; } = workerId; }
internal sealed class BuildingThing(int objectId, PlaceableKind placeable, CellPosition position)
    : Thing($"building:{objectId}", position, ThingKind.Building)
{
    public int ObjectId { get; } = objectId;
    public PlaceableKind Placeable { get; } = placeable;
    public override bool BlocksMovement => true;
}
internal sealed class ResourceThing(int depositId, ResourceKind resource, int amount, CellPosition position)
    : Thing($"resource:{depositId}", position, ThingKind.Resource)
{
    public int DepositId { get; } = depositId;
    public int Amount { get; } = Math.Max(0, amount);
    public override bool IsEdible => Resource == ResourceKind.Food && Amount > 0;
    public override ResourceKind? Resource { get; } = resource;
}
internal sealed class ArchitectureThing(CellPosition position, ArchitectureKind architecture)
    : Thing($"architecture:{position.X}:{position.Y}", position, ThingKind.Architecture)
{
    public ArchitectureKind Architecture { get; } = architecture;
    public override bool BlocksMovement => Architecture == ArchitectureKind.Wall;
}

/// <summary>Spatial index for all registered map things.</summary>
internal sealed class ThingGrid
{
    private readonly Dictionary<string, Thing> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<CellPosition, List<Thing>> _byCell = [];
    public int Count => _byId.Count;
    public IEnumerable<Thing> All => _byId.Values;
    public void Clear() { _byId.Clear(); _byCell.Clear(); }
    public bool Register(Thing thing)
    {
        ArgumentNullException.ThrowIfNull(thing);
        if (string.IsNullOrWhiteSpace(thing.Id) || _byId.ContainsKey(thing.Id)) return false;
        _byId.Add(thing.Id, thing); AddToCell(thing); return true;
    }
    public bool Remove(string id)
    {
        if (!_byId.Remove(id, out var thing)) return false;
        if (_byCell.TryGetValue(thing.Position, out var items))
        { items.Remove(thing); if (items.Count == 0) _byCell.Remove(thing.Position); }
        return true;
    }
    public bool Move(string id, CellPosition destination)
    {
        if (!_byId.TryGetValue(id, out var thing)) return false;
        if (thing.Position == destination) return true;
        if (_byCell.TryGetValue(thing.Position, out var oldCell))
        { oldCell.Remove(thing); if (oldCell.Count == 0) _byCell.Remove(thing.Position); }
        thing.Position = destination; AddToCell(thing); return true;
    }
    public IReadOnlyList<Thing> At(CellPosition cell) =>
        _byCell.TryGetValue(cell, out var things) ? things.ToArray() : Array.Empty<Thing>();
    public Thing? FindNearest(CellPosition origin, Func<Thing, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return _byId.Values.Where(predicate)
            .OrderBy(t => Math.Abs(t.Position.X - origin.X) + Math.Abs(t.Position.Y - origin.Y))
            .ThenBy(t => t.Id, StringComparer.Ordinal).FirstOrDefault();
    }
    public void Rebuild(IEnumerable<Thing> things)
    {
        ArgumentNullException.ThrowIfNull(things); Clear();
        foreach (var thing in things) Register(thing);
    }
    private void AddToCell(Thing thing)
    { if (!_byCell.TryGetValue(thing.Position, out var items)) _byCell[thing.Position] = items = []; items.Add(thing); }
}
