using KorTheBlight.Simulation;
using KorTheBlight.Simulation.Combat;
using KorTheBlight.WorldModel;

namespace KorTheBlight.Tests;

/// <summary>Cross-system checks: research, the canonical settlement ledger, combat entities, and unified persistence.</summary>
internal static class CombatIntegrationChecks
{
    public static void Run()
    {
        VerifyResearchWorkshopCombatAndSaveShareOneSession();
    }

    private static void VerifyResearchWorkshopCombatAndSaveShareOneSession()
    {
        var world = new WorldSimulation(880421);
        var home = world.SpatialWorld.Entities.Single(entity =>
            entity.Kind == WorldEntityKind.Settlement && entity.Faction == "Kor");

        // Build a genuine local workbench through the colony workers before crafting.
        var benchSite = Enumerable.Range(1, ColonySimulation.Height - 2)
            .SelectMany(y => Enumerable.Range(1, ColonySimulation.Width - 2)
                .Select(x => new CellPosition(x, y)))
            .Where(cell => world.Colony.CanPlaceObjectAt(PlaceableKind.Workbench, cell.X, cell.Y))
            .OrderBy(cell => world.Colony.Workers.Min(worker =>
                MathF.Abs(worker.X - (cell.X + 1f)) + MathF.Abs(worker.Y - (cell.Y + 1f))))
            .FirstOrDefault();
        int workbenchId = world.Colony.PlaceObjectBlueprint(PlaceableKind.Workbench, benchSite.X, benchSite.Y);
        Check(workbenchId > 0, "a clear workbench site should accept an industrial workstation blueprint");
        for (int i = 0; i < 1_000 &&
             !world.Colony.Objects.Any(item => item.Id == workbenchId && item.IsBuilt); i++)
            world.Step(0.1f);
        Check(world.Colony.Objects.Any(item => item.Id == workbenchId && item.IsBuilt),
            "workers should build the workbench under the shared simulation clock");

        // Research IDs and the workshop catalogue must resolve against the loaded content data.
        Check(world.ResearchManager.Start("Basic_Agriculture"), "starter research should be startable");
        Check(world.ResearchManager.AddProgress(500), "starter research should complete");
        Check(world.ResearchManager.Start("tech_industrial_weapons"), "weapons research should unlock after its prerequisite");
        Check(world.ResearchManager.AddProgress(1000), "weapons research should complete");

        Check(world.SpatialWorld.TryAddInventory(home.Id, WorldResourceKind.Alloy, 20),
            "the shared settlement ledger should accept alloy while storage has room");
        int alloyBeforeCraft = (int)world.SpatialWorld.InventoryAt(home.Id, WorldResourceKind.Alloy);
        int componentsBeforeCraft = (int)world.SpatialWorld.InventoryAt(home.Id, WorldResourceKind.Components);
        Check(world.Workshop.CanStartCraft("weapon_rifle_industrial"),
            "research-gated weapon recipe should read real settlement inventory");
        Check(world.TryStartCraft("weapon_rifle_industrial", out long jobId),
            "funded, researched weapon production should start through the world/workbench integration");
        Check(world.SpatialWorld.InventoryAt(home.Id, WorldResourceKind.Alloy) == alloyBeforeCraft - 20 &&
              world.SpatialWorld.InventoryAt(home.Id, WorldResourceKind.Components) == componentsBeforeCraft - 3,
            "workshop inputs should be debited from the canonical world inventory exactly once");

        for (int i = 0; i < 400; i++) world.Step(0.1f);
        Check(world.Workshop.Jobs.All(job => job.Id != jobId) &&
              world.SpatialWorld.ItemAmountAt(home.Id, WorkbenchManager.InfantryRifle) == 1,
            "first completed craft should create exactly one item in the shared settlement ledger");

        // A nearby strategic army becomes a local combat entity linked by stable world ID.
        var raider = world.SpatialWorld.AddEntity(WorldEntityKind.Army, "Integration Test Raider", "Raiders",
            Math.Clamp(home.X + 900, 0, ContinuousWorldState.WorldExtent),
            Math.Clamp(home.Y + 650, 0, ContinuousWorldState.WorldExtent));
        // Start another job so the save boundary includes unfinished workshop work.
        Check(world.SpatialWorld.TryAddInventory(home.Id, WorldResourceKind.Alloy, 20) &&
              world.SpatialWorld.TryAddInventory(home.Id, WorldResourceKind.Components, 3),
            "the ledger should accept materials for a second job");
        Check(world.TryStartCraft("weapon_rifle_industrial", out long pendingJobId),
            "second funded craft should start");
        world.Step(0.1f);
        Check(world.CombatEngine.Entities.Any(entity => entity.SourceWorldEntityId == raider.Id),
            "nearby strategic raiders should materialize in the local combat simulation");

        // A deployed resident is the same logical pawn in both simulations, rather than a duplicate.
        int deployed = world.TryDeployColonists();
        Check(deployed > 0, "eligible colony residents should deploy as combat entities");
        var linkedPawn = world.CombatEngine.Entities.FirstOrDefault(entity => entity.SourcePawnId > 0);
        Check(linkedPawn.SourcePawnId > 0 &&
              world.Colony.Workers.Any(worker => worker.Id == linkedPawn.SourcePawnId && worker.IsCombatDeployed),
            "combat deployment should suspend the worker job and preserve a stable pawn link");

        string json = GameSessionService.Serialize(world);
        var loaded = GameSessionService.Deserialize(json);
        var restored = new WorldSimulation(1);
        restored.Restore(loaded);
        Check(restored.CombatEngine.Entities.Any(entity => entity.SourceWorldEntityId == raider.Id),
            "unified save/load should preserve world-army-to-combat identity");
        Check(restored.CombatEngine.Entities.Any(entity => entity.SourcePawnId == linkedPawn.SourcePawnId) &&
              restored.Colony.Workers.Any(worker => worker.Id == linkedPawn.SourcePawnId && worker.IsCombatDeployed),
            "unified save/load should preserve deployed pawn identity and disable duplicate colony jobs");
        Check(restored.Workshop.Jobs.Any(job => job.Id == pendingJobId) &&
              restored.Workshop.Jobs.Any(job => job.Id == jobId) == false &&
              restored.SpatialWorld.ItemAmountAt(home.Id, WorkbenchManager.InfantryRifle) == 0,
            "an active workshop job and the canonical item ledger should survive save/load");

        for (int i = 0; i < 400; i++) restored.Step(0.1f);
        Check(restored.Workshop.Jobs.All(job => job.Id != pendingJobId) &&
              restored.SpatialWorld.ItemAmountAt(home.Id, WorkbenchManager.InfantryRifle) == 1,
            "the restored workbench job should finish and commit exactly one rifle into the shared item ledger");

        // Research is part of the canonical saved world, so the connection must remain live after restore.
        Check(restored.Workshop.IsResearchUnlocked("tech_industrial_weapons"),
            "completed research should remain visible to the restored workshop");
    }

    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException($"Combat integration check failed: {reason}");
    }
}
