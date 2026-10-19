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
            WorldSimulationChecks.Run();
            Console.WriteLine("Kor checks passed: colony construction and logistics, tile-grid stockpile capacity and reservations, citizen needs and movement, job-broker capacity and workplace removal, deterministic regional simulation, unified real-time clock, campaign/colony transfers, and session persistence.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
