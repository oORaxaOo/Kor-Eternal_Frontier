using System.Runtime.InteropServices;

namespace KorTheBlight.Simulation.Combat;

internal enum CombatEntityType : byte { Human, Vehicle, HeavyTank, BlightBeast, BlightTitan }
internal enum CombatWeaponType : byte { Melee, Pistol, Rifle, Plasma, TankCannon }
internal enum CoverType : byte { None, Low, High }

[StructLayout(LayoutKind.Sequential)]
internal struct CombatEntity
{
    public int Id;
    public int FactionId;
    public CombatEntityType Type;
    public bool IsActive;
    public float X, Y, Speed, Health, SharpArmor, BluntArmor;
    public CombatWeaponType EquippedWeapon;
    public int CurrentAmmo, MaxAmmo, TargetEntityId;
    public float AttackCooldown, CurrentFuel;
    public int DriverPawnId, PassengerCount, NameSeed;
    public bool HasBlightMutation, LostEye;
    public int KilledPawnId;
}

/// <summary>Local combat cover grid. The grid is a snapshot owned by the simulation thread.</summary>
internal sealed class CombatCoverGrid
{
    public const int Width = 40;
    public const int Height = 26;
    public const int CellCount = Width * Height;
    private readonly byte[] _cover = new byte[CellCount];

    public CoverType this[int x, int y]
    {
        get => InBounds(x, y) ? (CoverType)_cover[x + y * Width] : CoverType.None;
        set
        {
            if (!InBounds(x, y)) throw new ArgumentOutOfRangeException(nameof(x));
            if (value > CoverType.High) throw new ArgumentOutOfRangeException(nameof(value));
            _cover[x + y * Width] = (byte)value;
        }
    }

    public byte[] Snapshot() => (byte[])_cover.Clone();
    public void Restore(byte[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length != CellCount || values.Any(v => v > (byte)CoverType.High))
            throw new ArgumentException("Cover snapshot has invalid dimensions or values.", nameof(values));
        Array.Copy(values, _cover, values.Length);
    }

    private static bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;
}

internal readonly record struct CombatFlash(int AttackerId, int TargetId, float FromX, float FromY, float ToX, float ToY, bool IsHeavy);

/// <summary>
/// Deterministic, fixed-step local combat. Entity updates and damage resolution are deliberately
/// single-writer; background AI must submit commands rather than mutate this array.
/// Spatial buckets avoid a full entity-array scan for every target query.
/// </summary>
internal sealed class CombatEngine
{
    public const int MaximumEntities = 5_000;
    private readonly CombatEntity[] _entities = new CombatEntity[MaximumEntities];
    private readonly List<int>[] _cells = Enumerable.Range(0, CombatCoverGrid.CellCount).Select(_ => new List<int>()).ToArray();
    private readonly float[] _pendingDamage = new float[MaximumEntities];
    private readonly Dictionary<int, int> _slotById = new();
    private readonly List<CombatFlash> _flashes = new();
    private readonly List<(int EntityId, string ItemId, int Amount)> _deathDrops = new();
    private int _nextEntityId = 1;
    private long _tick;

    public IReadOnlyList<CombatEntity> Entities => _entities.Where(e => e.IsActive).ToArray();
    public IReadOnlyList<CombatFlash> Flashes => _flashes;
    public IReadOnlyList<(int EntityId, string ItemId, int Amount)> DeathDrops => _deathDrops;

    public bool TrySpawn(CombatEntity entity, out int entityId)
    {
        entityId = -1;
        int slot = Array.FindIndex(_entities, e => !e.IsActive);
        if (slot < 0) return false;
        entity.Id = _nextEntityId++;
        entity.IsActive = true;
        entity.Health = Math.Clamp(float.IsFinite(entity.Health) ? entity.Health : 1f, 0.01f, 100f);
        entity.Speed = Math.Clamp(float.IsFinite(entity.Speed) ? entity.Speed : 1f, 0f, 20f);
        entity.SharpArmor = Math.Clamp(entity.SharpArmor, 0f, 0.95f);
        entity.BluntArmor = Math.Clamp(entity.BluntArmor, 0f, 0.95f);
        entity.TargetEntityId = -1;
        _entities[slot] = entity;
        _slotById[entity.Id] = slot;
        entityId = entity.Id;
        return true;
    }

