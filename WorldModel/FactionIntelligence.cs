namespace KorTheBlight.WorldModel;

internal enum FactionStrategy { Expansion, Defense, EconomicBoom, WarPreparation, Containment }
internal enum FactionStanceTowardKor { Allied, Friendly, Neutral, Wary, Hostile, War }
internal enum FactionRelationshipCause { Trade, SharedThreat, BorderTension, IdeologicalDifference, RecentIncident }

internal sealed class FactionModifierState
{
    public string Cause { get; set; } = string.Empty;
    public int Value { get; set; }
}

internal sealed class FactionRelationshipState
{
    public string OtherFaction { get; set; } = string.Empty;
    public List<FactionModifierState> Modifiers { get; set; } = [];
    public int Score => Math.Clamp(Modifiers.Sum(modifier => modifier.Value), -100, 100);
}

internal sealed class FactionIntelligenceState
{
    public string Faction { get; set; } = string.Empty;
    public int TerritoryCount { get; set; }
    public double Treasury { get; set; }
    public double MilitaryPower { get; set; }
    public FactionStrategy Strategy { get; set; }
    public int Aggression { get; set; }
    public int ThreatLevel { get; set; }
    public double LastDecisionAt { get; set; }
    public List<FactionRelationshipState> Relationships { get; set; } = [];

    public FactionStanceTowardKor StanceToward(string faction = "Kor")
    {
        int score = Relationships.FirstOrDefault(item => item.OtherFaction == faction)?.Score ?? 0;
        if (score >= 70) return FactionStanceTowardKor.Allied;
        if (score >= 35) return FactionStanceTowardKor.Friendly;
        if (score <= -70) return FactionStanceTowardKor.War;
        if (score <= -35) return FactionStanceTowardKor.Hostile;
        if (score < -10) return FactionStanceTowardKor.Wary;
        return FactionStanceTowardKor.Neutral;
    }

    public int RelationshipTo(string faction)
        => Relationships.FirstOrDefault(item => item.OtherFaction == faction)?.Score ?? 0;
}

/// <summary>
/// Deterministic, persisted strategic faction director. Its inputs come from the live world:
/// settlements, inventories, armies, Blight fronts, and relationships. It does not issue war
/// declarations or pretend that diplomacy actions exist before those actions are implemented.
/// </summary>
internal static class FactionIntelligenceSystem
{
    public static void Initialize(ContinuousWorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.FactionIntelligence.Count > 0)
        {
            EnsureRelationships(world);
            return;
        }

        var factions = world.Entities
            .Where(entity => entity.Kind == WorldEntityKind.Settlement && entity.Faction != "Neutral")
            .Select(entity => entity.Faction)
            .Append("Kor")
            .Distinct(StringComparer.Ordinal)
            .Where(name => name != "Blight")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var random = new Random(unchecked(world.Seed * 397 ^ 0x4B4F52));
        foreach (string faction in factions)
        {
            var settlements = world.Entities.Where(entity => entity.Kind == WorldEntityKind.Settlement && entity.Faction == faction).ToArray();
            var armies = world.Entities.Count(entity => entity.Kind == WorldEntityKind.Army && entity.Faction == faction);
            double treasury = settlements.Sum(settlement => world.InventoryTotalAt(settlement.Id)) * 18d + random.Next(300, 1_601);
            double military = armies * random.Next(260, 501) + settlements.Length * random.Next(90, 221);
            var profile = new FactionIntelligenceState
            {
                Faction = faction,
                TerritoryCount = settlements.Length,
                Treasury = Math.Round(treasury, 0),
                MilitaryPower = Math.Round(military, 0),
                Aggression = faction == "Kor" ? 20 : random.Next(15, 76),
                Strategy = faction == "Kor" ? FactionStrategy.Defense : (FactionStrategy)random.Next(0, 4),
                ThreatLevel = 0
            };
            world.FactionIntelligence.Add(profile);
        }

