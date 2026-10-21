using KorTheBlight.Simulation;
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
        ContinuousWorldState spatialWorld, float worldCameraX, float worldCameraY, long? selectedWorldEntityId, WorldResourceKind? selectedShipmentResource,
        float mouseScreenX, float mouseScreenY, bool factionDashboardOpen, string? selectedFactionName, int factionTab, bool workMatrixOpen, int workerInspectorTab, int workMatrixPage)
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
            DrawPlacementPreview(canvas, simulation, cameraX, cameraY, zoom, architectureMode, objectType, placingObject, excavationMode, demolitionMode, mouseScreenX, mouseScreenY);
        }
        DrawHud(canvas, simulation, architectureMode, objectType, placingObject, excavationMode, demolitionMode, buildMenuPage, paused, speedMultiplier, worldView, spatialWorld, selectedWorldEntityId, selectedShipmentResource);
        DrawFactionButton(canvas, factionDashboardOpen);
        DrawWorkMatrixButton(canvas, workMatrixOpen);
        if (!worldView && !workMatrixOpen && simulation.SelectedWorkerId.HasValue)
            DrawWorkerInspector(canvas, simulation, workerInspectorTab);
        if (factionDashboardOpen) DrawFactionDashboard(canvas, spatialWorld, selectedFactionName, factionTab);
        if (workMatrixOpen) DrawWorkMatrix(canvas, simulation, workMatrixPage);
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

            _fill.Color = item.Kind switch
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
            canvas.DrawRect(Inset(rect, size * 0.04f), _fill);
            _line.Color = item.Status switch
            {
                ConstructionStatus.Complete => new SKColor(153, 168, 151),
                ConstructionStatus.WaitingForMaterials => Crimson,
                ConstructionStatus.Blocked => new SKColor(110, 73, 76),
                _ => Gold
            };
            _line.StrokeWidth = Math.Max(1f, zoom * 1.4f);
            canvas.DrawRect(Inset(rect, size * 0.07f), _line);

            string label = item.Kind switch
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
            canvas.DrawText(label, rect.MidX, rect.MidY + 4f * zoom, SKTextAlign.Center, _headingFont, _text);

            if (!item.IsBuilt)
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
        canvas.DrawText($"{worker.Name}  /  COLONIST INSPECTOR", 24, 497, SKTextAlign.Left, _headingFont, _text);
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
        canvas.DrawText("KOR ETERNAL FRONTIER", 18, 27, SKTextAlign.Left, _titleFont, _text);
        _muted.Color = MutedColor;
        canvas.DrawText(worldView ? "CONTINUOUS WORLD / SINGLE SIMULATION" : "SYMBOLIC COLONY GRID / LIVE SIMULATION", 19, 47, SKTextAlign.Left, _smallFont, _muted);

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
        canvas.DrawText(worldView ? "WORLD ENTITY INSPECTOR" : "STRATEGIC FRONTIER", 790, 289, SKTextAlign.Left, _headingFont, _text);
        _text.Color = TextColor;
        if (worldView)
        {
            var selected = selectedWorldEntityId.HasValue ? spatialWorld.Entities.FirstOrDefault(entity => entity.Id == selectedWorldEntityId.Value) : null;
            canvas.DrawText(selected == null ? "Click a symbol to inspect" : $"{selected.Name} / {selected.Kind}", 790, 309, SKTextAlign.Left, _smallFont, _text);
            string inventorySummary = selected != null && spatialWorld.Inventories.TryGetValue(selected.Id, out var inventory)
                ? $"W {inventory.GetValueOrDefault(WorldResourceKind.Wood):0} S {inventory.GetValueOrDefault(WorldResourceKind.Stone):0} F {inventory.GetValueOrDefault(WorldResourceKind.Food):0} A {inventory.GetValueOrDefault(WorldResourceKind.Alloy):0}"
                : selected == null ? "Select army/caravan to move" : $"Faction: {selected.Faction}";
            canvas.DrawText(inventorySummary, 790, 326, SKTextAlign.Left, _smallFont, _text);
        }
        else
        {
            canvas.DrawText($"Settlements {spatialWorld.Entities.Count(entity => entity.Kind == WorldEntityKind.Settlement)} | Caravans {spatialWorld.Entities.Count(entity => entity.Kind == WorldEntityKind.Caravan)} | Cargo {spatialWorld.Shipments.Count(shipment => shipment.Status == WorldShipmentStatus.InTransit)}", 790, 309, SKTextAlign.Left, _smallFont, _text);
            canvas.DrawText($"World clock {spatialWorld.ClockSeconds / 60:0} min | Blight fronts {spatialWorld.Entities.Count(entity => entity.Kind == WorldEntityKind.BlightFront)}", 790, 326, SKTextAlign.Left, _smallFont, _text);
        }

        _text.Color = Gold;
        canvas.DrawText(worldView ? "WORLD ECOLOGY" : simulation.SelectedWorkOrderId.HasValue ? "WORK ORDER INSPECTOR" : "POPULATION", 790, 350, SKTextAlign.Left, _headingFont, _text);
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
                canvas.DrawText($"+ {simulation.Workers.Count - 2} more residents", 790, 466, SKTextAlign.Left, _smallFont, _muted);
            }
        }

        _text.Color = Gold;
        canvas.DrawText("WORLD CONTROLS", 790, 493, SKTextAlign.Left, _headingFont, _text);
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
        canvas.DrawText(worldView ? "TRADE & LOGISTICS" : "BUILD MENU", 790, 558, SKTextAlign.Left, _headingFont, _text);
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
            DrawButton(canvas, "WALL", 784, 566, 45, 22, architectureMode == ArchitectureKind.Wall);
            DrawButton(canvas, "FLOOR", 834, 566, 45, 22, architectureMode == ArchitectureKind.Floor);
            DrawButton(canvas, "PATH", 884, 566, 45, 22, architectureMode == ArchitectureKind.Path);
            DrawButton(canvas, "DOOR", 934, 566, 45, 22, architectureMode == ArchitectureKind.Door);
            DrawButton(canvas, "COND", 984, 566, 45, 22, architectureMode == ArchitectureKind.Conduit);
            DrawButton(canvas, "DIG", 1034, 566, 45, 22, excavationMode);
            if (buildMenuPage == 0)
            {
                DrawButton(canvas, "BENCH", 784, 593, 47, 22, placingObject && objectType == PlaceableKind.Workbench);
                DrawButton(canvas, "POD", 835, 593, 47, 22, placingObject && objectType == PlaceableKind.SleepingPod);
                DrawButton(canvas, "RACK", 886, 593, 47, 22, placingObject && objectType == PlaceableKind.StorageRack);
                DrawButton(canvas, "GEN", 937, 593, 47, 22, placingObject && objectType == PlaceableKind.Generator);
                DrawButton(canvas, "MORE >", 988, 593, 47, 22, false);
            }
            else
            {
                DrawButton(canvas, "GARDEN", 784, 593, 47, 22, placingObject && objectType == PlaceableKind.Garden);
                DrawButton(canvas, "TABLE", 835, 593, 47, 22, placingObject && objectType == PlaceableKind.DiningTable);
                DrawButton(canvas, "LAMP", 886, 593, 47, 22, placingObject && objectType == PlaceableKind.Lamp);
                DrawButton(canvas, "SEAT", 937, 593, 47, 22, placingObject && objectType == PlaceableKind.ComfortBench);
                DrawButton(canvas, "< BACK", 988, 593, 47, 22, false);
            }
            DrawButton(canvas, demolitionMode ? "DONE" : "REMOVE", 1039, 593, 47, 22, demolitionMode);
        }

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
        canvas.DrawText(actionHint, 790, 635, SKTextAlign.Left, _smallFont, _muted);
        canvas.DrawText(worldView ? "Right click: move selected army/caravan" : "Right click: order colonist", 790, 646, SKTextAlign.Left, _smallFont, _muted);

        string mode = demolitionMode ? "DEMOLITION MODE" : placingObject ? $"OBJECT BLUEPRINT: {objectType}" :
            excavationMode ? "EXCAVATION DESIGNATION" :
            architectureMode != ArchitectureKind.None ? $"TILE CONSTRUCTION: {architectureMode}" : "SELECT / INSPECT";
        _text.Color = paused ? Gold : MutedColor;
        canvas.DrawText($"{mode}{(paused ? "  [PAUSED]" : "")}", 18, 675, SKTextAlign.Left, _normalFont, _text);
        _text.Color = TextColor;
        canvas.DrawText(simulation.Message, 18, 700, SKTextAlign.Left, _smallFont, _text);
        if (!worldView) DrawButton(canvas, "RECRUIT +10", 920, 660, 165, 27);
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
