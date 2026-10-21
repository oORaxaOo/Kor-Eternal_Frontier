namespace KorTheBlight.Tests;

internal static class ScenarioChecks
{
    public static int Run()
    {
        try
        {
            ColonyChecks.Run();
            ColonyEcosystemChecks.Run();
            PawnUtilityAiChecks.Run();
            IntegrationChecks.Run();
            WorldSimulationChecks.Run();
            ContinuousWorldChecks.Run();
            SimulationEventQueueChecks.Run();
            ColonyStorytellerChecks.Run();
            SocialRelationshipChecks.Run();
            WorldSpatialIndexChecks.Run();
            RegionalWorldMapChecks.Run();
            FactionIntelligenceChecks.Run();
            StrategicSystemsChecks.Run();
            Console.WriteLine("Kor checks passed: pawn utility AI, colony construction and logistics, bounded stockpile reservations, citizen needs and movement, job-broker capacity, continuous world movement and queries, shared settlement ledger, unified clock, session persistence, deterministic bounded simulation events, data-driven colony storyteller events and cooldowns, resident social relationships and incident milestones, and indexed world-space radius queries, deterministic regional biomes and local-map seeds, seeded faction strategy, diplomatic relationship matrices and persistent threat intelligence, colonist work matrices, target reservations, strategic routes, macro-faction actions, wealth valuation and threat-scaled storyteller incidents.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
