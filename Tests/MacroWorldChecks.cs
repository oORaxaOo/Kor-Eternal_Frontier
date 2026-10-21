using KorTheBlight.Simulation.Research;
using KorTheBlight.WorldModel;

namespace KorTheBlight.Tests;

internal static class MacroWorldChecks
{
    public static void Run()
    {
        var first = ContinuousWorldState.Generate(314159);
        var second = ContinuousWorldState.Generate(314159);
        Check(first.MacroWorld.Cells.Length == 10_000, "Macro layer must contain a 100x100 grid.");
        Check(first.MacroWorld.Cells.Select(cell => cell.CellId).Distinct().Count() == 10_000, "Macro cell IDs must be unique.");
        Check(first.MacroWorld.Cells.Select(cell => cell.Population).SequenceEqual(second.MacroWorld.Cells.Select(cell => cell.Population)),
            "Macro population must be deterministic for a seed.");
        Check(first.MacroWorld.Cells.Select(cell => cell.BlightPresence).SequenceEqual(second.MacroWorld.Cells.Select(cell => cell.BlightPresence)),
            "Macro Blight pressure must be deterministic for a seed.");
        Check(first.MacroWorld.Cells.Any(cell => cell.OwningFactionId != 0), "Generated macro world needs inhabited cells.");
        Check(first.MacroWorld.Cells.Any(cell => cell.Fertility > 0), "Macro cells need fertility modifiers.");
        Check(first.MacroWorld.Cells.Any(cell => cell.IsLocalMapGenerated), "The Kor local map must link to a macro cell.");

        var macro = first.MacroWorld;
        macro.Advance(399.9, first);
        Check(macro.SimulationTicks == 23_994 && macro.MacroTurnsProcessed == 0, "Macro economy must wait for the full 24,000-tick interval.");
        macro.Advance(0.1, first);
        Check(macro.SimulationTicks == 24_000 && macro.MacroTurnsProcessed == 1, "The first macro turn must run on tick 24,000.");
        Check(macro.Market.LastUpdatedTick == 24_000, "Market prices must refresh on the macro turn.");
        Check(macro.Market.FoodPrice is >= 0.25f and <= 8f, "Food market price must stay bounded.");
        Check(macro.Notifications.All(item => item.Id > 0 && !string.IsNullOrWhiteSpace(item.Message)), "World notifications must be valid.");
        macro.Validate();

        var database = new DatabaseBootstrap();
        database.LoadOrGenerate();
        Check(database.ResearchDatabase.Count >= 72, "Generated research catalogue should contain at least 72 projects.");
        Check(database.ItemDatabase.Count >= 550, "Generated item catalogue should contain hundreds of definitions.");
        var manager = new ResearchManager(database);
        string firstResearch = database.ResearchDatabase.Values.OrderBy(project => project.CostPoints).First().Id;
        Check(manager.Start(firstResearch), "First research project should be startable.");
        float cost = database.ResearchDatabase[firstResearch].CostPoints;
        Check(manager.AddProgress(cost), "Research should complete when progress reaches its cost.");
        Check(manager.State.CompletedResearchIds.Contains(firstResearch), "Completed research must persist in the research state.");
        Check(!manager.CanStart(firstResearch), "Completed research must not be startable again.");

        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