    public bool TryGetEntity(int id, out CombatEntity entity)
    {
        int slot = FindSlot(id);
        if (slot < 0) { entity = default; return false; }
        entity = _entities[slot];
        return true;
    }

    public bool TrySetTarget(int entityId, int targetEntityId)
    {
        int slot = FindSlot(entityId);
        if (slot < 0 || (targetEntityId != -1 && FindSlot(targetEntityId) < 0)) return false;
        _entities[slot].TargetEntityId = targetEntityId;
        return true;
    }

    public void Step(float deltaSeconds, CombatCoverGrid cover)
    {
        ArgumentNullException.ThrowIfNull(cover);
        if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0f || deltaSeconds > 0.25f)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds), "Combat uses bounded fixed steps no longer than 250 ms.");
        _tick++;
        _flashes.Clear();
        _deathDrops.Clear();
        Array.Clear(_pendingDamage);
        RebuildSpatialIndex();

        // Phase one: movement and target selection. No entity is removed or damaged in this phase.
        for (int i = 0; i < _entities.Length; i++)
        {
            ref var entity = ref _entities[i];
            if (!entity.IsActive || entity.Health <= 0f) continue;
            entity.AttackCooldown = MathF.Max(0f, entity.AttackCooldown - deltaSeconds);
            if (entity.TargetEntityId < 0 || !IsEnemyAlive(entity.FactionId, entity.TargetEntityId))
                entity.TargetEntityId = FindNearestEnemy(i, entity, cover);
            if (entity.TargetEntityId < 0) continue;

            int targetSlot = FindSlot(entity.TargetEntityId);
            if (targetSlot < 0) continue;
            ref var target = ref _entities[targetSlot];
            float dx = target.X - entity.X, dy = target.Y - entity.Y;
            float distance = MathF.Sqrt(dx * dx + dy * dy);
            float range = WeaponRange(entity.EquippedWeapon);
            if (distance > range && entity.EquippedWeapon != CombatWeaponType.Melee)
            {
                MoveTowards(ref entity, target.X, target.Y, deltaSeconds, cover);
            }
            else if (distance > range && entity.EquippedWeapon == CombatWeaponType.Melee)
            {
                MoveTowards(ref entity, target.X, target.Y, deltaSeconds, cover);
            }
        }

        // Positions changed, so refresh the spatial buckets before the firing phase.
        RebuildSpatialIndex();
        for (int i = 0; i < _entities.Length; i++)
        {
            ref var attacker = ref _entities[i];
            if (!attacker.IsActive || attacker.Health <= 0f || attacker.TargetEntityId < 0) continue;
            int targetSlot = FindSlot(attacker.TargetEntityId);
            if (targetSlot < 0) continue;
            ref var target = ref _entities[targetSlot];
            if (!target.IsActive || target.Health <= 0f || target.FactionId == attacker.FactionId) continue;
            float dx = target.X - attacker.X, dy = target.Y - attacker.Y;
            float distance = MathF.Sqrt(dx * dx + dy * dy);
            if (distance > WeaponRange(attacker.EquippedWeapon)) continue;

            if (attacker.AttackCooldown > 0f) continue;
            if (attacker.EquippedWeapon != CombatWeaponType.Melee && attacker.CurrentAmmo <= 0) continue;

            float damage = WeaponDamage(attacker.EquippedWeapon);
            CoverType pathCover = CheckLineOfSightCover(attacker.X, attacker.Y, target.X, target.Y, cover);
            float armor = attacker.EquippedWeapon == CombatWeaponType.Melee ? target.BluntArmor : target.SharpArmor;
            float penetration = WeaponArmorPenetration(attacker.EquippedWeapon);
            float effectiveArmor = Math.Clamp(armor - penetration, 0f, 0.95f);
            if (pathCover == CoverType.High && DeterministicRoll(attacker.Id, target.Id, _tick) < 0.75d)
                damage = 0f;
            else if (pathCover == CoverType.Low)
                damage *= 0.70f;

            _pendingDamage[targetSlot] += damage * (1f - effectiveArmor);
            attacker.AttackCooldown = WeaponCooldown(attacker.EquippedWeapon);
            if (attacker.EquippedWeapon != CombatWeaponType.Melee) attacker.CurrentAmmo--;
            _flashes.Add(new CombatFlash(attacker.Id, target.Id, attacker.X, attacker.Y, target.X, target.Y,
                attacker.Type is CombatEntityType.Vehicle or CombatEntityType.HeavyTank));
        }

        // Phase two: apply accumulated damage, then emit deterministic drops exactly once.
        for (int i = 0; i < _entities.Length; i++)
        {
            ref var entity = ref _entities[i];
            if (!entity.IsActive || _pendingDamage[i] <= 0f) continue;
            entity.Health -= _pendingDamage[i];
            if (entity.Health > 0f) continue;
            entity.Health = 0f;
            entity.IsActive = false;
            _slotById.Remove(entity.Id);
            if (entity.Type is CombatEntityType.Vehicle or CombatEntityType.HeavyTank)
                _deathDrops.Add((entity.Id, "resource_alloy", 25));
            else if (entity.Type is CombatEntityType.BlightBeast or CombatEntityType.BlightTitan)
                _deathDrops.Add((entity.Id, "resource_blight_biomass", entity.Type == CombatEntityType.BlightTitan ? 20 : 3));
        }
        RebuildSpatialIndex();
    }

    private void RebuildSpatialIndex()
    {
        foreach (var cell in _cells) cell.Clear();
        for (int i = 0; i < _entities.Length; i++)
        {
            var entity = _entities[i];
            if (!entity.IsActive || entity.Health <= 0f) continue;
            int x = Math.Clamp((int)entity.X, 0, CombatCoverGrid.Width - 1);
            int y = Math.Clamp((int)entity.Y, 0, CombatCoverGrid.Height - 1);
            _cells[x + y * CombatCoverGrid.Width].Add(i);
        }
    }

    private int FindNearestEnemy(int sourceSlot, CombatEntity source, CombatCoverGrid cover)
    {
        int bestId = -1;
        float bestDistance = float.MaxValue;
        int cx = Math.Clamp((int)source.X, 0, CombatCoverGrid.Width - 1);
        int cy = Math.Clamp((int)source.Y, 0, CombatCoverGrid.Height - 1);
        int radius = Math.Min(CombatCoverGrid.Width, (int)MathF.Ceiling(MathF.Max(WeaponRange(source.EquippedWeapon), 8f)));
        int minX = Math.Max(0, cx - radius), maxX = Math.Min(CombatCoverGrid.Width - 1, cx + radius);
        int minY = Math.Max(0, cy - radius), maxY = Math.Min(CombatCoverGrid.Height - 1, cy + radius);
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        foreach (int slot in _cells[x + y * CombatCoverGrid.Width])
        {
            if (slot == sourceSlot) continue;
            var candidate = _entities[slot];
            if (!candidate.IsActive || candidate.Health <= 0f || candidate.FactionId == source.FactionId) continue;
            float dx = candidate.X - source.X, dy = candidate.Y - source.Y;
            float distance = MathF.Sqrt(dx * dx + dy * dy);
            if (distance < bestDistance || (MathF.Abs(distance - bestDistance) < 0.001f && candidate.Id < bestId))
            { bestDistance = distance; bestId = candidate.Id; }
        }
        return bestId;
    }

    private bool IsEnemyAlive(int factionId, int targetId)
    {
        int slot = FindSlot(targetId);
        return slot >= 0 && _entities[slot].IsActive && _entities[slot].Health > 0f && _entities[slot].FactionId != factionId;
    }

    private int FindSlot(int id)
    {
        if (id <= 0 || !_slotById.TryGetValue(id, out int slot)) return -1;
        return _entities[slot].IsActive && _entities[slot].Id == id ? slot : -1;
    }

    private static void MoveTowards(ref CombatEntity entity, float tx, float ty, float dt, CombatCoverGrid cover)
    {
        float dx = tx - entity.X, dy = ty - entity.Y;
        float distance = MathF.Sqrt(dx * dx + dy * dy);
        if (distance <= 0.1f || entity.Speed <= 0f) return;
        int x = Math.Clamp((int)entity.X, 0, CombatCoverGrid.Width - 1);
        int y = Math.Clamp((int)entity.Y, 0, CombatCoverGrid.Height - 1);
        float terrain = cover[x, y] == CoverType.High ? 0.75f : cover[x, y] == CoverType.Low ? 0.9f : 1f;
        float step = MathF.Min(distance - 0.1f, entity.Speed * terrain * dt);
        entity.X = Math.Clamp(entity.X + dx / distance * step, 0f, CombatCoverGrid.Width - 0.001f);
        entity.Y = Math.Clamp(entity.Y + dy / distance * step, 0f, CombatCoverGrid.Height - 0.001f);
    }

    internal static CoverType CheckLineOfSightCover(float x1, float y1, float x2, float y2, CombatCoverGrid cover)
    {
        int x = (int)x1, y = (int)y1, endX = (int)x2, endY = (int)y2;
        int dx = Math.Abs(endX - x), dy = Math.Abs(endY - y);
        int sx = x < endX ? 1 : -1, sy = y < endY ? 1 : -1, error = dx - dy;
        CoverType highest = CoverType.None;
        while (x != endX || y != endY)
        {
            int twice = 2 * error;
            if (twice > -dy) { error -= dy; x += sx; }
            if (twice < dx) { error += dx; y += sy; }
            if (x == endX && y == endY) break;
            if (x >= 0 && x < CombatCoverGrid.Width && y >= 0 && y < CombatCoverGrid.Height)
                if (cover[x, y] > highest) highest = cover[x, y];
        }
        return highest;
    }

    private static double DeterministicRoll(int attackerId, int targetId, long tick)
    {
        unchecked
        {
            uint value = (uint)attackerId * 0x9E3779B9u ^ (uint)targetId * 0x85EBCA6Bu ^ (uint)tick * 0xC2B2AE35u;
            value ^= value >> 16; value *= 0x7FEB352Du; value ^= value >> 15; value *= 0x846CA68Bu; value ^= value >> 16;
            return value / (double)uint.MaxValue;
        }
    }

    private static float WeaponRange(CombatWeaponType weapon) => weapon switch
    {
        CombatWeaponType.Melee => 1.2f, CombatWeaponType.Pistol => 6f, CombatWeaponType.Rifle => 15f,
        CombatWeaponType.Plasma => 12f, CombatWeaponType.TankCannon => 25f, _ => 1f
    };
    private static float WeaponDamage(CombatWeaponType weapon) => weapon switch
    {
        CombatWeaponType.Melee => 0.10f, CombatWeaponType.Pistol => 0.15f, CombatWeaponType.Rifle => 0.25f,
        CombatWeaponType.Plasma => 0.40f, CombatWeaponType.TankCannon => 0.95f, _ => 0f
    };
    private static float WeaponArmorPenetration(CombatWeaponType weapon) => weapon switch
    { CombatWeaponType.Rifle => 0.30f, CombatWeaponType.Plasma => 0.45f, CombatWeaponType.TankCannon => 0.90f, _ => 0f };
    private static float WeaponCooldown(CombatWeaponType weapon) => weapon switch
    { CombatWeaponType.Pistol => 0.5f, CombatWeaponType.Rifle => 0.8f, CombatWeaponType.TankCannon => 4f, _ => 1f };
}

