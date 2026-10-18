using KorTheBlight.Simulation;
using KorTheBlight.Campaign;

namespace KorTheBlight.Tests;

/// <summary>Small dependency-free checks for the deterministic starter scenario.</summary>
internal static class ScenarioChecks
{
    public static int Run()
    {
        string balancePath = Path.Combine(AppContext.BaseDirectory, "Data", "Balance", "scenario.json");
        var configuredBalance = ScenarioBalance.Load(balancePath);
        Require(configuredBalance.PurifierAlloyCost == 80, "shipped balance file should load its configured purifier cost");
        string invalidConfig = Path.GetTempFileName();
        try
        {
            File.WriteAllText(invalidConfig, "{ \"workerSpeed\": -1 }");
            bool rejected = false;
            try { ScenarioBalance.Load(invalidConfig); }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected, "invalid balance config should fail with a useful validation error");
        }
        finally { File.Delete(invalidConfig); }

        var first = new OutpostSimulation();
        var second = new OutpostSimulation();
        for (int i = 0; i < 1800; i++) { first.Step(.1f); second.Step(.1f); }
        for (int y = 0; y < OutpostSimulation.Height; y++)
        for (int x = 0; x < OutpostSimulation.Width; x++)
            Require(first.BlightAt(x, y) == second.BlightAt(x, y), "same scenario inputs must yield the same Blight map");
        int infected = 0;
        for (int y = 0; y < OutpostSimulation.Height; y++)
        for (int x = 0; x < OutpostSimulation.Width; x++) if (first.BlightAt(x, y) > 0) infected++;
        Require(infected > 3, $"Blight should change terrain and spread from its starting cluster (found {infected} infected tiles)");

        var uncontrolled = new OutpostSimulation();
        var contained = new OutpostSimulation();
        var purifierSite = OutpostSimulation.TileCenter(2, 6);
        contained.PlacePurifier(purifierSite.X, purifierSite.Y);
        for (int i = 0; i < 250; i++) { uncontrolled.Step(.1f); contained.Step(.1f); }
        Require(contained.BlightAt(1, 7) < uncontrolled.BlightAt(1, 7), "completed purifier should cleanse nearby infected terrain");

        var economy = new OutpostSimulation();
        var worker = economy.WorkerScreen();
        Require(economy.SelectWorker(worker.X, worker.Y), "worker must be selectable at its starting position");
        var deposit = OutpostSimulation.TileCenter(2, 4);
        economy.Command(deposit.X, deposit.Y);
        for (int i = 0; i < 40; i++) economy.Step(.1f);
        Require(economy.Alloy > 140, "worker should gather alloy at the salvage deposit");

        int alloyBefore = economy.Alloy, suppliesBefore = economy.Supplies;
        var commandSite = OutpostSimulation.TileCenter(OutpostSimulation.BaseX, OutpostSimulation.BaseY);
        economy.PlacePurifier(commandSite.X, commandSite.Y);
        Require(economy.Alloy == alloyBefore && economy.Supplies == suppliesBefore, "blocked placement must not charge resources");

        var buildSite = OutpostSimulation.TileCenter(2, 6);
        economy.PlacePurifier(buildSite.X, buildSite.Y);
        Require(economy.Alloy == alloyBefore - 80 && economy.Supplies == suppliesBefore - 40, "valid construction should charge its cost");
        for (int i = 0; i < 250; i++) economy.Step(.1f);
        Require(economy.CompletedPurifiers == 1, "worker should complete a purifier when sent to its foundation");
        var secondSite = OutpostSimulation.TileCenter(3, 6);
        economy.PlacePurifier(secondSite.X, secondSite.Y);
        for (int i = 0; i < 250; i++) economy.Step(.1f);
        Require(economy.Won, "two completed purifiers should satisfy the containment objective");
        CampaignChecks.Run();
        Console.WriteLine("Scenario checks passed: tactical spread, unit selection, gathering, placement, construction, objective completion; campaign generation, orders, research, battle reconciliation, and save round-trip.");
        return 0;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Scenario check failed: {message}");
    }
}
