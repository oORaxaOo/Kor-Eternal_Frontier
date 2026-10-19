using KorTheBlight.Simulation;

namespace KorTheBlight.Campaign;

internal sealed class CampaignSimulation
{
    private static readonly Dictionary<string, (string Resource, int Production)> RegionYields = new()
    {
        ["Granary"] = ("Supplies", 12),
        ["Mine"] = ("Alloy", 9),
        ["Solar field"] = ("Energy", 8),
        ["Archive"] = ("Knowledge", 6)
    };

    public CampaignState State { get; }
    public RegionState SelectedRegion => State.Regions.Single(region => region.Id == State.SelectedRegionId);
    public int PendingArmyDestination => State.ArmyDestinationRegionId;
    public string Message => State.LastEvent;
    public bool HasSave => File.Exists(CampaignSaveService.SavePath);
    public int TacticalGuardianDamage => State.WardProtocolResearched ? 36 : 24;

    public CampaignSimulation(int seed)
    {
        State = WorldGenerationService.Generate(seed);
    }

    public CampaignSimulation(CampaignState state)
    {
        state.Validate();
        State = state;
    }

    public void SelectRegion(int id)
    {
        if (id < 0 || id >= State.Regions.Count) return;
        State.SelectedRegionId = id;
    }

    public bool CanDevelopSelected
    {
        get
        {
            var region = SelectedRegion;
            if (region.Owner == "Kor") return region.DevelopmentLevel < 3 && State.Supplies >= 45 && State.Alloy >= 35;
            return region.Owner == "Neutral" && region.Explored && IsAdjacentToKor(region) && State.Supplies >= 80 && State.Alloy >= 50;
        }
    }

    public string DevelopButtonLabel => SelectedRegion.Owner == "Kor"
        ? $"Upgrade settlement  (45 S / 35 A)"
        : "Establish outpost  (80 S / 50 A)";

    public bool DevelopSelected()
    {
        if (!CanDevelopSelected) return false;
        var region = SelectedRegion;
        if (region.Owner == "Kor")
        {
            State.Supplies -= 45; State.Alloy -= 35;
            region.DevelopmentLevel++;
            State.LastEvent = $"{region.Name} upgraded to development level {region.DevelopmentLevel}.";
        }
        else
        {
            State.Supplies -= 80; State.Alloy -= 50;
            region.Owner = "Kor"; region.DevelopmentLevel = 1; region.Explored = true;
            State.LastEvent = $"Kor outpost established in {region.Name}.";
        }
        return true;
    }

    public bool CanResearchWard => !State.WardProtocolResearched && State.Knowledge >= 30 && State.Energy >= 20;

    public bool ResearchWard()
    {
        if (!CanResearchWard) return false;
        State.Knowledge -= 30; State.Energy -= 20;
        State.WardProtocolResearched = true;
        State.LastEvent = "Ward Protocol researched: Guardians deal more damage and Blight spreads more slowly.";
        return true;
    }

    public bool RecruitUnit(string unitId)
    {
        var unit = ProgressionCatalog.Units.FirstOrDefault(item => item.Id == unitId);
        if (unit == null)
        {
            State.LastEvent = "Recruitment failed: unknown catalogue unit.";
            return false;
        }
        if (State.Supplies < unit.SupplyCost || State.Alloy < unit.AlloyCost)
        {
            State.LastEvent = $"Recruitment failed: {unit.Name} needs {unit.SupplyCost} Supplies and {unit.AlloyCost} Alloy.";
            return false;
        }
        State.Supplies -= unit.SupplyCost;
        State.Alloy -= unit.AlloyCost;
        State.RecruitedUnits.Add(unit.Id);
        State.ArmyStrength = checked(State.ArmyStrength + unit.CombatStrength);
        State.LastEvent = $"Recruited {unit.Name}: +{unit.CombatStrength} army strength. Upkeep {unit.Upkeep} Supplies per turn.";
        return true;
    }

    public bool CanMarchSelected => SelectedRegion.Id != State.ArmyRegionId &&
        SelectedRegion.Owner != "Blight" && IsAdjacent(State.ArmyRegionId, SelectedRegion.Id) && State.ArmyStrength > 0;

    public bool OrderMarch()
    {
        if (!CanMarchSelected) return false;
        State.ArmyDestinationRegionId = SelectedRegion.Id;
        State.LastEvent = $"Army orders set: march to {SelectedRegion.Name}. End the turn to complete the move.";
        return true;
    }

