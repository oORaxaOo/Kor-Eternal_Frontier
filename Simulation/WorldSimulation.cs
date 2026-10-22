using System.Text.Json;
using KorTheBlight.WorldModel;
using KorTheBlight.Simulation.Research;
using KorTheBlight.Simulation.Combat;

namespace KorTheBlight.Simulation;

/// <summary>
/// Coordinates the local settlement and continuous world under one clock. Inventory and storage
/// capacity are owned by the world settlement ledger; the colony is a local simulation view.
/// </summary>
internal sealed class WorldSimulation
{
    private float _simulationAccumulator;
    private float _blightInfectionAccumulatorSeconds;
    private const float FixedStepSeconds = 0.1f;
    private const float BlightInfectionIntervalSeconds = 10f;
    private const int MaxFixedStepsPerFrame = 8;

    public ColonySimulation Colony { get; private set; }
    public ContinuousWorldState SpatialWorld { get; private set; }
    public double ElapsedSeconds { get; private set; }
    public bool IsPaused { get; set; }
    public float SpeedMultiplier { get; private set; } = 1f;
    public ResearchManager ResearchManager { get; private set; } = null!;
    public CombatEngine CombatEngine { get; private set; } = null!;
    public CombatCoverGrid CombatCover { get; private set; } = null!;
    public WorkbenchManager Workshop { get; private set; } = null!;
    public IReadOnlyDictionary<int, NemesisRecord> Nemeses => _nemeses;
    public IReadOnlyList<PendingCombatLoot> PendingCombatLoot => _pendingCombatLoot.AsReadOnly();

    private readonly Dictionary<long, int> _combatByWorldEntity = [];
    private readonly Dictionary<int, NemesisRecord> _nemeses = [];
    private readonly List<PendingCombatLoot> _pendingCombatLoot = [];

    public WorldSimulation(int seed)
    {
        Colony = new ColonySimulation(seed);
        SpatialWorld = ContinuousWorldState.Generate(seed);
        Colony.BindWorldInventory(SpatialWorld, KorSettlementId(SpatialWorld));
        var database = new DatabaseBootstrap();
        string dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
        if (!Directory.Exists(dataDirectory)) dataDirectory = Path.Combine(Directory.GetCurrentDirectory(), "data");
        database.LoadOrGenerate(dataDirectory);
        ResearchManager = new ResearchManager(database, SpatialWorld.MacroWorld.Research);
        InitializeCombatSystems();
    }

