using System.Runtime.InteropServices;
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
}

internal sealed record WorldNotification(long Id, long Tick, string Category, string Message, int CellId, bool Important);

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

/// <summary>Supply/demand pricing based on the macro ledger. Prices are bounded to prevent runaway values.</summary>
internal static class MarketSystem
{
    public static void UpdatePrices(MacroWorldState world, long tick)
    {
        ArgumentNullException.ThrowIfNull(world);
        double foodDemand = 0, foodSupply = 0, woodDemand = 0, woodSupply = 0;
        double stoneDemand = 0, stoneSupply = 0, alloyDemand = 0, alloySupply = 0;
        double energyDemand = 0, energySupply = 0, componentsDemand = 0, componentsSupply = 0;

        foreach (ref readonly var cell in world.Cells.AsSpan())
        {
            if (cell.OwningFactionId == 0 || cell.Population <= 0) continue;
            double population = cell.Population;
            foodDemand += population * 0.01d;
            foodSupply += Math.Max(0, cell.FoodCount);
            woodDemand += population * (cell.ProductionFocus == ProductionFocus.Industry ? 0.0018d : 0.0006d);
            woodSupply += Math.Max(0, cell.WoodCount);
            stoneDemand += population * (cell.ProductionFocus == ProductionFocus.Mining ? 0.001d : 0.00025d);
            stoneSupply += Math.Max(0, cell.StoneCount);
            alloyDemand += population * (cell.ProductionFocus == ProductionFocus.Military ? 0.001d : 0.00015d);
            alloySupply += Math.Max(0, cell.AlloyCount);
            energyDemand += population * (cell.ProductionFocus == ProductionFocus.Industry ? 0.0012d : 0.0002d);
            energySupply += Math.Max(0, cell.EnergyCount);
            componentsDemand += population * (cell.ProductionFocus == ProductionFocus.Industry ? 0.0008d : 0.0001d);
            componentsSupply += Math.Max(0, cell.ComponentsCount);
        }

        world.Market.FoodPrice = Price(foodDemand, foodSupply);
        world.Market.WoodPrice = Price(woodDemand, woodSupply);
        world.Market.StonePrice = Price(stoneDemand, stoneSupply);
        world.Market.AlloyPrice = Price(alloyDemand, alloySupply);
        world.Market.EnergyPrice = Price(energyDemand, energySupply);
        world.Market.ComponentsPrice = Price(componentsDemand, componentsSupply);
        world.Market.LastUpdatedTick = tick;
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
    public List<WorldNotification> Notifications { get; set; } = [];
    public MarketSnapshot Market { get; set; } = new();
    public ResearchProgressState Research { get; set; } = new();
    public long SimulationTicks { get; set; }
    public double FractionalTickRemainder { get; set; }
    public long NextNotificationId { get; set; } = 1;
    public long NextCaravanId { get; set; } = 1;
    public int MacroTurnsProcessed { get; set; }
    public int TickSliceCursor { get; set; }
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
        return true;
    }

    public void Advance(double deltaSeconds, ContinuousWorldState continuous)
    {
        if (!double.IsFinite(deltaSeconds) || deltaSeconds < 0) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        ArgumentNullException.ThrowIfNull(continuous);
        double totalTickDelta = deltaSeconds * TickManager.TicksPerSecond + FractionalTickRemainder;
        long tickDelta = (long)Math.Floor(totalTickDelta + 1e-8);
        FractionalTickRemainder = Math.Max(0d, totalTickDelta - tickDelta);
        SimulationTicks = checked(SimulationTicks + tickDelta);
        UpdateCaravans(deltaSeconds);
        // Storyteller-style cell scanning is sliced into small deterministic batches every shared tick.
        EvaluateIncidentSlice(continuous, 600);
        while (SimulationTicks / TicksPerMacroTurn > MacroTurnsProcessed)
        {
            ProcessMacroTurn(continuous);
            MacroTurnsProcessed++;
        }
    }

    public void ProcessMacroTurn(ContinuousWorldState continuous)
    {
        ArgumentNullException.ThrowIfNull(continuous);
        for (int i = 0; i < Cells.Length; i++)
        {
            ref var cell = ref Cells[i];
            if (cell.OwningFactionId == 0) continue;
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
                    TryTriggerRefugeeWave(i, continuous);
            }
            else if (cell.FoodCount > population * 0.03f && cell.Population > 0)
                cell.Population = Math.Min(100_000, cell.Population + Math.Max(1, (int)(cell.Population * 0.002f)));
        }