    public bool CanStartBattle
    {
        get
        {
            var region = SelectedRegion;
            return region.Owner == "Blight" && IsAdjacent(State.ArmyRegionId, region.Id) && State.ArmyStrength > 0 && State.Supplies >= 80 && State.Alloy >= 80;
        }
    }

    public BattleSetup PrepareBattle()
    {
        if (!CanStartBattle) throw new InvalidOperationException("The selected region cannot currently be attacked.");
        State.SelectedRegionId = SelectedRegion.Id;
        int supplies = State.Supplies, alloy = State.Alloy;
        State.Supplies = 0; State.Alloy = 0;
        State.ActiveBattleRegionId = SelectedRegion.Id;
        int seed = unchecked(State.Seed * 397 ^ SelectedRegion.Id * 7919 ^ State.Turn * 104729);
        State.LastEvent = $"Kor forces entered {SelectedRegion.Name}. The campaign is paused during battle.";
        return new BattleSetup(SelectedRegion.Id, seed, supplies, alloy, TacticalGuardianDamage);
    }

    public void ResolveBattle(bool won, int remainingSupplies, int remainingAlloy)
    {
        if (State.ActiveBattleRegionId < 0 || State.ActiveBattleRegionId >= State.Regions.Count)
            throw new InvalidOperationException("Battle result has no valid campaign region.");
        var region = State.Regions[State.ActiveBattleRegionId];
        State.Supplies += Math.Max(0, remainingSupplies);
        State.Alloy += Math.Max(0, remainingAlloy);
        if (won)
        {
            region.Owner = "Kor"; region.Blight = 0; region.Explored = true;
            region.DevelopmentLevel = Math.Max(1, region.DevelopmentLevel);
            State.ArmyRegionId = region.Id;
            State.ArmyReadiness = Math.Min(100, State.ArmyReadiness + 15);
            State.LastEvent = $"Victory at {region.Name}. The region is now under Kor control; surviving battle resources returned to the campaign.";
        }
        else
        {
            var weakest = State.RecruitedUnits.Select((id, index) => new
                {
                    Index = index,
                    Unit = ProgressionCatalog.Units.First(item => item.Id == id)
                })
                .OrderBy(item => item.Unit.CombatStrength)
                .ThenBy(item => item.Unit.Era)
                .FirstOrDefault();
            if (weakest != null)
            {
                State.RecruitedUnits.RemoveAt(weakest.Index);
                State.ArmyStrength = Math.Max(0, State.ArmyStrength - weakest.Unit.CombatStrength);
            }
            else State.ArmyStrength = Math.Max(0, State.ArmyStrength - 1);
            State.ArmyReadiness = Math.Max(0, State.ArmyReadiness - 35);
            State.LastEvent = State.ArmyStrength == 0
                ? $"Defeat at {region.Name}. The field army was lost; rebuild it before another assault."
                : $"Defeat at {region.Name}. Kor forces withdrew with losses and low readiness.";
        }
        State.ActiveBattleRegionId = -1;
    }

    public void EndTurn()
    {
        State.Turn++;
        var income = new Dictionary<string, int> { ["Supplies"] = 8, ["Alloy"] = 3, ["Energy"] = 3, ["Knowledge"] = 2 };
        foreach (var region in State.Regions.Where(region => region.Owner == "Kor" && region.DevelopmentLevel > 0))
        {
            int level = region.DevelopmentLevel;
            if (RegionYields.TryGetValue(region.Terrain, out var yield))
                income[yield.Resource] += yield.Production * level;
            else income[region.Resource] += region.Production * level;
            if (region.Blight > 0) income[region.Resource] = Math.Max(0, income[region.Resource] - region.Blight * 2);
        }
        State.Supplies += income["Supplies"];
        State.Alloy += income["Alloy"];
        State.Energy += income["Energy"];
        State.Knowledge += income["Knowledge"];

        if (State.ArmyDestinationRegionId >= 0)
        {
            State.ArmyRegionId = State.ArmyDestinationRegionId;
            State.ArmyReadiness = Math.Max(25, State.ArmyReadiness - 5);
            State.ArmyDestinationRegionId = -1;
        }
        int recruitedStrength = State.RecruitedUnits.Sum(id =>
            ProgressionCatalog.Units.First(unit => unit.Id == id).CombatStrength);
        int unitUpkeep = State.RecruitedUnits.Sum(id =>
            ProgressionCatalog.Units.First(unit => unit.Id == id).Upkeep);
        int baseArmyUpkeep = Math.Max(0, State.ArmyStrength - recruitedStrength) * 5;
        int totalUpkeep = baseArmyUpkeep + unitUpkeep;
        bool upkeepPaid = State.Supplies >= totalUpkeep;
        if (upkeepPaid) State.Supplies -= totalUpkeep;
        else State.ArmyReadiness = Math.Max(0, State.ArmyReadiness - 12);

        if (State.WardProtocolResearched)
        {
            foreach (var region in State.Regions.Where(region => region.Owner == "Kor" && region.Blight > 0)) region.Blight--;
        }
        if (State.Turn % (State.WardProtocolResearched ? 4 : 2) == 0)
            SpreadBlightOneRegion();

        State.LastEvent = $"Turn {State.Turn}: +{income["Supplies"]} supplies, +{income["Alloy"]} alloy, +{income["Energy"]} energy, +{income["Knowledge"]} knowledge. Unit upkeep {unitUpkeep} Supplies; {(upkeepPaid ? "upkeep paid" : "upkeep unpaid")}. Army readiness {State.ArmyReadiness}%.";
    }

