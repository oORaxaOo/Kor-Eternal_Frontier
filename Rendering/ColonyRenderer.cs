using KorTheBlight.Simulation;
using KorTheBlight.Simulation.Combat;
using KorTheBlight.Simulation.Research;
using KorTheBlight.WorldModel;
using SkiaSharp;

namespace KorTheBlight.Rendering;

/// <summary>
/// Minimal symbol renderer for the first colony foundation. It draws state but never changes it.
/// </summary>
internal sealed class ColonyRenderer : IDisposable
{
    private static readonly SKColor Background = new(10, 13, 14);
    private static readonly SKColor Panel = new(19, 24, 25);
    private static readonly SKColor Border = new(64, 75, 72);
    private static readonly SKColor TextColor = new(230, 229, 216);
    private static readonly SKColor MutedColor = new(160, 173, 165);
    private static readonly SKColor Crimson = new(192, 42, 54);
    private static readonly SKColor Gold = new(216, 177, 89);
    private static readonly SKColor WorkerColor = new(216, 225, 204);

    private readonly SKPaint _fill = new() { IsAntialias = true };
    private readonly SKPaint _line = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
    private readonly SKPaint _text = new() { IsAntialias = true, Color = TextColor };
    private readonly SKPaint _muted = new() { IsAntialias = true, Color = MutedColor };
    private readonly SKFont _normalFont = new(SKTypeface.FromFamilyName("sans-serif"), 13);
    private readonly SKFont _smallFont = new(SKTypeface.FromFamilyName("sans-serif"), 11);
    private readonly SKFont _glyphFont = new(SKTypeface.FromFamilyName("monospace"), 14);
    private readonly SKFont _headingFont = new(SKTypeface.FromFamilyName("sans-serif", SKFontStyle.Bold), 17);
    private readonly SKFont _titleFont = new(SKTypeface.FromFamilyName("sans-serif", SKFontStyle.Bold), 22);

    public void Draw(SKCanvas canvas, ColonySimulation simulation, float cameraX, float cameraY, float zoom,
        ArchitectureKind architectureMode, PlaceableKind objectType, bool placingObject, bool excavationMode, bool demolitionMode, int buildMenuPage, bool paused, float speedMultiplier,
        ContinuousWorldState spatialWorld, float worldCameraX, float worldCameraY, long? selectedWorldEntityId, int? selectedMacroCellId, WorldResourceKind? selectedShipmentResource,
        float mouseScreenX, float mouseScreenY, bool factionDashboardOpen, string? selectedFactionName, int factionTab, bool workMatrixOpen, int workerInspectorTab, int workMatrixPage,
        WorldSimulation world, bool systemsPanelOpen, int systemsTab, int? selectedCombatEntityId)
    {
        canvas.Clear(Background);
        bool worldView = zoom < .65f;
        if (worldView) DrawContinuousWorld(canvas, spatialWorld, worldCameraX, worldCameraY, zoom, selectedWorldEntityId);
        else
        {
            DrawMap(canvas, simulation, cameraX, cameraY, zoom);
            DrawDeposits(canvas, simulation, cameraX, cameraY, zoom);
            DrawObjects(canvas, simulation, cameraX, cameraY, zoom);
            DrawWorkers(canvas, simulation, cameraX, cameraY, zoom);
            DrawCombatEntities(canvas, world, cameraX, cameraY, zoom, selectedCombatEntityId);
            DrawPlacementPreview(canvas, simulation, cameraX, cameraY, zoom, architectureMode, objectType, placingObject, excavationMode, demolitionMode, mouseScreenX, mouseScreenY);
        }
        DrawHud(canvas, simulation, architectureMode, objectType, placingObject, excavationMode, demolitionMode, buildMenuPage, paused, speedMultiplier, worldView, spatialWorld, selectedWorldEntityId, selectedShipmentResource);
        DrawWorldTicker(canvas, spatialWorld);
        if (worldView && selectedMacroCellId.HasValue) DrawMacroCellInspector(canvas, spatialWorld, selectedMacroCellId.Value);
        DrawFactionButton(canvas, factionDashboardOpen);
        DrawWorkMatrixButton(canvas, workMatrixOpen);
        DrawSystemsButton(canvas, systemsPanelOpen);
        if (!worldView && !workMatrixOpen && simulation.SelectedWorkerId.HasValue)
            DrawWorkerInspector(canvas, simulation, workerInspectorTab);
        if (factionDashboardOpen) DrawFactionDashboard(canvas, spatialWorld, selectedFactionName, factionTab);
        if (workMatrixOpen) DrawWorkMatrix(canvas, simulation, workMatrixPage);
        if (systemsPanelOpen) DrawSystemsPanel(canvas, world, systemsTab, selectedCombatEntityId);
    }