internal static class FactionLoadoutGenerator
{
    public static CombatEntity Create(int factionId, int seed, int idHint = 0)
    {
        var random = new Random(seed);
        var entity = new CombatEntity { FactionId = factionId, Health = 1f, Speed = 2f, CurrentFuel = 100f, DriverPawnId = -1, TargetEntityId = -1, NameSeed = seed };
        switch (factionId)
        {
            case 1:
                entity.Type = CombatEntityType.Human; entity.EquippedWeapon = CombatWeaponType.Rifle;
                entity.MaxAmmo = entity.CurrentAmmo = 30; entity.SharpArmor = 0.25f;
                break;
            case 2:
                if (random.NextDouble() < 0.20)
                {
                    entity.Type = CombatEntityType.HeavyTank; entity.EquippedWeapon = CombatWeaponType.TankCannon;
                    entity.MaxAmmo = entity.CurrentAmmo = 12; entity.SharpArmor = 0.85f;
                    entity.BluntArmor = 0.60f; entity.Speed = 1f;
                }
                else
                {
                    entity.Type = CombatEntityType.Human; entity.EquippedWeapon = CombatWeaponType.Pistol;
                    entity.MaxAmmo = entity.CurrentAmmo = 18; entity.SharpArmor = 0.40f; entity.Speed = 1.8f;
                }
                break;
            case 99:
                entity.HasBlightMutation = true;
                if (random.NextDouble() < 0.05)
                {
                    entity.Type = CombatEntityType.BlightTitan; entity.EquippedWeapon = CombatWeaponType.Melee;
                    entity.Health = 5f; entity.SharpArmor = 0.50f; entity.BluntArmor = 0.35f; entity.Speed = 0.8f;
                }
                else
                {
                    entity.Type = CombatEntityType.BlightBeast; entity.EquippedWeapon = CombatWeaponType.Melee;
                    entity.Health = 0.8f; entity.SharpArmor = 0.05f; entity.Speed = 3.5f;
                }
                break;
            default:
                entity.Type = CombatEntityType.Human; entity.EquippedWeapon = CombatWeaponType.Pistol;
                entity.MaxAmmo = entity.CurrentAmmo = 12; entity.SharpArmor = 0.1f;
                break;
        }
        return entity;
    }
}