    /// <summary>Advances the unified simulation clock. deltaSeconds is real elapsed time.</summary>
    public void Step(float deltaSeconds)
    {
        if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds), "Elapsed time must be finite and non-negative.");
        if (deltaSeconds == 0f || IsPaused) return;

        float scaledSeconds = deltaSeconds * SpeedMultiplier;
        if (!float.IsFinite(scaledSeconds))
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds), "Scaled elapsed time must be finite.");

        // Consume bounded fixed-step work per render frame. Excess time remains in the accumulator
        // instead of turning a long frame into an unbounded catch-up spike.
        _simulationAccumulator += scaledSeconds;
        int fixedSteps = 0;
        while (_simulationAccumulator + 1e-8f >= FixedStepSeconds && fixedSteps < MaxFixedStepsPerFrame)
        {
            Colony.Step(FixedStepSeconds);
            ProcessStorytellerThreats();
            SpatialWorld.SetInventoryCapacity(KorSettlementId(SpatialWorld), Colony.InventoryCapacity);
            // The world and colony consume the exact same fixed interval. Macro cells are separately
            // timesliced inside SpatialWorld.Step so no daily world turn monopolizes this loop.
            SpatialWorld.Step(FixedStepSeconds);
            ProcessBlightPressureThreats();
            ProcessMacroArrivals();
            SyncCombatCoverFromColony();
            MaterializeNearbyArmies();
            CombatEngine.Step(FixedStepSeconds, CombatCover);
            ApplyCombatSiege(FixedStepSeconds);
            ApplyCombatConsequences();
            _blightInfectionAccumulatorSeconds += FixedStepSeconds;
            if (_blightInfectionAccumulatorSeconds >= BlightInfectionIntervalSeconds)
            {
                _blightInfectionAccumulatorSeconds -= BlightInfectionIntervalSeconds;
                Colony.ApplyBlightInfectionTick();
            }
            SyncCombatantPawnPositions();
            Workshop.Advance(1);
            ResearchManager.AddProgress(1f);
            FlushPendingCombatLoot();
            _simulationAccumulator -= FixedStepSeconds;
            fixedSteps++;
        }
        ElapsedSeconds = SpatialWorld.ClockSeconds;
    }

    private void ProcessMacroArrivals()
    {
        var macro = SpatialWorld.MacroWorld;
        // Arrivals is a pending-work queue: completed entries are removed immediately.
        // Avoid sorting/rescanning every historical caravan arrival on every fixed step.
        int index = 0;
        while (index < macro.Arrivals.Count)
        {
            var arrival = macro.Arrivals[index];
            if (macro.ConsumedArrivalIds.Contains(arrival.Id))
            {
                macro.Arrivals.RemoveAt(index);
                macro.ConsumedArrivalIds.Remove(arrival.Id);
                continue;
            }
            if (arrival.TargetCellId < 1 || arrival.TargetCellId > macro.Cells.Length)
            {
                macro.Arrivals.RemoveAt(index);
                continue;
            }
            var target = macro.Cells[arrival.TargetCellId - 1];
            if (!target.IsLocalMapGenerated)
            {
                index++;
                continue;
            }

            string faction = macro.FactionIds.FirstOrDefault(pair => pair.Value == arrival.FactionId).Key ?? "Unknown";
            var cargo = new Dictionary<ResourceKind, int>();
            foreach (var item in arrival.Cargo)
            {
                ResourceKind? localResource = item.Key switch
                {
                    MacroResource.Food => ResourceKind.Food,
                    MacroResource.Wood => ResourceKind.Wood,
                    MacroResource.Stone => ResourceKind.Stone,
                    MacroResource.Alloy => ResourceKind.Alloy,
                    MacroResource.Components => ResourceKind.Components,
                    _ => null
                };
                if (localResource.HasValue && item.Value > 0)
                    cargo[localResource.Value] = checked((int)Math.Ceiling(item.Value));
            }

            int desiredVisitors = arrival.IsRefugeeWave ? Math.Clamp(arrival.Population, 1, 12) : 1;
            while (arrival.SpawnedVisitorCount < desiredVisitors)
            {
                int spawned = Colony.SpawnExternalVisitor(faction, arrival.IsRefugeeWave ? null : cargo);
                if (spawned == 0) break;
                arrival.SpawnedVisitorCount++;
            }

            if (arrival.SpawnedVisitorCount >= desiredVisitors)
            {
                macro.Arrivals.RemoveAt(index);
                macro.ConsumedArrivalIds.Remove(arrival.Id);
                Colony.SetMessage(arrival.IsRefugeeWave
                    ? $"{desiredVisitors} refugees from {faction} reached the colony. Select a visitor to admit or dismiss them."
                    : $"A {faction} caravan reached the colony. Its trader pawn carries the strategic cargo for barter.");
            }
            else
            {
                // Keep only the unfinished arrival in the queue and retry on a later tick.
                index++;
            }
        }
    }

    private void ProcessStorytellerThreats()
    {
        var home = SpatialWorld.Entities.FirstOrDefault(entity =>
            entity.Kind == WorldEntityKind.Settlement && entity.Faction == "Kor");
        if (home == null) return;
        foreach (var incident in Colony.Storyteller.Events
                     .Where(item => item.Code == "wealth_attracts_attention" && item.ThreatPoints > 0)
                     .OrderBy(item => item.Id))
        {
            string prefix = $"Kor Wealth Raid {incident.Id}";
            if (SpatialWorld.Entities.Any(entity => entity.Name.StartsWith(prefix, StringComparison.Ordinal))) continue;
            int units = Math.Clamp(incident.ThreatUnits, 1, 10);
            var random = new Random(unchecked(SpatialWorld.Seed * 397 ^ (int)incident.Id));
            for (int i = 0; i < units; i++)
            {
                double x = Math.Clamp(home.X + random.Next(-4_000, 4_001), 0, ContinuousWorldState.WorldExtent);
                double y = Math.Clamp(home.Y + random.Next(-4_000, 4_001), 0, ContinuousWorldState.WorldExtent);
                if (Math.Abs(x - home.X) + Math.Abs(y - home.Y) < 1_500)
                    x = Math.Clamp(x + 2_000, 0, ContinuousWorldState.WorldExtent);
                var raider = SpatialWorld.AddEntity(WorldEntityKind.Army, $"{prefix} / Unit {i + 1}", "Raiders", x, y);
                var route = WorldRoutePlanner.FindRoute(SpatialWorld, x, y, home.X, home.Y, 40);
                double straightDistance = Math.Sqrt(Math.Pow(home.X - x, 2) + Math.Pow(home.Y - y, 2));
                double speed = straightDistance / Math.Max(1, route.EstimatedSeconds);
                SpatialWorld.OrderMove(raider.Id, home.X, home.Y, Math.Max(1, speed));
            }
            Colony.SetMessage($"STORYTELLER RAID: {units} hostile units generated from {incident.ThreatPoints} threat points and advancing toward Kor.");
        }
    }

    /// <summary>
    /// Runs an independent Blight escalation check at fixed strategic-tick intervals. Wealth is
    /// only a scarcity input: it never scales Blight aggression down when the colony is damaged.
    /// The incident key is encoded in world entities so saves naturally preserve cooldown state.
    /// </summary>
    private void ProcessBlightPressureThreats()
    {
        var home = SpatialWorld.Entities.FirstOrDefault(entity =>
            entity.Kind == WorldEntityKind.Settlement && entity.Faction == "Kor");
        if (home == null) return;

        long worldTicks = (long)Math.Floor(SpatialWorld.ClockSeconds * TickManager.TicksPerSecond + 1e-6);
        const long incidentIntervalTicks = 6_000;
        long incidentIndex = worldTicks / incidentIntervalTicks;
        if (incidentIndex <= 0 || worldTicks % incidentIntervalTicks > 6) return;

        string prefix = $"Kor Blight Raid {incidentIndex}";
        if (SpatialWorld.Entities.Any(entity => entity.Name.StartsWith(prefix, StringComparison.Ordinal))) return;

        double localPressure = SpatialWorld.Ecology.EnvironmentalPressureAt(home.X, home.Y);
        int macroX = Math.Clamp((int)(home.X / MacroWorldState.WorldCellSize), 0, MacroWorldState.Columns - 1);
        int macroY = Math.Clamp((int)(home.Y / MacroWorldState.WorldCellSize), 0, MacroWorldState.Rows - 1);
        double macroPressure = Math.Clamp(SpatialWorld.MacroWorld.GetCell(macroX, macroY).BlightPresence, 0d, 1d);
        var assessment = BlightThreatModel.Assess(localPressure, macroPressure, Colony.Wealth.TotalWealth);
        if (!assessment.ShouldTrigger) return;

        int units = Math.Clamp((assessment.ThreatPoints + 24) / 25, 1, 10);
        var random = new Random(unchecked(SpatialWorld.Seed * 7919 ^ (int)incidentIndex * 104729));
        for (int i = 0; i < units; i++)
        {
            double x = Math.Clamp(home.X + random.Next(-5_000, 5_001), 0, ContinuousWorldState.WorldExtent);
            double y = Math.Clamp(home.Y + random.Next(-5_000, 5_001), 0, ContinuousWorldState.WorldExtent);
            if (Math.Abs(x - home.X) + Math.Abs(y - home.Y) < 1_500)
                x = Math.Clamp(x + 2_000, 0, ContinuousWorldState.WorldExtent);

            var blightRaid = SpatialWorld.AddEntity(
                WorldEntityKind.Army, $"{prefix} / Unit {i + 1}", "Blight", x, y);
            var route = WorldRoutePlanner.FindRoute(SpatialWorld, x, y, home.X, home.Y, 40);
            double straightDistance = Math.Sqrt(Math.Pow(home.X - x, 2) + Math.Pow(home.Y - y, 2));
            double speed = straightDistance / Math.Max(1, route.EstimatedSeconds);
            SpatialWorld.OrderMove(blightRaid.Id, home.X, home.Y, Math.Max(1, speed));
        }

        Colony.SetMessage(
            $"BLIGHT PRESSURE {assessment.Pressure:P0}: {units} hostile units advancing toward Kor " +
            $"({assessment.ThreatPoints} pressure points; independent of colony wealth).");
    }

    public void SetSpeed(float multiplier)
    {
        if (!float.IsFinite(multiplier) || multiplier is < 0.25f or > 4f)
            throw new ArgumentOutOfRangeException(nameof(multiplier), "Speed must be between 0.25x and 4x.");
        SpeedMultiplier = multiplier;
    }

    private static long KorSettlementId(ContinuousWorldState world)
        => world.Entities.Single(entity => entity.Kind == WorldEntityKind.Settlement && entity.Faction == "Kor").Id;

    private void InitializeCombatSystems()
    {
        CombatEngine = new CombatEngine();
        CombatCover = new CombatCoverGrid();
        long homeId = KorSettlementId(SpatialWorld);
        Workshop = new WorkbenchManager(
            new WorldWorkbenchInventory(SpatialWorld, homeId),
            researchId => ResearchManager.State.CompletedResearchIds.Contains(researchId));
        SyncCombatCoverFromColony();
    }

    private void SyncCombatCoverFromColony()
    {
        CombatCover.Clear();
        for (int y = 0; y < ColonySimulation.Height; y++)
        for (int x = 0; x < ColonySimulation.Width; x++)
        {
            var tile = Colony.TileAt(x, y);
            if (tile is null) continue;
            CoverType cover = tile.Terrain == TerrainKind.Rock || tile.Architecture == ArchitectureKind.Wall
                ? CoverType.High
                : tile.Architecture == ArchitectureKind.Door ? CoverType.Low
                : CoverType.None;
            if (cover == CoverType.None && Colony.Objects.Any(item => item.IsBuilt && item.Occupies(new CellPosition(x, y))))
                cover = CoverType.Low;
            CombatCover[x, y] = cover;
        }
    }

    private void MaterializeNearbyArmies()
    {
        var home = SpatialWorld.Entities.FirstOrDefault(entity =>
            entity.Kind == WorldEntityKind.Settlement && entity.Faction == "Kor");
        if (home is null) return;

        foreach (var army in SpatialWorld.Entities
                     .Where(entity => entity.Kind is WorldEntityKind.Army or WorldEntityKind.BlightFront)
                     .OrderBy(entity => entity.Id).ToArray())
        {
            double dx = army.X - home.X, dy = army.Y - home.Y;
            if (dx * dx + dy * dy > 3_000d * 3_000d) continue;
            if (_combatByWorldEntity.TryGetValue(army.Id, out int linkedId) &&
                CombatEngine.TryGetEntity(linkedId, out _)) continue;

            int factionId = army.Faction == "Kor" ? 1 :
                army.Faction.Equals("Blight", StringComparison.OrdinalIgnoreCase) || army.Kind == WorldEntityKind.BlightFront ? 99 : 2;
            int seed = unchecked(SpatialWorld.Seed ^ (int)(army.Id * 397L));
            var combatant = FactionLoadoutGenerator.Create(factionId, seed);
            // Use seeded positions within a short, bounded engagement area so incoming world forces
            // become a visible local encounter instead of remaining permanently outside local target acquisition.
            if (factionId == 1)
            {
                combatant.X = 18.0f + (seed & 3) * 0.35f;
                combatant.Y = 10.0f + ((seed >> 3) & 7) * 0.55f;
            }
            else
            {
                combatant.X = 12.0f + (seed & 3) * 0.30f;
                combatant.Y = 10.0f + ((seed >> 3) & 7) * 0.55f;
            }
            combatant.SourceWorldEntityId = army.Id;
            combatant.SourcePawnId = -1;
            combatant.NameSeed = seed;
            combatant.KilledPawnId = -1;
            if (!CombatEngine.TrySpawn(combatant, out int combatId)) continue;

            _combatByWorldEntity[army.Id] = combatId;
            if (factionId != 1 && !_nemeses.ContainsKey(combatId))
            {
                int power = combatant.Type == CombatEntityType.BlightTitan ? 10 :
                    combatant.Type == CombatEntityType.HeavyTank ? 7 :
                    combatant.Type == CombatEntityType.BlightBeast ? 4 : 2;
                _nemeses[combatId] = new NemesisRecord(combatId, seed, factionId, power, -1,
                    combatant.HasBlightMutation, combatant.LostEye, combatant.Type);
                Colony.SetMessage($"CONTACT: {NemesisNameGenerator.GenerateFullTitle(_nemeses[combatId])} entered local combat.");
            }
            else if (factionId == 1)
                Colony.SetMessage($"Kor Vanguard materialized as a local combatant for army #{army.Id}.");
        }

        // A world entity may have been removed by combat or a previous load; release its stale link.
        foreach (long worldId in _combatByWorldEntity.Keys.Where(id =>
                     SpatialWorld.Entities.All(entity => entity.Id != id)).ToArray())
            _combatByWorldEntity.Remove(worldId);
    }


    /// <summary>
    /// Hostiles without a living combatant target advance on the nearest remaining local structure.
    /// A hit enters the same colony model that owns construction, storage, cover and persistence.
    /// </summary>
    private void ApplyCombatSiege(float deltaSeconds)
    {
        // Avoid a full map scan/sort if every hostile still has a living combat target.
        var attackers = CombatEngine.Entities
            .Where(entity => entity.FactionId != 1 && entity.TargetEntityId < 0)
            .OrderBy(entity => entity.Id).ToArray();
        if (attackers.Length == 0) return;

        var targets = new List<(float X, float Y, int CellX, int CellY)>();
        foreach (var item in Colony.Objects.Where(item => item.IsBuilt).OrderBy(item => item.Id))
        {
            int cellX = Math.Clamp(item.Origin.X + item.Width / 2, 0, ColonySimulation.Width - 1);
            int cellY = Math.Clamp(item.Origin.Y + item.Height / 2, 0, ColonySimulation.Height - 1);
            targets.Add((cellX + 0.5f, cellY + 0.5f, cellX, cellY));
        }

        for (int y = 0; y < ColonySimulation.Height; y++)
        for (int x = 0; x < ColonySimulation.Width; x++)
        {
            var tile = Colony.TileAt(x, y);
            if (tile is null || tile.Architecture == ArchitectureKind.None || Colony.ObjectAt(x, y) is not null)
                continue;
            targets.Add((x + 0.5f, y + 0.5f, x, y));
        }

        if (targets.Count == 0) return;
        foreach (var attacker in attackers)
        {
            var nearestTarget = targets[0];
            float nearestDistanceSquared = float.PositiveInfinity;
            foreach (var target in targets)
            {
                float dx = target.X - attacker.X;
                float dy = target.Y - attacker.Y;
                float distanceSquared = dx * dx + dy * dy;
                // Targets have a stable order (object ID then row-major tile index), so the
                // strict comparison also gives deterministic tie-breaking without per-attacker sorting.
                if (distanceSquared < nearestDistanceSquared)
                {
                    nearestDistanceSquared = distanceSquared;
                    nearestTarget = target;
                }
            }

            float nearestDistance = MathF.Sqrt(nearestDistanceSquared);
            if (nearestDistance > 1.25f)
            {
                float dx = nearestTarget.X - attacker.X;
                float dy = nearestTarget.Y - attacker.Y;
                float step = MathF.Min(nearestDistance - 1f,
                    MathF.Max(0.35f, attacker.Speed) * deltaSeconds);
                float length = MathF.Max(0.001f, nearestDistance);
                CombatEngine.TrySetPosition(attacker.Id,
                    Math.Clamp(attacker.X + dx / length * step, 0.01f, CombatCoverGrid.Width - 0.01f),
                    Math.Clamp(attacker.Y + dy / length * step, 0.01f, CombatCoverGrid.Height - 0.01f));
                continue;
            }

            if (attacker.StructureAttackCooldown > 0f) continue;
            float damage = attacker.EquippedWeapon switch
            {
                CombatWeaponType.TankCannon => 24f,
                CombatWeaponType.Plasma => 14f,
                CombatWeaponType.Rifle => 7f,
                CombatWeaponType.Pistol => 5f,
                _ => attacker.Type == CombatEntityType.BlightTitan ? 28f : 9f
            };
            // Siege volleys consume the same ammunition as unit combat. Empty ranged weapons
            // fall back to weak close-quarters structure damage instead of firing infinitely.
            bool hasAmmunition = attacker.EquippedWeapon == CombatWeaponType.Melee ||
                CombatEngine.TryConsumeAmmo(attacker.Id);
            if (!hasAmmunition) damage = 2f;
            bool blightAttack = attacker.FactionId == 99 ||
                attacker.Type is CombatEntityType.BlightBeast or CombatEntityType.BlightTitan;
            Colony.DamageStructureAt(nearestTarget.CellX, nearestTarget.CellY, damage, blightAttack);
            CombatEngine.TrySetStructureAttackCooldown(attacker.Id,
                attacker.Type == CombatEntityType.BlightTitan ? 1.5f : 2f);
        }
    }

    public int RepairDamagedStructures()
    {
        int repaired = Colony.RepairAllDamagedStructures();
        SyncCombatCoverFromColony();
        return repaired;
    }

    private void ApplyCombatConsequences()
    {
        foreach (var death in CombatEngine.Deaths)
        {
            if (death.Entity.SourcePawnId > 0)
                Colony.RemoveCombatCasualty(death.Entity.SourcePawnId);

            if (death.KillerId > 0 && _nemeses.TryGetValue(death.KillerId, out var nemesis))
            {
                int kills = nemesis.KillCount + 1;
                _nemeses[death.KillerId] = nemesis with
                {
                    KillCount = kills,
                    PowerLevel = Math.Min(20, nemesis.PowerLevel + 1),
                    KilledPawnId = death.Entity.SourcePawnId > 0 ? death.Entity.SourcePawnId : nemesis.KilledPawnId,
                    LostEye = nemesis.LostEye || (kills >= 3 && (nemesis.NameSeed & 1) == 0)
                };
            }

            if (death.Entity.SourceWorldEntityId > 0)
            {
                long worldId = death.Entity.SourceWorldEntityId;
                bool wasHostile = death.Entity.FactionId != 1;
                SpatialWorld.RemoveEntity(worldId);
                _combatByWorldEntity.Remove(worldId);
                string title = _nemeses.TryGetValue(death.Entity.Id, out var record)
                    ? NemesisNameGenerator.GenerateFullTitle(record)
                    : $"{death.Entity.Type} #{death.Entity.Id}";
                Colony.SetMessage(wasHostile ? $"THREAT DEFEATED: {title}." : $"Kor combat unit #{death.Entity.Id} was lost.");
            }
        }

        foreach (var drop in CombatEngine.DeathDrops)
        {
            if (drop.Amount > 0)
                _pendingCombatLoot.Add(new PendingCombatLoot(drop.ItemId, drop.Amount, drop.EntityId));
        }
    }

    private void FlushPendingCombatLoot()
    {
        long homeId = KorSettlementId(SpatialWorld);
        int index = 0;
        while (index < _pendingCombatLoot.Count)
        {
            var loot = _pendingCombatLoot[index];
            bool stored = loot.ItemId switch
            {
                WorkbenchManager.Alloy => SpatialWorld.TryAddInventory(homeId, WorldResourceKind.Alloy, loot.Amount),
                WorkbenchManager.Components => SpatialWorld.TryAddInventory(homeId, WorldResourceKind.Components, loot.Amount),
                _ => SpatialWorld.TryAddItem(homeId, loot.ItemId, loot.Amount)
            };
            if (stored) _pendingCombatLoot.RemoveAt(index);
            else index++;
        }
    }

    public int TryDeployColonists()
    {
        int deployed = 0;
        long homeId = KorSettlementId(SpatialWorld);
        foreach (var worker in Colony.Workers.Where(worker => !worker.IsExternalVisitor && !worker.IsLeaving)
                     .OrderBy(worker => worker.Id).ToArray())
        {
            if (CombatEngine.Entities.Any(entity => entity.SourcePawnId == worker.Id)) continue;
            bool hasRifle = SpatialWorld.ItemAmountAt(homeId, WorkbenchManager.InfantryRifle) > 0;
            if (hasRifle && !SpatialWorld.TryTakeItem(homeId, WorkbenchManager.InfantryRifle, 1)) hasRifle = false;

            var combatant = FactionLoadoutGenerator.Create(1, unchecked(SpatialWorld.Seed ^ worker.Id * 104729));
            combatant.X = Math.Clamp(worker.X, 0.01f, CombatCoverGrid.Width - 0.01f);
            combatant.Y = Math.Clamp(worker.Y, 0.01f, CombatCoverGrid.Height - 0.01f);
            combatant.SourcePawnId = worker.Id;
            combatant.SourceWorldEntityId = 0;
            combatant.KilledPawnId = -1;
            if (!hasRifle)
            {
                combatant.EquippedWeapon = CombatWeaponType.Pistol;
                combatant.MaxAmmo = combatant.CurrentAmmo = 12;
            }
            if (!CombatEngine.TrySpawn(combatant, out int combatId))
            {
                if (hasRifle) SpatialWorld.TryAddItem(homeId, WorkbenchManager.InfantryRifle, 1);
                break;
            }
            if (!Colony.PrepareWorkerForCombat(worker.Id))
            {
                CombatEngine.TryRemoveEntity(combatId);
                if (hasRifle) SpatialWorld.TryAddItem(homeId, WorkbenchManager.InfantryRifle, 1);
                continue;
            }
            deployed++;
        }
        Colony.SetMessage(deployed > 0
            ? $"{deployed} colonists deployed as local defenders; equipped rifles were drawn from settlement inventory."
            : "No eligible colonists could be deployed.");
        return deployed;
    }

    private void SyncCombatantPawnPositions()
    {
        foreach (var entity in CombatEngine.Entities)
            if (entity.SourcePawnId > 0)
                Colony.SetWorkerCombatPosition(entity.SourcePawnId, entity.X, entity.Y);
    }

    public string GetCombatantName(CombatEntity entity)
        => _nemeses.TryGetValue(entity.Id, out var nemesis)
            ? NemesisNameGenerator.GenerateFullTitle(nemesis)
            : entity.Type switch
            {
                CombatEntityType.HeavyTank => "Kor Heavy Tank",
                CombatEntityType.Vehicle => "Combat Vehicle",
                CombatEntityType.BlightTitan => "Blight Titan",
                CombatEntityType.BlightBeast => "Blight Beast",
                _ => entity.FactionId == 1 ? "Kor Vanguard" : "Hostile Trooper"
            };

    public bool TryStartCraft(string recipeId, out long jobId)
    {
        jobId = 0;
        if (!Colony.Objects.Any(item => item.Kind == PlaceableKind.Workbench && item.IsBuilt))
        {
            Colony.SetMessage("Crafting requires a completed workbench in the colony.");
            return false;
        }
        if (!Workshop.TryStartCraft(recipeId, out jobId))
        {
            Colony.SetMessage($"Cannot start recipe '{recipeId}': research or settlement materials are missing.");
            return false;
        }
        Colony.SetMessage($"Workshop job #{jobId} started: {recipeId}. Materials were deducted from the settlement ledger.");
        return true;
    }

    public bool TryBuildVehicle(out int entityId)
    {
        entityId = -1;
        if (!Colony.Objects.Any(item => item.Kind == PlaceableKind.Workbench && item.IsBuilt) ||
            !Workshop.IsResearchUnlocked("tech_armored_vehicles")) return false;
        var occupied = CombatEngine.Entities
            .Select(entity => ((int)entity.X, (int)entity.Y)).ToHashSet();
        for (int i = 0; i < CombatCoverGrid.CellCount; i++)
        {
            int x = i % CombatCoverGrid.Width, y = i / CombatCoverGrid.Width;
            var tile = Colony.TileAt(x, y);
            var cell = new CellPosition(x, y);
            if (tile is null || tile.Terrain != TerrainKind.Ground || tile.Architecture == ArchitectureKind.Wall ||
                occupied.Contains((x, y)) || Colony.Objects.Any(item => item.IsBuilt && item.Occupies(cell)) ||
                Colony.Deposits.Any(deposit => deposit.Cell == cell && deposit.Amount > 0) ||
                Colony.Workers.Any(worker => (int)worker.X == x && (int)worker.Y == y)) continue;
            if (Workshop.TryBuildVehicle(CombatEngine, i, out entityId)) return true;
        }
        return false;
    }

    public string SerializeCombatRuntime()
    {
        var snapshot = new CombatRuntimeSnapshot(
            CombatEngine.Snapshot(CombatCover),
            Workshop.SnapshotState(),
            _nemeses.Values.OrderBy(record => record.Id).ToList(),
            _pendingCombatLoot.ToList(),
            _blightInfectionAccumulatorSeconds);
        return JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true });
    }

    private void RestoreCombatRuntime(string? json)
    {
        _combatByWorldEntity.Clear();
        _nemeses.Clear();
        _pendingCombatLoot.Clear();
        _blightInfectionAccumulatorSeconds = 0f;
        if (string.IsNullOrWhiteSpace(json)) return;
        CombatRuntimeSnapshot snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<CombatRuntimeSnapshot>(json,
                new JsonSerializerOptions { IncludeFields = true })
                ?? throw new InvalidDataException("Combat runtime save is empty.");
        }
        catch (JsonException error)
        {
            throw new InvalidDataException($"Combat runtime save is invalid JSON: {error.Message}", error);
        }
        if (!float.IsFinite(snapshot.BlightInfectionAccumulatorSeconds) ||
            snapshot.BlightInfectionAccumulatorSeconds < 0f ||
            snapshot.BlightInfectionAccumulatorSeconds >= BlightInfectionIntervalSeconds)
            throw new InvalidDataException("Combat runtime has an invalid Blight infection accumulator.");
        _blightInfectionAccumulatorSeconds = snapshot.BlightInfectionAccumulatorSeconds;
        CombatEngine.Restore(snapshot.Engine, CombatCover);
        Workshop.RestoreState(snapshot.Workshop);
        foreach (var record in snapshot.Nemeses ?? [])
        {
            if (record.Id <= 0 || record.PowerLevel < 0 || record.KillCount < 0 ||
                !Enum.IsDefined(record.EntityType) || !_nemeses.TryAdd((int)record.Id, record))
                throw new InvalidDataException("Combat runtime contains an invalid or duplicate Nemesis record.");
        }
        foreach (var loot in snapshot.PendingLoot ?? [])
        {
            if (string.IsNullOrWhiteSpace(loot.ItemId) || loot.Amount <= 0)
                throw new InvalidDataException("Combat runtime contains invalid pending loot.");
            _pendingCombatLoot.Add(loot);
        }
        foreach (var entity in CombatEngine.Entities)
        {
            if (entity.SourceWorldEntityId > 0 &&
                SpatialWorld.Entities.Any(worldEntity => worldEntity.Id == entity.SourceWorldEntityId))
                _combatByWorldEntity[entity.SourceWorldEntityId] = entity.Id;
            if (entity.SourcePawnId > 0 && Colony.Workers.Any(worker => worker.Id == entity.SourcePawnId))
                Colony.PrepareWorkerForCombat(entity.SourcePawnId);
        }
        SyncCombatCoverFromColony();
    }



    public void Restore(LoadedGameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        Colony = session.Colony;
        SpatialWorld = session.ContinuousWorld;
        Colony.BindWorldInventory(SpatialWorld, KorSettlementId(SpatialWorld));
        var database = new DatabaseBootstrap();
        string dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
        if (!Directory.Exists(dataDirectory)) dataDirectory = Path.Combine(Directory.GetCurrentDirectory(), "data");
        database.LoadOrGenerate(dataDirectory);
        ResearchManager = new ResearchManager(database, SpatialWorld.MacroWorld.Research);
        InitializeCombatSystems();
        RestoreCombatRuntime(session.CombatRuntimeJson);
        ElapsedSeconds = SpatialWorld.ClockSeconds;
        _simulationAccumulator = 0f;
    }
}