        DispatchReliefCaravans();
        UpdatePricesAndEmitMarketNews();
    }

    private void DispatchReliefCaravans()
    {
        var factionIds = FactionIds.OrderBy(pair => pair.Value).ToArray();
        foreach (var (faction, factionId) in factionIds)
        {
            var hungry = Cells.Where(c => c.OwningFactionId == factionId && c.Population > 0 && c.FoodCount < c.Population * 0.004f)
                .OrderBy(c => c.FoodCount).ThenBy(c => c.CellId).FirstOrDefault();
            if (hungry.CellId == 0) continue;
            var donor = Cells.Where(c => c.OwningFactionId == factionId && c.CellId != hungry.CellId && c.FoodCount > c.Population * 0.02f)
                .OrderBy(c => Distance(c.X, c.Y, hungry.X, hungry.Y)).ThenBy(c => c.CellId).FirstOrDefault();
            if (donor.CellId == 0) continue;
            ref var source = ref Cells[donor.CellId - 1];
            float amount = Math.Min(100f, Math.Max(0f, source.FoodCount - source.Population * 0.01f));
            if (amount <= 0) continue;
            source.FoodCount -= amount;
            Caravans.Add(new MacroCaravanState
            {
                Id = NextCaravanId++, FactionId = factionId, X = source.X, Y = source.Y,
                TargetCellId = hungry.CellId, TravelSpeed = 0.05f,
                Cargo = new Dictionary<MacroResource, float> { [MacroResource.Food] = amount }
            });
            AddNotification(SimulationTicks, "trade", $"{faction} dispatched a relief caravan carrying {amount:0} food to cell {hungry.CellId}.", hungry.CellId, false);
        }
    }

    private void UpdateCaravans(double deltaSeconds)
    {
        for (int i = Caravans.Count - 1; i >= 0; i--)
        {
            var caravan = Caravans[i];
            if (caravan.TargetCellId < 1 || caravan.TargetCellId > Cells.Length) { Caravans.RemoveAt(i); continue; }
            var target = Cells[caravan.TargetCellId - 1];
            float dx = target.X - caravan.X, dy = target.Y - caravan.Y;
            float length = MathF.Sqrt(dx * dx + dy * dy);
            float travel = Math.Max(0f, caravan.TravelSpeed * (float)deltaSeconds * 60f);
            if (length > travel && length > 0.001f)
            {
                caravan.X += dx / length * travel;
                caravan.Y += dy / length * travel;
                continue;
            }

            ref var destination = ref Cells[caravan.TargetCellId - 1];
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
            AddNotification(SimulationTicks, "trade", $"A relief caravan arrived at cell {destination.CellId}; cargo was added to its ledger.", destination.CellId, destination.IsLocalMapGenerated);
            Caravans.RemoveAt(i);
        }
    }

    private void TryTriggerRefugeeWave(int sourceIndex, ContinuousWorldState continuous)
    {
        ref var source = ref Cells[sourceIndex];
        var destination = Cells.Where(c => c.OwningFactionId != source.OwningFactionId && c.IsLocalMapGenerated)
            .OrderBy(c => Distance(c.X, c.Y, source.X, source.Y)).FirstOrDefault();
        if (destination.CellId == 0) return;
        int refugeeCount = Math.Min(12, Math.Max(2, source.Population / 1000));
        source.Population = Math.Max(0, source.Population - refugeeCount);
        AddNotification(SimulationTicks, "refugees", $"{refugeeCount} refugees fled cell {source.CellId} toward the Kor colony. Local admission is awaiting player resolution.", destination.CellId, true);
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

    private void UpdatePricesAndEmitMarketNews()
    {
        float previousFood = Market.FoodPrice, previousAlloy = Market.AlloyPrice;
        MarketSystem.UpdatePrices(this, SimulationTicks);
        if (previousFood > 0 && Market.FoodPrice >= previousFood * 1.4f)
            AddNotification(SimulationTicks, "market", $"Global food price surged to {Market.FoodPrice:0.00}× as supplies tightened.", 0, true);
        if (previousAlloy > 0 && Market.AlloyPrice >= previousAlloy * 1.4f)
            AddNotification(SimulationTicks, "market", $"Alloy price surged to {Market.AlloyPrice:0.00}× amid industrial and military demand.", 0, true);
    }

    private void AddNotification(long tick, string category, string message, int cellId, bool important)
    {
        if (Notifications.Count >= 500) Notifications.RemoveAt(0);
        Notifications.Add(new WorldNotification(NextNotificationId++, tick, category, message, cellId, important));
    }

    public void Validate()
    {
        if (Cells is null || Cells.Length != CellCount || FactionIds is null || Notifications is null || Caravans is null || Market is null || Research is null ||
            SimulationTicks < 0 || !double.IsFinite(FractionalTickRemainder) || FractionalTickRemainder is < 0 or >= 1 || MacroTurnsProcessed < 0 || MacroTurnsProcessed > SimulationTicks / TicksPerMacroTurn || TickSliceCursor < 0 || TickSliceCursor >= CellCount ||
            NextNotificationId <= Notifications.Select(n => n.Id).DefaultIfEmpty(0).Max() ||
            NextCaravanId <= Caravans.Select(c => c.Id).DefaultIfEmpty(0).Max())
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
                !float.IsFinite(caravan.TravelSpeed) || caravan.TravelSpeed <= 0 || caravan.TargetCellId < 1 || caravan.TargetCellId > CellCount ||
                caravan.Cargo is null || caravan.Cargo.Any(item => !Enum.IsDefined(item.Key) || !float.IsFinite(item.Value) || item.Value < 0))
                throw new InvalidDataException("Macro caravan contains invalid values.");
        }
        if (Market.FoodPrice is < 0.25f or > 8f || Market.WoodPrice is < 0.25f or > 8f ||
            Market.StonePrice is < 0.25f or > 8f || Market.AlloyPrice is < 0.25f or > 8f ||
            Market.EnergyPrice is < 0.25f or > 8f || Market.ComponentsPrice is < 0.25f or > 8f)
            throw new InvalidDataException("Macro market prices are outside their supported range.");
    }

    private static float Distance(float x1, float y1, float x2, float y2)
    { float dx = x1 - x2, dy = y1 - y2; return MathF.Sqrt(dx * dx + dy * dy); }

    private static uint StableHash(int seed, int x, int y)
    {
        unchecked
        {
            uint value = (uint)seed ^ (uint)(x + 1) * 0x9E3779B9u ^ (uint)(y + 1) * 0x85EBCA6Bu;
            value ^= value >> 16; value *= 0x7FEB352Du; value ^= value >> 15; value *= 0x846CA68Bu; return value ^ (value >> 16);
        }
    }
}
