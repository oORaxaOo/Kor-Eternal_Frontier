using KorTheBlight.Campaign;
using KorTheBlight.Simulation;

namespace KorTheBlight.Tests;

internal static class IntegrationChecks
{
    public static void Run()
    {
        var campaign = new CampaignSimulation(70241);
        var colony = new ColonySimulation();

        campaign.AdvanceWorldInterval();
        int turn = campaign.State.Turn;
        int supplies = campaign.State.Supplies;
        int alloy = campaign.State.Alloy;
        int food = colony.ResourceCount(ResourceKind.Food);
        int localAlloy = colony.ResourceCount(ResourceKind.Alloy);

        string json = GameSessionService.Serialize(colony, campaign);
        var restored = GameSessionService.Deserialize(json);
        Check(restored.Campaign.State.Turn == turn &&
              restored.Campaign.State.Supplies == supplies &&
              restored.Campaign.State.Alloy == alloy,
            "compatibility session must preserve legacy campaign snapshot values");
        Check(restored.Colony.ResourceCount(ResourceKind.Food) == food &&
              restored.Colony.ResourceCount(ResourceKind.Alloy) == localAlloy,
            "session save must preserve local colony inventory");

        string legacyJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            SchemaVersion = 1,
            ColonyJson = ColonySaveService.Serialize(colony),
            CampaignJson = CampaignSaveService.Serialize(campaign.State)
        });
        var migratedLegacy = GameSessionService.Deserialize(legacyJson);
        Check(migratedLegacy.ContinuousWorld.Entities.Count > 0 &&
              migratedLegacy.ContinuousWorld.ClockSeconds == 0,
            "legacy session saves must migrate into a generated continuous world without interpreting hexes as geometry");

        string directory = Path.Combine(Path.GetTempPath(), "kor-session-check-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "session.json");
        try
        {
            GameSessionService.Save(colony, campaign, path);
            var fromDisk = GameSessionService.Load(path);
            Check(fromDisk.Campaign.State.Turn == turn &&
                  fromDisk.Colony.ResourceCount(ResourceKind.Food) == food,
                "unified disk save/load must preserve legacy migration data and colony inventory");
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
