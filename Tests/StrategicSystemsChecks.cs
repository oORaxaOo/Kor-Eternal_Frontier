using KorTheBlight.Simulation;
using KorTheBlight.WorldModel;

namespace KorTheBlight.Tests;

internal static class StrategicSystemsChecks
{
    public static void Run()
    {
        var reservations = new ReservationManager();
        Check(reservations.Reserve("deposit:7", 1, 6, 10), "first pawn should reserve six units from a ten-unit stack");
        Check(!reservations.Reserve("deposit:7", 2, 5, 10), "reservation manager must reject oversubscription");
        Check(reservations.Reserve("deposit:7", 2, 4, 10), "a second pawn may reserve only the remaining stack quantity");
        Check(reservations.ReservedByOthers("deposit:7", 1) == 4, "reservation queries must exclude the requesting pawn");
        reservations.ReleasePawn(2);
        Check(reservations.Count == 1, "releasing a pawn must release all of its reservations");

        var matrix = new WorkPriorityMatrix();
        Check(matrix.GetPriority(1, WorkCategory.Construction) == 3, "new colonists should receive deterministic default work priorities");
        Check(matrix.SetPriority(1, WorkCategory.Construction, 0) &&
              matrix.GetPriority(1, WorkCategory.Construction) == 0,
            "priority zero must disable a work category");
        Check(!matrix.SetPriority(1, WorkCategory.Mining, 5), "work priorities must clamp by rejection to the 0–4 UI range");

        var routeWorld = ContinuousWorldState.Generate(41021);
        var route = WorldRoutePlanner.FindRoute(routeWorld, 1_000, 1_000, 92_000, 88_000, 60);
        Check(route.Nodes.Count > 2 && route.TravelCost > 0 && route.EstimatedSeconds > 0,
            "strategic route planning must return a deterministic coarse route and travel-time estimate");
        var routeAgain = WorldRoutePlanner.FindRoute(routeWorld, 1_000, 1_000, 92_000, 88_000, 60);
        Check(route.Nodes.SequenceEqual(routeAgain.Nodes) &&
              Math.Abs(route.EstimatedSeconds - routeAgain.EstimatedSeconds) < 0.0001,
            "the same world and endpoints must produce the same strategic route");

        routeWorld.ClockSeconds = 400;
        FactionStrategicDirector.Step(routeWorld);
        Check(routeWorld.FactionIntelligence.Where(profile => profile.Faction != "Kor")
                  .All(profile => profile.LastStrategicDecisionTick >= FactionStrategicDirector.DecisionIntervalTicks),
            "each rival faction must record its 20,000-tick macro decision");

        var wealth = new ColonyWealth();
        var sampled = wealth.Sample(2_000,
            new Dictionary<ResourceKind, int>
            {
                [ResourceKind.Wood] = 100, [ResourceKind.Stone] = 100, [ResourceKind.Food] = 100,
                [ResourceKind.Alloy] = 100, [ResourceKind.Components] = 100
            },
            new[] { (PlaceableKind.Habitat, true), (PlaceableKind.Generator, true), (PlaceableKind.Lamp, false) },
            new[] { ArchitectureKind.Wall, ArchitectureKind.Door });
        Check(sampled.TotalWealth == sampled.StoredResourcesValue + sampled.StructuresValue &&
              sampled.ThreatPoints is > 0 and <= 250,
            "wealth must include market-valued stock and built structures and produce a bounded threat budget");
        var restoredWealth = new ColonyWealth();
        restoredWealth.Restore(sampled);
        Check(restoredWealth.Current == sampled, "wealth metrics must survive restoration");

        var storyteller = new ColonyStoryteller();
        var incidents = storyteller.Step(2_000, new StorytellerColonyState(
            3, 32, 0, 10, 10, 10_000, 160));
        var wealthIncident = incidents.SingleOrDefault(item => item.Code == "wealth_attracts_attention");
        Check(wealthIncident != null && wealthIncident.ThreatPoints == 160 && wealthIncident.ThreatUnits == 6,
            "high colony wealth must generate a scaled, bounded incident budget");

        var raidWorld = new WorldSimulation(41022);
        long korSettlementId = raidWorld.SpatialWorld.Entities.Single(entity =>
            entity.Kind == WorldEntityKind.Settlement && entity.Faction == "Kor").Id;
        raidWorld.SpatialWorld.SetInventoryCapacity(korSettlementId, 10_000);
        Check(raidWorld.SpatialWorld.TryAddInventory(korSettlementId, WorldResourceKind.Alloy, 1_000),
            "wealth incident test must be able to seed valuable stock in the shared ledger");
        raidWorld.Step(34f);
        Check(raidWorld.SpatialWorld.Entities.Any(entity =>
                entity.Kind == WorldEntityKind.Army && entity.Faction == "Raiders" &&
                entity.Name.StartsWith("Kor Wealth Raid ", StringComparison.Ordinal)),
            "a wealth-triggered storyteller budget must create a deterministic, moving raid force on the strategic map");

        var colony = new ColonySimulation();
        colony.WorkPriorities.SetPriority(1, WorkCategory.Construction, 0);
        var restoredColony = ColonySaveService.Deserialize(ColonySaveService.Serialize(colony));
        Check(restoredColony.WorkPriorities.GetPriority(1, WorkCategory.Construction) == 0,
            "the per-colonist work matrix must persist through save/load");

        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
