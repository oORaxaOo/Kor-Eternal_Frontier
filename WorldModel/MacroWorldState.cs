using System.Runtime.InteropServices;
using KorTheBlight.Simulation;
using KorTheBlight.Simulation.Research;

namespace KorTheBlight.WorldModel;

internal enum ProductionFocus : byte { Agriculture, Mining, Industry, Military }
internal enum MacroResource : byte { Food, Wood, Stone, Alloy, Energy, Knowledge, Components }

/// <summary>
/// Compact, fixed-layout macro cell. No references or dictionaries live inside the hot-loop array.
/// Values are persisted as part of the strategic world snapshot.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct MacroCellData
{
    public int CellId { get; set; }
    public short X { get; set; }
    public short Y { get; set; }
    public int OwningFactionId { get; set; }
    public int Population { get; set; }
    public float FoodCount { get; set; }
    public float WoodCount { get; set; }
    public float StoneCount { get; set; }
    public float AlloyCount { get; set; }
    public float EnergyCount { get; set; }
    public float KnowledgeCount { get; set; }
    public float ComponentsCount { get; set; }
    public float Fertility { get; set; }
    public float BlightPresence { get; set; }
    public ProductionFocus ProductionFocus { get; set; }
    public WorldBiomeKind Biome { get; set; }
    public bool IsLocalMapGenerated { get; set; }
}

internal sealed class MacroCaravanState
{
    public long Id { get; set; }
    public int FactionId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public int TargetCellId { get; set; }
    public float TravelSpeed { get; set; } = 0.05f;
    public Dictionary<MacroResource, float> Cargo { get; set; } = [];
    public List<WorldRouteNode> RouteNodes { get; set; } = [];
    public int RouteIndex { get; set; }
    public bool RoutePending { get; set; }
    public bool RouteReady { get; set; }
}

internal sealed record WorldNotification(long Id, long Tick, string Category, string Message, int CellId, bool Important);

internal sealed class MacroCaravanArrival
{
    public long Id { get; set; }
    public int FactionId { get; set; }
    public int TargetCellId { get; set; }
    public bool IsRefugeeWave { get; set; }
    public int Population { get; set; }
    public int SpawnedVisitorCount { get; set; }
    public long NotificationId { get; set; }
    public Dictionary<MacroResource, float> Cargo { get; set; } = [];
}

internal sealed class MarketSnapshot
{
    public float FoodPrice { get; set; } = 1f;
    public float WoodPrice { get; set; } = 1f;
    public float StonePrice { get; set; } = 1f;
    public float AlloyPrice { get; set; } = 1f;
    public float EnergyPrice { get; set; } = 1f;
    public float ComponentsPrice { get; set; } = 1f;
    public long LastUpdatedTick { get; set; }
}

internal enum MacroTurnPhase : byte { Idle, Economy, ReliefScan }

/// <summary>Serializable accumulators collected as each macro cell is processed.</summary>
internal sealed class MacroMarketAccumulator
{
    public double FoodDemand { get; set; }
    public double FoodSupply { get; set; }
    public double WoodDemand { get; set; }
    public double WoodSupply { get; set; }
    public double StoneDemand { get; set; }
    public double StoneSupply { get; set; }
    public double AlloyDemand { get; set; }
    public double AlloySupply { get; set; }
    public double EnergyDemand { get; set; }
    public double EnergySupply { get; set; }
    public double ComponentsDemand { get; set; }
    public double ComponentsSupply { get; set; }

    public void Reset()
    {
        FoodDemand = FoodSupply = WoodDemand = WoodSupply = 0;
        StoneDemand = StoneSupply = AlloyDemand = AlloySupply = 0;
        EnergyDemand = EnergySupply = ComponentsDemand = ComponentsSupply = 0;
    }
}

/// <summary>Supply/demand pricing based on the macro ledger. Prices are bounded to prevent runaway values.</summary>
internal static class MarketSystem
{
    public static void Accumulate(MacroMarketAccumulator totals, in MacroCellData cell)
    {
        ArgumentNullException.ThrowIfNull(totals);
        if (cell.OwningFactionId == 0 || cell.Population <= 0) return;
        double population = cell.Population;
        totals.FoodDemand += population * 0.01d;
        totals.FoodSupply += Math.Max(0, cell.FoodCount);
        totals.WoodDemand += population * (cell.ProductionFocus == ProductionFocus.Industry ? 0.0018d : 0.0006d);
        totals.WoodSupply += Math.Max(0, cell.WoodCount);
        totals.StoneDemand += population * (cell.ProductionFocus == ProductionFocus.Mining ? 0.001d : 0.00025d);
        totals.StoneSupply += Math.Max(0, cell.StoneCount);
        totals.AlloyDemand += population * (cell.ProductionFocus == ProductionFocus.Military ? 0.001d : 0.00015d);
        totals.AlloySupply += Math.Max(0, cell.AlloyCount);
        totals.EnergyDemand += population * (cell.ProductionFocus == ProductionFocus.Industry ? 0.0012d : 0.0002d);
        totals.EnergySupply += Math.Max(0, cell.EnergyCount);
        totals.ComponentsDemand += population * (cell.ProductionFocus == ProductionFocus.Industry ? 0.0008d : 0.0001d);
        totals.ComponentsSupply += Math.Max(0, cell.ComponentsCount);
    }