internal sealed record ProductionRecipe(string Id, string ResultItemId, string RequiredResearchId,
    IReadOnlyDictionary<string, int> InputCosts, int WorkTicksRequired);

internal sealed class WorkbenchManager
{
    public const string Alloy = "resource_alloy";
    public const string Components = "item_component";
    public const string TankChassis = "part_tank_chassis";
    public const string CombustionEngine = "part_engine";
    public const string HeavyArmorPlates = "part_armor_plates";
    public const string InfantryRifle = "weapon_rifle";
    public const string TankCannon = "weapon_tank_cannon";

    private readonly Dictionary<string, ProductionRecipe> _recipes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _inventory = new(StringComparer.Ordinal);
    private readonly HashSet<string> _unlockedResearch = new(StringComparer.Ordinal);
    private readonly Dictionary<long, CraftJob> _jobs = new();
    private long _nextJobId = 1;
    public IReadOnlyDictionary<string, int> Inventory => _inventory;
    public IReadOnlyCollection<ProductionRecipe> Recipes => _recipes.Values;
    public IReadOnlyCollection<CraftJob> Jobs => _jobs.Values;

    public WorkbenchManager()
    {
        AddRecipe(new("weapon_rifle_industrial", InfantryRifle, "tech_industrial_weapons",
            new Dictionary<string, int> { [Alloy] = 20, [Components] = 3 }, 400));
        AddRecipe(new("part_engine_automotive", CombustionEngine, "tech_automotive_basics",
            new Dictionary<string, int> { [Alloy] = 50, [Components] = 10 }, 800));
        AddRecipe(new("part_chassis_armored", TankChassis, "tech_armored_vehicles",
            new Dictionary<string, int> { [Alloy] = 150, [Components] = 5 }, 1200));
        AddRecipe(new("part_armor_plates", HeavyArmorPlates, "tech_armored_vehicles",
            new Dictionary<string, int> { [Alloy] = 40, [Components] = 2 }, 600));
        AddRecipe(new("weapon_tank_cannon", TankCannon, "tech_armored_vehicles",
            new Dictionary<string, int> { [Alloy] = 60, [Components] = 12 }, 1000));
    }

