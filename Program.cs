using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Gondwana;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Scenes;
using KorTheBlight.Rendering;
using KorTheBlight.Simulation;
using KorTheBlight.Simulation.Combat;
using KorTheBlight.Tests;
using KorTheBlight.WorldModel;
using SkiaSharp;

namespace KorTheBlight;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Contains("--self-test", StringComparer.Ordinal))
        {
            try { return ScenarioChecks.Run(); }
            catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
        }
        IntPtr window = IntPtr.Zero, rendererHandle = IntPtr.Zero;
        SdlAdapter? adapter = null;
        ColonyRenderer? painter = null;
        MainMenuRenderer? menuPainter = null;
        Engine? engine = null;
        bool engineStarted = false, sdlInitialized = false;
        try
        {
            if (Sdl.SDL_Init(Sdl.InitVideo) < 0) throw new InvalidOperationException(Sdl.Error());
            sdlInitialized = true;
            window = Sdl.SDL_CreateWindow("KOR-ETERNAL FRONTIER", 1100, 720, Sdl.WindowResizable);
            if (window == IntPtr.Zero) throw new InvalidOperationException(Sdl.Error());
            rendererHandle = Sdl.SDL_CreateRenderer(window, IntPtr.Zero);
            if (rendererHandle == IntPtr.Zero) throw new InvalidOperationException(Sdl.Error());

            // New sessions receive a fresh seed; the generated seed is displayed on the overworld for reproducibility.
            var world = new WorldSimulation(Random.Shared.Next(1, int.MaxValue));
            var simulation = world.Colony;
            bool gameStarted = false, settingsMenu = false, startPaused = false;
            string menuMessage = string.Empty;
            adapter = new SdlAdapter(rendererHandle, 1100, 720);
            painter = new ColonyRenderer();
            menuPainter = new MainMenuRenderer();
            using var host = new RenderSurfaceHost<BitmapBackbuffer>(adapter);
            float previewMouseX = 385, previewMouseY = 350;
            using var scene = new Scene();
            host.Bind(scene, false);
            host.ViewManager.ConfigureSingleFullView();
            host.RedrawDirtyRectangleOnly = false;

            float cameraX = 0, cameraY = 0, zoom = 1;
            float worldCameraX = (float)(ContinuousWorldState.WorldExtent / 2), worldCameraY = (float)(ContinuousWorldState.WorldExtent / 2);
            long? selectedWorldEntityId = null;
            int? selectedMacroCellId = null;
            WorldResourceKind? selectedShipmentResource = null;
            bool factionDashboardOpen = false;
            string? selectedFactionName = "Kor";
            int factionTab = 0;
            bool workMatrixOpen = false;
            int workerInspectorTab = 0;
            int workMatrixPage = 0;
            bool systemsPanelOpen = false;
            int systemsTab = 0;
            int? selectedCombatEntityId = null;
            ArchitectureKind architectureMode = ArchitectureKind.None;
            PlaceableKind objectType = PlaceableKind.Workbench;
            bool placingObject = false, excavationMode = false, demolitionMode = false, paused = false;
            int buildMenuPage = 0;
            host.RenderBackbufferPostScene += canvas =>
            {
                if (gameStarted) painter.Draw(canvas, simulation, cameraX, cameraY, zoom, architectureMode, objectType, placingObject, excavationMode, demolitionMode, buildMenuPage, paused, world.SpeedMultiplier, world.SpatialWorld, worldCameraX, worldCameraY, selectedWorldEntityId, selectedMacroCellId, selectedShipmentResource, previewMouseX, previewMouseY, factionDashboardOpen, selectedFactionName, factionTab, workMatrixOpen, workerInspectorTab, workMatrixPage, world, systemsPanelOpen, systemsTab, selectedCombatEntityId);
                else menuPainter.Draw(canvas, settingsMenu, startPaused, menuMessage);
            };
            engine = Engine.Instance;
            engine.Initialize();
            engine.Configuration.TargetFPS = 60;
            var context = new PumpSynchronizationContext();
            engine.StartTimerDriven(context);
            engineStarted = true;

            bool running = true, wasLeft = false, wasRight = false, wasMiddle = false;
            float pendingWheelY = 0;
            float previousMouseX = 0, previousMouseY = 0;
            bool architectureDragActive = false;
            int architectureDragStartX = 0, architectureDragStartY = 0, architectureDragLastX = 0, architectureDragLastY = 0;
            ArchitectureKind architectureDragKind = ArchitectureKind.None;
            var clock = Stopwatch.StartNew();
            var appLifetime = Stopwatch.StartNew();
            double accumulator = 0;
            while (running)
            {
                while (Sdl.SDL_PollEvent(out var e) != 0)
                {
                    if (e.Type == Sdl.EventQuit || e.Type == Sdl.EventWindowClose) running = false;
                    else if (e.Type == Sdl.EventMouseWheel) pendingWheelY += e.WheelY;
                    else if (e.Type == Sdl.EventWindowResized && Sdl.SDL_GetWindowSize(window, out int width, out int height)) adapter.Resize(width, height);
                }

                double frame = Math.Min(clock.Elapsed.TotalSeconds, .1);
                clock.Restart();
                accumulator += frame;
                if (gameStarted && !paused)
                {
                    while (accumulator >= .1) { world.Step(.1f); simulation = world.Colony; accumulator -= .1; }
                }
                else accumulator = 0;
                scene.FullRefreshNeeded = true;

                uint buttons = Sdl.SDL_GetMouseState(out float mouseX, out float mouseY);
                var logicalMouse = adapter.AdapterPxToScreenPx(new System.Drawing.PointF(mouseX, mouseY));
                previewMouseX = logicalMouse.X;
                previewMouseY = logicalMouse.Y;
                bool pointerOverMap = logicalMouse.X < 770 && logicalMouse.Y >= 62 && logicalMouse.Y < 650;
                if (pendingWheelY != 0 && gameStarted && pointerOverMap)
                {
                    float oldZoom = zoom;
                    bool wasWorldView = oldZoom < .65f;
                    if (wasWorldView)
                    {
                        var anchor = ContinuousWorldProjection.ScreenToWorld(logicalMouse.X, logicalMouse.Y, worldCameraX, worldCameraY, oldZoom);
                        zoom = Math.Clamp(oldZoom * MathF.Pow(1.12f, pendingWheelY), .08f, 1.7f);
                        if (zoom < .65f)
                        {
                            float scale = ContinuousWorldProjection.Scale(zoom);
                            worldCameraX = Math.Clamp((float)(anchor.X - (logicalMouse.X - 385f) / scale), 0f, (float)ContinuousWorldState.WorldExtent);
                            worldCameraY = Math.Clamp((float)(anchor.Y - (logicalMouse.Y - 356f) / scale), 0f, (float)ContinuousWorldState.WorldExtent);
                        }
                    }
                    else
                    {
                        var anchor = OrthogonalGridProjection.ScreenToWorld(logicalMouse.X, logicalMouse.Y, cameraX, cameraY, oldZoom);
                        zoom = Math.Clamp(oldZoom * MathF.Pow(1.12f, pendingWheelY), .08f, 1.7f);
                        if (zoom >= .65f)
                        {
                            float scale = OrthogonalGridProjection.TileSize * Math.Clamp(zoom, .25f, 4f);
                            cameraX = Math.Clamp(logicalMouse.X - 385f - (anchor.X - ColonySimulation.Width / 2f) * scale, -500f, 500f);
                            cameraY = Math.Clamp(logicalMouse.Y - 350f - (anchor.Y - ColonySimulation.Height / 2f) * scale, -400f, 400f);
                        }
                    }
                    pendingWheelY = 0;
                }
                else if (pendingWheelY != 0)
                {
                    // Scrolling menus or the inspector must not zoom the map later when the
                    // pointer returns to it; discard wheel input outside the visible map.
                    pendingWheelY = 0;
                }
                bool worldView = zoom < .65f;
                var continuousMouse = ContinuousWorldProjection.ScreenToWorld(logicalMouse.X, logicalMouse.Y, worldCameraX, worldCameraY, zoom);
                var colonyMouse = OrthogonalGridProjection.ScreenToWorld(logicalMouse.X, logicalMouse.Y, cameraX, cameraY, zoom);
                (float X, float Y) worldMouse = worldView
                    ? ((float)continuousMouse.X, (float)continuousMouse.Y)
                    : colonyMouse;
                bool inWorldView = logicalMouse.X < 770 && logicalMouse.Y >= 62 && logicalMouse.Y < 650;
                bool left = (buttons & Sdl.ButtonLeftMask) != 0;
                bool right = (buttons & Sdl.ButtonRightMask) != 0;
                bool middle = (buttons & Sdl.ButtonMiddleMask) != 0;

                // Middle-mouse drag pans the camera; no keyboard is required for navigation.
                if (middle && wasMiddle)
                {
                    if (worldView)
                    {
                        float worldScale = ContinuousWorldProjection.Scale(zoom);
                        worldCameraX = Math.Clamp(worldCameraX - (logicalMouse.X - previousMouseX) / worldScale, 0f, (float)ContinuousWorldState.WorldExtent);
                        worldCameraY = Math.Clamp(worldCameraY - (logicalMouse.Y - previousMouseY) / worldScale, 0f, (float)ContinuousWorldState.WorldExtent);
                    }
                    else
                    {
                        cameraX = Math.Clamp(cameraX - (logicalMouse.X - previousMouseX) / Math.Max(.65f, zoom), -350f, 350f);
                        cameraY = Math.Clamp(cameraY - (logicalMouse.Y - previousMouseY) / Math.Max(.65f, zoom), -240f, 240f);
                    }
                }
                previousMouseX = logicalMouse.X;
                previousMouseY = logicalMouse.Y;
                wasMiddle = middle;

                if (left && !wasLeft)
                {
                    if (!gameStarted)
                    {
                        float mx = logicalMouse.X, my = logicalMouse.Y;
                        if (settingsMenu)
                        {
                            if (Hit(mx, my, 370, 420, 730, 472)) startPaused = !startPaused;
                            else if (Hit(mx, my, 370, 490, 730, 542)) { settingsMenu = false; menuMessage = string.Empty; }
                        }
                        else if (Hit(mx, my, 370, 260, 730, 312))
                        {
                            // Generate a fresh world for each new-game action; reusing a fixed seed made every new session identical.
                            world = new WorldSimulation(Random.Shared.Next(1, int.MaxValue));
                            simulation = world.Colony;
                            paused = startPaused;
                            world.IsPaused = paused;
                            gameStarted = true;
                            menuMessage = string.Empty;
                        }
                        else if (Hit(mx, my, 370, 326, 730, 378))
                        {
                            try
                            {
                                var loaded = GameSessionService.Load();
                                world = new WorldSimulation(70241);
                                world.Restore(loaded);
                                simulation = world.Colony;
                                paused = false;
                                gameStarted = true;
                                menuMessage = string.Empty;
                            }
                            catch (Exception ex) { menuMessage = $"Spielstand konnte nicht geladen werden: {ex.Message}"; }
                        }
                        else if (Hit(mx, my, 370, 392, 730, 444)) settingsMenu = true;
                        else if (Hit(mx, my, 370, 458, 730, 510)) running = false;
                    }
                    else if (Hit(logicalMouse.X, logicalMouse.Y, 12, 38, 1088, 59))
                    {
                        var latest = world.SpatialWorld.MacroWorld.Notifications.LastOrDefault();
                        if (latest is not null && latest.CellId > 0 && latest.CellId <= world.SpatialWorld.MacroWorld.Cells.Length)
                        {
                            var cell = world.SpatialWorld.MacroWorld.Cells[latest.CellId - 1];
                            worldCameraX = (cell.X + 0.5f) * MacroWorldState.WorldCellSize;
                            worldCameraY = (cell.Y + 0.5f) * MacroWorldState.WorldCellSize;
                            zoom = 0.5f;
                            selectedWorldEntityId = null;
                            selectedMacroCellId = latest.CellId;
                        }
                        else if (latest is not null)
                            simulation.SetMessage($"World report: {latest.Message}");
                    }
                    else if (workMatrixOpen)
                    {
                        float mx = logicalMouse.X, my = logicalMouse.Y;
                        if (Hit(mx, my, 850, 584, 942, 612)) workMatrixOpen = false;
                        else if (Hit(mx, my, 710, 584, 770, 612)) workMatrixPage = Math.Max(0, workMatrixPage - 1);
                        else if (Hit(mx, my, 778, 584, 838, 612)) workMatrixPage = Math.Min(Math.Max(0, (simulation.Workers.Count - 1) / 9), workMatrixPage + 1);
                        else
                        {
                            var categories = Enum.GetValues<WorkCategory>();
                            int[] columnX = [420, 520, 620, 720, 820];
                            var workers = simulation.Workers.Skip(workMatrixPage * 9).Take(9).ToArray();
                            for (int row = 0; row < workers.Length; row++)
                            {
                                float rowY = 244 + row * 38;
                                for (int col = 0; col < categories.Length; col++)
                                {
                                    if (!Hit(mx, my, columnX[col], rowY, columnX[col] + 64, rowY + 28)) continue;
                                    int current = simulation.WorkPriorities.GetPriority(workers[row].Id, categories[col]);
                                    simulation.WorkPriorities.SetPriority(workers[row].Id, categories[col], (current + 1) % 5);
                                    simulation.SetMessage($"{workers[row].Name}: {categories[col]} priority is now {(current + 1) % 5}.");
                                    goto WorkMatrixClickHandled;
                                }
                            }
                            WorkMatrixClickHandled:;
                        }
                    }
                    else if (systemsPanelOpen)
                    {
                        float mx = logicalMouse.X, my = logicalMouse.Y;
                        if (Hit(mx, my, 862, 88, 956, 116))
                        {
                            systemsPanelOpen = false;
                        }
                        else if (Hit(mx, my, 140, 116, 252, 144)) systemsTab = 0;
                        else if (Hit(mx, my, 262, 116, 374, 144)) systemsTab = 1;
                        else if (Hit(mx, my, 384, 116, 496, 144)) systemsTab = 2;
                        else if (Hit(mx, my, 506, 116, 618, 144)) systemsTab = 3;
                        else if (systemsTab == 0 && Hit(mx, my, 140, 490, 320, 516))
                        {
                            int deployed = world.TryDeployColonists();
                            simulation.SetMessage(deployed > 0
                                ? $"{deployed} colonists deployed to defend the settlement."
                                : "Deployment unavailable: no eligible residents or no room in the combat roster.");
                        }
                        else if (systemsTab == 0 && Hit(mx, my, 330, 490, 510, 516))
                        {
                            int repaired = world.RepairDamagedStructures();
                            if (repaired == 0)
                                simulation.SetMessage("No structures repaired. Damage/infection may be absent, or repair materials/components are missing.");
                        }
                        else if (systemsTab == 0)
                        {
                            var listed = world.CombatEngine.Entities
                                .OrderBy(entity => entity.FactionId == 1 ? 0 : 1)
                                .ThenBy(entity => entity.Id).Take(10).ToArray();
                            for (int i = 0; i < listed.Length; i++)
                            {
                                float rowTop = 180 + i * 30;
                                if (!Hit(mx, my, 140, rowTop, 955, rowTop + 26)) continue;
                                var clicked = listed[i];
                                if (selectedCombatEntityId.HasValue &&
                                    world.CombatEngine.TryGetEntity(selectedCombatEntityId.Value, out var selected) &&
                                    selected.FactionId == 1 && clicked.FactionId != 1)
                                {
                                    world.CombatEngine.TrySetTarget(selected.Id, clicked.Id);
                                    simulation.SetMessage($"Attack order: {world.GetCombatantName(selected)} -> {world.GetCombatantName(clicked)}.");
                                }
                                else
                                {
                                    selectedCombatEntityId = clicked.Id;
                                    simulation.SetMessage($"Selected combatant: {world.GetCombatantName(clicked)}.");
                                }
                                break;
                            }
                        }
                        else if (systemsTab == 1)
                        {
                            var recipes = world.Workshop.Recipes.OrderBy(recipe => recipe.Id, StringComparer.Ordinal).Take(5).ToArray();
                            bool handled = false;
                            for (int i = 0; i < recipes.Length; i++)
                            {
                                float rowTop = 174 + i * 44;
                                if (!Hit(mx, my, 820, rowTop + 5, 944, rowTop + 33)) continue;
                                var recipe = recipes[i];
                                if (world.TryStartCraft(recipe.Id, out long jobId))
                                    simulation.SetMessage($"Workshop job #{jobId} started: {recipe.ResultItemId}. Inputs reserved from the settlement ledger.");
                                else
                                    simulation.SetMessage($"Cannot start {recipe.ResultItemId}: build a workbench, unlock the required research, and stock the settlement ledger.");
                                handled = true;
                                break;
                            }
                            if (!handled && Hit(mx, my, 142, 512, 362, 544))
                            {
                                if (world.TryBuildVehicle(out int vehicleId))
                                    simulation.SetMessage($"Heavy tank #{vehicleId} assembled from the settlement item ledger.");
                                else
                                    simulation.SetMessage("Tank assembly unavailable: unlock armored-vehicle research, supply every part, and free storage/tile capacity.");
                            }
                        }
                        else if (systemsTab == 3)
                        {
                            var units = world.CombatEngine.Entities.Where(entity => entity.FactionId == 1 &&
                                    entity.Type == CombatEntityType.Human && entity.SourcePawnId > 0)
                                .OrderBy(entity => entity.SourcePawnId).Take(9).ToArray();
                            bool handled = false;
                            for (int i = 0; i < units.Length; i++)
                            {
                                float rowTop = 196 + i * 31;
                                if (!Hit(mx, my, 140, rowTop, 955, rowTop + 27)) continue;
                                selectedCombatEntityId = units[i].Id;
                                simulation.SetMessage($"Selected {world.GetCombatantName(units[i])}: {units[i].EquippedWeapon}, ammunition {units[i].CurrentAmmo}/{units[i].MaxAmmo}.");
                                handled = true;
                                break;
                            }
                            if (!handled && Hit(mx, my, 140, 490, 320, 520))
                            {
                                if (!selectedCombatEntityId.HasValue || !world.TryEquipRifle(selectedCombatEntityId.Value))
                                    if (!selectedCombatEntityId.HasValue)
                                        simulation.SetMessage("Select a deployed Kor colonist from the loadout list first.");
                            }
                        }
                        else if (systemsTab == 2)
                        {
                            var projects = world.ResearchManager.Database.ResearchDatabase.Values
                                .OrderBy(project => project.Name, StringComparer.Ordinal).Take(6).ToArray();
                            for (int i = 0; i < projects.Length; i++)
                            {
                                float rowTop = 174 + i * 56;
                                if (!Hit(mx, my, 820, rowTop + 9, 944, rowTop + 37)) continue;
                                var project = projects[i];
                                if (world.ResearchManager.State.CompletedResearchIds.Contains(project.Id))
                                    simulation.SetMessage($"{project.Name} is already complete.");
                                else if (world.ResearchManager.State.CurrentResearchId == project.Id)
                                    simulation.SetMessage($"{project.Name} is already in progress.");
                                else if (world.ResearchManager.Start(project.Id))
                                    simulation.SetMessage($"Research started: {project.Name}. Work advances under the unified world clock.");
                                else
                                    simulation.SetMessage($"Research locked: complete all prerequisites for {project.Name} first.");
                                break;
                            }
                        }
                    }
                    else if (factionDashboardOpen)
                    {
                        float mx = logicalMouse.X, my = logicalMouse.Y;
                        if (Hit(mx, my, 970, 80, 1046, 106)) factionDashboardOpen = false;
                        else if (Hit(mx, my, 410, 207, 518, 233)) factionTab = 0;
                        else if (Hit(mx, my, 526, 207, 634, 233)) factionTab = 1;
                        else if (Hit(mx, my, 642, 207, 750, 233)) factionTab = 2;
                        else if (Hit(mx, my, 66, 173, 354, 212)) selectedFactionName = world.SpatialWorld.FactionIntelligence.OrderBy(profile => profile.Faction == "Kor" ? 0 : 1).ThenBy(profile => profile.Faction, StringComparer.Ordinal).ElementAtOrDefault(0)?.Faction;
                        else
                        {
                            var roster = world.SpatialWorld.FactionIntelligence.OrderBy(profile => profile.Faction == "Kor" ? 0 : 1).ThenBy(profile => profile.Faction, StringComparer.Ordinal).Take(10).ToArray();
                            for (int i = 0; i < roster.Length; i++)
                            {
                                float rowTop = 173 + i * 43;
                                if (Hit(mx, my, 66, rowTop, 354, rowTop + 39))
                                {
                                    selectedFactionName = roster[i].Faction;
                                    break;
                                }
                            }
                        }
                    }
                    else if (Hit(logicalMouse.X, logicalMouse.Y, 855, 8, 950, 35))
                    {
                        systemsPanelOpen = true;
                        systemsTab = 0;
                        workMatrixOpen = false;
                        factionDashboardOpen = false;
                    }
                    else if (Hit(logicalMouse.X, logicalMouse.Y, 1008, 8, 1085, 35))
                    {
                        workMatrixOpen = true;
                        factionDashboardOpen = false;
                        systemsPanelOpen = false;
                    }
                    else if (!worldView && simulation.SelectedWorkerId.HasValue && Hit(logicalMouse.X, logicalMouse.Y, 14, 476, 374, 646))
                    {
                        var inspectedWorker = simulation.Workers.FirstOrDefault(worker => worker.Id == simulation.SelectedWorkerId.Value);
                        if (inspectedWorker?.IsExternalVisitor == true)
                        {
                            if (Hit(logicalMouse.X, logicalMouse.Y, 22, 506, 122, 528))
                            {
                                bool resolved = inspectedWorker.TradeCargo.Count == 0
                                    ? simulation.ResolveExternalVisitor(inspectedWorker.Id, true)
                                    : simulation.TradeWithVisitor(inspectedWorker.Id, world.SpatialWorld.MacroWorld.Market);
                                if (!resolved) simulation.SetMessage("Visitor action unavailable: check cargo, market value, and colony stock.");
                            }
                            else if (Hit(logicalMouse.X, logicalMouse.Y, 128, 506, 228, 528))
                                simulation.ResolveExternalVisitor(inspectedWorker.Id, false);
                        }
                        else
                        {
                            if (Hit(logicalMouse.X, logicalMouse.Y, 22, 506, 122, 528)) workerInspectorTab = 0;
                            else if (Hit(logicalMouse.X, logicalMouse.Y, 128, 506, 228, 528)) workerInspectorTab = 1;
                        }
                    }
                    else if (Hit(logicalMouse.X, logicalMouse.Y, 955, 8, 1005, 35))
                    {
                        factionDashboardOpen = true;
                        if (!world.SpatialWorld.FactionIntelligence.Any(profile => profile.Faction == selectedFactionName))
                            selectedFactionName = "Kor";
                    }
                    else if (!worldView && Hit(logicalMouse.X, logicalMouse.Y, 920, 660, 1085, 687))
                    {
                        int recruited = simulation.RecruitColonists(10);
                        simulation.SetMessage(recruited > 0
                            ? $"Recruited {recruited} residents. Population: {simulation.Workers.Count}."
                            : "Recruitment failed: no walkable ground is available.");
                    }
                    else if (inWorldView && worldView)
                    {
                        // Convert the 20-pixel hit radius to world units and ask the spatial index
                        // for local candidates instead of projecting every world entity on each click.
                        double worldHitRadius = 20d / ContinuousWorldProjection.Scale(zoom);
                        var hit = world.SpatialWorld.QueryRadius(continuousMouse.X, continuousMouse.Y, worldHitRadius)
                            .Select(entity => (Entity: entity, Screen: ContinuousWorldProjection.WorldToScreen(entity.X, entity.Y, worldCameraX, worldCameraY, zoom)))
                            .Select(item => (item.Entity, Distance: MathF.Sqrt(MathF.Pow(item.Screen.X - logicalMouse.X, 2) + MathF.Pow(item.Screen.Y - logicalMouse.Y, 2))))
                            .Where(item => item.Distance <= 20f).OrderBy(item => item.Distance).FirstOrDefault();
                        if (hit.Entity is null)
                        {
                            int cellX = Math.Clamp((int)(continuousMouse.X / MacroWorldState.WorldCellSize), 0, MacroWorldState.Columns - 1);
                            int cellY = Math.Clamp((int)(continuousMouse.Y / MacroWorldState.WorldCellSize), 0, MacroWorldState.Rows - 1);
                            selectedMacroCellId = cellY * MacroWorldState.Columns + cellX + 1;
                            selectedWorldEntityId = null;
                        }
                        else
                        {
                            selectedMacroCellId = null;
                        }
                        if (hit.Entity != null && selectedShipmentResource.HasValue &&
                            selectedWorldEntityId.HasValue &&
                            world.SpatialWorld.Entities.Any(entity => entity.Id == selectedWorldEntityId.Value && entity.Kind == WorldEntityKind.Caravan) &&
                            hit.Entity.Kind == WorldEntityKind.Settlement)
                        {
                            var home = world.SpatialWorld.Entities.Single(entity => entity.Kind == WorldEntityKind.Settlement && entity.Faction == "Kor");
                            if (home.Id == hit.Entity.Id)
                                simulation.SetMessage("Choose a different destination settlement.");
                            else
                            {
                                bool created = world.SpatialWorld.CreateShipment(selectedWorldEntityId.Value, home.Id, hit.Entity.Id, selectedShipmentResource.Value, 10, 1_000);
                                simulation.SetMessage(created
                                    ? $"Shipment dispatched: 10 {selectedShipmentResource.Value} to {hit.Entity.Name}."
                                    : $"Shipment unavailable: Kor Hearthhold lacks {selectedShipmentResource.Value}.");
                                if (created) selectedShipmentResource = null;
                            }
                        }
                        else if (hit.Entity != null)
                        {
                            selectedWorldEntityId = hit.Entity.Id;
                            simulation.SetMessage($"Selected world entity: {hit.Entity.Name} ({hit.Entity.Kind}).");
                        }
                    }
                    else if (inWorldView)
                    {
                        int tileX = (int)MathF.Floor(worldMouse.X), tileY = (int)MathF.Floor(worldMouse.Y);
                        var clickedCombat = world.CombatEngine.Entities
                            .Select(entity => (Entity: entity,
                                Distance: MathF.Sqrt(MathF.Pow(entity.X - worldMouse.X, 2) + MathF.Pow(entity.Y - worldMouse.Y, 2))))
                            .Where(item => item.Distance <= 1.0f)
                            .OrderBy(item => item.Distance).ThenBy(item => item.Entity.Id)
                            .Select(item => item.Entity).FirstOrDefault();
                        if (clickedCombat.Id > 0)
                        {
                            if (selectedCombatEntityId.HasValue &&
                                world.CombatEngine.TryGetEntity(selectedCombatEntityId.Value, out var selectedCombat) &&
                                selectedCombat.FactionId == 1 && clickedCombat.FactionId != 1)
                            {
                                world.CombatEngine.TrySetTarget(selectedCombat.Id, clickedCombat.Id);
                                simulation.SetMessage($"Attack order: {world.GetCombatantName(selectedCombat)} -> {world.GetCombatantName(clickedCombat)}.");
                            }
                            else
                            {
                                selectedCombatEntityId = clickedCombat.Id;
                                simulation.SetMessage($"Selected combatant: {world.GetCombatantName(clickedCombat)}. Right-click an enemy or use WAR ROOM.");
                            }
                        }
                        else if (demolitionMode) simulation.DemolishAt(tileX, tileY);
                        else if (placingObject) simulation.PlaceObjectBlueprint(objectType, tileX, tileY);
                        else if (excavationMode) simulation.DesignExcavation(tileX, tileY);
                        else if (architectureMode != ArchitectureKind.None)
                        {
                            architectureDragActive = architectureMode is ArchitectureKind.Wall or ArchitectureKind.Floor;
                            architectureDragKind = architectureMode;
                            architectureDragStartX = architectureDragLastX = tileX;
                            architectureDragStartY = architectureDragLastY = tileY;
                            if (architectureMode == ArchitectureKind.Wall)
                                DesignArchitectureLine(simulation, tileX, tileY, tileX, tileY, ArchitectureKind.Wall);
                            else if (architectureMode != ArchitectureKind.Floor)
                                simulation.DesignTileConstruction(tileX, tileY, architectureMode);
                            simulation.SetMessage(architectureMode == ArchitectureKind.Wall
                                ? "Wall tool: hold the left mouse button and drag to draw a straight wall."
                                : architectureMode == ArchitectureKind.Floor
                                    ? "Floor tool: hold the left mouse button and drag to fill a rectangular area."
                                    : $"Construction order: {architectureMode}.");
                        }
                        else if (!simulation.SelectWorkOrderAt(worldMouse.X, worldMouse.Y)) simulation.SelectWorkerAt(worldMouse.X, worldMouse.Y);
                    }
                    else if (logicalMouse.X >= 770 && logicalMouse.Y >= 423 && logicalMouse.Y < 650)
                    {
                        float mx = logicalMouse.X, my = logicalMouse.Y;
                        if (!worldView && Hit(mx, my, 784, 425, 838, 447))
                        {
                            simulation.ChangeSelectedWorkOrderPriority(-1);
                        }
                        else if (!worldView && Hit(mx, my, 842, 425, 896, 447))
                        {
                            simulation.ChangeSelectedWorkOrderPriority(1);
                        }
                        else if (Hit(mx, my, 784, 501, 850, 523))
                        {
                            paused = !paused;
                            world.IsPaused = paused;
                            simulation.SetMessage(paused ? "Simulation paused." : "Simulation resumed.");
                        }
                        else if (Hit(mx, my, 856, 501, 914, 523))
                        {
                            world.SetSpeed(Math.Max(.25f, world.SpeedMultiplier / 2f));
                            simulation.SetMessage($"World speed: {world.SpeedMultiplier:0.##}x.");
                        }
                        else if (Hit(mx, my, 920, 501, 978, 523))
                        {
                            world.SetSpeed(Math.Min(4f, world.SpeedMultiplier * 2f));
                            simulation.SetMessage($"World speed: {world.SpeedMultiplier:0.##}x.");
                        }
                        else if (Hit(mx, my, 984, 501, 1050, 523))
                        {
                            architectureMode = ArchitectureKind.None;
                            placingObject = false;
                            excavationMode = false;
                            demolitionMode = false;
                            simulation.SetMessage("Current placement mode cancelled.");
                        }
                        else if (Hit(mx, my, 784, 527, 850, 549))
                        {
                            try { GameSessionService.Save(world); simulation.SetMessage("World session saved."); }
                            catch (Exception ex) { simulation.SetMessage($"Save failed: {ex.Message}"); }
                        }
                        else if (Hit(mx, my, 854, 527, 920, 549))
                        {
                            try
                            {
                                var loaded = GameSessionService.Load();
                                world.Restore(loaded);
                                simulation = world.Colony;
                                selectedCombatEntityId = null;
                                simulation.SetMessage("World session loaded.");
                            }
                            catch (Exception ex) { simulation.SetMessage($"Load failed: {ex.Message}"); }
                        }
                        else if (Hit(mx, my, 924, 527, 990, 549))
                        {
                            zoom = Math.Max(.08f, zoom / 1.15f);
                        }
                        else if (Hit(mx, my, 994, 527, 1060, 549))
                        {
                            zoom = Math.Min(1.7f, zoom * 1.15f);
                        }
                        else if (!worldView && simulation.SelectedWorkOrderId.HasValue && Hit(mx, my, 904, 425, 1086, 447))
                        {
                            simulation.CancelSelectedWorkOrder();
                        }
                        else if (worldView && Hit(mx, my, 784, 566, 838, 588)) { selectedShipmentResource = WorldResourceKind.Wood; simulation.SetMessage("Cargo selected: Wood. Select a caravan, then click a destination settlement."); }
                        else if (worldView && Hit(mx, my, 842, 566, 896, 588)) { selectedShipmentResource = WorldResourceKind.Stone; simulation.SetMessage("Cargo selected: Stone. Select a caravan, then click a destination settlement."); }
                        else if (worldView && Hit(mx, my, 900, 566, 954, 588)) { selectedShipmentResource = WorldResourceKind.Food; simulation.SetMessage("Cargo selected: Food. Select a caravan, then click a destination settlement."); }
                        else if (worldView && Hit(mx, my, 958, 566, 1012, 588)) { selectedShipmentResource = WorldResourceKind.Alloy; simulation.SetMessage("Cargo selected: Alloy. Select a caravan, then click a destination settlement."); }
                        else if (worldView && Hit(mx, my, 1016, 566, 1070, 588)) { selectedShipmentResource = null; simulation.SetMessage("Shipment selection cleared."); }
                        else if (worldView && Hit(mx, my, 784, 593, 1086, 615)) { selectedShipmentResource = null; simulation.SetMessage("Shipment selection cleared."); }
                        else if (!worldView && Hit(mx, my, 784, 566, 829, 588)) { architectureMode = ArchitectureKind.Wall; placingObject = false; excavationMode = false; }
                        else if (!worldView && Hit(mx, my, 834, 566, 879, 588)) { architectureMode = ArchitectureKind.Floor; placingObject = false; excavationMode = false; demolitionMode = false; }
                        else if (!worldView && Hit(mx, my, 884, 566, 929, 588)) { architectureMode = ArchitectureKind.Path; placingObject = false; excavationMode = false; demolitionMode = false; }
                        else if (!worldView && Hit(mx, my, 934, 566, 979, 588)) { architectureMode = ArchitectureKind.Door; placingObject = false; excavationMode = false; demolitionMode = false; }
                        else if (!worldView && Hit(mx, my, 984, 566, 1029, 588)) { architectureMode = ArchitectureKind.Conduit; placingObject = false; excavationMode = false; demolitionMode = false; }
                        else if (!worldView && Hit(mx, my, 1034, 566, 1079, 588)) { architectureMode = ArchitectureKind.None; placingObject = false; excavationMode = true; demolitionMode = false; simulation.SetMessage("Excavation mode: click solid rock to designate mining."); }
                        else if (!worldView && Hit(mx, my, 784, 593, 831, 615) && buildMenuPage == 0) { objectType = PlaceableKind.Workbench; placingObject = true; architectureMode = ArchitectureKind.None; excavationMode = false; demolitionMode = false; }
                        else if (!worldView && Hit(mx, my, 835, 593, 882, 615) && buildMenuPage == 0) { objectType = PlaceableKind.SleepingPod; placingObject = true; architectureMode = ArchitectureKind.None; excavationMode = false; demolitionMode = false; }
                        else if (!worldView && Hit(mx, my, 886, 593, 933, 615) && buildMenuPage == 0) { objectType = PlaceableKind.StorageRack; placingObject = true; architectureMode = ArchitectureKind.None; excavationMode = false; demolitionMode = false; }
                        else if (!worldView && Hit(mx, my, 937, 593, 984, 615) && buildMenuPage == 0) { objectType = PlaceableKind.Generator; placingObject = true; architectureMode = ArchitectureKind.None; excavationMode = false; demolitionMode = false; }
                        else if (!worldView && Hit(mx, my, 988, 593, 1035, 615) && buildMenuPage == 0) { buildMenuPage = 1; architectureMode = ArchitectureKind.None; placingObject = false; excavationMode = false; demolitionMode = false; simulation.SetMessage("Build menu: comfort and recreation."); }
                        else if (!worldView && Hit(mx, my, 784, 593, 831, 615) && buildMenuPage == 1) { objectType = PlaceableKind.Garden; placingObject = true; architectureMode = ArchitectureKind.None; excavationMode = false; demolitionMode = false; }
                        else if (!worldView && Hit(mx, my, 835, 593, 882, 615) && buildMenuPage == 1) { objectType = PlaceableKind.DiningTable; placingObject = true; architectureMode = ArchitectureKind.None; excavationMode = false; demolitionMode = false; }
                        else if (!worldView && Hit(mx, my, 886, 593, 933, 615) && buildMenuPage == 1) { objectType = PlaceableKind.Lamp; placingObject = true; architectureMode = ArchitectureKind.None; excavationMode = false; demolitionMode = false; }
                        else if (!worldView && Hit(mx, my, 937, 593, 984, 615) && buildMenuPage == 1) { objectType = PlaceableKind.ComfortBench; placingObject = true; architectureMode = ArchitectureKind.None; excavationMode = false; demolitionMode = false; }
                        else if (!worldView && Hit(mx, my, 988, 593, 1035, 615) && buildMenuPage == 1) { buildMenuPage = 0; architectureMode = ArchitectureKind.None; placingObject = false; excavationMode = false; demolitionMode = false; simulation.SetMessage("Build menu: work and infrastructure."); }
                        else if (!worldView && Hit(mx, my, 1039, 593, 1086, 615))
                        {
                            architectureMode = ArchitectureKind.None;
                            placingObject = false;
                            excavationMode = false;
                            demolitionMode = !demolitionMode;
                            simulation.SetMessage(demolitionMode ? "Demolition mode: click a completed structure or built tile." : "Demolition mode disabled.");
                        }
                    }
                }
                // While dragging, walls are connected as straight grid segments. Floors are
                // filled as a rectangle on release, avoiding a dotted trail of individual tiles.
                if (gameStarted && architectureDragActive && left && wasLeft && !worldView && inWorldView)
                {
                    int tileX = (int)MathF.Floor(worldMouse.X);
                    int tileY = (int)MathF.Floor(worldMouse.Y);
                    if (architectureDragKind == ArchitectureKind.Wall &&
                        (tileX != architectureDragLastX || tileY != architectureDragLastY))
                    {
                        DesignArchitectureLine(simulation, architectureDragLastX, architectureDragLastY, tileX, tileY, ArchitectureKind.Wall);
                        architectureDragLastX = tileX;
                        architectureDragLastY = tileY;
                    }
                }
                if (gameStarted && architectureDragActive && !left)
                {
                    if (architectureDragKind == ArchitectureKind.Floor && !worldView && inWorldView)
                    {
                        int endX = (int)MathF.Floor(worldMouse.X);
                        int endY = (int)MathF.Floor(worldMouse.Y);
                        DesignArchitectureRectangle(simulation, architectureDragStartX, architectureDragStartY, endX, endY, ArchitectureKind.Floor);
                    }
                    architectureDragActive = false;
                    architectureDragKind = ArchitectureKind.None;
                }

                if (gameStarted && inWorldView && right && !wasRight)
                {
                    if (worldView && selectedWorldEntityId.HasValue && !world.SpatialWorld.Shipments.Any(shipment => shipment.CarrierEntityId == selectedWorldEntityId.Value && shipment.Status == WorldShipmentStatus.InTransit) && world.SpatialWorld.Entities.Any(entity => entity.Id == selectedWorldEntityId.Value && entity.Kind is WorldEntityKind.Army or WorldEntityKind.Caravan))
                    {
                        world.SpatialWorld.OrderMove(selectedWorldEntityId.Value, worldMouse.X, worldMouse.Y, 240d);
                        simulation.SetMessage("World movement order issued.");
                    }
                    else if (!worldView && selectedCombatEntityId.HasValue &&
                             world.CombatEngine.TryGetEntity(selectedCombatEntityId.Value, out var selectedCombat) &&
                             selectedCombat.FactionId == 1)
                    {
                        var targetCombat = world.CombatEngine.Entities
                            .Where(entity => entity.FactionId != 1)
                            .Select(entity => (Entity: entity,
                                Distance: MathF.Sqrt(MathF.Pow(entity.X - worldMouse.X, 2) + MathF.Pow(entity.Y - worldMouse.Y, 2))))
                            .Where(item => item.Distance <= 2.5f)
                            .OrderBy(item => item.Distance).ThenBy(item => item.Entity.Id)
                            .Select(item => item.Entity).FirstOrDefault();
                        if (targetCombat.Id > 0 && world.CombatEngine.TrySetTarget(selectedCombat.Id, targetCombat.Id))
                            simulation.SetMessage($"Attack order: {world.GetCombatantName(selectedCombat)} -> {world.GetCombatantName(targetCombat)}.");
                        else
                            simulation.SetMessage("No hostile combatant close to that order position. Select an enemy in WAR ROOM or right-click beside a visible enemy.");
                    }
                    else if (!worldView) simulation.CommandSelectedTo(worldMouse.X, worldMouse.Y);
                }
                wasLeft = left;
                wasRight = right;

                engine.Tick();
                context.Drain();
                if (args.Contains("--smoke-test", StringComparer.Ordinal) && appLifetime.Elapsed >= TimeSpan.FromSeconds(1)) gameStarted = true;
                if (args.Contains("--smoke-test", StringComparer.Ordinal) && appLifetime.Elapsed >= TimeSpan.FromSeconds(2)) running = false;
                Sdl.SDL_Delay(4);
            }
            engine.StopAndWait(); engineStarted = false;
            engine.Dispose(); engine = null;
            if (args.Contains("--smoke-test", StringComparer.Ordinal)) Console.WriteLine("SDL/Gondwana runtime smoke check passed; shutdown completed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"KOR-ETERNAL FRONTIER could not start: {ex}");
            return 1;
        }
        finally
        {
            if (engineStarted) { engine!.StopAndWait(); engine.Dispose(); }
            menuPainter?.Dispose(); painter?.Dispose(); adapter?.Dispose();
            if (rendererHandle != IntPtr.Zero) Sdl.SDL_DestroyRenderer(rendererHandle);
            if (window != IntPtr.Zero) Sdl.SDL_DestroyWindow(window);
            if (sdlInitialized) Sdl.SDL_Quit();
        }
    }

    private static void DesignArchitectureLine(ColonySimulation simulation, int x0, int y0, int x1, int y1, ArchitectureKind kind)
    {
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;
        while (true)
        {
            simulation.DesignTileConstruction(x0, y0, kind);
            if (x0 == x1 && y0 == y1) break;
            int twiceError = 2 * error;
            if (twiceError >= dy) { error += dy; x0 += sx; }
            if (twiceError <= dx) { error += dx; y0 += sy; }
        }
    }

    private static void DesignArchitectureRectangle(ColonySimulation simulation, int x0, int y0, int x1, int y1, ArchitectureKind kind)
    {
        int left = Math.Min(x0, x1), right = Math.Max(x0, x1);
        int top = Math.Min(y0, y1), bottom = Math.Max(y0, y1);
        for (int y = top; y <= bottom; y++)
            for (int x = left; x <= right; x++)
                simulation.DesignTileConstruction(x, y, kind);
        simulation.SetMessage($"Rectangular {kind.ToString().ToLowerInvariant()} area designated: {right-left+1} × {bottom-top+1} tiles.");
    }

    private static bool Hit(float x, float y, float left, float top, float right, float bottom)
        => x >= left && x <= right && y >= top && y <= bottom;

}

