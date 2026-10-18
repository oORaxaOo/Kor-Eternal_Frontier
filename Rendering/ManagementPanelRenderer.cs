using KorTheBlight.Campaign;
using KorTheBlight.Simulation;
using SkiaSharp;

namespace KorTheBlight.Rendering;

/// <summary>
/// Mouse-first management dashboard for colony, production, research, resources and strategic state.
/// This view reports authoritative simulation data; buttons are routed by Program to simulation actions.
/// </summary>
internal sealed class ManagementPanelRenderer : IDisposable
{
    private static readonly string[] Pages =
    [
        "Overview", "Population", "Resources", "Construction", "Production",
        "Research", "Statistics", "Diplomacy", "Territories", "Industry & Tech"
    ];

    private readonly SKPaint _fill = new() { IsAntialias = true };
    private readonly SKPaint _line = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
    private readonly SKFont _font = new(SKTypeface.FromFamilyName("sans-serif", SKFontStyle.Bold), 14);
    private readonly SKFont _small = new(SKTypeface.FromFamilyName("sans-serif"), 12);
    private readonly SKFont _tiny = new(SKTypeface.FromFamilyName("sans-serif"), 11);

    public static string[] PageNames => Pages;

    public void Draw(SKCanvas canvas, int page, ColonySimulation colony, CampaignSimulation campaign,
        WorldMapSimulation world, bool worldMapMode)
    {
        if (page <= 0) return;
        page = Math.Clamp(page, 1, Pages.Length);
        _fill.Color = new SKColor(8, 11, 12, 248);
        canvas.DrawRect(new SKRect(0, 58, 1100, 720), _fill);
        _line.Color = new SKColor(102, 75, 58);
        canvas.DrawRect(new SKRect(8, 65, 1092, 710), _line);

        Text(canvas, "COLONY MANAGEMENT", 24, 91, _font, new SKColor(192, 42, 54));
        Text(canvas, worldMapMode ? "STRATEGIC LAYER" : "SETTLEMENT LAYER", 24, 111, _tiny, Muted);
        for (int i = 0; i < Pages.Length; i++)
        {
            float y = 128 + i * 48;
            _fill.Color = page == i + 1 ? new SKColor(91, 29, 39) : new SKColor(23, 30, 30);
            canvas.DrawRoundRect(new SKRect(18, y, 214, y + 38), 5, 5, _fill);
            _line.Color = page == i + 1 ? Gold : Border;
            canvas.DrawRoundRect(new SKRect(18, y, 214, y + 38), 5, 5, _line);
            Text(canvas, Pages[i], 31, y + 24, _small, TextColor);
        }

        _fill.Color = new SKColor(17, 22, 22);
        canvas.DrawRoundRect(new SKRect(232, 78, 1078, 690), 8, 8, _fill);
        _line.Color = Border;
        canvas.DrawRoundRect(new SKRect(232, 78, 1078, 690), 8, 8, _line);
        Text(canvas, Pages[page - 1].ToUpperInvariant(), 254, 109, _font, Gold);
        Text(canvas, "Select a category on the left. All displayed figures are live simulation values.", 254, 131, _tiny, Muted);
        DrawPage(canvas, page, colony, campaign, world, worldMapMode);
        Text(canvas, "CLOSE: click Manage in the top bar  •  All core actions are mouse-accessible", 254, 674, _tiny, Muted);
    }

