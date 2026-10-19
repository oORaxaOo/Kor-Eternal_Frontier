using System.Text.Json;

namespace KorTheBlight.Campaign;

internal sealed class CampaignState
{
    public int SchemaVersion { get; set; } = 1;
    public int Seed { get; set; }
    public int Turn { get; set; } = 1;
    public int Supplies { get; set; }
    public int Alloy { get; set; }
    public int Energy { get; set; }
    public int Knowledge { get; set; }
    public int ArmyRegionId { get; set; }
    public int ArmyStrength { get; set; } = 2;
    public int ArmyReadiness { get; set; } = 100;
    public int SelectedRegionId { get; set; }
    public int ArmyDestinationRegionId { get; set; } = -1;
    public int ActiveBattleRegionId { get; set; } = -1;
    public bool WardProtocolResearched { get; set; }
    public string LastEvent { get; set; } = "The Kor frontier is ready to expand.";
    public List<RegionState> Regions { get; set; } = [];

    public void Validate()
    {
        if (SchemaVersion != 1) throw new InvalidDataException($"Unsupported campaign save version: {SchemaVersion}.");
        if (Regions.Count != WorldGenerationService.RegionPositions.Length)
            throw new InvalidDataException("Campaign map has an unexpected region count.");
        if (Turn < 1 || Supplies < 0 || Alloy < 0 || Energy < 0 || Knowledge < 0 || ArmyStrength < 0 || ArmyReadiness is < 0 or > 100)
            throw new InvalidDataException("Campaign contains invalid turn, resource, or army values.");
        if (Regions.Select(region => region.Id).Distinct().Count() != Regions.Count || Regions.Any(region =>
                region.Id < 0 || region.Id >= Regions.Count ||
                region.Owner is not ("Kor" or "Neutral" or "Blight") ||
                region.Resource is not ("Supplies" or "Alloy" or "Energy" or "Knowledge") ||
                region.Terrain is not ("Granary" or "Mine" or "Solar field" or "Archive") || region.Production < 0 ||
                region.Blight is < 0 or > 3 || region.DevelopmentLevel is < 0 or > 3))
            throw new InvalidDataException("Campaign regions contain invalid IDs or state values.");
        if (!Regions.Any(region => region.Id == ArmyRegionId) || !Regions.Any(region => region.Id == SelectedRegionId))
            throw new InvalidDataException("Campaign army or selection refers to a missing region.");
        if (ArmyDestinationRegionId >= Regions.Count || ArmyDestinationRegionId < -1 || ActiveBattleRegionId >= Regions.Count || ActiveBattleRegionId < -1)
            throw new InvalidDataException("Campaign contains an invalid army order or battle region.");
    }
}

internal sealed class RegionState
{
    public int Id { get; set; }
    public int Q { get; set; }
    public int R { get; set; }
    public string Name { get; set; } = "Unnamed Reach";
    public string Terrain { get; set; } = "Frontier";
    public string Resource { get; set; } = "Supplies";
    public string Owner { get; set; } = "Neutral";
    public int Production { get; set; }
    public int Blight { get; set; }
    public int DevelopmentLevel { get; set; }
    public bool Explored { get; set; }
}

internal sealed record BattleSetup(int RegionId, int Seed, int Supplies, int Alloy, int GuardianDamage);

internal static class CampaignSaveService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string Serialize(CampaignState state)
    {
        state.Validate();
        return JsonSerializer.Serialize(state, JsonOptions);
    }

    public static CampaignState Deserialize(string json)
    {
        var state = JsonSerializer.Deserialize<CampaignState>(json, JsonOptions)
            ?? throw new InvalidDataException("Campaign save is empty.");
        state.Validate();
        return state;
    }


}
