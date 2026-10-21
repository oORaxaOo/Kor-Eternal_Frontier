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
        Check(macro.SimulationTicks == 24_000 && macro.MacroTurnsScheduled == 1 && macro.MacroTurnsProcessed == 0,
            "Tick 24,000 must schedule the first macro turn without running the whole grid synchronously.");
        Check(macro.TurnPhase == MacroTurnPhase.Economy && macro.MacroTurnCursor == 128,
            "A macro update must process no more than its bounded cell slice.");
        for (int frame = 0; frame < 200 && macro.MacroTurnsProcessed == 0; frame++)
            macro.Advance(0.1, first);
        Check(macro.MacroTurnsProcessed == 1 && macro.MacroTurnCursor == 0,
            "A scheduled macro turn must complete across multiple bounded slices.");
        Check(macro.Market.LastUpdatedTick == 24_000, "Market prices must refresh against the scheduled macro-turn tick.");
        Check(macro.Market.FoodPrice is >= 0.25f and <= 8f, "Food market price must stay bounded.");
        Check(macro.Notifications.All(item => item.Id > 0 && !string.IsNullOrWhiteSpace(item.Message)), "World notifications must be valid.");
        macro.Validate();

        int routeFaction = macro.FactionIds.Values.OrderBy(id => id).First();
        const int routeTargetCellId = MacroWorldState.CellCount;
        macro.Caravans.Add(new MacroCaravanState
        {
            Id = macro.NextCaravanId++, FactionId = routeFaction, X = 0, Y = 0,
            TargetCellId = routeTargetCellId, TravelSpeed = 0.05f,
            Cargo = new Dictionary<MacroResource, float> { [MacroResource.Food] = 10f }
        });
        macro.Advance(0.1, first);
        var dispatchedCaravan = macro.Caravans.Single(item => item.TargetCellId == routeTargetCellId);
        Check(dispatchedCaravan.RoutePending || dispatchedCaravan.RouteReady,
            "A real macro caravan must enter the asynchronous route scheduler.");
        var routeTimeout = System.Diagnostics.Stopwatch.StartNew();
        while (!dispatchedCaravan.RouteReady && routeTimeout.Elapsed < TimeSpan.FromSeconds(5))
        {
            Thread.Sleep(1);
            macro.Advance(0.1, first);
        }
        Check(dispatchedCaravan.RouteReady && dispatchedCaravan.RouteNodes.Count > 1,
            "The world coordinator must apply completed A* routes back to the live caravan.");
        Check(dispatchedCaravan.RouteNodes[0] == new WorldRouteNode(0, 0) &&
              dispatchedCaravan.RouteNodes[^1] == new WorldRouteNode(MacroWorldState.Columns - 1, MacroWorldState.Rows - 1),
            "The live caravan route must preserve its correct start and target.");
        macro.Validate();

        // The async route scheduler must bound background concurrency, return deterministic A* paths,
        // and never expose mutable live-world state to worker threads.
        var routeScheduler = new BackgroundWorldRouteScheduler();
        var routeCosts = Enumerable.Repeat(1f, MacroWorldState.CellCount).ToArray();
        var routeRequests = new List<(long Id, int Start, int Target)>();
        int routeCount = routeScheduler.MaxConcurrentRoutes;
        for (int i = 0; i < routeCount; i++)
        {
            int startCell = 1 + i * 17;
            int targetCell = MacroWorldState.CellCount - i * 23;
            bool queued = routeScheduler.TrySchedule(i + 1, targetCell, MacroWorldState.Columns, startCell, routeCosts);
            if (queued) routeRequests.Add((i + 1, startCell, targetCell));
        }
        Check(routeRequests.Count == routeCount, "Route scheduler should fill but never exceed its worker budget.");
        Check(routeScheduler.InFlightCount <= routeScheduler.MaxConcurrentRoutes, "Background route count must remain bounded.");
        var routeResults = new List<BackgroundWorldRouteResult>();
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (routeResults.Count < routeRequests.Count && timeout.Elapsed < TimeSpan.FromSeconds(5))
        {
            Thread.Sleep(1);
            routeResults.AddRange(routeScheduler.DrainCompleted());
        }
        Check(routeResults.Count == routeRequests.Count && routeResults.All(result => result.Error is null && result.Nodes is { Count: > 0 }),
            "All background macro route requests must complete without errors.");
        foreach (var request in routeRequests)
        {
            var route = routeResults.Single(result => result.CaravanId == request.Id).Nodes!;
            Check(route[0] == new WorldRouteNode((request.Start - 1) % MacroWorldState.Columns, (request.Start - 1) / MacroWorldState.Columns),
                "A* path must start at its requested cell.");
            Check(route[^1] == new WorldRouteNode((request.Target - 1) % MacroWorldState.Columns, (request.Target - 1) / MacroWorldState.Columns),
                "A* path must end at its requested cell.");
            Check(route.Zip(route.Skip(1), (a, b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) == 1).All(adjacent => adjacent),
                "Background A* routes must contain only orthogonally adjacent cells.");
        }

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
