using KorTheBlight.Campaign;

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
    }

    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException($"Campaign check failed: {reason}");
    }
}