    /// <summary>Initial-world refresh; normal macro turns use their timesliced accumulator.</summary>
    public static void UpdatePrices(MacroWorldState world, long tick)
    {
        ArgumentNullException.ThrowIfNull(world);
        var totals = new MacroMarketAccumulator();
        foreach (ref readonly var cell in world.Cells.AsSpan()) Accumulate(totals, cell);
        ApplyTotals(world.Market, totals, tick);
    }

    public static void ApplyTotals(MarketSnapshot market, MacroMarketAccumulator totals, long tick)
    {
        ArgumentNullException.ThrowIfNull(market);
        ArgumentNullException.ThrowIfNull(totals);
        market.FoodPrice = Price(totals.FoodDemand, totals.FoodSupply);
        market.WoodPrice = Price(totals.WoodDemand, totals.WoodSupply);
        market.StonePrice = Price(totals.StoneDemand, totals.StoneSupply);
        market.AlloyPrice = Price(totals.AlloyDemand, totals.AlloySupply);
        market.EnergyPrice = Price(totals.EnergyDemand, totals.EnergySupply);
        market.ComponentsPrice = Price(totals.ComponentsDemand, totals.ComponentsSupply);
        market.LastUpdatedTick = tick;
    }

    private static float Price(double demand, double supply)
    {
        double ratio = demand / Math.Max(1d, supply);
        return (float)Math.Clamp(Math.Sqrt(Math.Max(0.01d, ratio)), 0.25d, 8d);
    }
}

/// <summary>
/// 100x100 strategic layer. Macro turns are deterministic and run every 24,000 shared world ticks;
/// detailed local pawns/pathfinding remain exclusive to the active 40x26 colony.
/// </summary>
internal sealed class MacroWorldState
{
    public const int Columns = 100;
    public const int Rows = 100;
    public const int CellCount = Columns * Rows;
    public const int TicksPerMacroTurn = 24_000;
    public const float WorldCellSize = (float)(ContinuousWorldState.WorldExtent / Columns);
    public MacroCellData[] Cells { get; set; } = new MacroCellData[CellCount];
    public Dictionary<string, int> FactionIds { get; set; } = new(StringComparer.Ordinal);
    public List<MacroCaravanState> Caravans { get; set; } = [];
    public List<MacroCaravanArrival> Arrivals { get; set; } = [];
    public HashSet<long> ConsumedArrivalIds { get; set; } = [];
    public List<WorldNotification> Notifications { get; set; } = [];
    public MarketSnapshot Market { get; set; } = new();
    public ResearchProgressState Research { get; set; } = new();
    public long SimulationTicks { get; set; }
    public double FractionalTickRemainder { get; set; }
    public long NextNotificationId { get; set; } = 1;
    public long NextCaravanId { get; set; } = 1;
    public int MacroTurnsProcessed { get; set; }
    public int MacroTurnsScheduled { get; set; }
    public int MacroTurnCursor { get; set; }
    public MacroTurnPhase MacroTurnPhase { get; set; }
    public MacroMarketAccumulator MarketTurnAccumulator { get; set; } = new();
    public Dictionary<int, int> TurnHungryCellIds { get; set; } = [];
    public Dictionary<int, int> TurnBestDonorCellIds { get; set; } = [];
    public Dictionary<int, double> TurnBestDonorDistances { get; set; } = [];
    public int TickSliceCursor { get; set; }
    private int _cachedLocalMapCellId;
    private readonly BackgroundWorldRouteScheduler _routeScheduler = new();
    private const int MacroCellsPerSlice = 128;
    private const int IncidentCellsPerSlice = 128;
    public int Seed { get; set; }

