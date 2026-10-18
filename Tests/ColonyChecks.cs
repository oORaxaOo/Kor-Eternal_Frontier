using KorTheBlight.Simulation;
using KorTheBlight.Rendering;

namespace KorTheBlight.Tests;

internal static class ColonyChecks
{
    public static void Run()
    {
        var center = OrthogonalGridProjection.TileCenter(5, 3);
        var world = OrthogonalGridProjection.ScreenToWorld(center.X, center.Y);
        Check(MathF.Abs(world.X - 5.5f) < 0.001f && MathF.Abs(world.Y - 3.5f) < 0.001f,
            "screen projection must round-trip tile centers");
        var east = OrthogonalGridProjection.TileCenter(6, 3);
        Check(MathF.Abs(east.X - center.X - OrthogonalGridProjection.TileSize) < 0.001f &&
              MathF.Abs(east.Y - center.Y) < 0.001f,
            "orthogonal neighbors must be separated by one square tile");

        var colony = new ColonySimulation();
        Check(colony.Workers.Count == 3, "a new colony should contain three independent colonists");
        Check(colony.ResourceCount(ResourceKind.Wood) == 20, "initial wood should be explicit inventory");
        var woodDeposit = colony.Deposits.Single(d => d.Resource == ResourceKind.Wood);
        int woodBefore = colony.ResourceCount(ResourceKind.Wood);
        int depositBefore = woodDeposit.Amount;
        var worker = colony.Workers[0];
        Check(colony.SelectWorkerAt(worker.X, worker.Y), "colonists should be selectable in logical world coordinates");
        colony.CommandSelectedTo(woodDeposit.Cell.X + 0.5f, woodDeposit.Cell.Y + 0.5f);
        for (int i = 0; i < 300; i++) colony.Step(0.1f);
        Check(colony.ResourceCount(ResourceKind.Wood) == woodBefore + 5,
            "a gathered resource stack must be delivered to inventory rather than appear at the deposit");
        Check(woodDeposit.Amount == depositBefore - 5, "gathering must subtract the same quantity from the source");
        Check(!worker.CarriedResource.HasValue, "a successful round trip must leave no ghost cargo");

        Check(colony.DesignTileConstruction(16, 10, ArchitectureKind.Wall),
            "a wall should create an individual tile construction order");
        Check(colony.DesignTileConstruction(17, 10, ArchitectureKind.Floor),
            "a neighboring floor should create its own independent tile order");
        Check(!colony.DesignTileConstruction(16, 10, ArchitectureKind.Wall),
            "the same tile must not accept duplicate architecture orders");
        Check(colony.TileOrders.Select(o => o.Cell).Distinct().Count() == 2,
            "tile architecture orders must remain one-per-cell");

        Check(colony.PlaceObjectBlueprint(PlaceableKind.Workbench, 15, 8) == 0,
            "object placement must reject a footprint overlapping an existing building");
        int workbenchId = colony.PlaceObjectBlueprint(PlaceableKind.Workbench, 13, 12);
        Check(workbenchId > 0, "a valid multi-tile object should create a separate blueprint");
        var workbench = colony.Objects.Single(o => o.Id == workbenchId);
        Check(workbench.Width == 2 && workbench.Height == 2 && !workbench.IsBuilt,
            "a placeable object must keep its footprint and remain unfinished after placement");

        for (int i = 0; i < 1000; i++) colony.Step(0.1f);
        Check(colony.TileAt(16, 10)?.Architecture == ArchitectureKind.Wall,
            "a worker must complete wall construction on the designated tile");
        Check(colony.TileAt(17, 10)?.Architecture == ArchitectureKind.Floor,
            "a worker must complete floor construction as its own tile job");
        Check(colony.TileOrders.All(o => o.Status == ConstructionStatus.Complete),
            "tile orders should expose their final construction status");
        Check(workbench.IsBuilt, "an object blueprint must require and complete worker labor");

        int componentBefore = colony.ResourceCount(ResourceKind.Components);
        int alloyBefore = colony.ResourceCount(ResourceKind.Alloy);
        int woodBeforeProduction = colony.ResourceCount(ResourceKind.Wood);
        Check(colony.QueueComponentProduction(workbenchId), "a completed workbench should accept a funded production order");
        Check(colony.ResourceCount(ResourceKind.Alloy) == alloyBefore - 2 &&
              colony.ResourceCount(ResourceKind.Wood) == woodBeforeProduction - 1,
            "queued production must commit its real input materials");
        for (int i = 0; i < 300; i++) colony.Step(0.1f);
        Check(colony.ResourceCount(ResourceKind.Components) == componentBefore + 1,
            "a worker must finish the queued recipe and place the output in inventory");
        Check(!colony.QueueComponentProduction(999999), "production at a missing workstation must be rejected");

        Check(colony.DesignTileConstruction(19, 14, ArchitectureKind.Path), "a new path order should be accepted before saving");
        colony.Step(0.1f);
        string json = ColonySaveService.Serialize(colony);
        var restored = ColonySaveService.Deserialize(json);
        Check(restored.Workers.Count == colony.Workers.Count, "save/load must preserve the colony population");
        Check(restored.ResourceCount(ResourceKind.Wood) == colony.ResourceCount(ResourceKind.Wood) &&
              restored.ResourceCount(ResourceKind.Components) == colony.ResourceCount(ResourceKind.Components),
            "save/load must preserve actual inventory balances");
        Check(restored.TileOrders.Count == colony.TileOrders.Count &&
              restored.TileOrders.Select(o => (o.Cell, o.Target, o.Status, o.WorkDone))
                  .SequenceEqual(colony.TileOrders.Select(o => (o.Cell, o.Target, o.Status, o.WorkDone))),
            "save/load must preserve each tile construction state and work progress");
        Check(restored.Deposits.Select(d => (d.Id, d.Amount)).SequenceEqual(colony.Deposits.Select(d => (d.Id, d.Amount))),
            "save/load must preserve finite deposit quantities");
        Check(restored.Workers.Select(w => (w.Id, w.X, w.Y, w.Hunger, w.CarriedResource, w.CarriedAmount, w.Task?.Kind))
              .SequenceEqual(colony.Workers.Select(w => (w.Id, w.X, w.Y, w.Hunger, w.CarriedResource, w.CarriedAmount, w.Task?.Kind))),
            "save/load must preserve worker positions, needs, cargo, and task kinds");

        string directory = Path.Combine(Path.GetTempPath(), "kor-save-check-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "colony.json");
        try
        {
            ColonySaveService.Save(colony, path);
            var loadedFromDisk = ColonySaveService.Load(path);
            Check(loadedFromDisk.ResourceCount(ResourceKind.Food) == colony.ResourceCount(ResourceKind.Food),
                "disk save/load must preserve inventory state");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }

        bool versionRejected = false;
        try { ColonySaveService.Deserialize("{\"SchemaVersion\":99}"); }
        catch (InvalidDataException) { versionRejected = true; }
        Check(versionRejected, "unsupported or incomplete save data must be rejected rather than loaded");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Colony check failed: {message}");
    }
}
