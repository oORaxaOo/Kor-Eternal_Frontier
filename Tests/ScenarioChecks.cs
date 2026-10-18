using KorTheBlight.Campaign;

namespace KorTheBlight.Tests;

internal static class ScenarioChecks
{
    public static int Run()
    {
        try
        {
            ColonyChecks.Run();
            CampaignChecks.Run();
            IntegrationChecks.Run();
            WorldMapChecks.Run();
            if (!ProgressionCatalog.Validate(out string progressionError))
                throw new InvalidOperationException(progressionError);
            if (ProgressionCatalog.Research.Count < 400 || ProgressionCatalog.Recipes.Count < 90 ||
                ProgressionCatalog.Buildings.Count < 90 || ProgressionCatalog.Units.Count < 75)
                throw new InvalidOperationException("Industrial progression catalogue is smaller than its minimum content contract.");
            Console.WriteLine("Progression catalogue checks passed: unique IDs, valid research prerequisites, valid building/unit/recipe unlocks, and minimum content scale.");
            Console.WriteLine("Kor checks passed: orthogonal grid projection, worker selection, real gather/deliver inventory transfer, per-tile construction, independent multi-tile object blueprints, worker construction labor, production input/output accounting, deterministic campaign generation, strategic orders, research, battle transfer, and campaign save round-trip, capacity-safe campaign/colony logistics, unified session persistence, large-world territory generation, faction borders, diplomacy state, and strategic territory selection.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