    public static MacroWorldState Generate(int seed, RegionalWorldMap regions, IReadOnlyList<WorldEntityState> entities,
        IReadOnlyDictionary<long, Dictionary<WorldResourceKind, double>> inventories, WorldEcologyState ecology)
    {
        var world = new MacroWorldState { Seed = seed };
        var factionNames = entities.Where(e => e.Kind == WorldEntityKind.Settlement)
            .Select(e => e.Faction).Where(f => !string.Equals(f, "Blight", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        for (int i = 0; i < factionNames.Count; i++) world.FactionIds[factionNames[i]] = i + 1;

        var settlements = entities.Where(e => e.Kind == WorldEntityKind.Settlement).ToArray();
        var blightFronts = entities.Where(e => e.Kind == WorldEntityKind.BlightFront).ToArray();
        var random = new Random(unchecked(seed ^ 0x41C64E6D));
        for (int y = 0; y < Rows; y++)
        for (int x = 0; x < Columns; x++)
        {
            int index = y * Columns + x;
            double wx = (x + 0.5d) * WorldCellSize, wy = (y + 0.5d) * WorldCellSize;
            var region = regions.Regions.First(r => r.X == Math.Min(RegionalWorldMap.Columns - 1, x / 5) &&
                                                    r.Y == Math.Min(RegionalWorldMap.Rows - 1, y / 5));
            var nearest = settlements.Select(s => (Entity: s, Distance: Distance(wx, wy, s.X, s.Y)))
                .OrderBy(pair => pair.Distance).ThenBy(pair => pair.Entity.Id).FirstOrDefault();
            int owner = nearest.Entity != null && nearest.Distance <= 8_000
                ? world.FactionIds.GetValueOrDefault(nearest.Entity.Faction) : 0;
            int population = owner == 0 ? 0 : nearest.Distance < 1_000
                ? 2_000 + random.Next(8_001) : 250 + random.Next(2_751);
            float blight = 0;
            foreach (var front in blightFronts)
            {
                double distance = Distance(wx, wy, front.X, front.Y);
                blight = Math.Max(blight, (float)Math.Clamp(1d - distance / 9_000d, 0d, 1d));
            }
            blight = Math.Max(blight, (float)Math.Clamp(ecology.EnvironmentalPressureAt(wx, wy), 0d, 1d));
            float fertility = region.Biome switch
            {
                WorldBiomeKind.Plains => 1.15f,
                WorldBiomeKind.Forest => 0.95f,
                WorldBiomeKind.Wetland => 1.2f,
                WorldBiomeKind.Desert => 0.3f,
                WorldBiomeKind.Tundra => 0.45f,
                WorldBiomeKind.Mountain => 0.55f,
                _ => 0.8f
            } * (0.8f + region.Moisture * 0.4f);

            var focus = owner == 0 ? ProductionFocus.Agriculture
                : (ProductionFocus)(StableHash(seed, x, y) % 4);
            world.Cells[index] = new MacroCellData
            {
                CellId = index + 1, X = (short)x, Y = (short)y, OwningFactionId = owner,
                Population = population, FoodCount = owner == 0 ? 0 : 100 + random.Next(401),
                WoodCount = owner == 0 ? 0 : 60 + random.Next(301),
                StoneCount = owner == 0 ? 0 : 40 + random.Next(241),
                AlloyCount = owner == 0 ? 0 : 10 + random.Next(91),
                EnergyCount = owner == 0 ? 0 : random.Next(81),
                KnowledgeCount = owner == 0 ? 0 : random.Next(51),
                ComponentsCount = owner == 0 ? 0 : random.Next(31),
                Fertility = fertility, BlightPresence = blight, ProductionFocus = focus,
                Biome = region.Biome,
                IsLocalMapGenerated = nearest.Entity != null && nearest.Entity.Faction == "Kor" && nearest.Distance < 1_000
            };
        }

        // Project existing strategic ledgers into the cells that actually contain settlements.
        foreach (var settlement in settlements)
        {
            int x = Math.Clamp((int)(settlement.X / WorldCellSize), 0, Columns - 1);
            int y = Math.Clamp((int)(settlement.Y / WorldCellSize), 0, Rows - 1);
            int index = y * Columns + x;
            ref var cell = ref world.Cells[index];
            cell.OwningFactionId = world.FactionIds.GetValueOrDefault(settlement.Faction);
            cell.Population = Math.Max(cell.Population, 2_000 + (int)(StableHash(seed, x, y) % 8_001));
            if (inventories.TryGetValue(settlement.Id, out var inventory))
            {
                cell.FoodCount = (float)inventory.GetValueOrDefault(WorldResourceKind.Food);
                cell.WoodCount = (float)inventory.GetValueOrDefault(WorldResourceKind.Wood);
                cell.StoneCount = (float)inventory.GetValueOrDefault(WorldResourceKind.Stone);
                cell.AlloyCount = (float)inventory.GetValueOrDefault(WorldResourceKind.Alloy);
                cell.EnergyCount = (float)inventory.GetValueOrDefault(WorldResourceKind.Energy);
                cell.KnowledgeCount = (float)inventory.GetValueOrDefault(WorldResourceKind.Knowledge);
                cell.ComponentsCount = (float)inventory.GetValueOrDefault(WorldResourceKind.Components);
            }
            if (settlement.Faction == "Kor") cell.IsLocalMapGenerated = true;
        }
        MarketSystem.UpdatePrices(world, 0);
        return world;
    }

    public MacroCellData GetCell(int x, int y)
    {
        if ((uint)x >= Columns || (uint)y >= Rows) throw new ArgumentOutOfRangeException(nameof(x));
        return Cells[y * Columns + x];
    }

    public bool TrySetLocalMapGenerated(int cellId, bool generated)
    {
        if (cellId < 1 || cellId > Cells.Length) return false;
        Cells[cellId - 1].IsLocalMapGenerated = generated;
        if (!generated && _cachedLocalMapCellId == cellId) _cachedLocalMapCellId = 0;
        else if (generated && _cachedLocalMapCellId == 0) _cachedLocalMapCellId = cellId;
        return true;
    }

    private int FindLocalMapCellId()
    {
        if (_cachedLocalMapCellId > 0 && _cachedLocalMapCellId <= Cells.Length &&
            Cells[_cachedLocalMapCellId - 1].IsLocalMapGenerated)
            return _cachedLocalMapCellId;
        _cachedLocalMapCellId = 0;
        for (int i = 0; i < Cells.Length; i++)
        {
            if (!Cells[i].IsLocalMapGenerated) continue;
            _cachedLocalMapCellId = Cells[i].CellId;
            break;
        }
        return _cachedLocalMapCellId;
    }

    public void Advance(double deltaSeconds, ContinuousWorldState continuous)
    {
        if (!double.IsFinite(deltaSeconds) || deltaSeconds < 0) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        ArgumentNullException.ThrowIfNull(continuous);
        double totalTickDelta = deltaSeconds * TickManager.TicksPerSecond + FractionalTickRemainder;
        long tickDelta = (long)Math.Floor(totalTickDelta + 1e-8);
        FractionalTickRemainder = Math.Max(0d, totalTickDelta - tickDelta);
        SimulationTicks = checked(SimulationTicks + tickDelta);

        long dueTurns = SimulationTicks / TicksPerMacroTurn;
        MacroTurnsScheduled = (int)Math.Max(MacroTurnsScheduled, Math.Min(int.MaxValue, dueTurns));

        ApplyCompletedRoutes();
        UpdateCaravans(deltaSeconds);
        EvaluateIncidentSlice(continuous, IncidentCellsPerSlice);
        ProcessMacroTurnSlice(continuous, MacroCellsPerSlice);
        StartPendingCaravanRoutes();
    }

    /// <summary>
    /// Processes at most a fixed number of regions per call. All mutations stay on the simulation
    /// thread; no daily turn is allowed to run an unbounded 10,000-cell loop in one frame.
    /// </summary>
    private void ProcessMacroTurnSlice(ContinuousWorldState continuous, int cellBudget)
    {
        ArgumentNullException.ThrowIfNull(continuous);
        int remaining = Math.Max(1, cellBudget);
        while (remaining > 0)
        {
            if (MacroTurnPhase == MacroTurnPhase.Idle)
            {
                if (MacroTurnsProcessed >= MacroTurnsScheduled) break;
                BeginMacroTurn();
            }

            if (MacroTurnPhase == MacroTurnPhase.Economy)
            {
                int index = MacroTurnCursor;
                ProcessMacroCell(index, continuous);
                MarketSystem.Accumulate(MarketTurnAccumulator, Cells[index]);
                TrackHungryCell(index);
                MacroTurnCursor++;
                remaining--;
                if (MacroTurnCursor >= Cells.Length)
                {
                    MacroTurnCursor = 0;
                    MacroTurnPhase = MacroTurnPhase.ReliefScan;
                }
                continue;
            }

            if (MacroTurnPhase == MacroTurnPhase.ReliefScan)
            {
                ConsiderReliefDonor(MacroTurnCursor);
                MacroTurnCursor++;
                remaining--;
                if (MacroTurnCursor >= Cells.Length)
                {
                    MacroTurnCursor = 0;
                    CompleteMacroTurn();
                }
                continue;
            }

            throw new InvalidDataException($"Unknown macro turn phase: {MacroTurnPhase}.");
        }
    }

    private void BeginMacroTurn()
    {
        MarketTurnAccumulator ??= new MacroMarketAccumulator();
        MarketTurnAccumulator.Reset();
        TurnHungryCellIds.Clear();
        TurnBestDonorCellIds.Clear();
        TurnBestDonorDistances.Clear();
        MacroTurnCursor = 0;
        MacroTurnPhase = MacroTurnPhase.Economy;
    }

    private void CompleteMacroTurn()
    {
        DispatchReliefCaravans();
        UpdatePricesAndEmitMarketNews((long)(MacroTurnsProcessed + 1) * TicksPerMacroTurn, MarketTurnAccumulator);
        MarketTurnAccumulator.Reset();
        TurnHungryCellIds.Clear();
        TurnBestDonorCellIds.Clear();
        TurnBestDonorDistances.Clear();
        MacroTurnsProcessed++;
        MacroTurnPhase = MacroTurnPhase.Idle;
    }

    private void ProcessMacroCell(int index, ContinuousWorldState continuous)
    {
        ref var cell = ref Cells[index];
        if (cell.OwningFactionId == 0) return;
        float population = Math.Max(0, cell.Population);
        float pressure = Math.Clamp(cell.BlightPresence, 0f, 1f);
        float consumed = population * 0.01f;
        cell.FoodCount = Math.Max(0f, cell.FoodCount - consumed);
        switch (cell.ProductionFocus)
        {
            case ProductionFocus.Agriculture:
                cell.FoodCount += population * 0.015f * cell.Fertility * (1f - pressure * 0.9f);
                break;
            case ProductionFocus.Mining:
                cell.StoneCount += population * 0.0025f;
                cell.AlloyCount += population * 0.0003f;
                break;
            case ProductionFocus.Industry:
                cell.ComponentsCount += population * 0.0005f;
                cell.AlloyCount += population * 0.0004f;
                cell.EnergyCount = Math.Max(0, cell.EnergyCount - population * 0.0006f);
                break;
            case ProductionFocus.Military:
                cell.AlloyCount = Math.Max(0, cell.AlloyCount - population * 0.0005f);
                cell.ComponentsCount = Math.Max(0, cell.ComponentsCount - population * 0.00025f);
                break;
        }
        cell.WoodCount += population * (cell.Biome == WorldBiomeKind.Forest ? 0.0015f : 0.0003f);
        cell.BlightPresence = Math.Clamp(pressure + (pressure > 0.15f ? 0.01f : -0.002f), 0f, 1f);
        if (cell.FoodCount < population * 0.002f && cell.Population > 0)
        {
            int losses = Math.Max(1, (int)Math.Ceiling(cell.Population * 0.015f));
            cell.Population = Math.Max(0, cell.Population - losses);
            AddNotification(SimulationTicks, "famine", $"Famine in macro cell {cell.CellId}: population declined by {losses}.", cell.CellId, true);
            if (cell.Population > 0 && cell.FoodCount < population * 0.0005f)
                TryTriggerRefugeeWave(index, continuous);
        }
        else if (cell.FoodCount > population * 0.03f && cell.Population > 0)
            cell.Population = Math.Min(100_000, cell.Population + Math.Max(1, (int)(cell.Population * 0.002f)));
    }

    private void TrackHungryCell(int index)
    {
        ref readonly var cell = ref Cells[index];
        if (cell.OwningFactionId == 0 || cell.Population <= 0 || cell.FoodCount >= cell.Population * 0.004f) return;
        if (!TurnHungryCellIds.TryGetValue(cell.OwningFactionId, out int knownCellId))
        {
            TurnHungryCellIds[cell.OwningFactionId] = cell.CellId;
            return;
        }

        ref readonly var known = ref Cells[knownCellId - 1];
        if (cell.FoodCount < known.FoodCount || (cell.FoodCount == known.FoodCount && cell.CellId < known.CellId))
            TurnHungryCellIds[cell.OwningFactionId] = cell.CellId;
    }

    private void ConsiderReliefDonor(int index)
    {
        ref readonly var cell = ref Cells[index];
        if (cell.OwningFactionId == 0 || cell.Population <= 0 ||
            cell.FoodCount <= cell.Population * 0.02f ||
            !TurnHungryCellIds.TryGetValue(cell.OwningFactionId, out int hungryCellId) ||
            cell.CellId == hungryCellId)
            return;

        ref readonly var hungry = ref Cells[hungryCellId - 1];
        double dx = cell.X - hungry.X, dy = cell.Y - hungry.Y;
        double distanceSquared = dx * dx + dy * dy;
        if (!TurnBestDonorDistances.TryGetValue(cell.OwningFactionId, out double knownDistance) ||
            distanceSquared < knownDistance ||
            (distanceSquared == knownDistance &&
             cell.CellId < TurnBestDonorCellIds.GetValueOrDefault(cell.OwningFactionId, int.MaxValue)))
        {
            TurnBestDonorCellIds[cell.OwningFactionId] = cell.CellId;
            TurnBestDonorDistances[cell.OwningFactionId] = distanceSquared;
        }
    }

    private float[] BuildRouteCostSnapshot()
    {
        var costs = new float[Cells.Length];
        for (int i = 0; i < Cells.Length; i++)
        {
            ref readonly var cell = ref Cells[i];
            float terrain = cell.Biome switch
            {
                WorldBiomeKind.Mountain => 2.4f,
                WorldBiomeKind.Wetland => 1.9f,
                WorldBiomeKind.Desert => 1.45f,
                WorldBiomeKind.Tundra => 1.4f,
                WorldBiomeKind.Forest => 1.25f,
                _ => 1f
            };
            costs[i] = terrain * (1f + Math.Clamp(cell.BlightPresence, 0f, 1f) * 5f);
        }
        return costs;
    }

    private void ApplyCompletedRoutes()
    {
        foreach (var result in _routeScheduler.DrainCompleted())
        {
            var caravan = Caravans.FirstOrDefault(item => item.Id == result.CaravanId);
            if (caravan is null || caravan.TargetCellId != result.TargetCellId) continue;
            caravan.RoutePending = false;
            if (result.Nodes is { Count: > 0 })
            {
                caravan.RouteNodes = result.Nodes.ToList();
                caravan.RouteIndex = caravan.RouteNodes.Count > 1 ? 1 : caravan.RouteNodes.Count;
                caravan.RouteReady = true;
                AddNotification(SimulationTicks, "route", $"Caravan {caravan.Id} completed its strategic route.", caravan.TargetCellId, false);
            }
            else
            {
                int startX = Math.Clamp((int)MathF.Round(caravan.X), 0, Columns - 1);
                int startY = Math.Clamp((int)MathF.Round(caravan.Y), 0, Rows - 1);
                var target = Cells[caravan.TargetCellId - 1];
                caravan.RouteNodes =
                [
                    new WorldRouteNode(startX, startY),
                    new WorldRouteNode(target.X, target.Y)
                ];
                caravan.RouteIndex = 1;
                caravan.RouteReady = true;
                AddNotification(SimulationTicks, "route", $"Caravan {caravan.Id} route planning failed ({result.Error}); direct fallback route selected.", caravan.TargetCellId, true);
            }
        }

        // A save can capture a pending task but not the process-local Task itself. Requeue it after load.
        foreach (var caravan in Caravans)
            if (caravan.RoutePending && !_routeScheduler.IsInFlight(caravan.Id) && !caravan.RouteReady)
                caravan.RoutePending = false;
    }

    private void StartPendingCaravanRoutes()
    {
        int available = _routeScheduler.MaxConcurrentRoutes - _routeScheduler.InFlightCount;
        if (available <= 0) return;
        var pending = Caravans.Where(item => !item.RouteReady && !item.RoutePending)
            .OrderBy(item => item.Id).Take(available).ToArray();
        if (pending.Length == 0) return;
        float[] costSnapshot = BuildRouteCostSnapshot();
        foreach (var caravan in pending)
        {
            int startX = Math.Clamp((int)MathF.Round(caravan.X), 0, Columns - 1);
            int startY = Math.Clamp((int)MathF.Round(caravan.Y), 0, Rows - 1);
            int startCellId = startY * Columns + startX + 1;
            if (_routeScheduler.TrySchedule(caravan.Id, caravan.TargetCellId, Columns, startCellId, costSnapshot))
                caravan.RoutePending = true;
        }
    }

    private void DispatchReliefCaravans()
    {
        foreach (var faction in FactionIds.OrderBy(pair => pair.Value))
        {
            int factionId = faction.Value;
            if (!TurnHungryCellIds.TryGetValue(factionId, out int hungryCellId) ||
                !TurnBestDonorCellIds.TryGetValue(factionId, out int donorCellId))
                continue;

            ref var hungry = ref Cells[hungryCellId - 1];
            ref var source = ref Cells[donorCellId - 1];
            if (source.OwningFactionId != factionId || hungry.OwningFactionId != factionId ||
                source.CellId == hungry.CellId || source.Population <= 0 || hungry.Population <= 0 ||
                source.FoodCount <= source.Population * 0.02f || hungry.FoodCount >= hungry.Population * 0.004f)
                continue;

            float amount = Math.Min(100f, Math.Max(0f, source.FoodCount - source.Population * 0.01f));
            if (amount <= 0) continue;
            source.FoodCount -= amount;
            Caravans.Add(new MacroCaravanState
            {
                Id = NextCaravanId++, FactionId = factionId, X = source.X, Y = source.Y,
                TargetCellId = hungryCellId, TravelSpeed = 0.05f,
                Cargo = new Dictionary<MacroResource, float> { [MacroResource.Food] = amount }
            });
            AddNotification(SimulationTicks, "trade",
                $"{faction.Key} dispatched a relief caravan carrying {amount:0} food to cell {hungryCellId}.",
                hungryCellId, false);
        }
    }

    private void UpdateCaravans(double deltaSeconds)
    {
        double remainingTravel = 0;
        for (int i = Caravans.Count - 1; i >= 0; i--)
        {
            var caravan = Caravans[i];
            if (caravan.TargetCellId < 1 || caravan.TargetCellId > Cells.Length)
            {
                Caravans.RemoveAt(i);
                continue;
            }
            if (!caravan.RouteReady || caravan.RouteNodes.Count == 0) continue;

            remainingTravel = Math.Max(0d, caravan.TravelSpeed * deltaSeconds * TickManager.TicksPerSecond);
            bool arrived = false;
            int safetySteps = 0;
            while (remainingTravel > 0.0001d && safetySteps++ < Columns * Rows)
            {
                if (caravan.RouteIndex >= caravan.RouteNodes.Count)
                {
                    arrived = true;
                    break;
                }

                var node = caravan.RouteNodes[caravan.RouteIndex];
                float dx = node.X - caravan.X, dy = node.Y - caravan.Y;
                float distance = MathF.Sqrt(dx * dx + dy * dy);
                if (distance <= 0.0001f)
                {
                    caravan.X = node.X;
                    caravan.Y = node.Y;
                    caravan.RouteIndex++;
                    continue;
                }

                if (distance <= remainingTravel)
                {
                    caravan.X = node.X;
                    caravan.Y = node.Y;
                    remainingTravel -= distance;
                    caravan.RouteIndex++;
                }
                else
                {
                    caravan.X += dx / distance * (float)remainingTravel;
                    caravan.Y += dy / distance * (float)remainingTravel;
                    remainingTravel = 0;
                }
            }
            if (!arrived && caravan.RouteIndex >= caravan.RouteNodes.Count) arrived = true;
            if (!arrived) continue;

            ref var destination = ref Cells[caravan.TargetCellId - 1];
            long notificationId;
            if (destination.IsLocalMapGenerated)
            {
                notificationId = AddNotification(SimulationTicks, "trade",
                    $"A relief caravan arrived at the Kor colony cell with {caravan.Cargo.Values.Sum():0} units of cargo.",
                    destination.CellId, true);
                Arrivals.Add(new MacroCaravanArrival
                {
                    Id = caravan.Id, FactionId = caravan.FactionId, TargetCellId = destination.CellId,
                    NotificationId = notificationId, Cargo = new Dictionary<MacroResource, float>(caravan.Cargo)
                });
            }
            else
            {
                foreach (var cargo in caravan.Cargo)
                {
                    switch (cargo.Key)
                    {
                        case MacroResource.Food: destination.FoodCount += cargo.Value; break;
                        case MacroResource.Wood: destination.WoodCount += cargo.Value; break;
                        case MacroResource.Stone: destination.StoneCount += cargo.Value; break;
                        case MacroResource.Alloy: destination.AlloyCount += cargo.Value; break;
                        case MacroResource.Energy: destination.EnergyCount += cargo.Value; break;
                        case MacroResource.Knowledge: destination.KnowledgeCount += cargo.Value; break;
                        case MacroResource.Components: destination.ComponentsCount += cargo.Value; break;
                    }
                }
                AddNotification(SimulationTicks, "trade",
                    $"A relief caravan arrived at cell {destination.CellId}; cargo was added to its ledger.",
                    destination.CellId, false);
            }
            Caravans.RemoveAt(i);
        }
    }

    private void TryTriggerRefugeeWave(int sourceIndex, ContinuousWorldState continuous)
    {
        ref var source = ref Cells[sourceIndex];
        int sourceFactionId = source.OwningFactionId;
        int destinationCellId = FindLocalMapCellId();
        if (destinationCellId <= 0) return;
        ref var destination = ref Cells[destinationCellId - 1];
        if (destination.OwningFactionId == sourceFactionId) return;
        int refugeeCount = Math.Min(12, Math.Max(2, source.Population / 1000));
        int sourceCellId = source.CellId;
        source.Population = Math.Max(0, source.Population - refugeeCount);
        long arrivalId = NextCaravanId++;
        long notificationId = AddNotification(SimulationTicks, "refugees", $"{refugeeCount} refugees fled macro cell {sourceCellId} toward the Kor colony. Decide whether to admit or dismiss them.", destination.CellId, true);
        Arrivals.Add(new MacroCaravanArrival
        {
            Id = arrivalId, FactionId = sourceFactionId, TargetCellId = destination.CellId,
            IsRefugeeWave = true, Population = refugeeCount, NotificationId = notificationId
        });
    }

    private void EvaluateIncidentSlice(ContinuousWorldState continuous, int cellsPerTick)
    {
        if (Cells.Length == 0) return;
        int start = TickSliceCursor;
        int end = Math.Min(Cells.Length, start + Math.Max(1, cellsPerTick));
        for (int i = start; i < end; i++)
        {
            ref var cell = ref Cells[i];
            float pressure = cell.BlightPresence;
            if (pressure > 0.85f && cell.OwningFactionId != 0)
            {
                cell.BlightPresence = Math.Min(1f, pressure + 0.0005f);
                if (cell.BlightPresence >= 0.95f && cell.Population > 0)
                {
                    int lost = Math.Max(1, cell.Population / 100);
                    cell.Population -= lost;
                    AddNotification(SimulationTicks, "blight", $"Blight consumed cell {cell.CellId}; {lost} residents were lost or displaced.", cell.CellId, true);
                }
            }
        }
        TickSliceCursor = end >= Cells.Length ? 0 : end;
    }

    private void UpdatePricesAndEmitMarketNews(long priceSampleTick, MacroMarketAccumulator turnTotals)
    {
        float previousFood = Market.FoodPrice, previousAlloy = Market.AlloyPrice;
        MarketSystem.ApplyTotals(Market, turnTotals, priceSampleTick);
        if (previousFood > 0 && Market.FoodPrice >= previousFood * 1.4f)
            AddNotification(SimulationTicks, "market", $"Global food price surged to {Market.FoodPrice:0.00}× as supplies tightened.", 0, true);
        if (previousAlloy > 0 && Market.AlloyPrice >= previousAlloy * 1.4f)
            AddNotification(SimulationTicks, "market", $"Alloy price surged to {Market.AlloyPrice:0.00}× amid industrial and military demand.", 0, true);
    }

    private long AddNotification(long tick, string category, string message, int cellId, bool important)
    {
        if (Notifications.Count >= 500) Notifications.RemoveAt(0);
        long id = NextNotificationId++;
        Notifications.Add(new WorldNotification(id, tick, category, message, cellId, important));
        return id;
    }

    public void Validate()
    {
        // Migration for saves created before macro turns and route requests became slice-based.
        if (MacroTurnsScheduled < MacroTurnsProcessed) MacroTurnsScheduled = MacroTurnsProcessed;
        if (Cells is null || Cells.Length != CellCount || FactionIds is null || Notifications is null || Caravans is null || Arrivals is null || ConsumedArrivalIds is null || Market is null || Research is null ||
            SimulationTicks < 0 || !double.IsFinite(FractionalTickRemainder) || FractionalTickRemainder is < 0 or >= 1 || MacroTurnsProcessed < 0 || MacroTurnsProcessed > MacroTurnsScheduled || MacroTurnsScheduled > SimulationTicks / TicksPerMacroTurn || MacroTurnCursor < 0 || MacroTurnCursor >= CellCount || (MacroTurnCursor > 0 && MacroTurnsProcessed >= MacroTurnsScheduled) || TickSliceCursor < 0 || TickSliceCursor >= CellCount ||
            NextNotificationId <= Notifications.Select(n => n.Id).DefaultIfEmpty(0).Max() ||
            NextCaravanId <= Math.Max(Caravans.Select(c => c.Id).DefaultIfEmpty(0).Max(), Arrivals.Select(a => a.Id).DefaultIfEmpty(0).Max()))
            throw new InvalidDataException("Macro world data has invalid dimensions or counters.");
        foreach (var cell in Cells)
        {
            if (cell.CellId < 1 || cell.CellId > CellCount || cell.X < 0 || cell.X >= Columns || cell.Y < 0 || cell.Y >= Rows ||
                cell.Population < 0 || !float.IsFinite(cell.FoodCount) || cell.FoodCount < 0 ||
                !float.IsFinite(cell.WoodCount) || cell.WoodCount < 0 || !float.IsFinite(cell.StoneCount) || cell.StoneCount < 0 ||
                !float.IsFinite(cell.AlloyCount) || cell.AlloyCount < 0 || !float.IsFinite(cell.EnergyCount) || cell.EnergyCount < 0 ||
                !float.IsFinite(cell.KnowledgeCount) || cell.KnowledgeCount < 0 || !float.IsFinite(cell.ComponentsCount) || cell.ComponentsCount < 0 ||
                !float.IsFinite(cell.Fertility) || cell.Fertility is < 0 or > 2 || !float.IsFinite(cell.BlightPresence) || cell.BlightPresence is < 0 or > 1 ||
                !Enum.IsDefined(cell.ProductionFocus) || !Enum.IsDefined(cell.Biome))
                throw new InvalidDataException("Macro world cell contains invalid values.");
        }
        foreach (var caravan in Caravans)
        {
            if (caravan.Id <= 0 || caravan.FactionId <= 0 || !float.IsFinite(caravan.X) || !float.IsFinite(caravan.Y) ||
                caravan.X < 0 || caravan.X >= Columns || caravan.Y < 0 || caravan.Y >= Rows ||
                !float.IsFinite(caravan.TravelSpeed) || caravan.TravelSpeed <= 0 || caravan.TargetCellId < 1 || caravan.TargetCellId > CellCount ||
                caravan.Cargo is null || caravan.Cargo.Any(item => !Enum.IsDefined(item.Key) || !float.IsFinite(item.Value) || item.Value < 0) ||
                caravan.RouteNodes is null || caravan.RouteIndex < 0 || caravan.RouteIndex > caravan.RouteNodes.Count ||
                caravan.RouteReady && (caravan.RoutePending || caravan.RouteNodes.Count == 0) ||
                caravan.RouteNodes.Any(node => node.X < 0 || node.X >= Columns || node.Y < 0 || node.Y >= Rows))
                throw new InvalidDataException("Macro caravan contains invalid values.");
        }
        foreach (var arrival in Arrivals)
        {
            if (arrival.Id <= 0 || arrival.FactionId < 0 || arrival.TargetCellId < 1 || arrival.TargetCellId > CellCount ||
                arrival.Population < 0 || arrival.SpawnedVisitorCount < 0 || arrival.SpawnedVisitorCount > arrival.Population && arrival.IsRefugeeWave ||
                arrival.NotificationId <= 0 || arrival.Cargo is null ||
                arrival.Cargo.Any(item => !Enum.IsDefined(item.Key) || !float.IsFinite(item.Value) || item.Value < 0))
                throw new InvalidDataException("Macro caravan arrival contains invalid values.");
        }
        if (ConsumedArrivalIds.Any(id => !Arrivals.Any(arrival => arrival.Id == id)))
            throw new InvalidDataException("Macro arrival consumption state references a missing arrival.");
        if (Market.FoodPrice is < 0.25f or > 8f || Market.WoodPrice is < 0.25f or > 8f ||
            Market.StonePrice is < 0.25f or > 8f || Market.AlloyPrice is < 0.25f or > 8f ||
            Market.EnergyPrice is < 0.25f or > 8f || Market.ComponentsPrice is < 0.25f or > 8f)
            throw new InvalidDataException("Macro market prices are outside their supported range.");
    }

    private static double Distance(double x1, double y1, double x2, double y2)
    { double dx = x1 - x2, dy = y1 - y2; return Math.Sqrt(dx * dx + dy * dy); }

    private static uint StableHash(int seed, int x, int y)
    {
        unchecked
        {
            uint value = (uint)seed ^ (uint)(x + 1) * 0x9E3779B9u ^ (uint)(y + 1) * 0x85EBCA6Bu;
            value ^= value >> 16; value *= 0x7FEB352Du; value ^= value >> 15; value *= 0x846CA68Bu; return value ^ (value >> 16);
        }
    }
}