    public void AddResearch(string researchId)
    {
        if (string.IsNullOrWhiteSpace(researchId)) throw new ArgumentException("Research ID is required.", nameof(researchId));
        _unlockedResearch.Add(researchId);
    }

    public void AddInventory(string itemId, int amount)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        _inventory[itemId] = checked(_inventory.GetValueOrDefault(itemId) + amount);
    }

    public bool TryStartCraft(string recipeId, out long jobId)
    {
        jobId = 0;
        if (!_recipes.TryGetValue(recipeId, out var recipe) || !_unlockedResearch.Contains(recipe.RequiredResearchId)) return false;
        if (recipe.InputCosts.Any(c => _inventory.GetValueOrDefault(c.Key) < c.Value)) return false;
        foreach (var cost in recipe.InputCosts) _inventory[cost.Key] -= cost.Value;
        jobId = _nextJobId++;
        _jobs.Add(jobId, new CraftJob(jobId, recipe, recipe.WorkTicksRequired));
        return true;
    }

    public void Advance(int workTicks)
    {
        if (workTicks < 0) throw new ArgumentOutOfRangeException(nameof(workTicks));
        foreach (var job in _jobs.Values.ToArray())
        {
            int remaining = Math.Max(0, job.WorkLeft - workTicks);
            if (remaining == 0)
            {
                AddInventory(job.Recipe.ResultItemId, 1);
                _jobs.Remove(job.Id);
            }
            else _jobs[job.Id] = job with { WorkLeft = remaining };
        }
    }

    public bool TryBuildVehicle(CombatEngine engine, int tileIndex, out int entityId)
    {
        ArgumentNullException.ThrowIfNull(engine);
        entityId = -1;
        if (tileIndex < 0 || tileIndex >= CombatCoverGrid.CellCount) return false;
        var costs = new Dictionary<string, int> { [TankChassis] = 1, [CombustionEngine] = 1, [HeavyArmorPlates] = 4, [TankCannon] = 1 };
        if (costs.Any(c => _inventory.GetValueOrDefault(c.Key) < c.Value)) return false;
        int x = tileIndex % CombatCoverGrid.Width, y = tileIndex / CombatCoverGrid.Width;
        var tank = new CombatEntity
        {
            FactionId = 1, Type = CombatEntityType.HeavyTank, X = x, Y = y, Health = 1f, Speed = 1f,
            SharpArmor = 0.85f, BluntArmor = 0.60f, EquippedWeapon = CombatWeaponType.TankCannon,
            CurrentAmmo = 12, MaxAmmo = 12, CurrentFuel = 100f, DriverPawnId = -1
        };
        // Spawn first. Costs are only committed if the engine has a free entity slot.
        if (!engine.TrySpawn(tank, out entityId)) return false;
        foreach (var cost in costs) _inventory[cost.Key] -= cost.Value;
        return true;
    }

    private void AddRecipe(ProductionRecipe recipe)
    {
        if (recipe.WorkTicksRequired <= 0 || recipe.InputCosts.Count == 0 ||
            recipe.InputCosts.Any(c => string.IsNullOrWhiteSpace(c.Key) || c.Value <= 0) ||
            !_recipes.TryAdd(recipe.Id, recipe))
            throw new InvalidOperationException($"Invalid or duplicate production recipe '{recipe.Id}'.");
    }
}

