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
            CombatSystemsChecks.Run();
            CombatIntegrationChecks.Run();
            VerifyMapViewportInputBounds();
            MacroWorldChecks.Run();
            Console.WriteLine("Kor checks passed: pawn utility AI, colony construction and logistics, bounded stockpile reservations, citizen needs and movement, job-broker capacity, continuous world movement and queries, shared settlement ledger, unified clock, session persistence, deterministic bounded simulation events, data-driven colony storyteller events and cooldowns, resident social relationships and incident milestones, and indexed world-space radius queries, deterministic regional biomes and local-map seeds, seeded faction strategy, diplomatic relationship matrices and persistent threat intelligence, deterministic 100x100 macro cells, daily macro economy, bounded supply-demand prices, data-driven research and generated item definitions, colonist work matrices, target reservations, strategic routes, macro-faction actions, wealth valuation and threat-scaled storyteller incidents, deterministic tactical combat, cover, faction loadouts, research-gated production, vehicle assembly, persistent nemesis titles, and cross-system combat/workshop/research/save integration.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void VerifyMapViewportInputBounds()
    {
        if (!Program.IsMapViewport(769.9f, 100f) ||
            !Program.IsMapViewport(200f, 649.9f) ||
            Program.IsMapViewport(770f, 100f) ||
            Program.IsMapViewport(200f, 650f) ||
            Program.IsMapViewport(200f, 61.9f) ||
            Program.IsMapViewport(800f, 400f))
            throw new InvalidOperationException("Map viewport hit-test bounds do not match the visible map.");
    }
}
