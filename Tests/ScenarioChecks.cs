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
            Console.WriteLine("Kor checks passed: orthogonal grid projection, worker selection, real gather/deliver inventory transfer, per-tile construction, independent multi-tile object blueprints, worker construction labor, production input/output accounting, deterministic campaign generation, strategic orders, research, battle transfer, and campaign save round-trip.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
