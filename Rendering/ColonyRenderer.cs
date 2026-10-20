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
        ArchitectureKind architectureMode, PlaceableKind objectType, bool placingObject, bool paused, float speedMultiplier,
        ContinuousWorldState spatialWorld, float worldCameraX, float worldCameraY, long? selectedWorldEntityId, WorldResourceKind? selectedShipmentResource)
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
        }
        DrawHud(canvas, simulation, architectureMode, objectType, placingObject, paused, speedMultiplier, worldView, spatialWorld, selectedWorldEntityId, selectedShipmentResource);
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
            canvas.DrawText(entity.Name, point.X + radius + 5, point.Y - 3, SKTextAlign.Left, _smallFont, _text);
            _muted.Color = MutedColor;
            canvas.DrawText(entity.Faction, point.X + radius + 5, point.Y + 10, SKTextAlign.Left, _smallFont, _muted);
        }
        canvas.Restore();

        _text.Color = Gold;
        canvas.DrawText("CONTINUOUS WORLD SPACE", 18, 86, SKTextAlign.Left, _headingFont, _text);
        _muted.Color = MutedColor;
        canvas.DrawText($"World clock {world.ClockSeconds / 60:0} min  |  {world.Entities.Count} entities  |  scale {scale:0.0000} px/unit", 18, 105, SKTextAlign.Left, _smallFont, _muted);
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

            _line.Color = new SKColor(66, 79, 72, 170);
            _line.StrokeWidth = Math.Max(0.5f, zoom * 0.6f);
            canvas.DrawRect(rect, _line);

            // At settlement zoom, a compact glyph layer makes the map legible in the
            // Dwarf-Fortress-inspired symbolic style while retaining colour for quick scanning.
            if (zoom >= 0.82f)
            {
                string glyph = tile.Architecture switch
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

    private void DrawHud(SKCanvas canvas, ColonySimulation simulation, ArchitectureKind architectureMode,
        PlaceableKind objectType, bool placingObject, bool paused, float speedMultiplier, bool worldView,
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

        int x = 405;
        foreach (var resource in Enum.GetValues<ResourceKind>())
        {
            _text.Color = TextColor;
            canvas.DrawText($"{resource.ToString().ToUpperInvariant()} {simulation.ResourceCount(resource)}", x, 27, SKTextAlign.Left, _smallFont, _text);
            x += resource == ResourceKind.Components ? 0 : 80;
        }

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
        canvas.DrawText($"Storage: {simulation.InventoryUsed}/{simulation.InventoryCapacity}", 790, 263, SKTextAlign.Left, _normalFont, _text);

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
                row += 43;
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
            DrawButton(canvas, "WALL", 784, 566, 54, 22, architectureMode == ArchitectureKind.Wall);
            DrawButton(canvas, "FLOOR", 842, 566, 54, 22, architectureMode == ArchitectureKind.Floor);
            DrawButton(canvas, "PATH", 900, 566, 54, 22, architectureMode == ArchitectureKind.Path);
            DrawButton(canvas, "DOOR", 958, 566, 54, 22, architectureMode == ArchitectureKind.Door);
            DrawButton(canvas, "CONDUIT", 1016, 566, 54, 22, architectureMode == ArchitectureKind.Conduit);
            DrawButton(canvas, "BENCH", 784, 593, 47, 22, placingObject && objectType == PlaceableKind.Workbench);
            DrawButton(canvas, "POD", 835, 593, 47, 22, placingObject && objectType == PlaceableKind.SleepingPod);
            DrawButton(canvas, "RACK", 886, 593, 47, 22, placingObject && objectType == PlaceableKind.StorageRack);
            DrawButton(canvas, "GEN", 937, 593, 47, 22, placingObject && objectType == PlaceableKind.Generator);
            DrawButton(canvas, "GARDEN", 988, 593, 47, 22, placingObject && objectType == PlaceableKind.Garden);
            DrawButton(canvas, "CANCEL", 1039, 593, 47, 22);
        }

        _muted.Color = MutedColor;
        string actionHint = worldView
            ? (selectedShipmentResource.HasValue ? $"Cargo: 25 {selectedShipmentResource.Value}; click caravan, then destination" : "Select cargo, then caravan and destination")
            : simulation.ConstructionCostStatus(architectureMode, objectType, placingObject);
        _muted.Color = !worldView && (placingObject || architectureMode != ArchitectureKind.None) &&
                       actionHint.Contains("WAITING:", StringComparison.Ordinal)
            ? Crimson : MutedColor;
        canvas.DrawText(actionHint, 790, 635, SKTextAlign.Left, _smallFont, _muted);
        canvas.DrawText(worldView ? "Right click: move selected army/caravan" : "Right click: order colonist", 790, 646, SKTextAlign.Left, _smallFont, _muted);

        string mode = placingObject ? $"OBJECT BLUEPRINT: {objectType}" :
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
