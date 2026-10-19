using KorTheBlight.Campaign;
using KorTheBlight.Simulation;
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
    private readonly SKFont _headingFont = new(SKTypeface.FromFamilyName("sans-serif", SKFontStyle.Bold), 17);
    private readonly SKFont _titleFont = new(SKTypeface.FromFamilyName("sans-serif", SKFontStyle.Bold), 22);

    public void Draw(SKCanvas canvas, ColonySimulation simulation, float cameraX, float cameraY, float zoom,
        ArchitectureKind architectureMode, PlaceableKind objectType, bool placingObject, bool paused, CampaignSimulation campaign)
    {
        canvas.Clear(Background);
        DrawMap(canvas, simulation, cameraX, cameraY, zoom);
        DrawDeposits(canvas, simulation, cameraX, cameraY, zoom);
        DrawObjects(canvas, simulation, cameraX, cameraY, zoom);
        DrawWorkers(canvas, simulation, cameraX, cameraY, zoom);
        DrawHud(canvas, simulation, architectureMode, objectType, placingObject, paused, campaign);
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
                _line.Color = order.Status == ConstructionStatus.UnderConstruction ? Gold : new SKColor(177, 174, 148);
                _line.StrokeWidth = Math.Max(1f, zoom * 1.4f);
                canvas.DrawRect(Inset(rect, size * 0.12f), _line);
                _fill.Color = new SKColor(201, 179, 105, 40);
                canvas.DrawRect(rect, _fill);
            }

            _line.Color = new SKColor(66, 79, 72, 170);
            _line.StrokeWidth = Math.Max(0.5f, zoom * 0.6f);
            canvas.DrawRect(rect, _line);
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

        foreach (var deposit in simulation.MaterialDeposits.Where(d => d.Amount > 0))
        {
            var point = OrthogonalGridProjection.TileCenter(deposit.Cell.X, deposit.Cell.Y, cameraX, cameraY, zoom);
            float size = 13f * zoom;
            var rect = new SKRect(point.X - size / 2, point.Y - size / 2, point.X + size / 2, point.Y + size / 2);
            _fill.Color = deposit.MaterialId switch
            {
                "ore" => new SKColor(180, 111, 79),
                "rare_earth" => new SKColor(176, 98, 176),
                "crystal" => new SKColor(93, 177, 201),
                "reagents" => new SKColor(182, 151, 73),
                "water" => new SKColor(73, 145, 206),
                "biomass" => new SKColor(93, 163, 91),
                "recycled_feedstock" => new SKColor(151, 159, 151),
                "clay" => new SKColor(168, 111, 86),
                "sand" => new SKColor(205, 184, 112),
                "copper_ore" => new SKColor(190, 113, 76),
                "silica" => new SKColor(116, 174, 193),
                "fuel" => new SKColor(74, 77, 84),
                "medicine_precursor" => new SKColor(101, 175, 143),
                "blight_sample" => new SKColor(181, 49, 69),
                "exotic_matter" => new SKColor(118, 88, 190),
                _ => new SKColor(176, 116, 91)
            };
            canvas.DrawRect(rect, _fill);
            _line.Color = new SKColor(12, 15, 14);
            canvas.DrawRect(rect, _line);
            string letter = deposit.MaterialId switch
            {
                "rare_earth" => "R",
                "recycled_feedstock" => "Rf",
                "copper_ore" => "Cu",
                "silica" => "Si",
                "clay" => "Cl",
                "sand" => "Sa",
                "medicine_precursor" => "Mp",
                "blight_sample" => "B",
                "exotic_matter" => "X",
                _ => deposit.MaterialId[..1].ToUpperInvariant()
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

            var catalogBuilding = item.CatalogBuildingId == null ? null :
                ProgressionCatalog.Buildings.FirstOrDefault(building => building.Id == item.CatalogBuildingId);
            _fill.Color = item.Kind switch
            {
                PlaceableKind.Habitat => new SKColor(72, 27, 36),
                PlaceableKind.Stockpile => new SKColor(54, 60, 52),
                PlaceableKind.Workbench => new SKColor(61, 69, 74),
                PlaceableKind.SleepingPod => new SKColor(59, 67, 77),
                PlaceableKind.StorageRack => new SKColor(82, 70, 49),
                PlaceableKind.Generator => new SKColor(72, 48, 79),
                PlaceableKind.Garden => new SKColor(39, 75, 48),
                PlaceableKind.IndustryBuilding => catalogBuilding?.Family switch
                {
                    "housing" => new SKColor(72, 27, 36),
                    "storage" => new SKColor(82, 70, 49),
                    "workshop" or "fabrication" => new SKColor(61, 69, 74),
                    "farm" => new SKColor(39, 75, 48),
                    "power" or "energy" => new SKColor(72, 48, 79),
                    "medical" => new SKColor(55, 82, 78),
                    "defense" or "containment" => new SKColor(112, 38, 48),
                    _ => new SKColor(48, 50, 50)
                },
                _ => new SKColor(48, 50, 50)
            };
            canvas.DrawRect(Inset(rect, size * 0.04f), _fill);
            _line.Color = item.Status == ConstructionStatus.Complete ? new SKColor(153, 168, 151) : Gold;
            _line.StrokeWidth = Math.Max(1f, zoom * 1.4f);
            canvas.DrawRect(Inset(rect, size * 0.07f), _line);

            string label = item.Kind == PlaceableKind.IndustryBuilding
                ? catalogBuilding == null ? "I" : catalogBuilding.Family.Substring(0, 1).ToUpperInvariant()
                : item.Kind switch
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
                _text.Color = Gold;
                canvas.DrawText($"{Math.Min(100, (int)(item.WorkDone / Math.Max(1, item.WorkRequired) * 100))}%", rect.MidX, rect.Bottom - 3f * zoom, SKTextAlign.Center, _smallFont, _text);
                _text.Color = TextColor;
            }
        }
    }

    private void DrawWorkers(SKCanvas canvas, ColonySimulation simulation, float cameraX, float cameraY, float zoom)
    {
        foreach (var worker in simulation.Workers)
        {
            var point = OrthogonalGridProjection.WorldToScreen(worker.X, worker.Y, cameraX, cameraY, zoom);
            float radius = Math.Max(3f, 4.5f * zoom);
            _fill.Color = worker.Hungry ? new SKColor(222, 149, 73) : WorkerColor;
            canvas.DrawCircle(point.X, point.Y, radius, _fill);
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
        PlaceableKind objectType, bool placingObject, bool paused, CampaignSimulation campaign)
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
        canvas.DrawText("COLONY / WORLD SIMULATION FOUNDATION", 19, 47, SKTextAlign.Left, _smallFont, _muted);

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
        canvas.DrawText("STRATEGIC FRONTIER", 790, 289, SKTextAlign.Left, _headingFont, _text);
        _text.Color = TextColor;
        canvas.DrawText($"Turn {campaign.State.Turn} | S {campaign.State.Supplies} A {campaign.State.Alloy}", 790, 309, SKTextAlign.Left, _smallFont, _text);
        canvas.DrawText($"E {campaign.State.Energy} | K {campaign.State.Knowledge} | Blight {campaign.State.Regions.Count(region => region.Owner == "Blight")}", 790, 326, SKTextAlign.Left, _smallFont, _text);

        _text.Color = Gold;
        canvas.DrawText("POPULATION", 790, 350, SKTextAlign.Left, _headingFont, _text);
        int row = 376;
        foreach (var worker in simulation.Workers)
        {
            _text.Color = simulation.SelectedWorkerId == worker.Id ? Gold : TextColor;
            string job = worker.Task?.Kind.ToString() ?? "Idle";
            canvas.DrawText($"{worker.Name}: H {Math.Min(100, (int)worker.Hunger)}", 790, row, SKTextAlign.Left, _smallFont, _text);
            _muted.Color = MutedColor;
            canvas.DrawText(job, 790, row + 15, SKTextAlign.Left, _smallFont, _muted);
            row += 43;
        }

        _text.Color = Gold;
        canvas.DrawText("BUILD / CAMPAIGN", 790, 493, SKTextAlign.Left, _headingFont, _text);
        _text.Color = TextColor;
        canvas.DrawText("1 Wall 2 Floor 3 Path | 4 Door 5 Conduit", 790, 515, SKTextAlign.Left, _smallFont, _text);
        canvas.DrawText($"O place {objectType} | Tab next | Esc cancel", 790, 536, SKTextAlign.Left, _smallFont, _text);
        canvas.DrawText("Left click design/select", 790, 557, SKTextAlign.Left, _smallFont, _text);
        canvas.DrawText("Right click move/gather", 790, 578, SKTextAlign.Left, _smallFont, _text);
        canvas.DrawText("WASD camera | +/- zoom | Space pause", 790, 599, SKTextAlign.Left, _smallFont, _text);
        canvas.DrawText("T turn | I import | E export | F5/F9 save", 790, 620, SKTextAlign.Left, _smallFont, _text);

        string mode = placingObject ? $"OBJECT BLUEPRINT: {objectType}" :
            architectureMode != ArchitectureKind.None ? $"TILE CONSTRUCTION: {architectureMode}" : "SELECT / INSPECT";
        _text.Color = paused ? Gold : MutedColor;
        canvas.DrawText($"{mode}{(paused ? "  [PAUSED]" : "")}", 18, 675, SKTextAlign.Left, _normalFont, _text);
        _text.Color = TextColor;
        canvas.DrawText(simulation.Message, 18, 700, SKTextAlign.Left, _smallFont, _text);
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