        foreach (var profile in world.FactionIntelligence)
        {
            foreach (var other in world.FactionIntelligence.Where(candidate => candidate.Faction != profile.Faction))
            {
                int baseline = profile.Faction == "Kor" || other.Faction == "Kor"
                    ? random.Next(-18, 19)
                    : random.Next(-25, 26);
                var modifiers = new List<FactionModifierState>
                {
                    new() { Cause = "Diplomatic baseline", Value = baseline }
                };
                if (profile.Faction != "Kor" && other.Faction != "Kor")
                    modifiers.Add(new FactionModifierState { Cause = "Competing regional interests", Value = random.Next(-12, 7) });
                profile.Relationships.Add(new FactionRelationshipState { OtherFaction = other.Faction, Modifiers = modifiers });
            }
        }
        EnsureRelationships(world);
        Recalculate(world);
    }

    public static void Step(ContinuousWorldState world, double deltaSeconds)
    {
        if (deltaSeconds <= 0) return;
        if (world.FactionIntelligence.Count == 0) Initialize(world);
        foreach (var profile in world.FactionIntelligence)
        {
            if (world.ClockSeconds - profile.LastDecisionAt < 10d) continue;
            profile.LastDecisionAt = world.ClockSeconds;
            var settlements = world.Entities.Where(entity => entity.Kind == WorldEntityKind.Settlement && entity.Faction == profile.Faction).ToArray();
            int armies = world.Entities.Count(entity => entity.Kind == WorldEntityKind.Army && entity.Faction == profile.Faction);
            double food = settlements.Sum(settlement => world.InventoryAt(settlement.Id, WorldResourceKind.Food));
            double totalStock = settlements.Sum(settlement => world.InventoryTotalAt(settlement.Id));
            double blightPressure = settlements.Length == 0 ? 1d : settlements.Average(settlement => world.Ecology.EnvironmentalPressureAt(settlement.X, settlement.Y));
            int bestKorRelation = profile.RelationshipTo("Kor");
            int nearestThreat = world.Entities.Count(entity => entity.Kind == WorldEntityKind.BlightFront);
            profile.TerritoryCount = settlements.Length;
            profile.Treasury = Math.Max(0, Math.Round(totalStock * 18d + profile.Treasury * 0.015d + food * 0.05d, 0));
            profile.MilitaryPower = Math.Max(0, Math.Round(armies * 350d + settlements.Length * 120d, 0));
            profile.ThreatLevel = Math.Clamp((int)Math.Round(
                profile.Aggression * 0.48d +
                Math.Max(0, -bestKorRelation) * 0.35d +
                (profile.Faction == "Kor" ? 0 : Math.Min(30, nearestThreat * 5)) +
                Math.Min(20, blightPressure * 20d)), 0, 100);

            if (profile.Faction == "Kor")
                profile.Strategy = blightPressure >= 0.55d ? FactionStrategy.Containment : food < 25d ? FactionStrategy.EconomicBoom : FactionStrategy.Defense;
            else if (blightPressure >= 0.55d)
                profile.Strategy = FactionStrategy.Containment;
            else if (profile.ThreatLevel >= 68 && profile.MilitaryPower > 400)
                profile.Strategy = FactionStrategy.WarPreparation;
            else if (food < 25d || profile.Treasury < 800d)
                profile.Strategy = FactionStrategy.EconomicBoom;
            else if (profile.TerritoryCount < 2 && profile.Aggression > 45)
                profile.Strategy = FactionStrategy.Expansion;
            else
                profile.Strategy = FactionStrategy.Defense;
        }
    }

    public static void Recalculate(ContinuousWorldState world)
    {
        foreach (var profile in world.FactionIntelligence)
        {
            profile.TerritoryCount = world.Entities.Count(entity => entity.Kind == WorldEntityKind.Settlement && entity.Faction == profile.Faction);
            int korRelation = profile.RelationshipTo("Kor");
            profile.ThreatLevel = Math.Clamp((int)Math.Round(profile.Aggression * 0.48d + Math.Max(0, -korRelation) * 0.35d), 0, 100);
        }
    }

    private static void EnsureRelationships(ContinuousWorldState world)
    {
        foreach (var profile in world.FactionIntelligence)
        {
            profile.Relationships ??= [];
            foreach (var other in world.FactionIntelligence.Where(candidate => candidate.Faction != profile.Faction))
            {
                if (profile.Relationships.Any(relation => relation.OtherFaction == other.Faction)) continue;
                profile.Relationships.Add(new FactionRelationshipState
                {
                    OtherFaction = other.Faction,
                    Modifiers = [new FactionModifierState { Cause = "New diplomatic contact", Value = 0 }]
                });
            }
        }
    }
}
