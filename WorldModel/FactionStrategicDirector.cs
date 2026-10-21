namespace KorTheBlight.WorldModel;

/// <summary>Interval-driven faction blackboard: evaluates live ledgers and dispatches strategic agents.</summary>
internal static class FactionStrategicDirector
{
    public const long DecisionIntervalTicks = 20_000;
    public const double SecondsPerTick = 1d / 60d;

    public static void Step(ContinuousWorldState world)
    {
        long tick = (long)Math.Floor(world.ClockSeconds / SecondsPerTick);
        foreach (var profile in world.FactionIntelligence.OrderBy(item => item.Faction, StringComparer.Ordinal))
        {
            if (profile.Faction == "Kor") continue;
            if (tick - profile.LastStrategicDecisionTick < DecisionIntervalTicks) continue;
            if (tick < DecisionIntervalTicks) continue;
            profile.LastStrategicDecisionTick = tick;
            var settlements = world.Entities.Where(entity => entity.Kind == WorldEntityKind.Settlement && entity.Faction == profile.Faction)
                .OrderBy(entity => entity.Id).ToArray();
            if (settlements.Length == 0) continue;
            var home = settlements[0];
            double food = settlements.Sum(item => world.InventoryAt(item.Id, WorldResourceKind.Food));
            double stock = settlements.Sum(item => world.InventoryTotalAt(item.Id));
            bool activeCaravan = world.Entities.Any(entity => entity.Kind == WorldEntityKind.Caravan && entity.Faction == profile.Faction &&
                !world.Shipments.Any(shipment => shipment.CarrierEntityId == entity.Id && shipment.Status == WorldShipmentStatus.InTransit));
            bool hasArmy = world.Entities.Any(entity => entity.Kind == WorldEntityKind.Army && entity.Faction == profile.Faction);
            if ((food >= 25 && stock >= 60) && activeCaravan)
            {
                var destination = world.Entities.Where(entity => entity.Kind == WorldEntityKind.Settlement && entity.Faction != profile.Faction)
                    .OrderBy(entity => WorldRoutePlanner.FindRoute(world, home.X, home.Y, entity.X, entity.Y, 60).EstimatedSeconds)
                    .ThenBy(entity => entity.Id).FirstOrDefault();
                var caravan = world.Entities.First(entity => entity.Kind == WorldEntityKind.Caravan && entity.Faction == profile.Faction &&
                    !world.Shipments.Any(shipment => shipment.CarrierEntityId == entity.Id && shipment.Status == WorldShipmentStatus.InTransit));
                if (destination != null)
                {
                    var route = WorldRoutePlanner.FindRoute(world, home.X, home.Y, destination.X, destination.Y, 60);
                    // Existing shared ledger performs atomic debit and capacity-aware delivery.
                    var resource = world.InventoryAt(home.Id, WorldResourceKind.Food) >= 20 ? WorldResourceKind.Food : WorldResourceKind.Wood;
                    double amount = Math.Min(10, world.InventoryAt(home.Id, resource));
                    if (amount > 0)
                    {
                        double directDistance = Math.Sqrt(Math.Pow(destination.X - home.X, 2) + Math.Pow(destination.Y - home.Y, 2));
                        world.CreateShipment(caravan.Id, home.Id, destination.Id, resource, amount,
                            directDistance / Math.Max(1, route.EstimatedSeconds));
                    }
                }
            }
            else if (!hasArmy && (profile.Strategy is FactionStrategy.Defense or FactionStrategy.WarPreparation) &&
                     world.Entities.Count(entity => entity.Kind == WorldEntityKind.Army && entity.Faction == profile.Faction) < 3)
            {
                var army = world.AddEntity(WorldEntityKind.Army, $"{profile.Faction} Response Guard", profile.Faction,
                    home.X, home.Y);
                army.MovementSpeed = 0;
            }
        }
    }
}
