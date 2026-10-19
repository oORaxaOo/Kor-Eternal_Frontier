using KorTheBlight.Campaign;
using KorTheBlight.Simulation;

namespace KorTheBlight.Tests;

internal static class CampaignChecks
{
    public static void Run()
    {
        var first = new CampaignSimulation(70241);
        var second = new CampaignSimulation(70241);
        Check(first.State.Regions.Select(r => (r.Name, r.Terrain, r.Resource, r.Production, r.Owner))
            .SequenceEqual(second.State.Regions.Select(r => (r.Name, r.Terrain, r.Resource, r.Production, r.Owner))), "same seed must generate identical regions");
        var reached = new HashSet<int> { 0 };
        var frontier = new Queue<int>(); frontier.Enqueue(0);
        while (frontier.Count > 0)
        {
            int here = frontier.Dequeue();
            foreach (var next in first.State.Regions.Where(region => first.IsAdjacent(here, region.Id)))
                if (reached.Add(next.Id)) frontier.Enqueue(next.Id);
        }
        Check(first.State.Regions.Count == 9 && reached.Count == first.State.Regions.Count, "generated strategic map must be connected");

        first.SelectRegion(1);
        int supplies = first.State.Supplies;
        Check(first.DevelopSelected() && first.State.Supplies == supplies - 80, "outpost establishment must charge campaign resources");
        first.SelectRegion(0);
        Check(first.ResearchWard() && first.TacticalGuardianDamage == 36, "Ward research must produce a tactical damage effect");
        first.SelectRegion(4);
        Check(first.OrderMarch(), "adjacent neutral region should accept army march order");
        var loadedOrder = CampaignSaveService.Deserialize(CampaignSaveService.Serialize(first.State));
        var resumed = new CampaignSimulation(loadedOrder);
        Check(resumed.PendingArmyDestination == 4, "pending army order must survive save/load");
        resumed.EndTurn();
        Check(resumed.State.ArmyRegionId == 4 && resumed.State.Turn == 2, "end turn must execute army movement");

        // Region 3 is the seeded Blight threat. Region 4 is adjacent, so the RTS encounter is earned by moving there.
        resumed.SelectRegion(3);
        Check(resumed.CanStartBattle, "army at region 4 should be able to engage the adjacent Blight region");
        int oldSupplies = resumed.State.Supplies;
        int oldAlloy = resumed.State.Alloy;
        var setup = resumed.PrepareBattle();
        Check(resumed.State.ActiveBattleRegionId == 3 && resumed.State.Supplies == 0 && resumed.State.Alloy == 0, "battle must transfer campaign stocks into tactical stockpile");
        Check(setup.Supplies == oldSupplies && setup.Alloy == oldAlloy && setup.GuardianDamage == 36, "battle setup must use campaign resources and technology");
        resumed.ResolveBattle(true, 31, 47);
        Check(resumed.State.Regions[3].Owner == "Kor" && resumed.State.ArmyRegionId == 3, "battle victory must change region control and army location");
        Check(resumed.State.Supplies == 31 && resumed.State.Alloy == 47 && resumed.State.ActiveBattleRegionId == -1, "remaining tactical resources must return exactly once");
        var roundTrip = CampaignSaveService.Deserialize(CampaignSaveService.Serialize(resumed.State));
        Check(roundTrip.Regions[3].Owner == "Kor" && roundTrip.Supplies == 31 && roundTrip.Alloy == 47, "campaign save must preserve battle consequences");

        var recruitmentCampaign = new CampaignSimulation(9090);
        var recruitmentColony = new ColonySimulation();
        var recruitableUnit = ProgressionCatalog.Units.First(unit => unit.Era == 0);
        Check(!CampaignColonyBridge.CanRecruitUnit(recruitmentCampaign, recruitmentColony, recruitableUnit.Id, out string lockedUnitReason) &&
              lockedUnitReason.Contains("Locked by research", StringComparison.Ordinal),
            "catalogue unit recruitment must remain locked until the unit's technology is researched");
        recruitmentCampaign.State.Knowledge = 1000;
        recruitmentCampaign.State.Energy = 1000;
        Check(recruitmentColony.ResearchProject("research.defense.fortification.era0", recruitmentCampaign),
            "the foundational defense technology should be researchable before recruiting units");
        int suppliesBeforeRecruitment = recruitmentCampaign.State.Supplies;
        int alloyBeforeRecruitment = recruitmentCampaign.State.Alloy;
        Check(CampaignColonyBridge.RecruitUnit(recruitmentCampaign, recruitmentColony, recruitableUnit.Id),
            "a researched catalogue unit with sufficient supplies and alloy should be recruitable");
        Check(recruitmentCampaign.State.Supplies == suppliesBeforeRecruitment - recruitableUnit.SupplyCost &&
              recruitmentCampaign.State.Alloy == alloyBeforeRecruitment - recruitableUnit.AlloyCost &&
              recruitmentCampaign.State.RecruitedUnits.Contains(recruitableUnit.Id) &&
              recruitmentCampaign.State.ArmyStrength == 2 + recruitableUnit.CombatStrength,
            "recruitment must debit exact costs, persist the unit ID, and increase army strength");
        int suppliesBeforeUpkeep = recruitmentCampaign.State.Supplies;
        recruitmentCampaign.EndTurn();
        Check(recruitmentCampaign.State.Supplies == suppliesBeforeUpkeep + 20 - 10 - recruitableUnit.Upkeep,
            "strategic turns must pay unit upkeep in addition to the baseline army upkeep");
        var recruitedRoundTrip = CampaignSaveService.Deserialize(CampaignSaveService.Serialize(recruitmentCampaign.State));
        Check(recruitedRoundTrip.RecruitedUnits.SequenceEqual(recruitmentCampaign.State.RecruitedUnits),
            "campaign save/load must preserve the recruited unit roster");
        var lossCampaign = new CampaignSimulation(recruitedRoundTrip) { };
        lossCampaign.State.ActiveBattleRegionId = 3;
        int strengthBeforeLoss = lossCampaign.State.ArmyStrength;
        lossCampaign.ResolveBattle(false, 0, 0);
        Check(lossCampaign.State.RecruitedUnits.Count == 0 &&
              lossCampaign.State.ArmyStrength == strengthBeforeLoss - recruitableUnit.CombatStrength,
            "battle losses must remove a recruited unit and its contribution to army strength");
    }

    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException($"Campaign check failed: {reason}");
    }
}
