using KorTheBlight.Rendering;

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
            VerifyColonistRosterHitBounds();
            VerifyRightClickCancelsActiveMapTools();
            VerifyBuildPaletteLayout();
            VerifyWindowSizeParsing();
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

    private static void VerifyBuildPaletteLayout()
    {
        if (BuildPaletteLayout.HitCategory(784f, 566f) != 0 ||
            BuildPaletteLayout.HitCategory(845f, 566f) != 1 ||
            BuildPaletteLayout.HitCategory(1028f, 587.9f) != 4 ||
            BuildPaletteLayout.HitCategory(842f, 575f) != -1 ||
            BuildPaletteLayout.HitCategory(1085f, 575f) != -1 ||
            BuildPaletteLayout.HitItem(784f, 592f) != 0 ||
            BuildPaletteLayout.HitItem(860f, 592f) != 1 ||
            BuildPaletteLayout.HitItem(784f, 614f) != 4 ||
            BuildPaletteLayout.HitItem(856f, 600f) != -1 ||
            BuildPaletteLayout.HitItem(784f, 634f) != -1)
            throw new InvalidOperationException("Build palette hitboxes overlap, include gaps, or escape their panel.");

        if (BuildPaletteLayout.ActionAt(0, 0) != BuildPaletteAction.Wall ||
            BuildPaletteLayout.ActionAt(0, 5) != BuildPaletteAction.Dig ||
            BuildPaletteLayout.ActionAt(1, 0) != BuildPaletteAction.SleepingPod ||
            BuildPaletteLayout.ActionAt(2, 0) != BuildPaletteAction.Workbench ||
            BuildPaletteLayout.ActionAt(3, 0) != BuildPaletteAction.StorageRack ||
            BuildPaletteLayout.ActionAt(4, 0) != BuildPaletteAction.Garden ||
            BuildPaletteLayout.ActionAt(1, 7) != BuildPaletteAction.None)
            throw new InvalidOperationException("Build palette category-to-action mapping is incorrect.");
    }

    private static void VerifyWindowSizeParsing()
    {
        if (!Program.TryParseWindowSize(["--window-size=1280x720"], out int width, out int height) ||
            width != 1280 || height != 720 ||
            !Program.TryParseWindowSize(["--window-size=1920X1080"], out width, out height) ||
            width != 1920 || height != 1080 ||
            Program.TryParseWindowSize(["--window-size=640x480"], out _, out _) ||
            Program.TryParseWindowSize(["--window-size=wide"], out _, out _))
            throw new InvalidOperationException("Responsive window-size parsing did not accept valid sizes or reject invalid sizes.");
    }

    private static void VerifyRightClickCancelsActiveMapTools()
    {
        if (!Program.ShouldCancelMapToolOnRightClick(false, true, KorTheBlight.Simulation.ArchitectureKind.None, false, false) ||
            !Program.ShouldCancelMapToolOnRightClick(false, false, KorTheBlight.Simulation.ArchitectureKind.Wall, false, false) ||
            !Program.ShouldCancelMapToolOnRightClick(false, false, KorTheBlight.Simulation.ArchitectureKind.None, true, false) ||
            !Program.ShouldCancelMapToolOnRightClick(false, false, KorTheBlight.Simulation.ArchitectureKind.None, false, true) ||
            Program.ShouldCancelMapToolOnRightClick(true, true, KorTheBlight.Simulation.ArchitectureKind.None, false, false) ||
            Program.ShouldCancelMapToolOnRightClick(false, false, KorTheBlight.Simulation.ArchitectureKind.None, false, false))
            throw new InvalidOperationException("Right-click map-tool cancellation rules are inconsistent.");
    }

    private static void VerifyColonistRosterHitBounds()
    {
        if (KorTheBlight.Rendering.ColonyRosterLayout.HitTest(155f, 8f) != 0 ||
            KorTheBlight.Rendering.ColonyRosterLayout.HitTest(235f, 20f) != 1 ||
            KorTheBlight.Rendering.ColonyRosterLayout.HitTest(315f, 20f) != 2 ||
            KorTheBlight.Rendering.ColonyRosterLayout.HitTest(231f, 20f) != -1 ||
            KorTheBlight.Rendering.ColonyRosterLayout.HitTest(154.9f, 20f) != -1 ||
            KorTheBlight.Rendering.ColonyRosterLayout.HitTest(160f, 35f) != -1 ||
            KorTheBlight.Rendering.ColonyRosterLayout.HitTest(395f, 20f) != -1)
            throw new InvalidOperationException("Colonist roster hit-test bounds do not match the rendered buttons.");
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
