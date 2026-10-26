using KorTheBlight.WorldModel;
using KorTheBlight.Rendering;
using KorTheBlight.Simulation;

namespace KorTheBlight.Tests;

internal static class ContinuousWorldChecks
{
    public static void Run()
    {
        var legacyHerdEvent = System.Text.Json.JsonSerializer.Deserialize<WorldEventState>(
            """{"Id":7,"Kind":1,"OccurredAt":300,"Title":"herd movement","Description":"legacy event"}""");
        Check(legacyHerdEvent?.Kind == WorldEventKind.HerdMigration,
            "new world event kinds must not renumber the event IDs persisted in older saves");

        int FindSeedForWeather(WorldWeather desired)
        {
            for (int seed = 0; seed < 20_000; seed++)
            {
                var climateProbe = new WorldEcologyState();
                climateProbe.SynchronizeClimate(301, seed);
                if (climateProbe.CurrentWeather == desired) return seed;
            }
            throw new InvalidOperationException($"Could not find a deterministic seed for {desired}.");
        }

        int rainSeed = FindSeedForWeather(WorldWeather.Rain);
        int droughtSeed = FindSeedForWeather(WorldWeather.Drought);
        var wetEcology = new WorldEcologyState();
        wetEcology.Initialize(rainSeed);
        wetEcology.Hotspots[0].Intensity = .4d;
        wetEcology.Hotspots[0].Radius = 1_000d;
        wetEcology.Hotspots[0].NextSpreadAt = double.MaxValue;
        wetEcology.Step(1d, 301d, rainSeed);
        var dryEcology = new WorldEcologyState();
        dryEcology.Initialize(droughtSeed);
        dryEcology.Hotspots[0].Intensity = .4d;
        dryEcology.Hotspots[0].Radius = 1_000d;
        dryEcology.Hotspots[0].NextSpreadAt = double.MaxValue;
        dryEcology.Step(1d, 301d, droughtSeed);
        Check(wetEcology.CurrentWeather == WorldWeather.Rain &&
              dryEcology.CurrentWeather == WorldWeather.Drought &&
              wetEcology.Hotspots[0].Intensity < dryEcology.Hotspots[0].Intensity &&
              wetEcology.Hotspots[0].Radius < dryEcology.Hotspots[0].Radius,
            "rain must suppress strategic Blight growth while drought accelerates it on the same season tick");

        var ecologyWorld = ContinuousWorldState.Generate(81357);
        Check(ecologyWorld.Ecology.CurrentSeason == WorldSeason.Spring &&
              Enum.IsDefined(ecologyWorld.Ecology.CurrentWeather),
            "a new world must initialize a valid deterministic season and weather state");
        bool weatherChanged = false;
        WorldWeather firstWeather = ecologyWorld.Ecology.CurrentWeather;
        for (int i = 0; i < 6; i++)
        {
            ecologyWorld.Step(60);
            weatherChanged |= ecologyWorld.Ecology.CurrentWeather != firstWeather;
        }
        Check(weatherChanged,
            "world weather must transition over time instead of being an event-only label");
        Check(ecologyWorld.Ecology.RecentEvents.Any(item => item.Kind == WorldEventKind.WeatherShift),
            "actual climate changes must be recorded in the same world event history shown to the player");
        Check(ecologyWorld.Ecology.FoodGrowthMultiplier > 0,
            "season and weather must expose a usable modifier for the shared world food economy");

        int macroClimateSeed = Enumerable.Range(0, 10_000).First(seed =>
        {
            var probe = new WorldEcologyState();
            probe.SynchronizeClimate(1d, seed);
            double springMultiplier = probe.FoodGrowthMultiplier;
            probe.SynchronizeClimate(WorldEcologyState.SeasonDurationSeconds + 1d, seed);
            return Math.Abs(probe.FoodGrowthMultiplier - springMultiplier) > .01d;
        });
        double MeasureMacroFoodGrowth(double clockSeconds, out double expectedMultiplier)
        {
            var climateWorld = ContinuousWorldState.Generate(macroClimateSeed);
            foreach (var hotspot in climateWorld.Ecology.Hotspots) hotspot.IsActive = false;
            int cellIndex = Enumerable.Range(0, MacroWorldState.CellCount).First(index =>
            {
                var cell = climateWorld.MacroWorld.Cells[index];
                return cell.OwningFactionId != 0 && cell.Population > 0 &&
                       cell.ProductionFocus == ProductionFocus.Agriculture && cell.Fertility > 0 &&
                       cell.BlightPresence < .75f;
            });
            var initialCell = climateWorld.MacroWorld.Cells[cellIndex];
            climateWorld.ClockSeconds = clockSeconds;
            climateWorld.MacroWorld.SimulationTicks = MacroWorldState.TicksPerMacroTurn -
                                                      TickManager.TicksPerSecond;
            climateWorld.MacroWorld.MacroTurnsScheduled = 0;
            climateWorld.MacroWorld.TurnPhase = MacroTurnPhase.Economy;
            climateWorld.MacroWorld.MacroTurnCursor = cellIndex;
            climateWorld.MacroWorld.MarketTurnAccumulator.Reset();
            climateWorld.Step(1d);
            expectedMultiplier = climateWorld.Ecology.FoodGrowthMultiplier;
            var advancedCell = climateWorld.MacroWorld.Cells[cellIndex];
            double outputFactor = initialCell.Population * .015d * initialCell.Fertility *
                                  (1d - initialCell.BlightPresence * .9d);
            return (advancedCell.FoodCount - initialCell.FoodCount +
                    initialCell.Population * .01d) / outputFactor;
        }
        double observedSpringFoodGrowth = MeasureMacroFoodGrowth(0d, out double expectedSpringFoodGrowth);
        double observedSummerFoodGrowth = MeasureMacroFoodGrowth(
            WorldEcologyState.SeasonDurationSeconds, out double expectedSummerFoodGrowth);
        Check(Math.Abs(observedSpringFoodGrowth - expectedSpringFoodGrowth) < .02d &&
              Math.Abs(observedSummerFoodGrowth - expectedSummerFoodGrowth) < .02d &&
              Math.Abs(expectedSpringFoodGrowth - expectedSummerFoodGrowth) > .01d,
            "macro agriculture must consume the shared seasonal/weather multiplier rather than a separate fixed climate model");
        var seasonalWorld = ContinuousWorldState.Generate(81357);
        double springFoodMultiplier = seasonalWorld.Ecology.FoodGrowthMultiplier;
        for (int i = 0; i < 60; i++) seasonalWorld.Step(60);
        Check(seasonalWorld.Ecology.CurrentSeason == WorldSeason.Summer &&
              Math.Abs(seasonalWorld.Ecology.FoodGrowthMultiplier - springFoodMultiplier) > 0.001,
            "the seasonal calendar must advance and change food-growing conditions");
        var depletedHerdWorld = ContinuousWorldState.Generate(81358);
        var depletedHerd = depletedHerdWorld.Ecology.Herds[0];
        depletedHerd.Forage = 0;
        depletedHerd.NextPopulationChangeAt = 0;
        int populationBeforeLoss = depletedHerd.Population;
        depletedHerdWorld.Step(1);
        int populationAfterLoss = depletedHerd.Population;
        depletedHerdWorld.Step(1);
        Check(populationBeforeLoss - populationAfterLoss == 1 &&
              depletedHerd.Population == populationAfterLoss,
            "a starving herd must lose at most one animal per scheduled population update, not every render tick");
        int initialHotspotCount = ecologyWorld.Ecology.Hotspots.Count;
        Check(ecologyWorld.Ecology.Herds.Count >= 3 && initialHotspotCount >= 2 &&
              ecologyWorld.Entities.Count(entity => entity.Kind == WorldEntityKind.BlightFront) == initialHotspotCount &&
              ecologyWorld.Ecology.Hotspots.All(hotspot =>
                  hotspot.WorldEntityId.HasValue &&
                  ecologyWorld.Entities.Any(entity => entity.Id == hotspot.WorldEntityId &&
                                                       entity.Kind == WorldEntityKind.BlightFront)),
            "every generated strategic Blight front must have a linked, persisted ecological hotspot");
        var firstHerd = ecologyWorld.Ecology.Herds[0];
        double herdStartX = firstHerd.X, herdStartY = firstHerd.Y;
        for (int i = 0; i < 500; i++) ecologyWorld.Step(60);
        Check(Math.Abs(firstHerd.X - herdStartX) > .01 || Math.Abs(firstHerd.Y - herdStartY) > .01,
            "wildlife herds must migrate autonomously without player orders");
        Check(ecologyWorld.Ecology.RecentEvents.Count > 0,
            "the world must generate and retain time-based ambient events");
        Check(ecologyWorld.Ecology.Hotspots.Count > initialHotspotCount &&
              ecologyWorld.Entities.Count(entity => entity.Kind == WorldEntityKind.BlightFront) ==
                  ecologyWorld.Ecology.Hotspots.Count,
            "new Blight hotspots must spread into the world map as linked, visible strategic front entities");
        Check(ecologyWorld.Ecology.IsDangerousAt(
                ecologyWorld.Ecology.Hotspots[0].X, ecologyWorld.Ecology.Hotspots[0].Y),
            "high-pressure Blight regions must be queryable as dangerous terrain");
        string ecologyJson = ContinuousWorldState.Serialize(ecologyWorld);
        var ecologyRestored = ContinuousWorldState.Deserialize(ecologyJson);
        Check(ecologyRestored.Ecology.CurrentSeason == ecologyWorld.Ecology.CurrentSeason &&
              ecologyRestored.Ecology.CurrentWeather == ecologyWorld.Ecology.CurrentWeather &&
              Math.Abs(ecologyRestored.Ecology.FoodGrowthMultiplier - ecologyWorld.Ecology.FoodGrowthMultiplier) < 1e-9,
            "save/load must restore the climate state derived from the authoritative world clock");
        Check(ecologyRestored.Ecology.Herds.Count == ecologyWorld.Ecology.Herds.Count &&
              ecologyRestored.Ecology.Hotspots.Count == ecologyWorld.Ecology.Hotspots.Count &&
              ecologyRestored.Ecology.RecentEvents.Count == ecologyWorld.Ecology.RecentEvents.Count &&
              ecologyRestored.Ecology.Hotspots.All(hotspot =>
                  hotspot.WorldEntityId.HasValue &&
                  ecologyRestored.Entities.Any(entity => entity.Id == hotspot.WorldEntityId &&
                                                          entity.Kind == WorldEntityKind.BlightFront &&
                                                          Math.Abs(entity.X - hotspot.X) < 1e-6 &&
                                                          Math.Abs(entity.Y - hotspot.Y) < 1e-6)),
            "save/load must preserve wildlife, spread fronts, linked positions and world event history");

        var legacyLinkedWorld = ContinuousWorldState.Generate(81359);
        var legacyJsonNode = System.Text.Json.Nodes.JsonNode.Parse(
            ContinuousWorldState.Serialize(legacyLinkedWorld))!.AsObject();
        foreach (var hotspotNode in legacyJsonNode["Ecology"]!["Hotspots"]!.AsArray())
            hotspotNode!.AsObject().Remove("WorldEntityId");
        var legacyRestoredWorld = ContinuousWorldState.Deserialize(legacyJsonNode.ToJsonString());
        legacyRestoredWorld.Step(.1d);
        Check(legacyRestoredWorld.Ecology.Hotspots.All(hotspot =>
                  hotspot.WorldEntityId.HasValue &&
                  legacyRestoredWorld.Entities.Any(entity => entity.Id == hotspot.WorldEntityId &&
                                                             entity.Kind == WorldEntityKind.BlightFront)) &&
              legacyRestoredWorld.Entities.Count(entity => entity.Kind == WorldEntityKind.BlightFront) ==
                  legacyRestoredWorld.Ecology.Hotspots.Count,
            "older world saves without hotspot links must deterministically reconnect fronts without duplicates");

        var deterministicA = ContinuousWorldState.Generate(9912);
        var deterministicB = ContinuousWorldState.Generate(9912);
        for (int i = 0; i < 25; i++) { deterministicA.Step(60); deterministicB.Step(60); }
        Check(deterministicA.Ecology.RecentEvents.Select(item => item.Title)
                  .SequenceEqual(deterministicB.Ecology.RecentEvents.Select(item => item.Title)),
            "world events must be reproducible from the same seed and simulation time");

        var blightResourceWorld = ContinuousWorldState.Generate(4402);
        var resourceSite = blightResourceWorld.Entities.First(entity =>
            entity.Kind == WorldEntityKind.ResourceSite && entity.Resource == WorldResourceKind.Food);
        var resourceHotspot = blightResourceWorld.Ecology.Hotspots[0];
        resourceSite.X = resourceHotspot.X;
        resourceSite.Y = resourceHotspot.Y;
        resourceSite.ResourceAmount = 100d;
        blightResourceWorld.Step(100d);
        Check(resourceSite.ResourceAmount < 100d,
            "Blight pressure must gradually consume finite food-resource sites instead of only changing HUD labels");
        resourceSite.ResourceAmount = .001d;
        blightResourceWorld.Step(500d);
        Check(resourceSite.ResourceAmount == 0d &&
              blightResourceWorld.Ecology.RecentEvents.Any(item =>
                  item.Kind == WorldEventKind.ResourceSiteCorrupted &&
                  item.Description.Contains(resourceSite.Name, StringComparison.Ordinal)),
            "when Blight consumes the last organic resource, a persisted ecology event must identify the depleted site");
        var corruptedSiteSave = ContinuousWorldState.Deserialize(ContinuousWorldState.Serialize(blightResourceWorld));
        Check(corruptedSiteSave.Entities.Single(entity => entity.Id == resourceSite.Id).ResourceAmount == 0d &&
              corruptedSiteSave.Ecology.RecentEvents.Any(item =>
                  item.Kind == WorldEventKind.ResourceSiteCorrupted &&
                  item.Description.Contains(resourceSite.Name, StringComparison.Ordinal)),
            "depleted site reserves and the named corruption event must survive world save/load");

        var macroBlightWorld = ContinuousWorldState.Generate(4403);
        var macroHotspot = macroBlightWorld.Ecology.Hotspots[0];
        var linkedMacroFront = macroBlightWorld.Entities.Single(entity =>
            entity.Id == macroHotspot.WorldEntityId && entity.Kind == WorldEntityKind.BlightFront);
        double firstMacroCellCenter = MacroWorldState.WorldCellSize * .5d;
        macroHotspot.X = firstMacroCellCenter;
        macroHotspot.Y = firstMacroCellCenter;
        macroHotspot.Radius = 2_000d;
        macroHotspot.Intensity = .92d;
        macroHotspot.NextSpreadAt = double.MaxValue;
        linkedMacroFront.X = firstMacroCellCenter;
        linkedMacroFront.Y = firstMacroCellCenter;
        float macroPressureBefore = macroBlightWorld.MacroWorld.Cells[0].BlightPresence;
        macroBlightWorld.Step(1d);
        Check(macroBlightWorld.MacroWorld.Cells[0].BlightPresence >= .90f &&
              macroBlightWorld.MacroWorld.Cells[0].BlightPresence > macroPressureBefore,
            "live strategic Blight hotspots must update pressure in the matching macro-world cells");

        var pressureWorld = ContinuousWorldState.Generate(4401);
        var exposedSettlement = pressureWorld.Entities.First(entity =>
            entity.Kind == WorldEntityKind.Settlement && entity.Faction != "Kor");
        var initialHotspot = pressureWorld.Ecology.Hotspots[0];
        exposedSettlement.X = initialHotspot.X;
        exposedSettlement.Y = initialHotspot.Y;
        double foodBeforePressure = pressureWorld.InventoryAt(exposedSettlement.Id, WorldResourceKind.Food);
        pressureWorld.Step(100);
        double exposedFoodGain = pressureWorld.InventoryAt(exposedSettlement.Id, WorldResourceKind.Food) - foodBeforePressure;
        Check(exposedFoodGain < 1,
            "severe local Blight pressure must suppress settlement food production");

        var world = ContinuousWorldState.Generate(70241);
        var sameSeedWorld = ContinuousWorldState.Generate(70241);
        var otherSeedWorld = ContinuousWorldState.Generate(70242);
        string WorldSignature(ContinuousWorldState value) => string.Join("|", value.Entities.Select(entity =>
            $"{entity.Kind}:{entity.Name}:{entity.Faction}:{entity.X:R}:{entity.Y:R}:{entity.Resource}:{entity.ResourceAmount:R}"));
        Check(WorldSignature(world) == WorldSignature(sameSeedWorld),
            "the same seed must reproduce faction placement, settlements and resource sites exactly");
        Check(WorldSignature(world) != WorldSignature(otherSeedWorld),
            "different seeds must change overworld entity and resource placement");
        Check(world.Entities.Count(entity => entity.Kind == WorldEntityKind.Settlement) >= 8 &&
              world.Entities.Count(entity => entity.Kind == WorldEntityKind.ResourceSite) >= 28 &&
              world.Entities.Any(entity => entity.Kind == WorldEntityKind.Army && entity.Faction != "Kor"),
            "the generated world must contain multiple factions, settlements, resource sites and non-Kor forces");
        Check(world.Entities.Where(entity => entity.Kind == WorldEntityKind.ResourceSite)
                .All(entity => entity.Resource.HasValue && entity.ResourceAmount > 0),
            "strategic resource sites must have a resource type and a positive finite reserve");
        var spawnA = new WorldSimulation(1234);
        var spawnB = new WorldSimulation(1234);
        var spawnC = new WorldSimulation(1235);
        string SpawnSignature(ColonySimulation colony) => string.Join("|", colony.Deposits.Select(deposit =>
            $"{deposit.Cell.X}:{deposit.Cell.Y}:{deposit.Resource}:{deposit.Amount}")) + "/" +
            string.Join("|", Enumerable.Range(0, ColonySimulation.Width).SelectMany(x =>
                Enumerable.Range(0, ColonySimulation.Height).Where(y => colony.TileAt(x, y)?.Terrain == TerrainKind.Rock)
                    .Select(y => $"{x}:{y}")));
        Check(SpawnSignature(spawnA.Colony) == SpawnSignature(spawnB.Colony),
            "the same seed must reproduce the local spawn map and resource deposits");
        Check(SpawnSignature(spawnA.Colony) != SpawnSignature(spawnC.Colony),
            "different world seeds must produce different local terrain and resource spawn maps");
        var projected = ContinuousWorldProjection.WorldToScreen(50_000, 50_000, 50_000, 50_000, .65f);
        var unprojected = ContinuousWorldProjection.ScreenToWorld(projected.X, projected.Y, 50_000, 50_000, .65f);
        Check(Math.Abs(unprojected.X - 50_000) < .01 && Math.Abs(unprojected.Y - 50_000) < .01,
            "continuous world projection must round-trip world coordinates");
        Check(ContinuousWorldProjection.Scale(.08f) < ContinuousWorldProjection.Scale(.65f),
            "strategic zoom must smoothly scale the continuous world representation");
        var home = world.Entities.Single(entity => entity.Faction == "Kor" && entity.Kind == WorldEntityKind.Settlement);
        var nearby = world.QueryRadius(home.X, home.Y, 1);
        Check(nearby.Count == 1 && nearby[0].Id == home.Id,
            "continuous spatial queries must use distance rather than hex adjacency");

        double homeStartX = home.X;
        double homeStartY = home.Y;
        Check(world.OrderMove(home.Id, homeStartX + 20, homeStartY, 10),
            "world entities must accept coordinate destinations");
        world.Step(1);
        Check(Math.Abs(home.X - (homeStartX + 10)) < 0.0001 && home.IsMoving,
            "entity movement must progress continuously according to speed and elapsed time");
        world.Step(1);
        Check(Math.Abs(home.X - (homeStartX + 20)) < 0.0001 && !home.IsMoving,
            "entity movement must stop exactly at its destination without overshoot");
        Check(world.ClockSeconds == 2,
            "continuous world state must maintain its own serializable simulation clock");

        var destination = world.Entities.First(entity => entity.Kind == WorldEntityKind.Settlement && entity.Faction != "Kor");
        var caravan = world.Entities.Single(entity => entity.Kind == WorldEntityKind.Caravan && entity.Faction == "Kor");
        double shipmentDistance = Math.Sqrt(Math.Pow(destination.X - home.X, 2) + Math.Pow(destination.Y - home.Y, 2));
        double shipmentTravelSeconds = shipmentDistance / 1_000d + 1d;
        double woodBefore = world.Inventories.Values.Sum(inventory => inventory.GetValueOrDefault(WorldResourceKind.Wood));
        double destinationWoodBefore = world.InventoryAt(destination.Id, WorldResourceKind.Wood);
        Check(world.CreateShipment(caravan.Id, home.Id, destination.Id, WorldResourceKind.Wood, 10, 1_000),
            "a stocked settlement must be able to dispatch a real cargo shipment");
        Check(Math.Abs(world.InventoryAt(home.Id, WorldResourceKind.Wood) - 10) < 0.0001 &&
              Math.Abs(world.InventoryAt(destination.Id, WorldResourceKind.Wood) - destinationWoodBefore) < 0.0001,
            "dispatch must reserve cargo at origin and must not teleport it to the destination");
        world.Step(1);
        Check(world.Shipments.Single().Status == WorldShipmentStatus.InTransit,
            "cargo must remain in transit until travel distance is covered");
        string inTransitJson = ContinuousWorldState.Serialize(world);
        var inTransitRestore = ContinuousWorldState.Deserialize(inTransitJson);
        Check(inTransitRestore.Shipments.Single().Status == WorldShipmentStatus.InTransit &&
              Math.Abs(inTransitRestore.InventoryAt(home.Id, WorldResourceKind.Wood) - 10) < 0.0001,
            "save/load must preserve in-flight cargo and the origin's already-debited inventory");
        world.Step(shipmentTravelSeconds);
        Check(world.Shipments.Single().Status == WorldShipmentStatus.Delivered &&
              Math.Abs(world.InventoryAt(destination.Id, WorldResourceKind.Wood) - (destinationWoodBefore + 10)) < 0.0001,
            "cargo must be delivered only after its caravan reaches the destination");
        double woodAfter = world.Inventories.Values.Sum(inventory => inventory.GetValueOrDefault(WorldResourceKind.Wood));
        Check(Math.Abs(woodBefore - woodAfter) < 0.0001,
            "world shipments must conserve resources across origin, cargo and destination");

        // A full destination must hold the shipment in transit until storage capacity is available.
        world.SetInventoryCapacity(destination.Id, world.InventoryTotalAt(destination.Id));
        Check(world.CreateShipment(caravan.Id, home.Id, destination.Id, WorldResourceKind.Wood, 5, 1_000),
            "a second shipment must be funded from the same canonical origin ledger");
        world.Step(10);
        Check(world.Shipments.Last().Status == WorldShipmentStatus.InTransit &&
              Math.Abs(world.InventoryAt(destination.Id, WorldResourceKind.Wood) - (destinationWoodBefore + 10)) < 0.0001,
            "cargo must wait at a full destination instead of overfilling or disappearing");
        world.SetInventoryCapacity(destination.Id, world.InventoryTotalAt(destination.Id) + 100);
        world.Step(shipmentTravelSeconds);
        Check(world.Shipments.Last().Status == WorldShipmentStatus.Delivered &&
              Math.Abs(world.InventoryAt(destination.Id, WorldResourceKind.Wood) - (destinationWoodBefore + 15)) < 0.0001,
            "waiting cargo must be credited once storage capacity becomes available");

        string json = ContinuousWorldState.Serialize(world);
        var restored = ContinuousWorldState.Deserialize(json);
        var restoredHome = restored.Entities.Single(entity => entity.Id == home.Id);
        Check(restored.ClockSeconds == world.ClockSeconds &&
              Math.Abs(restoredHome.X - home.X) < 0.0001 &&
              restored.Entities.Count == world.Entities.Count,
            "continuous world save/load must preserve clock, entities and positions");

        CheckThrows(() => world.OrderMove(home.Id, 50_030, 50_000, 0),
            "movement speed must be positive");
        CheckThrows(() => world.QueryRadius(50_000, 50_000, double.NaN),
            "invalid spatial query radii must be rejected");
    }

    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException($"Continuous world check failed: {reason}");
    }

    private static void CheckThrows(Action action, string reason)
    {
        try { action(); }
        catch (ArgumentOutOfRangeException) { return; }
        throw new InvalidOperationException($"Continuous world check failed: {reason}");
    }
}