    private void DrawPage(SKCanvas c, int page, ColonySimulation colony, CampaignSimulation campaign,
        WorldMapSimulation world, bool worldMapMode)
    {
        switch (page)
        {
            case 1:
                Section(c, "Settlement status", 254, 164);
                Pair(c, "Colonists", colony.Workers.Count.ToString(), 254, 190);
                Pair(c, "Active jobs", colony.PendingJobs.ToString(), 254, 216);
                Pair(c, "Completed structures", (colony.CompletedObjects + colony.CompletedTileOrders).ToString(), 254, 242);
                Pair(c, "Inventory", $"{colony.InventoryUsed} / {colony.InventoryCapacity}", 254, 268);
                Pair(c, "Inventory free", colony.InventoryFreeCapacity.ToString(), 254, 294);
                Pair(c, "Simulation time", $"{colony.ElapsedSeconds:0.0} s", 254, 320);
                Section(c, "Empire status", 670, 164);
                Pair(c, "Strategic turn", campaign.State.Turn.ToString(), 670, 190);
                Pair(c, "Supplies / Alloy", $"{campaign.State.Supplies} / {campaign.State.Alloy}", 670, 216);
                Pair(c, "Energy / Knowledge", $"{campaign.State.Energy} / {campaign.State.Knowledge}", 670, 242);
                Pair(c, "Regions controlled", campaign.State.Regions.Count(r => r.Owner == "Kor").ToString(), 670, 268);
                Pair(c, "World territories", $"{world.Territories.Count(t => t.Owner == WorldFactionKind.Kor)} / {world.Territories.Count}", 670, 294);
                Note(c, colony.Message, 254, 380, 790);
                Note(c, campaign.Message, 254, 426, 790);
                Note(c, world.Message, 254, 472, 790);
                break;
            case 2:
                Section(c, "Colonist roster", 254, 164);
                int wy = 194;
                foreach (var worker in colony.Workers)
                {
                    string task = worker.Task?.Kind.ToString() ?? "Idle";
                    string cargo = worker.CarriedResource.HasValue ? $"{worker.CarriedResource} x{worker.CarriedAmount}" : "Empty";
                    Row(c, $"{worker.Name}  •  {(colony.SelectedWorkerId == worker.Id ? "SELECTED" : "Worker #" + worker.Id)}",
                        $"Hunger {worker.Hunger:0}%  |  {task}  |  {cargo}", 254, wy);
                    wy += 48;
                }
                Note(c, "Click a colonist on the map to select them, then use Orders → Move / gather order mode.", 254, 420, 790);
                Note(c, "Needs, skills, schedules, health, mood and workforce assignment are planned expansions; they are not yet simulated.", 254, 462, 790);
                break;
            case 3:
                Section(c, "Local inventory", 254, 164);
                int ry = 194;
                foreach (ResourceKind resource in Enum.GetValues<ResourceKind>())
                {
                    int deposits = colony.Deposits.Where(d => d.Resource == resource).Sum(d => d.Amount);
                    Row(c, resource.ToString(), $"Stored {colony.ResourceCount(resource)}  |  Nearby deposits {deposits}", 254, ry);
                    ry += 39;
                }
                Section(c, "Processed industry materials", 254, 420);
                int mi = 450;
                var resourceMaterialNames = ProgressionCatalog.Resources.ToDictionary(item => item.Id, item => item.Name, StringComparer.Ordinal);
                foreach (var material in colony.IndustryInventory.OrderBy(item => item.Key, StringComparer.Ordinal).Take(5))
                {
                    Row(c, resourceMaterialNames.GetValueOrDefault(material.Key, material.Key), $"Stored {material.Value}", 254, mi);
                    mi += 35;
                }
                if (colony.IndustryInventory.Count == 0)
                    Note(c, "Processed materials will appear here as recipes finish.", 254, 450, 380);
                Section(c, "Logistics actions", 670, 164);
                ActionRow(c, "Import strategic supplies → local Food / Alloy", 670, 194);
                ActionRow(c, "Export local Food / Alloy → empire stock", 670, 239);
                Pair(c, "Storage used", $"{colony.InventoryUsed} / {colony.InventoryCapacity}", 670, 300);
                Pair(c, "Storage available", colony.InventoryFreeCapacity.ToString(), 670, 326);
                Note(c, "Storage is shared by legacy resources and every stable-ID industry material.", 670, 372, 365);
                break;
            case 4:
                Section(c, "Construction & blueprints", 254, 164);
                Pair(c, "Architecture orders", colony.TileOrders.Count.ToString(), 254, 190);
                Pair(c, "Completed architecture", colony.CompletedTileOrders.ToString(), 254, 216);
                Pair(c, "Object blueprints", colony.Objects.Count.ToString(), 254, 242);
                Pair(c, "Completed objects", colony.CompletedObjects.ToString(), 254, 268);
                Pair(c, "Awaiting work / materials", colony.PendingJobs.ToString(), 254, 294);
                Section(c, "Choose placement", 670, 164);
                string[] buildActions = ["Wall", "Floor", "Path", "Door", "Conduit", "Workbench", "Sleep Pod", "Storage", "Generator", "Garden", "Habitat", "Stockpile"];
                for (int i = 0; i < buildActions.Length; i++)
                {
                    int col = i % 3, row = i / 3;
                    ActionRow(c, buildActions[i], 670 + col * 125, 194 + row * 43, 116);
                }
                Section(c, "Recent orders", 254, 354);
                int oy = 380;
                foreach (var order in colony.TileOrders.TakeLast(5).Reverse())
                {
                    Row(c, $"Tile {order.Cell.X},{order.Cell.Y} • {order.Target}", $"{order.Status} • {order.WorkDone:0}/{order.WorkRequired}", 254, oy);
                    oy += 39;
                }
                break;
            case 5:
                Section(c, "Workstations & orders", 254, 164);
                int py = 194;
                foreach (var bench in colony.Objects.Where(o => o.Kind == PlaceableKind.Workbench).Take(3))
                {
                    Row(c, $"Workbench #{bench.Id} • {bench.Status}",
                        $"Position {bench.Origin.X},{bench.Origin.Y} • {(bench.IsBuilt ? "Ready" : "Construction required")}", 254, py);
                    py += 42;
                }
                var firstBench = colony.Objects.FirstOrDefault(o => o.Kind == PlaceableKind.Workbench && o.IsBuilt);
                if (firstBench != null)
                    ActionRow(c, $"Queue legacy component batch at #{firstBench.Id}", 254, 350, 365);
                else
                    Note(c, "Build and complete a Workbench before queuing production.", 254, 350, 365);
                Section(c, "Recent production orders", 254, 408);
                int productionOrderY = 436;
                foreach (var order in colony.ProductionOrders.TakeLast(4).Reverse())
                {
                    string name = order.RecipeId == null ? "Components" :
                        ProgressionCatalog.Recipes.FirstOrDefault(recipe => recipe.Id == order.RecipeId)?.Name ?? order.RecipeId;
                    Row(c, name, order.Complete ? "COMPLETE" : $"{order.WorkDone:0.0} / {order.WorkRequired:0.0} work", 254, productionOrderY);
                    productionOrderY += 39;
                }
                if (colony.ProductionOrders.Count == 0)
                    Note(c, "No production orders yet. Select a recipe on the right.", 254, 438, 365);

                Section(c, "Recipe queue • click to manufacture", 670, 164);
                int recipeY = 194;
                var names = ProgressionCatalog.Resources.ToDictionary(item => item.Id, item => item.Name, StringComparer.Ordinal);
                foreach (var recipe in colony.ManagementRecipes)
                {
                    bool canQueue = colony.CanQueueRecipe(recipe.Id, out _);
                    string inputs = string.Join(" + ", recipe.Inputs.Select(item =>
                        $"{item.Value} {names.GetValueOrDefault(item.Key, item.Key)}"));
                    string outputs = string.Join(" + ", recipe.Outputs.Select(item =>
                        $"{item.Value} {names.GetValueOrDefault(item.Key, item.Key)}"));
                    ActionRow(c, recipe.Name, 670, recipeY, 365);
                    Note(c, $"{inputs} → {outputs} • {(canQueue ? "READY" : "MISSING INPUTS / STORAGE")}",
                        670, recipeY + 27, 365);
                    recipeY += 50;
                }
                Note(c, "Inputs are reserved when queued; colonists must walk to the Workbench and perform the work.", 670, 602, 365);
                break;
            case 6:
                Section(c, "Research projects", 254, 164);
                Row(c, "Ward Protocol", campaign.State.WardProtocolResearched ? "RESEARCHED" : "Unlocks stronger guardians and slows Blight spread", 254, 194);
                Pair(c, "Cost", "30 Knowledge + 20 Energy", 254, 246);
                Pair(c, "Available", campaign.CanResearchWard ? "YES" : "NO — resources missing or already researched", 254, 272);
                ActionRow(c, campaign.State.WardProtocolResearched ? "Ward Protocol already researched" : "Research Ward Protocol", 254, 316, 790);
                Note(c, "The research catalog currently contains one implemented strategic project. Local unlock trees and building prerequisites are not implemented yet.", 254, 380, 790);
                break;
            case 7:
                Section(c, "Colony statistics", 254, 164);
                Pair(c, "Elapsed simulation", $"{colony.ElapsedSeconds:0.0} seconds", 254, 190);
                Pair(c, "Colonists hungry", colony.Workers.Count(w => w.Hungry).ToString(), 254, 216);
                Pair(c, "Colonists carrying cargo", colony.Workers.Count(w => w.CarriedAmount > 0).ToString(), 254, 242);
                Pair(c, "Resource deposits remaining", colony.Deposits.Sum(d => d.Amount).ToString(), 254, 268);
                Pair(c, "Jobs pending", colony.PendingJobs.ToString(), 254, 294);
                Pair(c, "Production infrastructure", colony.Objects.Count(o => o.Kind == PlaceableKind.Workbench && o.IsBuilt).ToString(), 254, 320);
                Section(c, "Strategic statistics", 670, 164);
                Pair(c, "Campaign regions", campaign.State.Regions.Count.ToString(), 670, 190);
                Pair(c, "Kor-controlled regions", campaign.State.Regions.Count(r => r.Owner == "Kor").ToString(), 670, 216);
                Pair(c, "Army strength / readiness", $"{campaign.State.ArmyStrength} / {campaign.State.ArmyReadiness}%", 670, 242);
                Pair(c, "Ward Protocol", campaign.State.WardProtocolResearched ? "Researched" : "Not researched", 670, 268);
                Pair(c, "Blight territories", world.Territories.Count(t => t.Owner == WorldFactionKind.Blight).ToString(), 670, 294);
                Pair(c, "Mean Blight pressure", world.Territories.Count == 0 ? "0" : world.Territories.Average(t => t.BlightPressure).ToString("0.0"), 670, 320);
                break;
            case 8:
                Section(c, "Diplomatic relations", 254, 164);
                int dy = 194;
                foreach (WorldFactionKind faction in new[] { WorldFactionKind.AshenCompact, WorldFactionKind.FreeMarches, WorldFactionKind.HelionLeague, WorldFactionKind.VerdantCovenant, WorldFactionKind.Blight })
                {
                    Row(c, WorldMapSimulation.FactionName(faction), world.RelationTo(faction).ToString(), 254, dy);
                    dy += 40;
                }
                Section(c, "Selected world territory", 670, 164);
                Pair(c, "Owner", world.SelectedOwnerName, 670, 194);
                Pair(c, "Coordinates", $"{world.Selected.X + 1}, {world.Selected.Y + 1}", 670, 220);
                ActionRow(c, "Improve relations", 670, 274, 365);
                ActionRow(c, "Offer non-aggression pact", 670, 319, 365);
                ActionRow(c, "Propose trade agreement", 670, 364, 365);
                Note(c, worldMapMode ? "Diplomacy actions affect the owner of the selected world territory." : "Switch to Strategic World Map and select a faction territory before using diplomacy.", 670, 430, 365);
                break;
            case 9:
                Section(c, "World territory intelligence", 254, 164);
                Pair(c, "Selected territory", $"{world.Selected.X + 1},{world.Selected.Y + 1}", 254, 190);
                Pair(c, "Owner", world.SelectedOwnerName, 254, 216);
                Pair(c, "Development", world.Selected.Development.ToString(), 254, 242);
                Pair(c, "Settlement", world.Selected.HasSettlement ? "Present" : "None", 254, 268);
                Pair(c, "Explored", world.Selected.Explored ? "Yes" : "No", 254, 294);
                Pair(c, "Blight pressure", world.Selected.BlightPressure.ToString(), 254, 320);
                Pair(c, "Territory stock", world.Selected.Supplies.ToString(), 254, 346);
                Section(c, "Expansion", 670, 164);
                Pair(c, "Empire Supplies / Alloy", $"{world.State.EmpireSupplies} / {world.State.EmpireAlloy}", 670, 194);
                Pair(c, "Can claim selected", world.CanClaimSelected ? "YES" : "NO", 670, 220);
                ActionRow(c, "Claim selected frontier territory", 670, 274, 365);
                ActionRow(c, "Advance strategic turn", 670, 319, 365);
                Note(c, "Select a tile on the strategic map first. Claiming requires adjacent Kor territory and 40 Supplies / 25 Alloy.", 670, 380, 365);
                break;
            case 10:
                Section(c, "Industrial progression catalogue", 254, 164);
                Pair(c, "Resource definitions", ProgressionCatalog.Resources.Count.ToString(), 254, 190);
                Pair(c, "Research nodes", ProgressionCatalog.Research.Count.ToString(), 254, 216);
                Pair(c, "Building variants", ProgressionCatalog.Buildings.Count.ToString(), 254, 242);
                Pair(c, "Unit variants", ProgressionCatalog.Units.Count.ToString(), 254, 268);
                Pair(c, "Production recipes", ProgressionCatalog.Recipes.Count.ToString(), 254, 294);
                Pair(c, "Total linked definitions", ProgressionCatalog.TotalDefinitions.ToString(), 254, 320);
                Note(c, "Stable-ID material stock, live recipe queues and save/load support are now connected. Research unlocks and advanced extraction remain next.", 254, 362, 385);
                Section(c, "Starter production chains", 670, 164);
                int chainY = 194;
                var materialNames = ProgressionCatalog.Resources.ToDictionary(r => r.Id, r => r.Name, StringComparer.Ordinal);
                foreach (var recipe in ProgressionCatalog.Recipes.Where(r => r.Stage == 0).Take(5))
                {
                    string inputs = string.Join(" + ", recipe.Inputs.Select(p => $"{p.Value} {materialNames.GetValueOrDefault(p.Key, p.Key)}"));
                    string outputs = string.Join(" + ", recipe.Outputs.Select(p => $"{p.Value} {materialNames.GetValueOrDefault(p.Key, p.Key)}"));
                    Row(c, recipe.Industry, $"{inputs} → {outputs}", 670, chainY);
                    chainY += 39;
                }
                Section(c, "Research frontier • Era I", 254, 424);
                int researchY = 450;
                foreach (var node in ProgressionCatalog.Research.Where(r => r.Era == 0).Take(5))
                {
                    Row(c, node.Name, $"{node.CostKnowledge} Knowledge • {node.CostEnergy} Energy • unlock: {node.UnlockKind}", 254, researchY);
                    researchY += 39;
                }
                Section(c, "First infrastructure & units", 670, 424);
                int assetsY = 450;
                foreach (var item in ProgressionCatalog.Buildings.Where(b => b.Era == 0).Take(3))
                {
                    Row(c, item.Name, $"Footprint {item.Footprint} tiles • work {item.Work} • upkeep: {item.Upkeep}", 670, assetsY);
                    assetsY += 39;
                }
                foreach (var item in ProgressionCatalog.Units.Where(u => u.Era == 0).Take(2))
                {
                    Row(c, item.Name, $"Cost {item.SupplyCost} supplies / {item.AlloyCost} alloy • upkeep {item.Upkeep}", 670, assetsY);
                    assetsY += 39;
                }
                break;
        }
    }

