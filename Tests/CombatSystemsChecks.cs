using KorTheBlight.Simulation.Combat;

namespace KorTheBlight.Tests;

internal static class CombatSystemsChecks
{
    public static void Run()
    {
        VerifyCoverGridSnapshot();
        VerifyCombatDamageAndAmmo();
        VerifyFactionLoadoutsAreSeeded();
        VerifyResearchGatedProductionAndVehicleCosts();
        VerifyNemesisTitlesAreStableAndContextual();
    }

    private static void VerifyCoverGridSnapshot()
    {
        var cover = new CombatCoverGrid();
        cover[5, 5] = CoverType.High;
        var snapshot = cover.Snapshot();
        var restored = new CombatCoverGrid();
        restored.Restore(snapshot);
        Assert(restored[5, 5] == CoverType.High, "cover grid should round-trip");
        snapshot[5 + 5 * CombatCoverGrid.Width] = 0;
        Assert(restored[5, 5] == CoverType.High, "cover snapshot must not alias live cover storage");
    }

    private static void VerifyCombatDamageAndAmmo()
    {
        var engine = new CombatEngine();
        var cover = new CombatCoverGrid();
        Assert(engine.TrySpawn(new CombatEntity
        {
            FactionId = 1, Type = CombatEntityType.Human, X = 2, Y = 2, Health = 1,
            Speed = 0, EquippedWeapon = CombatWeaponType.Rifle, CurrentAmmo = 10, MaxAmmo = 10
        }, out int shooter), "shooter should spawn");
        Assert(engine.TrySpawn(new CombatEntity
        {
            FactionId = 2, Type = CombatEntityType.Human, X = 5, Y = 2, Health = 1,
            Speed = 0, EquippedWeapon = CombatWeaponType.Melee, SharpArmor = 0, BluntArmor = 0
        }, out int target), "target should spawn");
        Assert(engine.TrySetTarget(shooter, target), "explicit target should be accepted");
        engine.Step(1f / 60f, cover);
        Assert(engine.TryGetEntity(target, out var damaged) && damaged.Health < 1f, "in-range shot should apply damage");
        Assert(engine.TryGetEntity(shooter, out var afterShot) && afterShot.CurrentAmmo == 9, "shot should consume exactly one round");
        Assert(engine.Flashes.Count == 1 && engine.Flashes[0].AttackerId == shooter, "shot should emit a transient symbolic flash");
    }

    private static void VerifyFactionLoadoutsAreSeeded()
    {
        var first = FactionLoadoutGenerator.Create(2, 12345);
        var second = FactionLoadoutGenerator.Create(2, 12345);
        Assert(first.Type == second.Type && first.EquippedWeapon == second.EquippedWeapon &&
            first.MaxAmmo == second.MaxAmmo, "same seed and faction should produce same loadout");
        var blight = FactionLoadoutGenerator.Create(99, 77);
        Assert(blight.HasBlightMutation && blight.Type is CombatEntityType.BlightBeast or CombatEntityType.BlightTitan,
            "Blight faction should spawn a mutated creature type");
    }

    private static void VerifyResearchGatedProductionAndVehicleCosts()
    {
        var workshop = new WorkbenchManager();
        workshop.AddInventory(WorkbenchManager.Alloy, 20);
        workshop.AddInventory(WorkbenchManager.Components, 3);
        Assert(!workshop.TryStartCraft("weapon_rifle_industrial", out _), "locked recipe must not start");
        workshop.AddResearch("tech_industrial_weapons");
        workshop.AddResearch("tech_armored_vehicles");
        Assert(workshop.TryStartCraft("weapon_rifle_industrial", out _), "unlocked and funded recipe should start");
        Assert(workshop.Inventory[WorkbenchManager.Alloy] == 0, "craft inputs should be committed at job start");
        workshop.Advance(399);
        Assert(!workshop.Inventory.ContainsKey(WorkbenchManager.InfantryRifle), "incomplete craft must not produce output");
        workshop.Advance(1);
        Assert(workshop.Inventory[WorkbenchManager.InfantryRifle] == 1, "completed craft should produce exactly one output");

        workshop.AddInventory(WorkbenchManager.TankChassis, 1);
        workshop.AddInventory(WorkbenchManager.CombustionEngine, 1);
        workshop.AddInventory(WorkbenchManager.HeavyArmorPlates, 4);
        workshop.AddInventory(WorkbenchManager.TankCannon, 1);
        var engine = new CombatEngine();
        Assert(workshop.TryBuildVehicle(engine, 4 + 4 * CombatCoverGrid.Width, out int tankId), "fully supplied vehicle should spawn");
        Assert(engine.TryGetEntity(tankId, out var tank) && tank.Type == CombatEntityType.HeavyTank,
            "completed vehicle should become a combat entity");
        Assert(workshop.Inventory[WorkbenchManager.TankChassis] == 0 &&
            workshop.Inventory[WorkbenchManager.HeavyArmorPlates] == 0, "vehicle parts should be consumed once");
    }

    private static void VerifyNemesisTitlesAreStableAndContextual()
    {
        var baseNemesis = new NemesisRecord(1, 55, 2, 3, -1, false, true, CombatEntityType.Human);
        string first = NemesisNameGenerator.GenerateFullTitle(baseNemesis);
        string repeat = NemesisNameGenerator.GenerateFullTitle(baseNemesis);
        Assert(first == repeat && first.Contains("Einauge", StringComparison.Ordinal),
            "nemesis title should be stable and reflect persistent scars");
        var mutated = baseNemesis with { HasBlightMutation = true };
        string mutationTitle = NemesisNameGenerator.GenerateFullTitle(mutated);
        Assert(mutationTitle != first && (mutationTitle.Contains("Fäulnis", StringComparison.Ordinal) ||
            mutationTitle.Contains("Sporen", StringComparison.Ordinal) || mutationTitle.Contains("Pestauge", StringComparison.Ordinal) ||
            mutationTitle.Contains("Verseuchte", StringComparison.Ordinal)),
            "Blight mutation should select a contextual title");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Combat systems check failed: {message}");
    }
}