    private void DrawMacroGrid(SKCanvas canvas, ContinuousWorldState world, float cameraX, float cameraY, float zoom, float scale)
    {
        var macro = world.MacroWorld;
        if (macro.Cells is null || macro.Cells.Length != MacroWorldState.CellCount) return;
        float tilePixels = MacroWorldState.WorldCellSize * scale;
        if (tilePixels < 2.2f) return;
        var view = new SKRect(0, 62, 770, 650);
        var factionNames = macro.FactionIds.ToDictionary(pair => pair.Value, pair => pair.Key);
        canvas.Save();
        canvas.ClipRect(view);
        foreach (ref readonly var cell in macro.Cells.AsSpan())
        {
            double leftWorld = cell.X * MacroWorldState.WorldCellSize;
            double topWorld = cell.Y * MacroWorldState.WorldCellSize;
            var topLeft = ContinuousWorldProjection.WorldToScreen(leftWorld, topWorld, cameraX, cameraY, zoom);
            var bottomRight = ContinuousWorldProjection.WorldToScreen(leftWorld + MacroWorldState.WorldCellSize, topWorld + MacroWorldState.WorldCellSize, cameraX, cameraY, zoom);
            if (bottomRight.X < view.Left || topLeft.X > view.Right || bottomRight.Y < view.Top || topLeft.Y > view.Bottom) continue;
            SKColor ownerColor = cell.OwningFactionId == 0 ? new SKColor(20, 25, 24, 45)
                : factionNames.GetValueOrDefault(cell.OwningFactionId, string.Empty) switch
                {
                    "Kor" => new SKColor(192, 42, 54, 38),
                    "Ember Compact" => new SKColor(220, 105, 53, 38),
                    "Verdant Pact" => new SKColor(75, 156, 83, 38),
                    "Iron Dominion" => new SKColor(130, 143, 154, 45),
                    "Freeholds" => new SKColor(205, 183, 111, 38),
                    "Dawn Assembly" => new SKColor(92, 143, 197, 38),
                    _ => new SKColor(150, 150, 150, 35)
                };
            _fill.Color = ownerColor;
            var rect = new SKRect(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
            canvas.DrawRect(rect, _fill);
            if (cell.BlightPresence > 0.2f)
            {
                _fill.Color = new SKColor(177, 22, 58, (byte)Math.Clamp(25 + cell.BlightPresence * 105, 0, 130));
                canvas.DrawRect(rect, _fill);
            }
            if (tilePixels >= 5.5f)
            {
                _line.Color = new SKColor(8, 13, 13, 90);
                _line.StrokeWidth = 0.6f;
                canvas.DrawRect(rect, _line);
            }
        }
        canvas.Restore();
    }

    private void DrawMacroCellInspector(SKCanvas canvas, ContinuousWorldState world, int cellId)
    {
        if (cellId < 1 || cellId > world.MacroWorld.Cells.Length) return;
        var cell = world.MacroWorld.Cells[cellId - 1];
        string faction = world.MacroWorld.FactionIds.FirstOrDefault(pair => pair.Value == cell.OwningFactionId).Key ?? "Neutral wilderness";
        var panel = new SKRect(784, 72, 1088, 366);
        _fill.Color = Panel;
        canvas.DrawRoundRect(panel, 8, 8, _fill);
        _line.Color = Gold;
        _line.StrokeWidth = 1.2f;
        canvas.DrawRoundRect(panel, 8, 8, _line);
        _text.Color = Gold;
        canvas.DrawText($"WORLD CELL {cell.CellId:00000}", 800, 96, SKTextAlign.Left, _headingFont, _text);
        _muted.Color = MutedColor;
        canvas.DrawText($"Grid {cell.X}, {cell.Y}  ·  {cell.Biome}", 800, 116, SKTextAlign.Left, _smallFont, _muted);
        _text.Color = TextColor;
        canvas.DrawText($"Faction: {faction}", 800, 143, SKTextAlign.Left, _normalFont, _text);
        canvas.DrawText($"Population: {cell.Population:N0}", 800, 166, SKTextAlign.Left, _normalFont, _text);
        canvas.DrawText($"Focus: {cell.ProductionFocus}", 800, 189, SKTextAlign.Left, _normalFont, _text);
        canvas.DrawText($"Fertility: {cell.Fertility:0.00}×", 800, 212, SKTextAlign.Left, _normalFont, _text);
        _text.Color = cell.BlightPresence >= 0.5f ? new SKColor(241, 104, 124) : TextColor;
        canvas.DrawText($"Blight pressure: {cell.BlightPresence:P0}", 800, 235, SKTextAlign.Left, _normalFont, _text);
        _muted.Color = MutedColor;
        canvas.DrawText($"Food {cell.FoodCount:0}  ·  Wood {cell.WoodCount:0}  ·  Stone {cell.StoneCount:0}", 800, 263, SKTextAlign.Left, _smallFont, _muted);
        canvas.DrawText($"Alloy {cell.AlloyCount:0}  ·  Energy {cell.EnergyCount:0}  ·  Components {cell.ComponentsCount:0}", 800, 282, SKTextAlign.Left, _smallFont, _muted);
        canvas.DrawText($"Market: Food {world.MacroWorld.Market.FoodPrice:0.00}×  Alloy {world.MacroWorld.Market.AlloyPrice:0.00}×", 800, 310, SKTextAlign.Left, _smallFont, _muted);
        canvas.DrawText(cell.IsLocalMapGenerated ? "LOCAL MAP: ACTIVE" : "LOCAL MAP: ABSTRACT SIMULATION", 800, 339, SKTextAlign.Left, _smallFont, cell.IsLocalMapGenerated ? _text : _muted);
    }

    private void DrawWorldTicker(SKCanvas canvas, ContinuousWorldState world)
    {
        var latest = world.MacroWorld.Notifications.LastOrDefault();
        _fill.Color = new SKColor(12, 17, 18, 238);
        canvas.DrawRoundRect(new SKRect(12, 38, 1088, 59), 4, 4, _fill);
        _line.Color = latest?.Important == true ? new SKColor(192, 42, 54, 180) : Border;
        _line.StrokeWidth = 1;
        canvas.DrawRoundRect(new SKRect(12, 38, 1088, 59), 4, 4, _line);
        _text.Color = latest?.Important == true ? new SKColor(244, 151, 151) : Gold;
        string message = latest is null
            ? $"WORLD TICKER  ·  Food {world.MacroWorld.Market.FoodPrice:0.00}×  Alloy {world.MacroWorld.Market.AlloyPrice:0.00}×  ·  Macro turn {world.MacroWorld.MacroTurnsProcessed}"
            : $"WORLD TICKER  ·  {latest.Category.ToUpperInvariant()}  ·  {latest.Message}";
        canvas.DrawText(message, 22, 53, SKTextAlign.Left, _smallFont, _text);
    }

    private void DrawContinuousWorld(SKCanvas canvas, ContinuousWorldState world, float cameraX, float cameraY, float zoom, long? selectedId)
    {
        var view = new SKRect(0, 62, 770, 650);
        _fill.Color = new SKColor(12, 19, 20);
        canvas.DrawRect(view, _fill);

        // Continuous-coordinate backdrop: no linked cells, hexes, or campaign regions.
        _line.Color = new SKColor(35, 54, 48, 170);
        _line.StrokeWidth = 1;
        float scale = ContinuousWorldProjection.Scale(zoom);
        for (int i = 0; i <= 10; i++)
        {
            float x = view.Left + i * view.Width / 10f;
            float y = view.Top + i * view.Height / 10f;
            canvas.DrawLine(x, view.Top, x, view.Bottom, _line);
            canvas.DrawLine(view.Left, y, view.Right, y, _line);
        }

        canvas.Save();
        canvas.ClipRect(view);
        // The strategic view shows generated terrain instead of a featureless coordinate grid.
        foreach (var region in world.RegionalMap.Regions)
        {
            double leftX = region.X * RegionalWorldMap.RegionSize;
            double topY = region.Y * RegionalWorldMap.RegionSize;
            var topLeft = ContinuousWorldProjection.WorldToScreen(leftX, topY, cameraX, cameraY, zoom);
            var bottomRight = ContinuousWorldProjection.WorldToScreen(leftX + RegionalWorldMap.RegionSize, topY + RegionalWorldMap.RegionSize, cameraX, cameraY, zoom);
            _fill.Color = region.Biome switch
            {
                WorldBiomeKind.Plains => new SKColor(49, 66, 48, 220),
                WorldBiomeKind.Forest => new SKColor(29, 71, 49, 225),
                WorldBiomeKind.Desert => new SKColor(105, 85, 49, 220),
                WorldBiomeKind.Tundra => new SKColor(100, 112, 108, 220),
                WorldBiomeKind.Wetland => new SKColor(37, 78, 76, 225),
                WorldBiomeKind.Mountain => new SKColor(83, 86, 83, 230),
                _ => new SKColor(45, 55, 48, 220)
            };
            var regionRect = new SKRect(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
            canvas.DrawRect(regionRect, _fill);
            _line.Color = new SKColor(13, 24, 22, 170);
            _line.StrokeWidth = 1;
            canvas.DrawRect(regionRect, _line);
        }
        DrawMacroGrid(canvas, world, cameraX, cameraY, zoom, scale);
        // Region-sized overlays make ecological pressure legible at strategic zoom.
        foreach (var hotspot in world.Ecology.Hotspots.Where(item => item.IsActive))
        {
            var center = ContinuousWorldProjection.WorldToScreen(hotspot.X, hotspot.Y, cameraX, cameraY, zoom);
            float radius = Math.Clamp((float)(hotspot.Radius * scale), 8f, 210f);
            byte alpha = (byte)(35 + hotspot.Intensity * 65);
            _fill.Color = new SKColor(170, 22, 39, alpha);
            canvas.DrawCircle(center.X, center.Y, radius, _fill);
            _line.Color = new SKColor(236, 54, 69, (byte)(90 + hotspot.Intensity * 130));
            _line.StrokeWidth = 1.5f;
            canvas.DrawCircle(center.X, center.Y, radius, _line);
        }
        foreach (var herd in world.Ecology.Herds)
        {
            var point = ContinuousWorldProjection.WorldToScreen(herd.X, herd.Y, cameraX, cameraY, zoom);
            _fill.Color = herd.Species switch
            {
                WildlifeSpecies.LonghornGrazer => new SKColor(184, 196, 137),
                WildlifeSpecies.AshbackGrazer => new SKColor(114, 176, 155),
                WildlifeSpecies.DuskStalker => new SKColor(173, 151, 207),
                _ => Gold
            };
            canvas.DrawCircle(point.X, point.Y, 3.5f, _fill);
            _text.Color = new SKColor(220, 224, 210);
            canvas.DrawText($"H{herd.Population}", point.X + 5, point.Y - 4, SKTextAlign.Left, _smallFont, _text);
        }
        foreach (var caravan in world.MacroWorld.Caravans)
        {
            if (caravan.TargetCellId < 1 || caravan.TargetCellId > world.MacroWorld.Cells.Length) continue;
            var targetCell = world.MacroWorld.Cells[caravan.TargetCellId - 1];
            var point = ContinuousWorldProjection.WorldToScreen((caravan.X + 0.5d) * MacroWorldState.WorldCellSize,
                (caravan.Y + 0.5d) * MacroWorldState.WorldCellSize, cameraX, cameraY, zoom);
            var target = ContinuousWorldProjection.WorldToScreen((targetCell.X + 0.5d) * MacroWorldState.WorldCellSize,
                (targetCell.Y + 0.5d) * MacroWorldState.WorldCellSize, cameraX, cameraY, zoom);
            _line.Color = new SKColor(216, 177, 89, 140);
            _line.StrokeWidth = 1.5f;
            canvas.DrawLine(point.X, point.Y, target.X, target.Y, _line);
            _fill.Color = Gold;
            canvas.DrawCircle(point.X, point.Y, 3.5f, _fill);
        }
        foreach (var entity in world.Entities)
        {
            var point = ContinuousWorldProjection.WorldToScreen(entity.X, entity.Y, cameraX, cameraY, zoom);
            if (entity.IsMoving)
            {
                var destination = ContinuousWorldProjection.WorldToScreen(entity.DestinationX!.Value, entity.DestinationY!.Value, cameraX, cameraY, zoom);
                _line.Color = entity.Kind == WorldEntityKind.BlightFront ? Crimson : Gold;
                _line.StrokeWidth = 1.5f;
                canvas.DrawLine(point.X, point.Y, destination.X, destination.Y, _line);
                _fill.Color = Gold;
                canvas.DrawCircle(destination.X, destination.Y, 3, _fill);
            }

            _fill.Color = entity.Kind switch
            {
                WorldEntityKind.Settlement when entity.Faction == "Kor" => Crimson,
                WorldEntityKind.Settlement => new SKColor(120, 157, 147),
                WorldEntityKind.ResourceSite => new SKColor(114, 160, 93),
                WorldEntityKind.Caravan => Gold,
                WorldEntityKind.Army => new SKColor(211, 210, 194),
                WorldEntityKind.BlightFront => new SKColor(112, 36, 47),
                _ => MutedColor
            };
            float radius = entity.Kind switch
            {
                WorldEntityKind.Settlement => 7,
                WorldEntityKind.BlightFront => 10,
                _ => 5
            };
            canvas.DrawCircle(point.X, point.Y, radius, _fill);
            if (entity.Id == selectedId)
            {
                _line.Color = Gold;
                _line.StrokeWidth = 2;
                canvas.DrawCircle(point.X, point.Y, radius + 5, _line);
            }
            _text.Color = entity.Kind == WorldEntityKind.BlightFront ? new SKColor(238, 116, 122) : TextColor;
            string entityLabel = entity.Kind == WorldEntityKind.ResourceSite && entity.Resource.HasValue
                ? $"{entity.Resource.Value} · {entity.ResourceAmount:0}"
                : entity.Name;
            canvas.DrawText(entityLabel, point.X + radius + 5, point.Y - 3, SKTextAlign.Left, _smallFont, _text);
            _muted.Color = MutedColor;
            canvas.DrawText(entity.Faction, point.X + radius + 5, point.Y + 10, SKTextAlign.Left, _smallFont, _muted);
        }
        canvas.Restore();

        _text.Color = Gold;
        canvas.DrawText($"OVERWORLD · SEED {world.Seed}", 18, 86, SKTextAlign.Left, _headingFont, _text);
        _muted.Color = MutedColor;
        canvas.DrawText($"World clock {world.ClockSeconds / 60:0} min  |  {world.Entities.Count} entities  |  {world.Entities.Count(e => e.Kind == WorldEntityKind.Settlement)} settlements  |  scale {scale:0.0000} px/unit", 18, 105, SKTextAlign.Left, _smallFont, _muted);
    }

    private void DrawMap(SKCanvas canvas, ColonySimulation simulation, float cameraX, float cameraY, float zoom)
    {
        float size = OrthogonalGridProjection.TileSize * zoom;
        for (int y = 0; y < ColonySimulation.Height; y++)
        for (int x = 0; x < ColonySimulation.Width; x++)
        {
            var point = OrthogonalGridProjection.TileCenter(x, y, cameraX, cameraY, zoom);
            var rect = new SKRect(point.X - size / 2, point.Y - size / 2, point.X + size / 2, point.Y + size / 2);
            if (rect.Right < 0 || rect.Left > 770 || rect.Bottom < 62 || rect.Top > 650) continue;

            var tile = simulation.TileAt(x, y);
            if (tile == null) continue;
            _fill.Color = tile.Terrain switch
            {
                TerrainKind.Rock => new SKColor(64, 70, 68),
                TerrainKind.Water => new SKColor(25, 54, 68),
                _ => ((x * 7 + y * 11) & 3) == 0 ? new SKColor(34, 43, 39) : new SKColor(29, 37, 34)
            };
            canvas.DrawRect(rect, _fill);

            _fill.Color = tile.Architecture switch
            {
                ArchitectureKind.Floor => new SKColor(68, 73, 66),
                ArchitectureKind.Wall => new SKColor(97, 81, 71),
                ArchitectureKind.Door => new SKColor(140, 102, 59),
                ArchitectureKind.Path => new SKColor(74, 70, 57),
                ArchitectureKind.Conduit => new SKColor(72, 55, 93),
                _ => SKColors.Transparent
            };
            if (tile.Architecture != ArchitectureKind.None)
                canvas.DrawRect(Inset(rect, size * 0.04f), _fill);
            if (tile.Architecture != ArchitectureKind.None && tile.IsBlightInfected)
            {
                _fill.Color = new SKColor(173, 28, 84, 72);
                canvas.DrawRect(rect, _fill);
            }
            if (tile.Architecture != ArchitectureKind.None && tile.Health < tile.MaxHealth)
            {
                _line.Color = tile.IsBlightInfected ? new SKColor(220, 52, 112) : Gold;
                _line.StrokeWidth = Math.Max(1.4f, zoom * 1.8f);
                canvas.DrawRect(Inset(rect, size * 0.08f), _line);
            }

            var order = simulation.TileOrderAt(x, y);
            if (order != null)
            {
                _line.Color = order.Status switch
                {
                    ConstructionStatus.UnderConstruction => Gold,
                    ConstructionStatus.WaitingForMaterials => Crimson,
                    ConstructionStatus.Blocked => new SKColor(110, 73, 76),
                    _ => new SKColor(177, 174, 148)
                };
                _line.StrokeWidth = Math.Max(1f, zoom * 1.4f);
                canvas.DrawRect(Inset(rect, size * 0.12f), _line);
                _fill.Color = order.Status switch
                {
                    ConstructionStatus.WaitingForMaterials => new SKColor(192, 42, 54, 65),
                    ConstructionStatus.UnderConstruction => new SKColor(216, 177, 89, 50),
                    _ => new SKColor(201, 179, 105, 25)
                };
                canvas.DrawRect(rect, _fill);
                if (zoom >= 0.82f && order.Status == ConstructionStatus.WaitingForMaterials)
                {
                    _text.Color = Crimson;
                    canvas.DrawText("!", point.X, point.Y + 4f * zoom, SKTextAlign.Center, _glyphFont, _text);
                }
            }

            var excavationOrder = simulation.ExcavationOrderAt(x, y);
            if (excavationOrder != null)
            {
                _line.Color = excavationOrder.Status switch
                {
                    ConstructionStatus.UnderConstruction => Gold,
                    ConstructionStatus.Blocked => new SKColor(110, 73, 76),
                    _ => Crimson
                };
                _line.StrokeWidth = Math.Max(1f, zoom * 1.5f);
                canvas.DrawRect(Inset(rect, size * 0.16f), _line);
                _fill.Color = excavationOrder.Status == ConstructionStatus.UnderConstruction
                    ? new SKColor(216, 177, 89, 55)
                    : new SKColor(192, 42, 54, 55);
                canvas.DrawRect(rect, _fill);
            }

            _line.Color = new SKColor(66, 79, 72, 170);
            _line.StrokeWidth = Math.Max(0.5f, zoom * 0.6f);
            canvas.DrawRect(rect, _line);

            // At settlement zoom, a compact glyph layer makes the map legible in the
            // Dwarf-Fortress-inspired symbolic style while retaining colour for quick scanning.
            if (zoom >= 0.82f)
            {
                string glyph = excavationOrder != null
                    ? excavationOrder.Status == ConstructionStatus.UnderConstruction ? "M" : "D"
                    : tile.Architecture switch
                {
                    ArchitectureKind.Floor => ".",
                    ArchitectureKind.Wall => "#",
                    ArchitectureKind.Door => "+",
                    ArchitectureKind.Path => ":",
                    ArchitectureKind.Conduit => "=",
                    _ => tile.Terrain switch
                    {
                        TerrainKind.Rock => "▒",
                        TerrainKind.Water => "~",
                        _ => string.Empty
                    }
                };
                if (glyph.Length > 0)
                {
                    _text.Color = tile.Architecture switch
                    {
                        ArchitectureKind.Wall => new SKColor(232, 204, 177),
                        ArchitectureKind.Door => Gold,
                        ArchitectureKind.Conduit => new SKColor(202, 162, 235),
                        _ when tile.Terrain == TerrainKind.Water => new SKColor(126, 191, 214),
                        _ => new SKColor(194, 202, 184)
                    };
                    canvas.DrawText(glyph, point.X, point.Y + 4f * zoom, SKTextAlign.Center, _glyphFont, _text);
                }
            }
        }
    }

    private void DrawDeposits(SKCanvas canvas, ColonySimulation simulation, float cameraX, float cameraY, float zoom)
    {
        foreach (var deposit in simulation.Deposits.Where(d => d.Amount > 0))
        {
            var point = OrthogonalGridProjection.TileCenter(deposit.Cell.X, deposit.Cell.Y, cameraX, cameraY, zoom);
            float size = 13f * zoom;
            var rect = new SKRect(point.X - size / 2, point.Y - size / 2, point.X + size / 2, point.Y + size / 2);
            _fill.Color = deposit.Resource switch
            {
                ResourceKind.Wood => new SKColor(121, 149, 86),
                ResourceKind.Stone => new SKColor(153, 156, 144),
                ResourceKind.Food => new SKColor(203, 172, 73),
                ResourceKind.Alloy => new SKColor(181, 124, 90),
                _ => new SKColor(143, 113, 174)
            };
            canvas.DrawRect(rect, _fill);
            _line.Color = new SKColor(12, 15, 14);
            canvas.DrawRect(rect, _line);
            string letter = deposit.Resource switch
            {
                ResourceKind.Wood => "W",
                ResourceKind.Stone => "S",
                ResourceKind.Food => "F",
                ResourceKind.Alloy => "A",
                _ => "C"
            };
            canvas.DrawText(letter, point.X, point.Y + 4f * zoom, SKTextAlign.Center, _smallFont, _text);
        }
    }

    private void DrawObjects(SKCanvas canvas, ColonySimulation simulation, float cameraX, float cameraY, float zoom)
    {
        float size = OrthogonalGridProjection.TileSize * zoom;
        foreach (var item in simulation.Objects)
        {
            var topLeft = OrthogonalGridProjection.WorldToScreen(item.Origin.X, item.Origin.Y, cameraX, cameraY, zoom);
            var bottomRight = OrthogonalGridProjection.WorldToScreen(item.Origin.X + item.Width, item.Origin.Y + item.Height, cameraX, cameraY, zoom);
            var rect = new SKRect(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
            if (rect.Right < 0 || rect.Left > 770 || rect.Bottom < 62 || rect.Top > 650) continue;

            _fill.Color = item.IsDestroyed ? new SKColor(49, 21, 27) : item.Kind switch
            {
                PlaceableKind.Habitat => new SKColor(72, 27, 36),
                PlaceableKind.Stockpile => new SKColor(54, 60, 52),
                PlaceableKind.Workbench => new SKColor(61, 69, 74),
                PlaceableKind.SleepingPod => new SKColor(59, 67, 77),
                PlaceableKind.StorageRack => new SKColor(82, 70, 49),
                PlaceableKind.Generator => new SKColor(72, 48, 79),
                PlaceableKind.Garden => new SKColor(39, 75, 48),
                PlaceableKind.DiningTable => new SKColor(105, 75, 49),
                PlaceableKind.Lamp => new SKColor(105, 91, 48),
                PlaceableKind.ComfortBench => new SKColor(65, 79, 74),
                _ => new SKColor(48, 50, 50)
            };
            if (item.IsBlightInfected)
                _fill.Color = new SKColor(119, 25, 61, 190);
            canvas.DrawRect(Inset(rect, size * 0.04f), _fill);
            _line.Color = item.IsDestroyed || item.IsBlightInfected ? Crimson :
                item.IsBuilt && item.Health < item.MaxHealth ? Gold : item.Status switch
                {
                    ConstructionStatus.Complete => new SKColor(153, 168, 151),
                    ConstructionStatus.WaitingForMaterials => Crimson,
                    ConstructionStatus.Blocked => new SKColor(110, 73, 76),
                    _ => Gold
                };
            _line.StrokeWidth = Math.Max(1f, zoom * 1.4f);
            canvas.DrawRect(Inset(rect, size * 0.07f), _line);

            string label = item.IsDestroyed ? "×" : item.Kind switch
            {
                PlaceableKind.Habitat => "H",
                PlaceableKind.Stockpile => "S",
                PlaceableKind.Workbench => "W",
                PlaceableKind.SleepingPod => "B",
                PlaceableKind.StorageRack => "R",
                PlaceableKind.Generator => "G",
                PlaceableKind.Garden => "F",
                PlaceableKind.DiningTable => "T",
                PlaceableKind.Lamp => "L",
                PlaceableKind.ComfortBench => "C",
                _ => "?"
            };
            _text.Color = item.IsDestroyed ? Crimson : item.IsBlightInfected ? new SKColor(255, 116, 169) : TextColor;
            canvas.DrawText(label, rect.MidX, rect.MidY + 4f * zoom, SKTextAlign.Center, _headingFont, _text);
            if (item.IsBuilt && (item.Health < item.MaxHealth || item.IsBlightInfected) && zoom >= 0.8f)
            {
                _muted.Color = item.IsBlightInfected ? new SKColor(255, 116, 169) : Gold;
                canvas.DrawText($"{item.Health / item.MaxHealth:P0}{(item.IsBlightInfected ? " !" : string.Empty)}",
                    rect.MidX, rect.Bottom - 3f * zoom, SKTextAlign.Center, _smallFont, _muted);
            }
            _text.Color = TextColor;

            if (!item.IsBuilt && item.Status != ConstructionStatus.Complete)
            {
                _text.Color = item.Status == ConstructionStatus.WaitingForMaterials ? Crimson : Gold;
                string progress = item.Status == ConstructionStatus.WaitingForMaterials
                    ? "!"
                    : $"{Math.Min(100, (int)(item.WorkDone / Math.Max(1, item.WorkRequired) * 100))}%";
                canvas.DrawText(progress, rect.MidX, rect.Bottom - 3f * zoom, SKTextAlign.Center, _smallFont, _text);
                _text.Color = TextColor;
            }
        }
    }

    private void DrawWorkers(SKCanvas canvas, ColonySimulation simulation, float cameraX, float cameraY, float zoom)
    {
        foreach (var worker in simulation.Workers)
        {
            var point = OrthogonalGridProjection.WorldToScreen(worker.X, worker.Y, cameraX, cameraY, zoom);
            // Keep draw calls bounded to the visible colony viewport even for large populations.
            if (point.X < -12 || point.X > 782 || point.Y < 50 || point.Y > 662) continue;
            float radius = Math.Max(3f, 4.5f * zoom);
            _fill.Color = worker.Hungry ? new SKColor(222, 149, 73) : WorkerColor;
            if (zoom >= 0.82f)
            {
                _text.Color = worker.Hungry ? new SKColor(255, 173, 90) : WorkerColor;
                canvas.DrawText("@", point.X, point.Y + 4f * zoom, SKTextAlign.Center, _glyphFont, _text);
            }
            else
            {
                canvas.DrawCircle(point.X, point.Y, radius, _fill);
            }
            if (simulation.SelectedWorkerId == worker.Id)
            {
                _line.Color = Gold;
                _line.StrokeWidth = Math.Max(1f, 1.5f * zoom);
                canvas.DrawCircle(point.X, point.Y, radius + 4f * zoom, _line);
            }
            if (worker.CarriedResource.HasValue)
            {
                _fill.Color = new SKColor(203, 174, 104);
                canvas.DrawRect(new SKRect(point.X + radius, point.Y - radius, point.X + radius * 2.4f, point.Y + radius * 0.4f), _fill);
            }
        }
    }

    private void DrawPlacementPreview(SKCanvas canvas, ColonySimulation simulation, float cameraX, float cameraY, float zoom,
        ArchitectureKind architectureMode, PlaceableKind objectType, bool placingObject, bool excavationMode, bool demolitionMode,
        float mouseScreenX, float mouseScreenY)
    {
        if (mouseScreenX < 0 || mouseScreenX >= 770 || mouseScreenY < 62 || mouseScreenY >= 650) return;
        var world = OrthogonalGridProjection.ScreenToWorld(mouseScreenX, mouseScreenY, cameraX, cameraY, zoom);
        int x = (int)MathF.Floor(world.X), y = (int)MathF.Floor(world.Y);
        if (placingObject && simulation.TryGetPlacementFootprint(objectType, out int width, out int height))
        {
            bool valid = simulation.CanPlaceObjectAt(objectType, x, y);
            var topLeft = OrthogonalGridProjection.WorldToScreen(x, y, cameraX, cameraY, zoom);
            var bottomRight = OrthogonalGridProjection.WorldToScreen(x + width, y + height, cameraX, cameraY, zoom);
            var footprint = new SKRect(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
            _fill.Color = valid ? new SKColor(70, 180, 115, 62) : new SKColor(218, 42, 54, 76);
            canvas.DrawRect(footprint, _fill);
            _line.Color = valid ? new SKColor(110, 235, 157) : new SKColor(255, 85, 94);
            _line.StrokeWidth = 2;
            canvas.DrawRect(Inset(footprint, 1), _line);
            _text.Color = valid ? new SKColor(150, 255, 184) : new SKColor(255, 132, 139);
            canvas.DrawText($"{objectType} · {width}×{height}", Math.Clamp(footprint.Left + 3, 4, 680), Math.Clamp(footprint.Top - 5, 76, 638), SKTextAlign.Left, _smallFont, _text);
            return;
        }

        if (architectureMode != ArchitectureKind.None || excavationMode || demolitionMode)
        {
            var center = OrthogonalGridProjection.TileCenter(x, y, cameraX, cameraY, zoom);
            float size = OrthogonalGridProjection.TileSize * zoom;
            var tileRect = new SKRect(center.X - size / 2, center.Y - size / 2, center.X + size / 2, center.Y + size / 2);
            bool valid = architectureMode != ArchitectureKind.None
                ? simulation.CanDesignTileConstruction(x, y, architectureMode)
                : simulation.TileAt(x, y) != null;
            _fill.Color = valid ? new SKColor(216, 177, 89, 45) : new SKColor(218, 42, 54, 55);
            canvas.DrawRect(tileRect, _fill);
            _line.Color = valid ? Gold : new SKColor(255, 85, 94);
            _line.StrokeWidth = 1.5f;
            canvas.DrawRect(Inset(tileRect, 1), _line);
            if (architectureMode != ArchitectureKind.None)
            {
                _text.Color = Gold;
                canvas.DrawText(architectureMode.ToString().ToUpperInvariant(), Math.Clamp(tileRect.Left + 2, 4, 700), Math.Clamp(tileRect.Top - 4, 76, 638), SKTextAlign.Left, _smallFont, _text);
            }
        }
    }



    private void DrawCombatEntities(SKCanvas canvas, WorldSimulation world, float cameraX, float cameraY, float zoom, int? selectedCombatEntityId)
    {
        if (zoom < 0.65f) return;
        foreach (var flash in world.CombatEngine.Flashes)
        {
            var from = OrthogonalGridProjection.WorldToScreen(flash.FromX, flash.FromY, cameraX, cameraY, zoom);
            var to = OrthogonalGridProjection.WorldToScreen(flash.ToX, flash.ToY, cameraX, cameraY, zoom);
            _line.Color = flash.IsHeavy ? new SKColor(239, 174, 74, 230) : new SKColor(255, 92, 83, 210);
            _line.StrokeWidth = flash.IsHeavy ? 3f : 1.5f;
            canvas.DrawLine(from.X, from.Y, to.X, to.Y, _line);
        }

        foreach (var entity in world.CombatEngine.Entities.OrderBy(entity => entity.Id))
        {
            var point = OrthogonalGridProjection.WorldToScreen(entity.X, entity.Y, cameraX, cameraY, zoom);
            if (point.X < -24 || point.X > 794 || point.Y < 48 || point.Y > 666) continue;
            bool friendly = entity.FactionId == 1;
            _fill.Color = friendly ? new SKColor(230, 217, 167) :
                entity.HasBlightMutation ? new SKColor(179, 26, 67) : new SKColor(225, 76, 68);
            float size = Math.Max(5f, (entity.Type is CombatEntityType.Vehicle or CombatEntityType.HeavyTank ? 8f : 5f) * Math.Clamp(zoom, .75f, 1.5f));
            if (entity.Type is CombatEntityType.Vehicle or CombatEntityType.HeavyTank)
            {
                var rect = new SKRect(point.X - size, point.Y - size * .7f, point.X + size, point.Y + size * .7f);
                canvas.DrawRoundRect(rect, 2, 2, _fill);
                _line.Color = friendly ? Gold : Crimson;
                canvas.DrawRoundRect(rect, 2, 2, _line);
            }
            else if (entity.Type is CombatEntityType.BlightBeast or CombatEntityType.BlightTitan)
            {
                using var path = new SKPath();
                path.MoveTo(point.X, point.Y - size * 1.2f);
                path.LineTo(point.X + size, point.Y + size);
                path.LineTo(point.X - size, point.Y + size);
                path.Close();
                canvas.DrawPath(path, _fill);
                _line.Color = friendly ? Gold : Crimson;
                canvas.DrawPath(path, _line);
            }
            else
            {
                canvas.DrawCircle(point.X, point.Y, size * .78f, _fill);
                _line.Color = friendly ? Gold : Crimson;
                _line.StrokeWidth = 1.5f;
                canvas.DrawCircle(point.X, point.Y, size * .78f, _line);
            }

            if (selectedCombatEntityId == entity.Id)
            {
                _line.Color = Gold;
                _line.StrokeWidth = 2f;
                canvas.DrawCircle(point.X, point.Y, size + 5f, _line);
            }
            if (zoom >= .92f)
            {
                _text.Color = friendly ? TextColor : new SKColor(255, 142, 135);
                canvas.DrawText($"{entity.Health:0.0}", point.X + size + 2, point.Y - size, SKTextAlign.Left, _smallFont, _text);
            }
        }
    }

    private void DrawSystemsButton(SKCanvas canvas, bool isOpen)
    {
        _fill.Color = isOpen ? new SKColor(89, 58, 19) : new SKColor(28, 35, 32);
        canvas.DrawRoundRect(new SKRect(855, 8, 950, 35), 4, 4, _fill);
        _line.Color = isOpen ? Gold : Border;
        canvas.DrawRoundRect(new SKRect(855, 8, 950, 35), 4, 4, _line);
        _text.Color = isOpen ? Gold : TextColor;
        canvas.DrawText("WAR ROOM", 902, 26, SKTextAlign.Center, _smallFont, _text);
    }

    private void DrawSystemsPanel(SKCanvas canvas, WorldSimulation world, int tab, int? selectedCombatEntityId)
    {
        _fill.Color = new SKColor(5, 8, 9, 248);
        canvas.DrawRoundRect(new SKRect(110, 76, 990, 642), 8, 8, _fill);
        _line.Color = Gold;
        _line.StrokeWidth = 2;
        canvas.DrawRoundRect(new SKRect(110, 76, 990, 642), 8, 8, _line);
        _text.Color = Crimson;
        canvas.DrawText("KOR TACTICAL & INDUSTRIAL SYSTEMS", 140, 105, SKTextAlign.Left, _titleFont, _text);
        DrawButton(canvas, "COMBAT", 140, 116, 112, 28, tab == 0);
        DrawButton(canvas, "WORKSHOP", 262, 116, 112, 28, tab == 1);
        DrawButton(canvas, "RESEARCH", 384, 116, 112, 28, tab == 2);
        DrawButton(canvas, "LOADOUTS", 506, 116, 112, 28, tab == 3);
        DrawButton(canvas, "CLOSE ×", 862, 88, 94, 28);

        if (tab == 0)
        {
            var entities = world.CombatEngine.Entities
                .OrderBy(entity => entity.FactionId == 1 ? 0 : 1)
                .ThenBy(entity => entity.Id).Take(10).ToArray();
            int hostiles = entities.Count(entity => entity.FactionId != 1);
            int friendlies = entities.Length - hostiles;
            _text.Color = TextColor;
            canvas.DrawText($"LOCAL ENGAGEMENT · {friendlies} KOR · {hostiles} HOSTILE · {world.PendingCombatLoot.Count} LOOT STACKS WAITING FOR STORAGE",
                142, 164, SKTextAlign.Left, _smallFont, _text);
            int y = 180;
            foreach (var entity in entities)
            {
                bool selected = selectedCombatEntityId == entity.Id;
                _fill.Color = selected ? new SKColor(76, 55, 23, 190) : new SKColor(20, 28, 27, 225);
                canvas.DrawRoundRect(new SKRect(140, y, 955, y + 26), 3, 3, _fill);
                _line.Color = selected ? Gold : Border;
                canvas.DrawRoundRect(new SKRect(140, y, 955, y + 26), 3, 3, _line);
                _text.Color = entity.FactionId == 1 ? TextColor : new SKColor(255, 142, 135);
                string side = entity.FactionId == 1 ? "KOR" : entity.FactionId == 99 ? "BLIGHT" : "HOSTILE";
                string ammo = entity.EquippedWeapon == CombatWeaponType.Melee ? "MELEE" : $"AMMO {entity.CurrentAmmo}/{entity.MaxAmmo}";
                canvas.DrawText($"{side}  #{entity.Id}  {world.GetCombatantName(entity)}",
                    151, y + 17, SKTextAlign.Left, _smallFont, _text);
                _muted.Color = MutedColor;
                canvas.DrawText($"{entity.Type}  HP {entity.Health:0.00}  {ammo}", 790, y + 17, SKTextAlign.Left, _smallFont, _muted);
                y += 30;
            }
            DrawButton(canvas, "DEPLOY COLONISTS", 140, 490, 180, 26);
            DrawButton(canvas, "REPAIR ALL", 330, 490, 180, 26);
            _muted.Color = MutedColor;
            canvas.DrawText("Select a Kor unit, then click an enemy row to issue an attack order. Unopposed hostiles besiege nearby structures.",
                142, 544, SKTextAlign.Left, _smallFont, _muted);
            canvas.DrawText("Blight attacks can infect buildings; infection deals recurring damage until repaired with shared supplies.",
                142, 564, SKTextAlign.Left, _smallFont, _muted);
            canvas.DrawText("Repairs spend wood, stone, alloy and components from the same settlement inventory used by crafting.",
                142, 584, SKTextAlign.Left, _smallFont, _muted);
        }
        else if (tab == 1)
        {
            var inventory = world.Workshop.Inventory;
            bool hasWorkbench = world.Colony.Objects.Any(item => item.Kind == PlaceableKind.Workbench && item.IsBuilt);
            _text.Color = TextColor;
            canvas.DrawText($"CANONICAL SETTLEMENT STOCK · ALLOY {inventory.GetValueOrDefault(WorkbenchManager.Alloy)} · COMPONENTS {inventory.GetValueOrDefault(WorkbenchManager.Components)} · ITEM STACKS {inventory.Count}",
                142, 163, SKTextAlign.Left, _smallFont, _text);
            var recipes = world.Workshop.Recipes.OrderBy(recipe => recipe.Id, StringComparer.Ordinal).Take(5).ToArray();
            int y = 174;
            foreach (var recipe in recipes)
            {
                bool canCraft = hasWorkbench && world.Workshop.CanStartCraft(recipe.Id);
                _fill.Color = new SKColor(20, 28, 27, 225);
                canvas.DrawRoundRect(new SKRect(140, y, 955, y + 38), 3, 3, _fill);
                _line.Color = Border;
                canvas.DrawRoundRect(new SKRect(140, y, 955, y + 38), 3, 3, _line);
                _text.Color = TextColor;
                string costs = string.Join(", ", recipe.InputCosts.Select(cost => $"{cost.Value} {cost.Key}"));
                canvas.DrawText($"{recipe.ResultItemId}  ·  {costs}", 151, y + 15, SKTextAlign.Left, _smallFont, _text);
                _muted.Color = MutedColor;
                canvas.DrawText($"RESEARCH: {recipe.RequiredResearchId} · {recipe.WorkTicksRequired} work ticks", 151, y + 30, SKTextAlign.Left, _smallFont, _muted);
                string craftLabel = canCraft ? "CRAFT" : !hasWorkbench ? "BUILD BENCH" :
                    !world.Workshop.IsResearchUnlocked(recipe.RequiredResearchId) ? "LOCKED" : "NO STOCK";
                DrawButton(canvas, craftLabel, 820, y + 5, 124, 28, canCraft);
                y += 44;
            }
            _text.Color = Gold;
            canvas.DrawText("ACTIVE JOBS", 142, 413, SKTextAlign.Left, _headingFont, _text);
            _muted.Color = MutedColor;
            var jobs = world.Workshop.Jobs.OrderBy(job => job.Id).Take(3).ToArray();
            for (int i = 0; i < jobs.Length; i++)
                canvas.DrawText($"#{jobs[i].Id} {jobs[i].Recipe.ResultItemId} · {jobs[i].WorkLeft} work ticks remaining",
                    151, 436 + i * 20, SKTextAlign.Left, _smallFont, _muted);
            DrawButton(canvas, "ASSEMBLE HEAVY TANK", 142, 512, 220, 32);
            _muted.Color = MutedColor;
            canvas.DrawText("Vehicle assembly consumes one chassis, one engine, four armor-plate stacks and one tank cannon atomically.",
                142, 566, SKTextAlign.Left, _smallFont, _muted);
            canvas.DrawText(hasWorkbench
                ? "Outputs wait safely if storage is full; all costs use the shared settlement ledger."
                : "Place and finish a workbench in the colony before crafting. Resources and research are shared.",
                142, 588, SKTextAlign.Left, _smallFont, _muted);
        }
        else if (tab == 2)
        {
            var projects = world.ResearchManager.Database.ResearchDatabase.Values
                .OrderBy(project => project.Name, StringComparer.Ordinal).Take(6).ToArray();
            _text.Color = TextColor;
            var currentId = world.ResearchManager.State.CurrentResearchId;
            if (currentId is not null && world.ResearchManager.Database.ResearchDatabase.TryGetValue(currentId, out var current))
                canvas.DrawText($"CURRENT: {current.Name} · {world.ResearchManager.State.CurrentProgressPoints:0}/{current.CostPoints:0} RP",
                    142, 163, SKTextAlign.Left, _smallFont, _text);
            else
                canvas.DrawText("Choose a project. Research progresses with the shared simulation clock.",
                    142, 163, SKTextAlign.Left, _smallFont, _text);

            int y = 174;
            foreach (var project in projects)
            {
                bool complete = world.ResearchManager.State.CompletedResearchIds.Contains(project.Id);
                bool active = currentId == project.Id;
                bool canStart = world.ResearchManager.CanStart(project.Id);
                _fill.Color = new SKColor(20, 28, 27, 225);
                canvas.DrawRoundRect(new SKRect(140, y, 955, y + 48), 3, 3, _fill);
                _line.Color = active ? Gold : Border;
                canvas.DrawRoundRect(new SKRect(140, y, 955, y + 48), 3, 3, _line);
                _text.Color = TextColor;
                canvas.DrawText(project.Name, 151, y + 17, SKTextAlign.Left, _normalFont, _text);
                _muted.Color = MutedColor;
                string prereqs = project.Prerequisites.Count == 0 ? "no prerequisites" : $"requires {string.Join(", ", project.Prerequisites)}";
                canvas.DrawText($"{project.CostPoints:0} RP · {prereqs}", 151, y + 35, SKTextAlign.Left, _smallFont, _muted);
                DrawButton(canvas, complete ? "COMPLETE" : active ? "IN PROGRESS" : canStart ? "START" : "LOCKED",
                    820, y + 9, 124, 28, complete || active || canStart);
                y += 56;
            }
            _muted.Color = MutedColor;
            canvas.DrawText("Research unlocks the exact recipe IDs consumed by the connected workshop; save/load preserves completed and active research.",
                142, 566, SKTextAlign.Left, _smallFont, _muted);
        }
        else
        {
            // Equipment consumes from the canonical settlement ledger, so the button must
            // reflect that same source rather than the workshop's cached inventory snapshot.
            var home = world.SpatialWorld.Entities.FirstOrDefault(entity =>
                entity.Kind == WorldEntityKind.Settlement && entity.Faction == "Kor");
            int riflesInStock = home is null
                ? 0
                : world.SpatialWorld.ItemAmountAt(home.Id, WorkbenchManager.InfantryRifle);
            _text.Color = TextColor;
            canvas.DrawText($"FINISHED RIFLES IN SHARED STOCK: {riflesInStock}", 142, 163, SKTextAlign.Left, _headingFont, _text);
            _muted.Color = MutedColor;
            canvas.DrawText("Select a deployed Kor infantry unit below, then equip one finished rifle from stock.",
                142, 181, SKTextAlign.Left, _smallFont, _muted);
            var units = world.CombatEngine.Entities.Where(entity => entity.FactionId == 1 &&
                    entity.Type == CombatEntityType.Human && entity.SourcePawnId > 0)
                .OrderBy(entity => entity.SourcePawnId).Take(9).ToArray();
            for (int i = 0; i < units.Length; i++)
            {
                var unit = units[i];
                int y = 196 + i * 31;
                bool selected = selectedCombatEntityId == unit.Id;
                _fill.Color = selected ? new SKColor(76, 55, 23, 190) : new SKColor(20, 28, 27, 225);
                canvas.DrawRoundRect(new SKRect(140, y, 955, y + 27), 3, 3, _fill);
                _line.Color = selected ? Gold : Border;
                canvas.DrawRoundRect(new SKRect(140, y, 955, y + 27), 3, 3, _line);
                _text.Color = TextColor;
                canvas.DrawText($"{world.GetCombatantName(unit)}  ·  pawn #{unit.SourcePawnId}",
                    151, y + 17, SKTextAlign.Left, _smallFont, _text);
                _muted.Color = MutedColor;
                string equipment = unit.EquippedWeapon == CombatWeaponType.Melee ? "MELEE" :
                    $"{unit.EquippedWeapon}  {unit.CurrentAmmo}/{unit.MaxAmmo}";
                canvas.DrawText(equipment, 650, y + 17, SKTextAlign.Left, _smallFont, _muted);
            }
            bool selectedEligibleInfantry = selectedCombatEntityId.HasValue &&
                units.Any(unit => unit.Id == selectedCombatEntityId.Value);
            DrawButton(canvas, "EQUIP RIFLE", 140, 490, 180, 30,
                riflesInStock > 0 && selectedEligibleInfantry);
            _muted.Color = MutedColor;
            canvas.DrawText(units.Length == 0
                ? "No deployed colonists yet. Open COMBAT and choose DEPLOY COLONISTS first."
                : "Rifles are removed from the same stock used by crafting; equipment and ammunition persist in the combat save.",
                142, 544, SKTextAlign.Left, _smallFont, _muted);
            canvas.DrawText("If stock is empty: build a completed workbench, unlock industrial weapons, craft a rifle, then return here.",
                142, 566, SKTextAlign.Left, _smallFont, _muted);
        }
    }

    private void DrawWorkMatrixButton(SKCanvas canvas, bool isOpen)
    {
        _fill.Color = isOpen ? Gold : Panel;
        canvas.DrawRoundRect(new SKRect(1008, 8, 1085, 35), 4, 4, _fill);
        _line.Color = Gold;
        canvas.DrawRoundRect(new SKRect(1008, 8, 1085, 35), 4, 4, _line);
        _text.Color = isOpen ? Background : TextColor;
        canvas.DrawText("WORK", 1046, 26, SKTextAlign.Center, _smallFont, _text);
    }

    private void DrawWorkerInspector(SKCanvas canvas, ColonySimulation simulation, int tab)
    {
        var worker = simulation.Workers.FirstOrDefault(item => item.Id == simulation.SelectedWorkerId);
        if (worker == null) return;
        _fill.Color = new SKColor(12, 16, 17, 245);
        canvas.DrawRoundRect(new SKRect(14, 476, 374, 646), 5, 5, _fill);
        _line.Color = Gold;
        canvas.DrawRoundRect(new SKRect(14, 476, 374, 646), 5, 5, _line);
        _text.Color = Gold;
        canvas.DrawText(worker.IsExternalVisitor
            ? $"{worker.Name}  /  VISITOR"
            : $"{worker.Name}  /  COLONIST INSPECTOR", 24, 497, SKTextAlign.Left, _headingFont, _text);
        if (worker.IsExternalVisitor)
        {
            DrawButton(canvas, worker.TradeCargo.Count == 0 ? "ADMIT" : "TRADE", 22, 506, 100, 22);
            DrawButton(canvas, "DISMISS", 128, 506, 100, 22);
            _text.Color = TextColor;
            canvas.DrawText($"Faction: {worker.Faction}", 24, 552, SKTextAlign.Left, _smallFont, _text);
            canvas.DrawText($"Cargo: {(worker.TradeCargo.Count == 0 ? "none" : string.Join(", ", worker.TradeCargo.Select(item => $"{item.Key} {item.Value}")))}", 24, 574, SKTextAlign.Left, _smallFont, _text);
            canvas.DrawText($"Status: {(worker.IsLeaving ? "Leaving" : worker.Task is null ? "Waiting at stockpile" : "Entering colony")}", 24, 596, SKTextAlign.Left, _smallFont, _text);
            _muted.Color = MutedColor;
            canvas.DrawText(worker.TradeCargo.Count == 0
                ? "Admit this visitor to grow the colony, or dismiss them."
                : "Trade exchanges cargo at current macro market prices.", 24, 624, SKTextAlign.Left, _smallFont, _muted);
            return;
        }
        DrawButton(canvas, "STATUS", 22, 506, 100, 22, tab == 0);
        DrawButton(canvas, "SOCIAL", 128, 506, 100, 22, tab == 1);
        _text.Color = TextColor;
        if (tab == 0)
        {
            var citizen = simulation.CitizenForWorker(worker.Id);
            string toil = worker.Task?.JobTracker.CurrentToil?.Kind.ToString() ?? "Idle";
            canvas.DrawText($"Job: {worker.Task?.Kind.ToString() ?? "No assigned job"}", 24, 550, SKTextAlign.Left, _smallFont, _text);
            canvas.DrawText($"Current toil: {toil} | Route steps: {worker.Task?.Route.Count ?? 0}", 24, 570, SKTextAlign.Left, _smallFont, _text);
            canvas.DrawText($"Hunger {worker.Hunger:0}/100  |  Mood {citizen?.Mood ?? 100:0}  |  Stress {worker.Stress:0}", 24, 590, SKTextAlign.Left, _smallFont, _text);
            canvas.DrawText($"Fatigue {citizen?.Fatigue ?? 100:0}  |  Carried {worker.CarriedAmount} {worker.CarriedResource?.ToString() ?? "—"}", 24, 610, SKTextAlign.Left, _smallFont, _text);
            _muted.Color = MutedColor;
            canvas.DrawText(simulation.InspectWorker(worker.Id)?.PrimaryReason ?? "No current needs warning.", 24, 630, SKTextAlign.Left, _smallFont, _muted);
        }
        else
        {
            var relationships = simulation.SocialRelationships.Relationships
                .Where(item => item.ResidentA == worker.Id || item.ResidentB == worker.Id)
                .OrderByDescending(item => item.Affinity).ThenBy(item => item.ResidentA).Take(4).ToArray();
            if (relationships.Length == 0)
            {
                _muted.Color = MutedColor;
                canvas.DrawText("No recorded social encounters yet.", 24, 552, SKTextAlign.Left, _smallFont, _muted);
            }
            else
            {
                int row = 552;
                foreach (var relation in relationships)
                {
                    int otherId = relation.ResidentA == worker.Id ? relation.ResidentB : relation.ResidentA;
                    string otherName = simulation.Workers.FirstOrDefault(item => item.Id == otherId)?.Name ?? $"Resident {otherId}";
                    _text.Color = relation.Affinity < -30 ? Crimson : TextColor;
                    canvas.DrawText($"{otherName}: affinity {relation.Affinity:+#;-#;0} | trust {relation.Trust:+#;-#;0} | tension {relation.Tension}", 24, row, SKTextAlign.Left, _smallFont, _text);
                    row += 20;
                }
            }
        }
    }

    private void DrawWorkMatrix(SKCanvas canvas, ColonySimulation simulation, int page)
    {
        _fill.Color = new SKColor(8, 11, 12, 250);
        canvas.DrawRoundRect(new SKRect(130, 112, 970, 624), 8, 8, _fill);
        _line.Color = Gold;
        canvas.DrawRoundRect(new SKRect(130, 112, 970, 624), 8, 8, _line);
        _text.Color = Gold;
        canvas.DrawText("COLONY WORK PRIORITIES", 155, 145, SKTextAlign.Left, _titleFont, _text);
        _muted.Color = MutedColor;
        canvas.DrawText("Click a cell to cycle 0–4. 0 = disabled, 4 = highest priority. Hauling carried cargo remains mandatory.", 155, 170, SKTextAlign.Left, _smallFont, _muted);
        var categories = Enum.GetValues<WorkCategory>();
        int[] columnX = [420, 520, 620, 720, 820];
        for (int i = 0; i < categories.Length; i++)
        {
            _text.Color = TextColor;
            canvas.DrawText(categories[i].ToString().ToUpperInvariant(), columnX[i] + 32, 212, SKTextAlign.Center, _smallFont, _text);
        }
        int rowY = 244;
        foreach (var worker in simulation.Workers.Skip(page * 9).Take(9))
        {
            _text.Color = TextColor;
            canvas.DrawText(worker.Name, 160, rowY + 17, SKTextAlign.Left, _normalFont, _text);
            for (int i = 0; i < categories.Length; i++)
            {
                int priority = simulation.WorkPriorities.GetPriority(worker.Id, categories[i]);
                DrawButton(canvas, priority.ToString(), columnX[i], rowY, 64, 28, priority == 0);
            }
            rowY += 38;
        }
        _muted.Color = MutedColor;
        canvas.DrawText($"Page {page + 1}/{Math.Max(1, (simulation.Workers.Count + 8) / 9)} · priorities persist with the colony", 155, 598, SKTextAlign.Left, _smallFont, _muted);
        DrawButton(canvas, "PREV", 710, 584, 60, 28, page == 0);
        DrawButton(canvas, "NEXT", 778, 584, 60, 28, (page + 1) * 9 >= simulation.Workers.Count);
        DrawButton(canvas, "CLOSE", 850, 584, 92, 28);
    }

    private void DrawFactionButton(SKCanvas canvas, bool isOpen)
    {
        _fill.Color = isOpen ? new SKColor(87, 28, 35) : new SKColor(28, 35, 32);
        canvas.DrawRoundRect(new SKRect(955, 8, 1005, 34), 3, 3, _fill);
        _line.Color = isOpen ? Crimson : Border;
        canvas.DrawRoundRect(new SKRect(955, 8, 1005, 34), 3, 3, _line);
        _text.Color = isOpen ? Gold : TextColor;
        canvas.DrawText("FACTIONS", 980, 25, SKTextAlign.Center, _smallFont, _text);
    }

    private void DrawFactionDashboard(SKCanvas canvas, ContinuousWorldState world, string? selectedFactionName, int tab)
    {
        _fill.Color = new SKColor(5, 8, 9, 238);
        canvas.DrawRect(new SKRect(35, 70, 1065, 642), _fill);
        _line.Color = new SKColor(115, 45, 51);
        _line.StrokeWidth = 2;
        canvas.DrawRect(new SKRect(35, 70, 1065, 642), _line);
        _text.Color = Crimson;
        canvas.DrawText("STRATEGIC FACTION INTELLIGENCE", 58, 101, SKTextAlign.Left, _titleFont, _text);
        _muted.Color = MutedColor;
        canvas.DrawText($"DIRECTOR CYCLE · WORLD T+{world.ClockSeconds / 60:0.0} MIN · {world.FactionIntelligence.Count} FACTIONS", 60, 121, SKTextAlign.Left, _smallFont, _muted);
        DrawButton(canvas, "CLOSE ×", 970, 80, 76, 26);

        _fill.Color = Panel;
        canvas.DrawRoundRect(new SKRect(55, 138, 365, 615), 4, 4, _fill);
        canvas.DrawRoundRect(new SKRect(385, 138, 1045, 615), 4, 4, _fill);
        _line.Color = Border;
        canvas.DrawRoundRect(new SKRect(55, 138, 365, 615), 4, 4, _line);
        canvas.DrawRoundRect(new SKRect(385, 138, 1045, 615), 4, 4, _line);
        _text.Color = Gold;
        canvas.DrawText("FACTION ROSTER", 70, 161, SKTextAlign.Left, _headingFont, _text);

        var profiles = world.FactionIntelligence.OrderBy(profile => profile.Faction == "Kor" ? 0 : 1)
            .ThenBy(profile => profile.Faction, StringComparer.Ordinal).ToArray();
        int rowY = 190;
        foreach (var profile in profiles.Take(10))
        {
            bool selected = profile.Faction == selectedFactionName;
            _fill.Color = selected ? new SKColor(77, 28, 35) : new SKColor(24, 31, 30);
            canvas.DrawRoundRect(new SKRect(66, rowY - 17, 354, rowY + 22), 3, 3, _fill);
            _line.Color = selected ? Crimson : Border;
            canvas.DrawRoundRect(new SKRect(66, rowY - 17, 354, rowY + 22), 3, 3, _line);
            _text.Color = selected ? Gold : TextColor;
            canvas.DrawText($"{(profile.Faction == "Kor" ? "◆" : "◇")} {profile.Faction}", 77, rowY, SKTextAlign.Left, _normalFont, _text);
            _muted.Color = MutedColor;
            canvas.DrawText($"{profile.TerritoryCount} sites · threat {profile.ThreatLevel}/100", 77, rowY + 15, SKTextAlign.Left, _smallFont, _muted);
            rowY += 43;
        }

        var active = profiles.FirstOrDefault(profile => profile.Faction == selectedFactionName) ?? profiles.FirstOrDefault();
        if (active == null)
        {
            _text.Color = TextColor;
            canvas.DrawText("No faction intelligence available.", 410, 180, SKTextAlign.Left, _normalFont, _text);
            return;
        }

        _text.Color = Gold;
        canvas.DrawText(active.Faction.ToUpperInvariant(), 410, 170, SKTextAlign.Left, _titleFont, _text);
        _muted.Color = MutedColor;
        canvas.DrawText($"STANCE TOWARD KOR: {active.StanceToward()}   ·   STRATEGY: {active.Strategy}", 412, 191, SKTextAlign.Left, _smallFont, _muted);
        DrawButton(canvas, "OVERVIEW", 410, 207, 108, 26, tab == 0);
        DrawButton(canvas, "DIPLOMACY", 526, 207, 108, 26, tab == 1);
        DrawButton(canvas, "THREAT / AI", 642, 207, 108, 26, tab == 2);

        if (tab == 1)
        {
            _text.Color = Gold;
            canvas.DrawText("RELATIONSHIP MATRIX", 410, 260, SKTextAlign.Left, _headingFont, _text);
            int y = 286;
            foreach (var relation in active.Relationships.OrderByDescending(item => item.Score).Take(8))
            {
                _text.Color = TextColor;
                canvas.DrawText(relation.OtherFaction, 412, y, SKTextAlign.Left, _normalFont, _text);
                _text.Color = relation.Score >= 30 ? new SKColor(125, 220, 155) : relation.Score <= -30 ? new SKColor(255, 110, 110) : TextColor;
                canvas.DrawText($"{relation.Score:+#;-#;0}/100", 660, y, SKTextAlign.Left, _normalFont, _text);
                y += 24;
                foreach (var modifier in relation.Modifiers.Take(2))
                {
                    _muted.Color = MutedColor;
                    canvas.DrawText($"· {modifier.Cause}: {modifier.Value:+#;-#;0}", 430, y, SKTextAlign.Left, _smallFont, _muted);
                    y += 16;
                }
                y += 5;
                if (y > 575) break;
            }
        }
        else if (tab == 2)
        {
            _text.Color = Gold;
            canvas.DrawText("STRATEGIC DIRECTOR", 410, 260, SKTextAlign.Left, _headingFont, _text);
            _text.Color = TextColor;
            canvas.DrawText($"Current plan: {active.Strategy}", 412, 292, SKTextAlign.Left, _normalFont, _text);
            canvas.DrawText($"Aggression: {active.Aggression}/100", 412, 320, SKTextAlign.Left, _normalFont, _text);
            canvas.DrawText($"War / reorientation pressure: {active.ThreatLevel}/100", 412, 348, SKTextAlign.Left, _normalFont, _text);
            DrawThreatMeter(canvas, 412, 365, 600, active.ThreatLevel);
            _muted.Color = MutedColor;
            string assessment = active.ThreatLevel >= 70 ? "HIGH · strategic posture is elevated" :
                active.ThreatLevel >= 40 ? "MODERATE · monitor border and military changes" :
                "LOW · no elevated strategic pressure detected";
            canvas.DrawText(assessment, 412, 405, SKTextAlign.Left, _normalFont, _muted);
            canvas.DrawText($"Last director evaluation: T+{active.LastDecisionAt / 60:0.0} min", 412, 432, SKTextAlign.Left, _smallFont, _muted);
            canvas.DrawText("Threat is an estimate, not a declaration of war.", 412, 459, SKTextAlign.Left, _smallFont, _muted);
        }
        else
        {
            _text.Color = Gold;
            canvas.DrawText("MACROECONOMICS", 410, 260, SKTextAlign.Left, _headingFont, _text);
            _text.Color = TextColor;
            canvas.DrawText($"Territories / settlements: {active.TerritoryCount}", 412, 291, SKTextAlign.Left, _normalFont, _text);
            canvas.DrawText($"Treasury estimate: {active.Treasury:N0} K-Credits", 412, 318, SKTextAlign.Left, _normalFont, _text);
            canvas.DrawText($"Military power estimate: {active.MilitaryPower:N0}", 412, 345, SKTextAlign.Left, _normalFont, _text);
            canvas.DrawText($"Relationship with Kor: {active.RelationshipTo("Kor"):+#;-#;0}/100", 412, 372, SKTextAlign.Left, _normalFont, _text);
            _muted.Color = MutedColor;
            canvas.DrawText("Values are generated simulation estimates and update with the world.", 412, 410, SKTextAlign.Left, _smallFont, _muted);
            canvas.DrawText("Diplomatic actions are not enabled yet; this panel is read-only.", 412, 430, SKTextAlign.Left, _smallFont, _muted);
        }
    }

    private void DrawThreatMeter(SKCanvas canvas, float x, float y, float width, int value)
    {
        _fill.Color = new SKColor(40, 47, 44);
        canvas.DrawRoundRect(new SKRect(x, y, x + width, y + 14), 3, 3, _fill);
        _fill.Color = value >= 70 ? Crimson : value >= 40 ? Gold : new SKColor(75, 155, 105);
        canvas.DrawRoundRect(new SKRect(x, y, x + width * Math.Clamp(value, 0, 100) / 100f, y + 14), 3, 3, _fill);
    }

    private void DrawColonistRoster(SKCanvas canvas, ColonySimulation simulation)
    {
        var roster = simulation.Workers.Take(ColonyRosterLayout.VisibleSlots).ToArray();
        for (int i = 0; i < roster.Length; i++)
        {
            var worker = roster[i];
            float x = ColonyRosterLayout.Left + i * ColonyRosterLayout.Step;
            bool selected = simulation.SelectedWorkerId == worker.Id;
            string label = worker.Name.Length > 9 ? worker.Name[..8] + "…" : worker.Name;
            DrawButton(canvas, label, x, ColonyRosterLayout.Top, ColonyRosterLayout.ButtonWidth,
                ColonyRosterLayout.ButtonHeight, selected);
        }

        if (simulation.Workers.Count > ColonyRosterLayout.VisibleSlots)
        {
            _muted.Color = MutedColor;
            canvas.DrawText($"+{simulation.Workers.Count - ColonyRosterLayout.VisibleSlots}",
                394, 25, SKTextAlign.Left, _smallFont, _muted);
        }
    }

    private void DrawHud(SKCanvas canvas, ColonySimulation simulation, ArchitectureKind architectureMode,
        PlaceableKind objectType, bool placingObject, bool excavationMode, bool demolitionMode, int buildMenuPage, bool paused, float speedMultiplier, bool worldView,
        ContinuousWorldState spatialWorld, long? selectedWorldEntityId, WorldResourceKind? selectedShipmentResource)
    {
        _fill.Color = Background;
        canvas.DrawRect(new SKRect(0, 0, 1100, 62), _fill);
        _fill.Color = Panel;
        canvas.DrawRect(new SKRect(770, 62, 1100, 650), _fill);
        canvas.DrawRect(new SKRect(0, 650, 1100, 720), _fill);
        _line.Color = Border;
        _line.StrokeWidth = 1;
        canvas.DrawLine(0, 62, 1100, 62, _line);
        canvas.DrawLine(770, 62, 770, 650, _line);
        canvas.DrawLine(0, 650, 1100, 650, _line);

        _text.Color = Crimson;
        canvas.DrawText("KOR FRONTIER", 18, 27, SKTextAlign.Left, _headingFont, _text);
        _muted.Color = MutedColor;
        canvas.DrawText(worldView ? "WORLD / LIVE SIMULATION" : "COLONY / LIVE SIMULATION", 19, 47, SKTextAlign.Left, _smallFont, _muted);
        DrawColonistRoster(canvas, simulation);

        DrawFactionButton(canvas, false);
        _fill.Color = new SKColor(28, 35, 32);
        canvas.DrawRoundRect(new SKRect(1010, 10, 1084, 31), 3, 3, _fill);
        _text.Color = new SKColor(125, 220, 155);
        canvas.DrawText(paused ? "PAUSED" : "LIVE", 1047, 25, SKTextAlign.Center, _smallFont, _text);

        int x = 405;
        foreach (var resource in Enum.GetValues<ResourceKind>())
        {
            _fill.Color = new SKColor(24, 31, 29);
            canvas.DrawRoundRect(new SKRect(x, 10, x + 104, 32), 3, 3, _fill);
            _line.Color = Border;
            canvas.DrawRoundRect(new SKRect(x, 10, x + 104, 32), 3, 3, _line);
            _muted.Color = MutedColor;
            canvas.DrawText(resource.ToString().ToUpperInvariant(), x + 6, 24, SKTextAlign.Left, _smallFont, _muted);
            _text.Color = resource == ResourceKind.Components ? Gold : TextColor;
            canvas.DrawText(simulation.ResourceCount(resource).ToString(), x + 98, 24, SKTextAlign.Right, _smallFont, _text);
            x += 110;
        }

        // The side cockpit is split into three restrained modules; all displayed values come
        // from the live simulation/world model and all existing hit targets remain unchanged.
        _fill.Color = new SKColor(22, 28, 28);
        _line.Color = Border;
        canvas.DrawRoundRect(new SKRect(780, 75, 1090, 278), 4, 4, _fill);
        canvas.DrawRoundRect(new SKRect(780, 282, 1090, 485), 4, 4, _fill);
        canvas.DrawRoundRect(new SKRect(780, 488, 1090, 649), 4, 4, _fill);
        canvas.DrawRoundRect(new SKRect(780, 75, 1090, 278), 4, 4, _line);
        canvas.DrawRoundRect(new SKRect(780, 282, 1090, 485), 4, 4, _line);
        canvas.DrawRoundRect(new SKRect(780, 488, 1090, 649), 4, 4, _line);

        _text.Color = Gold;
        canvas.DrawText("COLONY STATUS", 790, 91, SKTextAlign.Left, _headingFont, _text);
        _text.Color = TextColor;
        canvas.DrawText($"Population: {simulation.Workers.Count}", 790, 119, SKTextAlign.Left, _normalFont, _text);
        canvas.DrawText($"Tile jobs done: {simulation.CompletedTileOrders}", 790, 143, SKTextAlign.Left, _normalFont, _text);
        canvas.DrawText($"Built objects: {simulation.CompletedObjects}", 790, 167, SKTextAlign.Left, _normalFont, _text);
        canvas.DrawText($"Open jobs: {simulation.PendingJobs}", 790, 191, SKTextAlign.Left, _normalFont, _text);
        canvas.DrawText($"Time: {(int)(simulation.ElapsedSeconds / 60)}m {((int)simulation.ElapsedSeconds % 60):00}s", 790, 215, SKTextAlign.Left, _normalFont, _text);
        canvas.DrawText(paused ? "SIMULATION PAUSED" : "SIMULATION RUNNING", 790, 239, SKTextAlign.Left, _normalFont, _text);
        _text.Color = simulation.InventoryFreeCapacity == 0 ? Crimson : TextColor;
        canvas.DrawText($"Storage {simulation.InventoryUsed}/{simulation.InventoryCapacity} | Wealth {simulation.Wealth.TotalWealth:0} | TP {simulation.Wealth.ThreatPoints}", 790, 263, SKTextAlign.Left, _smallFont, _text);

        _text.Color = Gold;
        canvas.DrawText(worldView ? "WORLD ENTITY INSPECTOR" : "STRATEGIC FRONTIER", 790, 300, SKTextAlign.Left, _headingFont, _text);
        _text.Color = TextColor;
        if (worldView)
        {
            var selected = selectedWorldEntityId.HasValue ? spatialWorld.Entities.FirstOrDefault(entity => entity.Id == selectedWorldEntityId.Value) : null;
            canvas.DrawText(selected == null ? "Click a symbol to inspect" : $"{selected.Name} / {selected.Kind}", 790, 320, SKTextAlign.Left, _smallFont, _text);
            string inventorySummary = selected != null && spatialWorld.Inventories.TryGetValue(selected.Id, out var inventory)
                ? $"W {inventory.GetValueOrDefault(WorldResourceKind.Wood):0} S {inventory.GetValueOrDefault(WorldResourceKind.Stone):0} F {inventory.GetValueOrDefault(WorldResourceKind.Food):0} A {inventory.GetValueOrDefault(WorldResourceKind.Alloy):0}"
                : selected == null ? "Select army/caravan to move" : $"Faction: {selected.Faction}";
            canvas.DrawText(inventorySummary, 790, 337, SKTextAlign.Left, _smallFont, _text);
        }
        else
        {
            canvas.DrawText($"Settlements {spatialWorld.Entities.Count(entity => entity.Kind == WorldEntityKind.Settlement)} | Caravans {spatialWorld.Entities.Count(entity => entity.Kind == WorldEntityKind.Caravan)} | Cargo {spatialWorld.Shipments.Count(shipment => shipment.Status == WorldShipmentStatus.InTransit)}", 790, 320, SKTextAlign.Left, _smallFont, _text);
            canvas.DrawText($"World clock {spatialWorld.ClockSeconds / 60:0} min | Blight fronts {spatialWorld.Entities.Count(entity => entity.Kind == WorldEntityKind.BlightFront)}", 790, 337, SKTextAlign.Left, _smallFont, _text);
        }

        _text.Color = Gold;
        canvas.DrawText(worldView ? "WORLD ECOLOGY" : simulation.SelectedWorkOrderId.HasValue ? "WORK ORDER INSPECTOR" : simulation.SelectedObjectId.HasValue ? "OBJECT INSPECTOR" : "POPULATION", 790, 356, SKTextAlign.Left, _smallFont, _text);
        if (worldView)
        {
            int animalCount = spatialWorld.Ecology.Herds.Sum(herd => herd.Population);
            canvas.DrawText($"Herds {spatialWorld.Ecology.Herds.Count} | Estimated animals {animalCount}", 790, 376, SKTextAlign.Left, _smallFont, _text);
            canvas.DrawText($"Active Blight hotspots {spatialWorld.Ecology.Hotspots.Count(item => item.IsActive)}", 790, 395, SKTextAlign.Left, _smallFont, _text);
            var latestEvent = spatialWorld.Ecology.RecentEvents.LastOrDefault();
            _muted.Color = MutedColor;
            canvas.DrawText(latestEvent == null ? "No world events recorded yet" : $"Latest: {latestEvent.Title}", 790, 417, SKTextAlign.Left, _smallFont, _muted);
            if (latestEvent != null)
                canvas.DrawText($"{latestEvent.Kind} | {latestEvent.OccurredAt / 60:0} min", 790, 435, SKTextAlign.Left, _smallFont, _muted);
        }
        else if (simulation.SelectedWorkOrderId.HasValue)
        {
            _text.Color = TextColor;
            canvas.DrawText(simulation.SelectedWorkOrderSummary, 790, 376, SKTextAlign.Left, _smallFont, _text);
            _muted.Color = MutedColor;
            canvas.DrawText("Priority 1 = low   /   5 = critical", 790, 397, SKTextAlign.Left, _smallFont, _muted);
            _text.Color = Gold;
            canvas.DrawText($"Current priority: {simulation.SelectedWorkOrderPriority}/5", 790, 416, SKTextAlign.Left, _smallFont, _text);
            DrawButton(canvas, "P -", 784, 425, 54, 22, simulation.SelectedWorkOrderPriority <= 1);
            DrawButton(canvas, "P +", 842, 425, 54, 22, simulation.SelectedWorkOrderPriority >= 5);
            DrawButton(canvas, "CANCEL ORDER", 904, 425, 182, 22);
            _muted.Color = MutedColor;
            canvas.DrawText(simulation.SelectedWorkOrderDiagnostic, 790, 462, SKTextAlign.Left, _smallFont, _muted);
        }
        else if (simulation.SelectedObjectId.HasValue && simulation.SelectedObject is { } selectedObject)
        {
            _text.Color = TextColor;
            canvas.DrawText(simulation.SelectedObjectSummary, 790, 376, SKTextAlign.Left, _smallFont, _text);
            _text.Color = Gold;
            canvas.DrawText($"Status {selectedObject.Status} · Condition {selectedObject.Health:0}/{selectedObject.MaxHealth:0}", 790, 397, SKTextAlign.Left, _smallFont, _text);
            _muted.Color = MutedColor;
            canvas.DrawText($"Footprint {selectedObject.Width}×{selectedObject.Height} · Position {selectedObject.Origin.X}, {selectedObject.Origin.Y}", 790, 416, SKTextAlign.Left, _smallFont, _muted);
            canvas.DrawText(simulation.SelectedObjectDiagnostic, 790, 435, SKTextAlign.Left, _smallFont, _muted);
        }
        else
        {
            int row = 376;
            // The inspector has fixed height: show a small sample instead of drawing thousands
            // of rows over the controls. A dedicated filterable resident inspector is a later UI slice.
            foreach (var worker in simulation.Workers.Take(2))
            {
                _text.Color = simulation.SelectedWorkerId == worker.Id ? Gold : TextColor;
                var citizen = simulation.CitizenForWorker(worker.Id);
                string legacyJob = worker.Task?.Kind.ToString() ?? "Idle";
                string citizenState = citizen?.State.ToString() ?? "Idle";
                canvas.DrawText($"{worker.Name}: H {Math.Min(100, (int)worker.Hunger)} | M {(citizen?.Mood ?? 100f):0} | S {worker.Stress:0}", 790, row, SKTextAlign.Left, _smallFont, _text);
                _muted.Color = MutedColor;
                canvas.DrawText($"{legacyJob} / {citizenState}", 790, row + 15, SKTextAlign.Left, _smallFont, _muted);
                var diagnostic = simulation.InspectWorker(worker.Id);
                if (diagnostic != null)
                {
                    string reason = diagnostic.PrimaryReason.Length > 43
                        ? diagnostic.PrimaryReason[..40] + "..."
                        : diagnostic.PrimaryReason;
                    _text.Color = diagnostic.Hunger < 30f || diagnostic.Stress >= 80f ? Crimson : Gold;
                    canvas.DrawText(reason, 790, row + 29, SKTextAlign.Left, _smallFont, _text);
                }
                row += 55;
            }
            if (simulation.Workers.Count > 2)
            {
                _muted.Color = MutedColor;
                canvas.DrawText($"+ {simulation.Workers.Count - 2} more residents", 790, 478, SKTextAlign.Left, _smallFont, _muted);
            }
        }

        _text.Color = Gold;
        canvas.DrawText("WORLD CONTROLS", 790, 495, SKTextAlign.Left, _smallFont, _text);
        DrawButton(canvas, paused ? "RESUME" : "PAUSE", 784, 501, 66, 22, paused);
        DrawButton(canvas, "SPEED -", 856, 501, 58, 22);
        DrawButton(canvas, "SPEED +", 920, 501, 58, 22);
        DrawButton(canvas, "CANCEL", 984, 501, 66, 22);
        _muted.Color = MutedColor;
        canvas.DrawText($"{speedMultiplier:0.##}x speed", 1054, 516, SKTextAlign.Left, _smallFont, _muted);

        DrawButton(canvas, "SAVE", 784, 527, 66, 22);
        DrawButton(canvas, "LOAD", 854, 527, 66, 22);
        DrawButton(canvas, "ZOOM -", 924, 527, 66, 22);
        DrawButton(canvas, "ZOOM +", 994, 527, 66, 22);

        _text.Color = Gold;
        canvas.DrawText(worldView ? "TRADE & LOGISTICS" : "ARCHITECT", 790, 558, SKTextAlign.Left, _smallFont, _text);
        if (worldView)
        {
            DrawButton(canvas, "WOOD", 784, 566, 54, 22, selectedShipmentResource == WorldResourceKind.Wood);
            DrawButton(canvas, "STONE", 842, 566, 54, 22, selectedShipmentResource == WorldResourceKind.Stone);
            DrawButton(canvas, "FOOD", 900, 566, 54, 22, selectedShipmentResource == WorldResourceKind.Food);
            DrawButton(canvas, "ALLOY", 958, 566, 54, 22, selectedShipmentResource == WorldResourceKind.Alloy);
            DrawButton(canvas, "CLEAR", 1016, 566, 54, 22);
            DrawButton(canvas, "CLEAR SHIPMENT", 784, 593, 302, 22);
            _muted.Color = MutedColor;
        }
        else
        {
            _text.Color = MutedColor;
            canvas.DrawText("Use the BUILD submenu in the right panel.", 790, 582, SKTextAlign.Left, _smallFont, _muted);
        }

        if (!worldView)
            DrawBuildMenuOverlay(canvas, simulation, architectureMode, objectType, placingObject, excavationMode, demolitionMode, buildMenuPage);

        _muted.Color = MutedColor;
        string actionHint = worldView
            ? (selectedShipmentResource.HasValue ? $"Cargo: 25 {selectedShipmentResource.Value}; click caravan, then destination" : "Select cargo, then caravan and destination")
            : demolitionMode
                ? "REMOVE: click a completed structure or built tile"
                : excavationMode
                    ? "DIG: click solid rock; mined stone appears on the ground and must be hauled"
                    : simulation.ConstructionCostStatus(architectureMode, objectType, placingObject);
        _muted.Color = !worldView && (placingObject || architectureMode != ArchitectureKind.None) &&
                       actionHint.Contains("WAITING:", StringComparison.Ordinal)
            ? Crimson : MutedColor;
        canvas.DrawText(actionHint, 790, 640, SKTextAlign.Left, _smallFont, _muted);

        string mode = demolitionMode ? "DEMOLITION MODE" : placingObject ? $"OBJECT BLUEPRINT: {objectType}" :
            excavationMode ? "EXCAVATION DESIGNATION" :
            architectureMode != ArchitectureKind.None ? $"TILE CONSTRUCTION: {architectureMode}" : "SELECT / INSPECT";
        _text.Color = paused ? Gold : MutedColor;
        canvas.DrawText($"{mode}{(paused ? "  [PAUSED]" : "")}", 18, 675, SKTextAlign.Left, _normalFont, _text);
        _text.Color = TextColor;
        canvas.DrawText(simulation.Message, 18, 700, SKTextAlign.Left, _smallFont, _text);
        if (!worldView) DrawButton(canvas, "RECRUIT +10", 920, 660, 165, 27);
    }

    private void DrawBuildMenuOverlay(SKCanvas canvas, ColonySimulation simulation,
        ArchitectureKind architectureMode, PlaceableKind objectType, bool placingObject,
        bool excavationMode, bool demolitionMode, int selectedCategory)
    {
        // RimWorld-inspired build palette: categories are the top-level menu and each category
        // opens a compact, readable submenu with large targets rather than a row of tiny buttons.
        _fill.Color = new SKColor(8, 12, 13, 252);
        canvas.DrawRoundRect(new SKRect(778, 74, 1092, 337), 6, 6, _fill);
        _line.Color = Gold;
        _line.StrokeWidth = 1;
        canvas.DrawRoundRect(new SKRect(778, 74, 1092, 337), 6, 6, _line);
        _text.Color = Gold;
        canvas.DrawText("ARCHITECT", 790, 98, SKTextAlign.Left, _headingFont, _text);
        _muted.Color = MutedColor;
        canvas.DrawText("Choose a category, then a tool or blueprint.", 790, 113, SKTextAlign.Left, _smallFont, _muted);

        for (int category = 0; category < BuildPaletteLayout.CategoryCount; category++)
        {
            float x = BuildPaletteLayout.Left + category * (BuildPaletteLayout.CategoryWidth + BuildPaletteLayout.CategoryGap);
            DrawButton(canvas, BuildPaletteLayout.CategoryLabel(category), x, BuildPaletteLayout.CategoryTop,
                BuildPaletteLayout.CategoryWidth, BuildPaletteLayout.CategoryHeight, selectedCategory == category);
        }

        for (int slot = 0; slot < BuildPaletteLayout.ItemCount; slot++)
        {
            var action = BuildPaletteLayout.ActionAt(selectedCategory, slot);
            if (action == BuildPaletteAction.None) continue;
            int row = slot / BuildPaletteLayout.ItemsPerRow;
            int column = slot % BuildPaletteLayout.ItemsPerRow;
            float x = BuildPaletteLayout.Left + column * (BuildPaletteLayout.ItemWidth + BuildPaletteLayout.ItemColumnGap);
            float y = BuildPaletteLayout.FirstRowTop + row * (BuildPaletteLayout.ItemHeight + BuildPaletteLayout.ItemGap);
            bool active = action switch
            {
                BuildPaletteAction.Wall => architectureMode == ArchitectureKind.Wall,
                BuildPaletteAction.Floor => architectureMode == ArchitectureKind.Floor,
                BuildPaletteAction.Path => architectureMode == ArchitectureKind.Path,
                BuildPaletteAction.Door => architectureMode == ArchitectureKind.Door,
                BuildPaletteAction.Conduit => architectureMode == ArchitectureKind.Conduit,
                BuildPaletteAction.Dig => excavationMode,
                BuildPaletteAction.Remove => demolitionMode,
                BuildPaletteAction.Cancel => architectureMode == ArchitectureKind.None && !placingObject && !excavationMode && !demolitionMode,
                BuildPaletteAction.SleepingPod => placingObject && objectType == PlaceableKind.SleepingPod,
                BuildPaletteAction.DiningTable => placingObject && objectType == PlaceableKind.DiningTable,
                BuildPaletteAction.ComfortBench => placingObject && objectType == PlaceableKind.ComfortBench,
                BuildPaletteAction.Lamp => placingObject && objectType == PlaceableKind.Lamp,
                BuildPaletteAction.Workbench => placingObject && objectType == PlaceableKind.Workbench,
                BuildPaletteAction.Generator => placingObject && objectType == PlaceableKind.Generator,
                BuildPaletteAction.StorageRack => placingObject && objectType == PlaceableKind.StorageRack,
                BuildPaletteAction.Garden => placingObject && objectType == PlaceableKind.Garden,
                _ => false
            };
            DrawButton(canvas, BuildPaletteLayout.ItemLabel(selectedCategory, slot), x, y,
                BuildPaletteLayout.ItemWidth, BuildPaletteLayout.ItemHeight, active);
        }
    }

    private void DrawButton(SKCanvas canvas, string label, float x, float y, float width, float height, bool active = false)
    {
        _fill.Color = active ? new SKColor(92, 38, 43) : new SKColor(31, 39, 37);
        canvas.DrawRoundRect(new SKRect(x, y, x + width, y + height), 3, 3, _fill);
        _line.Color = active ? Gold : Border;
        _line.StrokeWidth = 1;
        canvas.DrawRoundRect(new SKRect(x, y, x + width, y + height), 3, 3, _line);
        _text.Color = active ? Gold : TextColor;
        canvas.DrawText(label, x + width / 2, y + height / 2 + 4, SKTextAlign.Center, _smallFont, _text);
    }

    private static SKRect Inset(SKRect rect, float amount)
        => new(rect.Left + amount, rect.Top + amount, rect.Right - amount, rect.Bottom - amount);

    public void Dispose()
    {
        _fill.Dispose();
        _line.Dispose();
        _text.Dispose();
        _muted.Dispose();
        _normalFont.Dispose();
        _smallFont.Dispose();
        _headingFont.Dispose();
        _titleFont.Dispose();
    }
}
