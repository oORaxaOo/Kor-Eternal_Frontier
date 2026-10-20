namespace KorTheBlight.Tests;

internal static class SocialRelationshipChecks
{
    public static void Run()
    {
        CanonicalPairAndDeterminism();
        NeedsInfluenceRelationships();
        HistoryIsBoundedAndRestorable();
        IncidentsAreMilestoneDrivenAndRestorable();
    }

    private static void CanonicalPairAndDeterminism()
    {
        var first = new SocialRelationshipNetwork();
        var second = new SocialRelationshipNetwork();
        for (int tick = 1; tick <= 40; tick++)
        {
            first.RecordEncounter(7, 3, tick, 0, 0, 0, false, 0, 0, 0, false);
            second.RecordEncounter(3, 7, tick, 0, 0, 0, false, 0, 0, 0, false);
        }
        var forward = first.Get(7, 3) ?? throw new InvalidOperationException("Missing canonical social pair.");
        var reverse = first.Get(3, 7) ?? throw new InvalidOperationException("Reverse pair lookup failed.");
        Require(ReferenceEquals(forward, reverse), "Resident pair identity must be symmetric.");
        Require(first.RelationshipCount == 1, "A resident pair must be stored exactly once.");
        var other = second.Get(7, 3) ?? throw new InvalidOperationException("Deterministic pair missing.");
        Require(forward.Affinity == other.Affinity && forward.Tension == other.Tension &&
                forward.EncounterCount == other.EncounterCount, "Identical encounter sequences must produce identical outcomes.");
        Require(first.DrainEvents().Select(x => (x.Tick, x.Kind, x.ResidentA, x.ResidentB, x.CurrentScore))
            .SequenceEqual(second.DrainEvents().Select(x => (x.Tick, x.Kind, x.ResidentA, x.ResidentB, x.CurrentScore))),
            "Event ordering must be deterministic.");
    }

    private static void NeedsInfluenceRelationships()
    {
        var calm = new SocialRelationshipNetwork();
        var strained = new SocialRelationshipNetwork();
        for (int tick = 1; tick <= 20; tick++)
        {
            calm.RecordEncounter(1, 2, tick, 0, 0, 0, false, 0, 0, 0, false);
            strained.RecordEncounter(1, 2, tick, 100, 100, 100, true, 100, 100, 100, false);
        }
        Require(calm.Get(1, 2)!.Affinity > strained.Get(1, 2)!.Affinity,
            "Stress, hunger, fatigue and mental breaks must affect encounter outcomes.");
        Require(strained.Get(1, 2)!.Affinity >= SocialRelationshipNetwork.MinimumScore,
            "Affinity must remain within its lower bound.");
        Require(calm.Get(1, 2)!.Affinity <= SocialRelationshipNetwork.MaximumScore,
            "Affinity must remain within its upper bound.");
    }

    private static void HistoryIsBoundedAndRestorable()
    {
        var network = new SocialRelationshipNetwork();
        for (int tick = 1; tick <= 100; tick++)
            network.RecordEncounter(1, 2, tick, 10, 10, 10, false, 10, 10, 10, false);
        var relation = network.Get(1, 2)!;
        Require(relation.History.Count == SocialRelationshipNetwork.MaximumHistoryPerPair,
            "Relationship history must remain bounded.");
        var restored = SocialRelationshipNetwork.Restore(network.CaptureSnapshot(), new HashSet<int> { 1, 2 });
        var copy = restored.Get(2, 1)!;
        Require(copy.EncounterCount == relation.EncounterCount && copy.Affinity == relation.Affinity &&
                copy.History.SequenceEqual(relation.History), "Relationship state must round-trip through snapshots.");
    }

    private static void IncidentsAreMilestoneDrivenAndRestorable()
    {
        var network = new SocialRelationshipNetwork();
        for (int tick = 1; tick <= 25; tick++)
            network.RecordEncounter(1, 2, tick, 100, 100, 100, true, 100, 100, 100, true);
        var manager = new IncidentManager();
        manager.Consume(network.DrainEvents());
        Require(manager.Incidents.Any(x => x.Code == "resident_conflict"),
            "Crossing the conflict threshold must create an incident.");
        int count = manager.Count;
        manager.Consume(Array.Empty<SocialSimulationEvent>());
        Require(manager.Count == count, "Empty event batches must not duplicate incidents.");
        var restored = IncidentManager.Restore(manager.CaptureSnapshot(), new HashSet<int> { 1, 2 });
        Require(restored.Count == manager.Count, "Incident state must round-trip through snapshots.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
