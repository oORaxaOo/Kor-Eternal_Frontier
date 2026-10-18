using KorTheBlight.Simulation;

namespace KorTheBlight.Campaign;

/// <summary>
/// Explicit, capacity-aware transfer boundary between the strategic economy and the local colony.
/// Strategic Supplies map to local Food; Alloy maps directly to Alloy. Energy and Knowledge remain
/// strategic-only until the local simulation has corresponding resources and consumers.
/// </summary>
internal static class CampaignColonyBridge
{
    public const int MaxFoodImportPerOrder = 20;
    public const int MaxAlloyImportPerOrder = 10;
    public const int MaxFoodExportPerOrder = 20;
    public const int MaxAlloyExportPerOrder = 10;

    public static int ImportToColony(CampaignSimulation campaign, ColonySimulation colony)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(colony);

        int foodRequested = Math.Min(MaxFoodImportPerOrder, campaign.State.Supplies);
        int foodDelivered = colony.AddExternalResource(ResourceKind.Food, foodRequested);
        campaign.State.Supplies -= foodDelivered;

        int alloyRequested = Math.Min(MaxAlloyImportPerOrder, campaign.State.Alloy);
        int alloyDelivered = colony.AddExternalResource(ResourceKind.Alloy, alloyRequested);
        campaign.State.Alloy -= alloyDelivered;

        int total = foodDelivered + alloyDelivered;
        campaign.State.LastEvent = total == 0
            ? "Resupply denied: no strategic stock available or local storage is full."
            : $"Resupply delivered {foodDelivered} Food and {alloyDelivered} Alloy to the local colony.";
        colony.SetMessage(campaign.State.LastEvent);
        return total;
    }

    public static int ExportFromColony(CampaignSimulation campaign, ColonySimulation colony)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(colony);

        int food = colony.RemoveExternalResource(ResourceKind.Food, MaxFoodExportPerOrder);
        int alloy = colony.RemoveExternalResource(ResourceKind.Alloy, MaxAlloyExportPerOrder);
        campaign.State.Supplies = checked(campaign.State.Supplies + food);
        campaign.State.Alloy = checked(campaign.State.Alloy + alloy);

        int total = food + alloy;
        campaign.State.LastEvent = total == 0
            ? "Export manifest empty: no Food or Alloy available in colony stores."
            : $"Colony export received: {food} Food became strategic Supplies; {alloy} Alloy transferred.";
        colony.SetMessage(campaign.State.LastEvent);
        return total;
    }
}
