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
            SimulationEventQueueChecks.Run();
            Console.WriteLine("Kor checks passed: colony construction and logistics, bounded stockpile reservations, citizen needs and movement, job-broker capacity, continuous world movement and queries, shared settlement ledger, unified clock, session persistence, and deterministic bounded simulation events.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
