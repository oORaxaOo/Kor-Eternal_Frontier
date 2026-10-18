using KorTheBlight.Campaign;

namespace KorTheBlight.Tests;

internal static class ScenarioChecks
{
    public static int Run()
    {
        try
        {
            ColonyChecks.Run();
            ColonyEcosystemChecks.Run();
            CampaignChecks.Run();
            IntegrationChecks.Run();
            Console.WriteLine("Kor checks passed: colony construction and logistics, tile-grid stockpile capacity and reservations, citizen needs and movement, job-broker capacity and workplace removal, deterministic campaign simulation, campaign/colony transfers, and unified session persistence.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
