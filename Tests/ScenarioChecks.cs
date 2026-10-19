namespace KorTheBlight.Tests;

internal static class ScenarioChecks
{
    public static int Run()
    {
        try
        {
            ColonyChecks.Run();
            ColonyEcosystemChecks.Run();
            IntegrationChecks.Run();
            WorldSimulationChecks.Run();
            ContinuousWorldChecks.Run();
            Console.WriteLine("Kor checks passed: colony construction and logistics, tile-grid stockpile capacity and reservations, citizen needs and movement, job-broker capacity and workplace removal, continuous world-space movement and queries, shared settlement ledger, mouse-first simulation controls, unified clock, and session persistence.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
