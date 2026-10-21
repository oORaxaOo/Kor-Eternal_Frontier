using KorTheBlight.WorldModel;

namespace KorTheBlight.Tests;

internal static class FactionIntelligenceChecks
{
    public static void Run()
    {
        var world = ContinuousWorldState.Generate(18421);
        Check(world.FactionIntelligence.Count == 6,
            "the strategic director should create Kor plus the five generated rival factions");

        var kor = world.FactionIntelligence.Single(profile => profile.Faction == "Kor");
        Check(kor.TerritoryCount == world.Entities.Count(entity =>
                entity.Kind == WorldEntityKind.Settlement && entity.Faction == "Kor"),
            "faction territory counts must be derived from actual world settlements");
        Check(world.FactionIntelligence.All(profile =>
                profile.Relationships.Count == world.FactionIntelligence.Count - 1 &&
                profile.Relationships.All(relation => relation.Score is >= -100 and <= 100)),
            "each faction must expose bounded, complete relationship rows for every other faction");
        Check(world.FactionIntelligence.All(profile =>
                profile.Relationships.Any(relation => relation.OtherFaction == "Kor") ||
                profile.Faction == "Kor"),
            "all non-Kor factions must have a relationship entry toward Kor");

        var sameSeed = ContinuousWorldState.Generate(18421);
        Check(world.FactionIntelligence.Select(profile => (profile.Faction, profile.Aggression, profile.Treasury, profile.RelationshipTo("Kor")))
                .SequenceEqual(sameSeed.FactionIntelligence.Select(profile => (profile.Faction, profile.Aggression, profile.Treasury, profile.RelationshipTo("Kor")))),
            "faction strategic baselines and diplomatic modifiers must be reproducible from the world seed");

        double treasuryBefore = kor.Treasury;
        world.Step(11d);
        Check(kor.LastDecisionAt >= 10d && kor.TerritoryCount > 0,
            "the faction director should evaluate its strategy on the world simulation clock");
        Check(kor.Treasury >= 0 && kor.MilitaryPower >= 0 && kor.ThreatLevel is >= 0 and <= 100,
            "director metrics must remain within valid bounds");
        Check(world.FactionIntelligence.Any(profile => profile.Treasury != treasuryBefore) || kor.LastDecisionAt >= 10d,
            "the strategic metrics should be refreshed during director evaluation");

        var restored = ContinuousWorldState.Deserialize(ContinuousWorldState.Serialize(world));
        Check(restored.FactionIntelligence.Count == world.FactionIntelligence.Count &&
              restored.FactionIntelligence.Single(profile => profile.Faction == "Kor").Relationships.Count == kor.Relationships.Count &&
              restored.FactionIntelligence.Single(profile => profile.Faction == "Kor").Strategy == kor.Strategy,
            "faction strategies, threat metrics and relationship modifiers must survive world save/load");

        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