internal sealed class SdlAdapter(IntPtr renderer, int width, int height) : RenderSurfaceAdapterBase(width, height), IDisposable
{
    private IntPtr _texture;
    private int _textureWidth, _textureHeight;
    public void Resize(int width, int height) => SetDestinationSize(width, height);
    public override void Present(SKImage image, SKRectI source, SKRect destination)
    {
        using var pixels = SKBitmap.FromImage(image);
        if (pixels.GetPixels() == IntPtr.Zero) return;
        if (_texture == IntPtr.Zero || _textureWidth != image.Width || _textureHeight != image.Height)
        {
            if (_texture != IntPtr.Zero) Sdl.SDL_DestroyTexture(_texture);
            _texture = Sdl.SDL_CreateTexture(renderer, Sdl.PixelFormatBgra32, Sdl.TextureAccessStreaming, image.Width, image.Height);
            if (_texture == IntPtr.Zero) throw new InvalidOperationException(Sdl.Error());
            (_textureWidth, _textureHeight) = (image.Width, image.Height);
        }
        var src = new SdlFRect(source.Left, source.Top, source.Width, source.Height);
        var dst = new SdlFRect(destination.Left, destination.Top, destination.Width, destination.Height);
        if (Sdl.SDL_UpdateTexture(_texture, IntPtr.Zero, pixels.GetPixels(), pixels.RowBytes) < 0 ||
            Sdl.SDL_RenderClear(renderer) < 0 || Sdl.SDL_RenderTexture(renderer, _texture, ref src, ref dst) < 0)
            throw new InvalidOperationException(Sdl.Error());
        Sdl.SDL_RenderPresent(renderer);
    }
    public void Dispose() { if (_texture != IntPtr.Zero) Sdl.SDL_DestroyTexture(_texture); _texture = IntPtr.Zero; }
}

