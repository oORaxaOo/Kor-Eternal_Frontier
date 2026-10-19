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

    public static IReadOnlyList<ProgressionCatalog.UnitDef> ManagementUnits(int page)
    {
        const int pageSize = 5;
        return ProgressionCatalog.Units.OrderBy(unit => unit.Era)
            .ThenBy(unit => unit.Role, StringComparer.Ordinal)
            .Skip(Math.Max(0, page) * pageSize).Take(pageSize).ToArray();
    }

    public static int UnitPageCount => (ProgressionCatalog.Units.Count + 4) / 5;

    public static bool CanRecruitUnit(CampaignSimulation campaign, ColonySimulation colony, string unitId, out string reason)
    {
        var unit = ProgressionCatalog.Units.FirstOrDefault(item => item.Id == unitId);
        if (unit == null)
        {
            reason = "Unknown catalogue unit.";
            return false;
        }
        if (!colony.IsResearchComplete(unit.UnlockResearchId))
        {
            string researchName = ProgressionCatalog.Research.FirstOrDefault(item => item.Id == unit.UnlockResearchId)?.Name ?? unit.UnlockResearchId;
            reason = $"Locked by research: {researchName}.";
            return false;
        }
        if (campaign.State.Supplies < unit.SupplyCost || campaign.State.Alloy < unit.AlloyCost)
        {
            reason = $"Needs {unit.SupplyCost} Supplies and {unit.AlloyCost} Alloy.";
            return false;
        }
        reason = "Ready to recruit.";
        return true;
    }

    public static bool RecruitUnit(CampaignSimulation campaign, ColonySimulation colony, string unitId)
    {
        if (!CanRecruitUnit(campaign, colony, unitId, out string reason))
        {
            campaign.State.LastEvent = reason;
            colony.SetMessage(reason);
            return false;
        }
        bool recruited = campaign.RecruitUnit(unitId);
        colony.SetMessage(campaign.Message);
        return recruited;
    }

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