    private void SpreadBlightOneRegion()
    {
        foreach (var source in State.Regions.Where(region => region.Owner == "Blight").OrderBy(region => region.Id))
        {
            var next = State.Regions.Where(region => region.Owner == "Neutral" && IsAdjacent(source.Id, region.Id))
                .OrderBy(region => region.Id).FirstOrDefault();
            if (next == null) continue;
            next.Owner = "Blight"; next.Blight = Math.Max(1, source.Blight - 1); next.Explored = true;
            State.LastEvent = $"Blight spread into {next.Name}. Research Ward Protocol or contain the source.";
            return;
        }
    }

    private bool IsAdjacentToKor(RegionState region) => State.Regions.Any(other => other.Owner == "Kor" && other.DevelopmentLevel > 0 && IsAdjacent(region.Id, other.Id));

    public bool IsAdjacent(int aId, int bId)
    {
        if (aId < 0 || aId >= State.Regions.Count || bId < 0 || bId >= State.Regions.Count || aId == bId) return false;
        var a = State.Regions[aId]; var b = State.Regions[bId];
        int distance = (Math.Abs(a.Q - b.Q) + Math.Abs(a.R - b.R) + Math.Abs(a.Q + a.R - b.Q - b.R)) / 2;
        return distance == 1;
    }
}

internal static class WorldGenerationService
{
    internal static readonly (int Q, int R)[] RegionPositions =
    [
        (0, 0), (1, 0), (0, 1), (-1, 1), (-1, 0), (0, -1), (1, -1), (2, 0), (1, 1)
    ];

    private static readonly string[] Names =
    [
        "Morrow Reach", "Vesper Flats", "Ashen Vale", "Cinder Basin", "Avar Ridge",
        "Glasswater", "Sable March", "Red Orchard", "Kestrel Steppe", "Hollow Crown",
        "Ember Coast", "Old Meridian", "Thornfield", "Dawn Shelf", "Ironwood"
    ];
    private static readonly string[] Terrains = ["Granary", "Mine", "Solar field", "Archive"];

    public static CampaignState Generate(int seed)
    {
        var random = new Random(seed);
        var names = Names.OrderBy(_ => random.Next()).Take(RegionPositions.Length).ToArray();
        var state = new CampaignState
        {
            Seed = seed, Turn = 1, Supplies = 420, Alloy = 220, Energy = 100, Knowledge = 35,
            ArmyRegionId = 0, ArmyStrength = 2, ArmyReadiness = 100, SelectedRegionId = 0
        };
        for (int i = 0; i < RegionPositions.Length; i++)
        {
            string terrain = i == 0 ? "Granary" : Terrains[random.Next(Terrains.Length)];
            string owner = i == 0 ? "Kor" : i == 3 ? "Blight" : "Neutral";
            state.Regions.Add(new RegionState
            {
                Id = i, Q = RegionPositions[i].Q, R = RegionPositions[i].R,
                Name = names[i], Terrain = terrain, Owner = owner,
                Resource = terrain == "Granary" ? "Supplies" : terrain == "Mine" ? "Alloy" : terrain == "Solar field" ? "Energy" : "Knowledge",
                Production = 4 + random.Next(5), Blight = owner == "Blight" ? 3 : 0,
                DevelopmentLevel = i == 0 ? 1 : 0,
                Explored = i == 0 || (Math.Abs(RegionPositions[i].Q) + Math.Abs(RegionPositions[i].R) + Math.Abs(RegionPositions[i].Q + RegionPositions[i].R)) / 2 <= 1
            });
        }
        state.Validate();
        return state;
    }
}