[StructLayout(LayoutKind.Sequential)]
internal struct SdlFRect(float x, float y, float width, float height)
{
    public float X = x, Y = y, W = width, H = height;
}

internal sealed class PumpSynchronizationContext : SynchronizationContext
{
    private readonly Queue<(SendOrPostCallback Callback, object? State)> _queue = new();
    public override void Post(SendOrPostCallback callback, object? state) { lock (_queue) _queue.Enqueue((callback, state)); }
    public void Drain()
    {
        while (true)
        {
            (SendOrPostCallback Callback, object? State) item;
            lock (_queue) { if (_queue.Count == 0) return; item = _queue.Dequeue(); }
            item.Callback(item.State);
        }
    }
}

internal static class Sdl
{
    private const string Lib = "SDL3";
    public const uint InitVideo = 0x20, WindowResizable = 0x20, EventQuit = 0x100, EventWindowClose = 0x210, EventWindowResized = 0x206, EventMouseWheel = 0x403;
    public const uint ButtonLeftMask = 1, ButtonMiddleMask = 2, ButtonRightMask = 4;
    public const int TextureAccessStreaming = 1, PixelFormatBgra32 = 372645892;
    [StructLayout(LayoutKind.Explicit, Size = 128)] public struct Event { [FieldOffset(0)] public uint Type; [FieldOffset(28)] public float WheelY; }
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_Init(uint flags);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_Quit();
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr SDL_CreateWindow([MarshalAs(UnmanagedType.LPUTF8Str)] string title, int w, int h, uint flags);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr SDL_CreateRenderer(IntPtr window, IntPtr name);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr SDL_CreateTexture(IntPtr renderer, int format, int access, int w, int h);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_UpdateTexture(IntPtr texture, IntPtr rect, IntPtr pixels, int pitch);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_RenderClear(IntPtr renderer);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_RenderTexture(IntPtr renderer, IntPtr texture, ref SdlFRect source, ref SdlFRect destination);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_RenderPresent(IntPtr renderer);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_PollEvent(out Event e);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern uint SDL_GetMouseState(out float x, out float y);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] public static extern bool SDL_GetWindowSize(IntPtr window, out int width, out int height);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_Delay(uint ms);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_DestroyTexture(IntPtr texture);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_DestroyRenderer(IntPtr renderer);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_DestroyWindow(IntPtr window);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr SDL_GetError();
    public static string Error() => Marshal.PtrToStringUTF8(SDL_GetError()) ?? "unknown SDL3 error";
}
