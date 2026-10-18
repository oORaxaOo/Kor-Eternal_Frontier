using KorTheBlight.Campaign;
using KorTheBlight.Simulation;

namespace KorTheBlight.Tests;

internal static class IntegrationChecks
{
    public static void Run()
    {
        var campaign = new CampaignSimulation(70241);
        var colony = new ColonySimulation();

        int campaignSuppliesBefore = campaign.State.Supplies;
        int campaignAlloyBefore = campaign.State.Alloy;
        int colonyFoodBefore = colony.ResourceCount(ResourceKind.Food);
        int colonyAlloyBefore = colony.ResourceCount(ResourceKind.Alloy);
        int imported = CampaignColonyBridge.ImportToColony(campaign, colony);

        Check(imported == 30, "initial resupply should transfer 20 Food and 10 Alloy");
        Check(colony.ResourceCount(ResourceKind.Food) == colonyFoodBefore + 20 &&
              colony.ResourceCount(ResourceKind.Alloy) == colonyAlloyBefore + 10,
            "campaign imports must reach local inventory");
        Check(campaign.State.Supplies == campaignSuppliesBefore - 20 &&
              campaign.State.Alloy == campaignAlloyBefore - 10,
            "imports must debit strategic inventory by exactly the delivered amounts");
        Check(colony.InventoryUsed <= colony.InventoryCapacity, "imports must respect local storage capacity");

        int suppliesBeforeExport = campaign.State.Supplies;
        int alloyBeforeExport = campaign.State.Alloy;
        int foodBeforeExport = colony.ResourceCount(ResourceKind.Food);
        int localAlloyBeforeExport = colony.ResourceCount(ResourceKind.Alloy);
        int exported = CampaignColonyBridge.ExportFromColony(campaign, colony);
        Check(exported == 30, "initial export should transfer up to 20 Food and 10 Alloy");
        Check(campaign.State.Supplies == suppliesBeforeExport + 20 &&
              campaign.State.Alloy == alloyBeforeExport + 10,
            "exports must credit campaign inventory exactly once");
        Check(colony.ResourceCount(ResourceKind.Food) == foodBeforeExport - 20 &&
              colony.ResourceCount(ResourceKind.Alloy) == localAlloyBeforeExport - 10,
            "exports must debit local inventory exactly once");

        // Full storage must prevent import without losing strategic stock.
        var fullColony = ColonySimulation.RestoreSnapshot(colony.CaptureSnapshot());
        var snapshot = fullColony.CaptureSnapshot();
        int used = snapshot.Inventory.Sum(item => item.Amount);
        snapshot.Inventory.Single(item => item.Resource == ResourceKind.Food).Amount += fullColony.InventoryCapacity - used;
        fullColony = ColonySimulation.RestoreSnapshot(snapshot);
        int suppliesAtCapacity = campaign.State.Supplies;
        Check(CampaignColonyBridge.ImportToColony(campaign, fullColony) == 0 &&
              campaign.State.Supplies == suppliesAtCapacity,
            "full local storage must reject imports without consuming strategic supplies");

        campaign.EndTurn();
        int turn = campaign.State.Turn;
        string json = GameSessionService.Serialize(colony, campaign);
        var restored = GameSessionService.Deserialize(json);
        Check(restored.Campaign.State.Turn == turn &&
              restored.Campaign.State.Supplies == campaign.State.Supplies &&
              restored.Campaign.State.Alloy == campaign.State.Alloy,
            "unified save must preserve strategic turn and resource balances");
        Check(restored.Colony.ResourceCount(ResourceKind.Food) == colony.ResourceCount(ResourceKind.Food) &&
              restored.Colony.ResourceCount(ResourceKind.Alloy) == colony.ResourceCount(ResourceKind.Alloy),
            "unified save must preserve local inventory alongside campaign state");

        string directory = Path.Combine(Path.GetTempPath(), "kor-session-check-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "session.json");
        try
        {
            GameSessionService.Save(colony, campaign, path);
            var fromDisk = GameSessionService.Load(path);
            Check(fromDisk.Campaign.State.Turn == turn &&
                  fromDisk.Colony.ResourceCount(ResourceKind.Food) == colony.ResourceCount(ResourceKind.Food),
                "unified disk save/load must restore both linked systems");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException($"Integration check failed: {reason}");
    }
}
