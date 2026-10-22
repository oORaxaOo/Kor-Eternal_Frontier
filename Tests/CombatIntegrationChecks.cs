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
        VerifyStructureSiegeBlightInfectionRepairAndPersistence();
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


    private static void VerifyStructureSiegeBlightInfectionRepairAndPersistence()
    {
        var world = new WorldSimulation(881002);
        var home = world.SpatialWorld.Entities.Single(entity =>
            entity.Kind == WorldEntityKind.Settlement && entity.Faction == "Kor");

        // Keep this scenario isolated from randomly seeded strategic army encounters.
        foreach (var entity in world.SpatialWorld.Entities
                     .Where(entity => entity.Kind is WorldEntityKind.Army or WorldEntityKind.BlightFront)
                     .ToArray())
            world.SpatialWorld.RemoveEntity(entity.Id);
        foreach (var entity in world.CombatEngine.Entities.ToArray())
            world.CombatEngine.TryRemoveEntity(entity.Id);

        var habitat = world.Colony.ObjectAt(15, 8)
            ?? throw new InvalidOperationException("The starter habitat must exist for the siege test.");
        float originalHealth = habitat.Health;
        Check(originalHealth > 0f, "the starting habitat should have a valid structure-integrity pool");

        var raider = new CombatEntity
        {
            FactionId = 2, Type = CombatEntityType.Human, X = 16.5f, Y = 9.5f,
            Speed = 0f, Health = 1f, SharpArmor = 0.2f, BluntArmor = 0.2f,
            EquippedWeapon = CombatWeaponType.TankCannon, CurrentAmmo = 12, MaxAmmo = 12,
            DriverPawnId = -1, SourcePawnId = -1, KilledPawnId = -1
        };
        Check(world.CombatEngine.TrySpawn(raider, out int raiderId),
            "an unopposed hostile should spawn for the local siege test");
        for (int i = 0; i < 21 && habitat.Health >= originalHealth; i++) world.Step(0.1f);
        Check(habitat.Health < originalHealth,
            "a hostile without a living combat target should attack and damage nearby settlement structures");

        world.CombatEngine.TryRemoveEntity(raiderId);
        var blight = new CombatEntity
        {
            FactionId = 99, Type = CombatEntityType.BlightBeast, X = 16.5f, Y = 9.5f,
            Speed = 0f, Health = 0.8f, SharpArmor = 0.05f, BluntArmor = 0f,
            EquippedWeapon = CombatWeaponType.Melee, CurrentFuel = 0f,
            HasBlightMutation = true, DriverPawnId = -1, SourcePawnId = -1, KilledPawnId = -1
        };
        Check(world.CombatEngine.TrySpawn(blight, out int blightId),
            "a Blight attacker should spawn for the infection test");
        float beforeInfectionHit = habitat.Health;
        world.Step(0.1f);
        Check(habitat.IsBlightInfected && habitat.Health < beforeInfectionHit,
            "Blight siege hits should both damage structures and mark them infected");
        world.CombatEngine.TryRemoveEntity(blightId);

        float healthAfterHit = habitat.Health;
        for (int i = 0; i < 110; i++) world.Step(0.1f);
        Check(habitat.Health < healthAfterHit && habitat.IsBlightInfected,
            "infection should apply recurring structure damage on the shared simulation clock");

        string infectedSave = GameSessionService.Serialize(world);
        var loaded = GameSessionService.Deserialize(infectedSave);
        var restored = new WorldSimulation(1);
        restored.Restore(loaded);
        var restoredHabitat = restored.Colony.Objects.Single(item => item.Id == habitat.Id);
        Check(restoredHabitat.IsBlightInfected &&
              Math.Abs(restoredHabitat.Health - habitat.Health) < 0.001f,
            "unified save/load should preserve structure health and Blight infection state");
        Check(restored.SpatialWorld.InventoryAt(home.Id, WorldResourceKind.Components) > 0 &&
              restored.SpatialWorld.InventoryAt(home.Id, WorldResourceKind.Wood) > 0,
            "the shared settlement ledger should fund repair and infection cleansing");

        double woodBeforeRepair = restored.SpatialWorld.InventoryAt(home.Id, WorldResourceKind.Wood);
        double componentsBeforeRepair = restored.SpatialWorld.InventoryAt(home.Id, WorldResourceKind.Components);
        float healthBeforeRepair = restoredHabitat.Health;
        int repaired = restored.RepairDamagedStructures();
        Check(repaired > 0 && !restoredHabitat.IsBlightInfected && restoredHabitat.Health > healthBeforeRepair,
            "repair controls should restore integrity and cleanse Blight infection");
        Check(restored.SpatialWorld.InventoryAt(home.Id, WorldResourceKind.Wood) == woodBeforeRepair - 1 &&
              restored.SpatialWorld.InventoryAt(home.Id, WorldResourceKind.Components) == componentsBeforeRepair - 1,
            "repairs and cleansing must debit the canonical wood/components inventory exactly once");

        string repairedSave = GameSessionService.Serialize(restored);
        var repairedReload = new WorldSimulation(2);
        repairedReload.Restore(GameSessionService.Deserialize(repairedSave));
        var finalHabitat = repairedReload.Colony.Objects.Single(item => item.Id == habitat.Id);
        Check(!finalHabitat.IsBlightInfected &&
              Math.Abs(finalHabitat.Health - restoredHabitat.Health) < 0.001f,
            "repaired structure state should remain consistent after a second save/load round-trip");
    }

    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException($"Combat integration check failed: {reason}");
    }
}