internal sealed record CraftJob(long Id, ProductionRecipe Recipe, int WorkLeft);

internal sealed record NemesisRecord(
    long Id, int NameSeed, int FactionId, int PowerLevel, int KilledPawnId,
    bool HasBlightMutation, bool LostEye, CombatEntityType EntityType);

internal static class NemesisNameGenerator
{
    private static readonly string[] FirstNames = ["Gorgakh", "Krell", "Vandar", "Zarek", "Malakor", "Scythe", "Drakon", "Vulgsh", "Throm", "Morzan"];
    private static readonly string[] KillerTitles = ["der Kolonistenschlächter", "der Witwenmacher", "der Henker von Kor", "Bluttrinker"];
    private static readonly string[] VehicleTitles = ["der Stahldämon", "der Panzerkommandant", "Eisenhaut", "der Kettenfürst"];
    private static readonly string[] BlightTitles = ["die Fäulnis-Brut", "der Sporenwandler", "das Pestauge", "der Verseuchte"];
    private static readonly string[] DefaultTitles = ["der Unerbittliche", "Narbengesicht", "der Schakal", "Knochenbrecher"];

    public static string GenerateFullTitle(NemesisRecord nemesis)
    {
        var random = new Random(nemesis.NameSeed);
        string first = FirstNames[random.Next(FirstNames.Length)];
        string[] titles = nemesis.HasBlightMutation ? BlightTitles :
            nemesis.KilledPawnId >= 0 ? KillerTitles :
            nemesis.PowerLevel > 5 || nemesis.EntityType is CombatEntityType.Vehicle or CombatEntityType.HeavyTank ? VehicleTitles : DefaultTitles;
        string title = titles[random.Next(titles.Length)];
        return nemesis.LostEye && !nemesis.HasBlightMutation
            ? $"{first} {title}, das Einauge"
            : $"{first} {title}";
    }
}