    private void Section(SKCanvas c, string text, float x, float y)
    {
        Text(c, text, x, y, _font, Gold);
        _line.Color = Border;
        c.DrawLine(x, y + 8, x + 385, y + 8, _line);
    }

    private void Pair(SKCanvas c, string label, string value, float x, float y)
    {
        Text(c, label, x, y, _small, Muted);
        Text(c, value, x + 185, y, _small, TextColor);
    }

    private void Row(SKCanvas c, string title, string detail, float x, float y)
    {
        _fill.Color = new SKColor(26, 33, 32);
        c.DrawRoundRect(new SKRect(x, y, x + 385, y + 37), 4, 4, _fill);
        Text(c, title, x + 9, y + 14, _tiny, TextColor);
        Text(c, detail, x + 9, y + 29, _tiny, Muted);
    }

    private void ActionRow(SKCanvas c, string label, float x, float y, float width = 385)
    {
        _fill.Color = new SKColor(76, 30, 38);
        c.DrawRoundRect(new SKRect(x, y, x + width, y + 35), 4, 4, _fill);
        _line.Color = Gold;
        c.DrawRoundRect(new SKRect(x, y, x + width, y + 35), 4, 4, _line);
        Text(c, label, x + 9, y + 22, _tiny, TextColor);
    }

    private void Note(SKCanvas c, string text, float x, float y, float width)
    {
        _fill.Color = new SKColor(23, 30, 30);
        c.DrawRoundRect(new SKRect(x, y, x + width, y + 38), 4, 4, _fill);
        Text(c, text.Length > 112 ? text[..109] + "..." : text, x + 9, y + 23, _tiny, Muted);
    }

    private void Text(SKCanvas c, string value, float x, float y, SKFont font, SKColor color)
    {
        using var p = new SKPaint { IsAntialias = true, Color = color };
        c.DrawText(value, x, y, SKTextAlign.Left, font, p);
    }

    private static readonly SKColor TextColor = new(230, 229, 216);
    private static readonly SKColor Muted = new(160, 173, 165);
    private static readonly SKColor Gold = new(216, 177, 89);
    private static readonly SKColor Border = new(64, 75, 72);

    public void Dispose() { _fill.Dispose(); _line.Dispose(); _font.Dispose(); _small.Dispose(); _tiny.Dispose(); }
}
