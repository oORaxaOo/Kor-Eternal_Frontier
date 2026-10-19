using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Gondwana;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Scenes;
using KorTheBlight.Campaign;
using KorTheBlight.Rendering;
using KorTheBlight.Simulation;
using KorTheBlight.Tests;
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
        WorldMapRenderer? worldPainter = null;
        MouseMenuRenderer? menuPainter = null;
        ManagementPanelRenderer? managementPainter = null;
        Engine? engine = null;
        bool engineStarted = false;
        try
        {
            if (Sdl.SDL_Init(Sdl.InitVideo) < 0) throw new InvalidOperationException(Sdl.Error());
            window = Sdl.SDL_CreateWindow("KOR-ETERNAL FRONTIER", 1100, 720, Sdl.WindowResizable);
            if (window == IntPtr.Zero) throw new InvalidOperationException(Sdl.Error());
            rendererHandle = Sdl.SDL_CreateRenderer(window, IntPtr.Zero);
            if (rendererHandle == IntPtr.Zero) throw new InvalidOperationException(Sdl.Error());

            var simulation = new ColonySimulation();
            var campaign = new CampaignSimulation(70241);
            var worldMap = new WorldMapSimulation(70241);
            adapter = new SdlAdapter(rendererHandle, 1100, 720);
            painter = new ColonyRenderer();
            worldPainter = new WorldMapRenderer();
            menuPainter = new MouseMenuRenderer();
            managementPainter = new ManagementPanelRenderer();
            using var host = new RenderSurfaceHost<BitmapBackbuffer>(adapter);
            using var scene = new Scene();
            host.Bind(scene, false);
            host.ViewManager.ConfigureSingleFullView();
            host.RedrawDirtyRectangleOnly = false;

            float cameraX = 0, cameraY = 0, zoom = 1;
            ArchitectureKind architectureMode = ArchitectureKind.None;
            PlaceableKind objectType = PlaceableKind.Workbench;
            bool placingObject = false, paused = false, worldMapMode = false;
            int activeMenu = 0;
            bool managementOpen = false;
            int managementPage = 1;
            int catalogBuildingPage = 0;
            int recipeCatalogPage = 0;
            int researchCatalogPage = 0;
            int catalogUnitPage = 0;
            string? selectedCatalogBuildingId = null;
            bool commandMode = false;
            host.RenderBackbufferPostScene += canvas =>
            {
                if (worldMapMode) worldPainter.Draw(canvas, worldMap);
                else painter.Draw(canvas, simulation, cameraX, cameraY, zoom, architectureMode, objectType, placingObject, paused, campaign);
                menuPainter.Draw(canvas, activeMenu, worldMapMode, architectureMode, objectType, placingObject, paused);
                if (managementOpen) managementPainter.Draw(canvas, managementPage, simulation, campaign, worldMap, worldMapMode, catalogBuildingPage, recipeCatalogPage, researchCatalogPage, catalogUnitPage);
            };
            engine = Engine.Instance;
            engine.Initialize();
            engine.Configuration.TargetFPS = 60;
            var context = new PumpSynchronizationContext();
            engine.StartTimerDriven(context);
            engineStarted = true;

            bool running = true, wasLeft = false, wasRight = false, wasEscape = false, wasLeftArrow = false, wasRightArrow = false;
            bool was1 = false, was2 = false, was3 = false, was4 = false, was5 = false;
            bool wasObject = false, wasTab = false, wasSpace = false, wasF5 = false, wasF9 = false;
            bool wasT = false, wasI = false, wasE = false, wasM = false, wasY = false, wasG = false, wasR = false, wasC = false;
            var clock = Stopwatch.StartNew();
            var appLifetime = Stopwatch.StartNew();
            double accumulator = 0;
            while (running)
            {
                while (Sdl.SDL_PollEvent(out var e) != 0)
                {
                    if (e.Type == Sdl.EventQuit || e.Type == Sdl.EventWindowClose) running = false;
                    else if (e.Type == Sdl.EventWindowResized && Sdl.SDL_GetWindowSize(window, out int width, out int height)) adapter.Resize(width, height);
                }

                double frame = Math.Min(clock.Elapsed.TotalSeconds, .1);
                clock.Restart();
                accumulator += frame;
                if (!paused)
                {
                    while (accumulator >= .1) { simulation.Step(.1f); accumulator -= .1; }
                }
                else accumulator = 0;
                scene.FullRefreshNeeded = true;

                IntPtr keys = Sdl.SDL_GetKeyboardState(out _);
                float panSpeed = 300 * (float)frame;
                if (IsDown(keys, Sdl.ScancodeW)) cameraY = Math.Min(240, cameraY + panSpeed);
                if (IsDown(keys, Sdl.ScancodeS)) cameraY = Math.Max(-240, cameraY - panSpeed);
                if (IsDown(keys, Sdl.ScancodeA)) cameraX = Math.Min(350, cameraX + panSpeed);
                if (IsDown(keys, Sdl.ScancodeD)) cameraX = Math.Max(-350, cameraX - panSpeed);
                if (IsDown(keys, Sdl.ScancodeEquals)) zoom = Math.Min(1.7f, zoom + (float)frame * .65f);
                if (IsDown(keys, Sdl.ScancodeMinus)) zoom = Math.Max(.65f, zoom - (float)frame * .65f);

                bool oneDown = IsDown(keys, Sdl.Scancode1);
                bool twoDown = IsDown(keys, Sdl.Scancode2);
                bool threeDown = IsDown(keys, Sdl.Scancode3);
                bool fourDown = IsDown(keys, Sdl.Scancode4);
                bool fiveDown = IsDown(keys, Sdl.Scancode5);
                bool objectDown = IsDown(keys, Sdl.ScancodeO);
                bool tabDown = IsDown(keys, Sdl.ScancodeTab);
                bool spaceDown = IsDown(keys, Sdl.ScancodeSpace);
                bool f5Down = IsDown(keys, Sdl.ScancodeF5);
                bool f9Down = IsDown(keys, Sdl.ScancodeF9);
                bool tDown = IsDown(keys, Sdl.ScancodeT);
                bool iDown = IsDown(keys, Sdl.ScancodeI);
                bool eDown = IsDown(keys, Sdl.ScancodeE);
                bool escapeDown = IsDown(keys, Sdl.ScancodeEscape);
                bool mDown = IsDown(keys, Sdl.ScancodeM);
                bool yDown = IsDown(keys, Sdl.ScancodeY);
                bool gDown = IsDown(keys, Sdl.ScancodeG);
                bool rDown = IsDown(keys, Sdl.ScancodeR);
                bool cDown = IsDown(keys, Sdl.ScancodeC);
                bool leftDown = IsDown(keys, Sdl.ScancodeLeft);
                bool rightDown = IsDown(keys, Sdl.ScancodeRight);

                if (oneDown && !was1) { architectureMode = ArchitectureKind.Wall; placingObject = false; }
                if (twoDown && !was2) { architectureMode = ArchitectureKind.Floor; placingObject = false; }
                if (threeDown && !was3) { architectureMode = ArchitectureKind.Path; placingObject = false; }
                if (fourDown && !was4) { architectureMode = ArchitectureKind.Door; placingObject = false; }
                if (fiveDown && !was5) { architectureMode = ArchitectureKind.Conduit; placingObject = false; }
                if (objectDown && !wasObject) { placingObject = !placingObject; if (placingObject) { architectureMode = ArchitectureKind.None; selectedCatalogBuildingId = null; if (objectType == PlaceableKind.IndustryBuilding) objectType = PlaceableKind.Workbench; } }
                if (tabDown && !wasTab)
                {
                    objectType = NextPlaceable(objectType);
                    selectedCatalogBuildingId = null;
                    placingObject = true;
                    architectureMode = ArchitectureKind.None;
                }
                if (escapeDown && !wasEscape) { architectureMode = ArchitectureKind.None; placingObject = false; }
                if (mDown && !wasM) worldMapMode = !worldMapMode;
                if (worldMapMode && leftDown && !wasLeftArrow) worldMap.SelectNext(-1);
                if (worldMapMode && rightDown && !wasRightArrow) worldMap.SelectNext(1);
                if (worldMapMode && yDown && !wasY) worldMap.OfferNonAggression();
                if (worldMapMode && gDown && !wasG) worldMap.OfferTradePact();
                if (worldMapMode && rDown && !wasR) worldMap.ImproveRelations();
                if (worldMapMode && cDown && !wasC) worldMap.ClaimSelected();
                if (spaceDown && !wasSpace) paused = !paused;
                if (tDown && !wasT)
                {
                    try { campaign.EndTurn(); if (worldMapMode) worldMap.EndTurn(); simulation.SetMessage(campaign.Message); }
                    catch (Exception ex) { simulation.SetMessage($"Campaign turn failed: {ex.Message}"); }
                }
                if (iDown && !wasI)
                {
                    try { int moved = CampaignColonyBridge.ImportToColony(campaign, simulation); if (moved == 0) simulation.SetMessage("Resupply unavailable: colony storage may be full."); }
                    catch (Exception ex) { simulation.SetMessage($"Import failed: {ex.Message}"); }
                }
                if (eDown && !wasE)
                {
                    try { int moved = CampaignColonyBridge.ExportFromColony(campaign, simulation); if (moved == 0) simulation.SetMessage("Export unavailable: no Food or Alloy in colony stores."); }
                    catch (Exception ex) { simulation.SetMessage($"Export failed: {ex.Message}"); }
                }
                if (f5Down && !wasF5)
                {
                    try { GameSessionService.Save(simulation, campaign); simulation.SetMessage("Campaign and colony session saved."); }
                    catch (Exception ex) { simulation.SetMessage($"Session save failed: {ex.Message}"); }
                }
                if (f9Down && !wasF9)
                {
                    try
                    {
                        var loaded = GameSessionService.Load();
                        simulation = loaded.Colony;
                        campaign = loaded.Campaign;
                        simulation.SetMessage("Campaign and colony session loaded.");
                    }
                    catch (Exception ex) { simulation.SetMessage($"Session load failed: {ex.Message}"); }
                }

                wasLeftArrow = leftDown; wasRightArrow = rightDown;
                was1 = oneDown; was2 = twoDown; was3 = threeDown; was4 = fourDown; was5 = fiveDown;
                wasObject = objectDown; wasTab = tabDown; wasSpace = spaceDown;
                wasF5 = f5Down; wasF9 = f9Down; wasEscape = escapeDown;
                wasT = tDown; wasI = iDown; wasE = eDown; wasM = mDown; wasY = yDown; wasG = gDown; wasR = rDown; wasC = cDown;

                uint buttons = Sdl.SDL_GetMouseState(out float mouseX, out float mouseY);
                var logicalMouse = adapter.AdapterPxToScreenPx(new System.Drawing.PointF(mouseX, mouseY));
                var worldMouse = OrthogonalGridProjection.ScreenToWorld(logicalMouse.X, logicalMouse.Y, cameraX, cameraY, zoom);
                bool inWorldView = logicalMouse.X < 770 && logicalMouse.Y >= 62 && logicalMouse.Y < 650;
                bool left = (buttons & Sdl.ButtonLeftMask) != 0, right = (buttons & Sdl.ButtonRightMask) != 0;
                if (left && !wasLeft)
                {
                    bool menuClick = false;
                    if (logicalMouse.Y >= MouseMenuRenderer.Top && logicalMouse.Y <= MouseMenuRenderer.Top + MouseMenuRenderer.ButtonHeight &&
                        logicalMouse.X >= 10 && logicalMouse.X < 790)
                    {
                        int chosen = logicalMouse.X < 110 ? 1 : logicalMouse.X < 218 ? 2 :
                            logicalMouse.X < 326 ? 3 : logicalMouse.X < 458 ? 4 : logicalMouse.X < 562 ? 5 :
                            logicalMouse.X < 670 ? 10 : 11;
                        if (chosen == 11)
                        {
                            managementOpen = !managementOpen;
                            if (managementOpen) managementPage = 1;
                            activeMenu = 0;
                        }
                        else
                        {
                            managementOpen = false;
                            activeMenu = activeMenu == chosen ? 0 : chosen;
                        }
                        menuClick = true;
                    }
                    else if (managementOpen)
                    {
                        if (logicalMouse.Y < 120) { menuClick = true; }
                        else if (logicalMouse.X >= 18 && logicalMouse.X <= 214 && logicalMouse.Y >= 128)
                        {
                            managementPage = Math.Clamp((int)((logicalMouse.Y - 128) / 48) + 1, 1, ManagementPanelRenderer.PageNames.Length);
                        }
                        else
                        {
                            switch (managementPage)
                            {
                                case 3:
                                    if (logicalMouse.X >= 670 && logicalMouse.X <= 1035 && logicalMouse.Y >= 194 && logicalMouse.Y < 229)
                                    {
                                        try { int moved = CampaignColonyBridge.ImportToColony(campaign, simulation); simulation.SetMessage(moved == 0 ? "Import unavailable: storage may be full." : "Imported resources into colony stores."); }
                                        catch (Exception ex) { simulation.SetMessage("Import failed: " + ex.Message); }
                                    }
                                    else if (logicalMouse.X >= 670 && logicalMouse.X <= 1035 && logicalMouse.Y >= 239 && logicalMouse.Y < 274)
                                    {
                                        try { int moved = CampaignColonyBridge.ExportFromColony(campaign, simulation); simulation.SetMessage(moved == 0 ? "Export unavailable: no exportable resources." : "Exported resources to empire stock."); }
                                        catch (Exception ex) { simulation.SetMessage("Export failed: " + ex.Message); }
                                    }
                                    else if (logicalMouse.X >= 670 && logicalMouse.X <= 1035 &&
                                             logicalMouse.Y >= 446 && logicalMouse.Y < 628)
                                    {
                                        int index = (int)((logicalMouse.Y - 446) / 26);
                                        var deposit = simulation.MaterialDeposits.OrderBy(item => item.Id).Take(7).ElementAtOrDefault(index);
                                        if (deposit == null)
                                            simulation.SetMessage("No raw-material site is listed at that position.");
                                        else
                                        {
                                            int currentTarget = simulation.GatherTarget(deposit.MaterialId);
                                            int nextTarget = currentTarget switch { 0 => 10, 10 => 25, 25 => 50, _ => 0 };
                                            simulation.SetGatherTarget(deposit.MaterialId, nextTarget);
                                        }
                                    }
                                    break;
                                case 4:
                                    if (logicalMouse.Y >= 194 && logicalMouse.Y < 366 && logicalMouse.X >= 670 && logicalMouse.X < 1046)
                                    {
                                        int col = Math.Clamp((int)((logicalMouse.X - 670) / 125), 0, 2);
                                        int row = (int)((logicalMouse.Y - 194) / 43);
                                        int action = row * 3 + col;
                                        if (action >= 0 && action < 5)
                                        {
                                            architectureMode = action switch { 0 => ArchitectureKind.Wall, 1 => ArchitectureKind.Floor, 2 => ArchitectureKind.Path, 3 => ArchitectureKind.Door, _ => ArchitectureKind.Conduit };
                                            placingObject = false; selectedCatalogBuildingId = null; managementOpen = false;
                                        }
                                        else if (action >= 5 && action < 12)
                                        {
                                            objectType = action switch
                                            {
                                                5 => PlaceableKind.Workbench, 6 => PlaceableKind.SleepingPod, 7 => PlaceableKind.StorageRack,
                                                8 => PlaceableKind.Generator, 9 => PlaceableKind.Garden, 10 => PlaceableKind.Habitat,
                                                _ => PlaceableKind.Stockpile
                                            };
                                            selectedCatalogBuildingId = null;
                                            placingObject = true; architectureMode = ArchitectureKind.None; managementOpen = false;
                                        }
                                    }
                                    else if (logicalMouse.X >= 670 && logicalMouse.X <= 1035 &&
                                             logicalMouse.Y >= 416 && logicalMouse.Y < 616)
                                    {
                                        int index = (int)((logicalMouse.Y - 416) / 40);
                                        var building = simulation.ManagementBuildings(catalogBuildingPage).ElementAtOrDefault(index);
                                        if (building == null) simulation.SetMessage("No catalogue building is listed at that position.");
                                        else if (!simulation.CanPlaceCatalogBuilding(building.Id, out string buildingReason)) simulation.SetMessage(buildingReason);
                                        else
                                        {
                                            selectedCatalogBuildingId = building.Id;
                                            objectType = PlaceableKind.IndustryBuilding;
                                            placingObject = true; architectureMode = ArchitectureKind.None; managementOpen = false;
                                            simulation.SetMessage($"{building.Name} selected. Click clear ground to place its blueprint.");
                                        }
                                    }
                                    else if (logicalMouse.X >= 670 && logicalMouse.X <= 840 && logicalMouse.Y >= 632 && logicalMouse.Y < 666)
                                        catalogBuildingPage = Math.Max(0, catalogBuildingPage - 1);
                                    else if (logicalMouse.X >= 850 && logicalMouse.X <= 1035 && logicalMouse.Y >= 632 && logicalMouse.Y < 666)
                                        catalogBuildingPage = Math.Min(simulation.ManagementBuildingPageCount - 1, catalogBuildingPage + 1);
                                    break;
                                case 5:
                                    if (logicalMouse.X >= 254 && logicalMouse.X <= 650 &&
                                        logicalMouse.Y >= 350 && logicalMouse.Y < 385)
                                    {
                                        var bench = simulation.Objects.FirstOrDefault(o => o.Kind == PlaceableKind.Workbench && o.IsBuilt);
                                        if (bench == null)
                                            simulation.SetMessage("Build and complete a Workbench before queuing component production.");
                                        else
                                            simulation.QueueComponentProduction(bench.Id);
                                    }
                                    else if (logicalMouse.X >= 670 && logicalMouse.X <= 1035 &&
                                             logicalMouse.Y >= 194 && logicalMouse.Y < 594)
                                    {
                                        int index = (int)((logicalMouse.Y - 194) / 50);
                                        var recipe = simulation.ManagementRecipes(recipeCatalogPage).ElementAtOrDefault(index);
                                        var bench = simulation.Objects.FirstOrDefault(o => o.Kind == PlaceableKind.Workbench && o.IsBuilt);
                                        if (recipe == null)
                                            simulation.SetMessage("No recipe is listed at that position.");
                                        else if (logicalMouse.Y >= 194 + index * 50 + 25 && logicalMouse.Y < 194 + index * 50 + 46)
                                        {
                                            int currentTarget = simulation.ProductionTarget(recipe.Id);
                                            int nextTarget = currentTarget switch { 0 => 5, 5 => 10, 10 => 20, _ => 0 };
                                            simulation.SetProductionTarget(recipe.Id, nextTarget);
                                        }
                                        else if (bench == null)
                                            simulation.SetMessage("Build and complete a Workbench before queuing catalog recipes.");
                                        else
                                            simulation.QueueRecipeProduction(bench.Id, recipe.Id);
                                    }
                                    else if (logicalMouse.X >= 670 && logicalMouse.X <= 840 &&
                                             logicalMouse.Y >= 632 && logicalMouse.Y < 666)
                                        recipeCatalogPage = Math.Max(0, recipeCatalogPage - 1);
                                    else if (logicalMouse.X >= 850 && logicalMouse.X <= 1035 &&
                                             logicalMouse.Y >= 632 && logicalMouse.Y < 666)
                                        recipeCatalogPage = Math.Min(simulation.ManagementRecipePageCount - 1, recipeCatalogPage + 1);
                                    break;
                                case 6:
                                    if (logicalMouse.X >= 254 && logicalMouse.X <= 650 &&
                                        logicalMouse.Y >= 316 && logicalMouse.Y < 351)
                                        simulation.SetMessage(campaign.ResearchWard() ? campaign.Message : "Ward Protocol unavailable: need 30 Knowledge and 20 Energy, or project already complete.");
                                    else if (logicalMouse.X >= 670 && logicalMouse.X <= 1035 &&
                                             logicalMouse.Y >= 194 && logicalMouse.Y < 594)
                                    {
                                        int index = (int)((logicalMouse.Y - 194) / 50);
                                        var project = simulation.ManagementResearch(researchCatalogPage).ElementAtOrDefault(index);
                                        if (project == null)
                                            simulation.SetMessage("No local research project is listed at that position.");
                                        else
                                            simulation.ResearchProject(project.Id, campaign);
                                    }
                                    else if (logicalMouse.X >= 670 && logicalMouse.X <= 840 &&
                                             logicalMouse.Y >= 632 && logicalMouse.Y < 666)
                                        researchCatalogPage = Math.Max(0, researchCatalogPage - 1);
                                    else if (logicalMouse.X >= 850 && logicalMouse.X <= 1035 &&
                                             logicalMouse.Y >= 632 && logicalMouse.Y < 666)
                                        researchCatalogPage = Math.Min(simulation.ManagementResearchPageCount - 1, researchCatalogPage + 1);
                                    break;
                                case 8:
                                    if (logicalMouse.X >= 670 && logicalMouse.X <= 1035 &&
                                        ((logicalMouse.Y >= 274 && logicalMouse.Y < 309) ||
                                         (logicalMouse.Y >= 319 && logicalMouse.Y < 354) ||
                                         (logicalMouse.Y >= 364 && logicalMouse.Y < 399)))
                                    {
                                        worldMapMode = true;
                                        if (logicalMouse.Y < 309) worldMap.ImproveRelations();
                                        else if (logicalMouse.Y < 354) worldMap.OfferNonAggression();
                                        else worldMap.OfferTradePact();
                                    }
                                    break;
                                case 9:
                                    if (logicalMouse.X >= 670 && logicalMouse.X <= 1035 && logicalMouse.Y >= 274 && logicalMouse.Y < 309) worldMap.ClaimSelected();
                                    else if (logicalMouse.X >= 670 && logicalMouse.X <= 1035 && logicalMouse.Y >= 319 && logicalMouse.Y < 354)
                                    {
                                        try { campaign.EndTurn(); worldMap.EndTurn(); simulation.SetMessage(campaign.Message); }
                                        catch (Exception ex) { simulation.SetMessage("Strategic turn failed: " + ex.Message); }
                                    }
                                    break;
                                case 10:
                                    if (logicalMouse.X >= 670 && logicalMouse.X <= 1035 &&
                                        logicalMouse.Y >= 444 && logicalMouse.Y < 624)
                                    {
                                        int index = (int)((logicalMouse.Y - 444) / 36);
                                        var unit = CampaignColonyBridge.ManagementUnits(catalogUnitPage).ElementAtOrDefault(index);
                                        if (unit == null)
                                            simulation.SetMessage("No catalogue unit is listed at that position.");
                                        else
                                            CampaignColonyBridge.RecruitUnit(campaign, simulation, unit.Id);
                                    }
                                    else if (logicalMouse.X >= 670 && logicalMouse.X <= 840 &&
                                             logicalMouse.Y >= 632 && logicalMouse.Y < 666)
                                        catalogUnitPage = Math.Max(0, catalogUnitPage - 1);
                                    else if (logicalMouse.X >= 850 && logicalMouse.X <= 1035 &&
                                             logicalMouse.Y >= 632 && logicalMouse.Y < 666)
                                        catalogUnitPage = Math.Min(CampaignColonyBridge.UnitPageCount - 1, catalogUnitPage + 1);
                                    break;
                            }
                        }
                        menuClick = true;
                    }
                    else if (activeMenu != 0 && logicalMouse.Y >= MouseMenuRenderer.MenuTop)
                    {
                        string[] items = MouseMenuRenderer.ItemsFor(activeMenu);
                        float menuX = MouseMenuRenderer.MenuX(activeMenu);
                        if (logicalMouse.X >= menuX && logicalMouse.X <= menuX + MouseMenuRenderer.MenuWidth &&
                            logicalMouse.Y < MouseMenuRenderer.MenuTop + items.Length * MouseMenuRenderer.RowHeight + 10)
                        {
                            int index = (int)((logicalMouse.Y - MouseMenuRenderer.MenuTop - 5) / MouseMenuRenderer.RowHeight);
                            if (index >= 0 && index < items.Length)
                            {
                                switch (activeMenu)
                                {
                                    case 1:
                                        if (index == 0) worldMapMode = !worldMapMode;
                                        else if (index == 1) paused = !paused;
                                        else if (index == 2) activeMenu = 8;
                                        break;
                                    case 2:
                                        if (index == 0) activeMenu = 6;
                                        else if (index == 1) activeMenu = 7;
                                        else { architectureMode = ArchitectureKind.None; placingObject = false; activeMenu = 0; }
                                        break;
                                    case 3:
                                        if (index == 0) { try { campaign.EndTurn(); if (worldMapMode) worldMap.EndTurn(); simulation.SetMessage(campaign.Message); } catch (Exception ex) { simulation.SetMessage($"Turn failed: {ex.Message}"); } }
                                        else if (index == 1) activeMenu = 9;
                                        else if (index == 2 && worldMapMode) worldMap.ClaimSelected();
                                        else if (index == 2) simulation.SetMessage("Open Colony → Strategic World Map before claiming frontier territory.");
                                        break;
                                    case 4:
                                        if (worldMapMode) { if (index == 0) worldMap.ImproveRelations(); else if (index == 1) worldMap.OfferNonAggression(); else if (index == 2) worldMap.OfferTradePact(); }
                                        else simulation.SetMessage("Diplomacy actions are available from the Strategic World Map.");
                                        break;
                                    case 5:
                                        if (index == 0) { try { GameSessionService.Save(simulation, campaign); simulation.SetMessage("Campaign and colony session saved."); } catch (Exception ex) { simulation.SetMessage($"Save failed: {ex.Message}"); } }
                                        else if (index == 1) { try { var loaded = GameSessionService.Load(); simulation = loaded.Colony; campaign = loaded.Campaign; simulation.SetMessage("Campaign and colony session loaded."); } catch (Exception ex) { simulation.SetMessage($"Load failed: {ex.Message}"); } }
                                        else if (index == 2) activeMenu = 0;
                                        break;
                                    case 6:
                                        if (index < 5) { architectureMode = index switch { 0 => ArchitectureKind.Wall, 1 => ArchitectureKind.Floor, 2 => ArchitectureKind.Path, 3 => ArchitectureKind.Door, _ => ArchitectureKind.Conduit }; placingObject = false; activeMenu = 0; }
                                        else activeMenu = 2;
                                        break;
                                    case 7:
                                        if (index < 7) { objectType = index switch { 0 => PlaceableKind.Workbench, 1 => PlaceableKind.SleepingPod, 2 => PlaceableKind.StorageRack, 3 => PlaceableKind.Generator, 4 => PlaceableKind.Garden, 5 => PlaceableKind.Habitat, _ => PlaceableKind.Stockpile }; placingObject = true; architectureMode = ArchitectureKind.None; activeMenu = 0; }
                                        else activeMenu = 2;
                                        break;
                                    case 8:
                                        if (index == 0) cameraX = Math.Min(350, cameraX + 80);
                                        else if (index == 1) cameraX = Math.Max(-350, cameraX - 80);
                                        else if (index == 2) cameraY = Math.Min(240, cameraY + 80);
                                        else if (index == 3) cameraY = Math.Max(-240, cameraY - 80);
                                        else if (index == 4) zoom = Math.Min(1.7f, zoom + .15f);
                                        else if (index == 5) zoom = Math.Max(.65f, zoom - .15f);
                                        else activeMenu = 1;
                                        break;
                                    case 9:
                                        if (index == 0) { try { int moved = CampaignColonyBridge.ImportToColony(campaign, simulation); if (moved == 0) simulation.SetMessage("Import unavailable: storage may be full."); } catch (Exception ex) { simulation.SetMessage($"Import failed: {ex.Message}"); } }
                                        else if (index == 1) { try { int moved = CampaignColonyBridge.ExportFromColony(campaign, simulation); if (moved == 0) simulation.SetMessage("Export unavailable: no Food or Alloy."); } catch (Exception ex) { simulation.SetMessage($"Export failed: {ex.Message}"); } }
                                        else activeMenu = 3;
                                        break;
                                    case 10:
                                        if (index == 0) { commandMode = true; activeMenu = 0; simulation.SetMessage("Order mode active: left-click a destination or resource deposit."); }
                                        else { commandMode = false; activeMenu = 0; simulation.SetMessage("Order mode cancelled."); }
                                        break;
                                }
                                menuClick = true;
                            }
                        }
                        else { activeMenu = 0; menuClick = true; }
                    }
                    if (!menuClick && activeMenu == 0)
                    {
                        if (worldMapMode && logicalMouse.X >= 24 && logicalMouse.X < 24 + WorldMapSimulation.Width * 29 &&
                            logicalMouse.Y >= 82 && logicalMouse.Y < 82 + WorldMapSimulation.Height * 29)
                        {
                            int tx = (int)((logicalMouse.X - 24) / 29), ty = (int)((logicalMouse.Y - 82) / 29);
                            worldMap.SelectAt(tx, ty);
                        }
                        else if (!worldMapMode && inWorldView)
                        {
                            int tileX = (int)MathF.Floor(worldMouse.X), tileY = (int)MathF.Floor(worldMouse.Y);
                            if (commandMode) { simulation.CommandSelectedTo(worldMouse.X, worldMouse.Y); commandMode = false; }
                            else if (placingObject)
                             {
                                 if (objectType == PlaceableKind.IndustryBuilding && selectedCatalogBuildingId != null)
                                     simulation.PlaceCatalogBuildingBlueprint(selectedCatalogBuildingId, tileX, tileY);
                                 else simulation.PlaceObjectBlueprint(objectType, tileX, tileY);
                             }
                            else if (architectureMode != ArchitectureKind.None) simulation.DesignTileConstruction(tileX, tileY, architectureMode);
                            else simulation.SelectWorkerAt(worldMouse.X, worldMouse.Y);
                        }
                    }
                }
                if (!worldMapMode && activeMenu == 0 && inWorldView && right && !wasRight)
                {
                    simulation.CommandSelectedTo(worldMouse.X, worldMouse.Y);
                    commandMode = false;
                }
                wasLeft = left; wasRight = right;

                engine.Tick();
                context.Drain();
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
            painter?.Dispose(); worldPainter?.Dispose(); menuPainter?.Dispose(); managementPainter?.Dispose(); adapter?.Dispose();
            if (rendererHandle != IntPtr.Zero) Sdl.SDL_DestroyRenderer(rendererHandle);
            if (window != IntPtr.Zero) Sdl.SDL_DestroyWindow(window);
            Sdl.SDL_Quit();
        }
    }

    private static PlaceableKind NextPlaceable(PlaceableKind current) => current switch
    {
        PlaceableKind.Workbench => PlaceableKind.SleepingPod,
        PlaceableKind.SleepingPod => PlaceableKind.StorageRack,
        PlaceableKind.StorageRack => PlaceableKind.Generator,
        PlaceableKind.Generator => PlaceableKind.Garden,
        _ => PlaceableKind.Workbench
    };

    private static bool IsDown(IntPtr state, int scancode) => state != IntPtr.Zero && Marshal.ReadByte(state, scancode) != 0;
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
    public const uint InitVideo = 0x20, WindowResizable = 0x20, EventQuit = 0x100, EventWindowClose = 0x210, EventWindowResized = 0x206;
    public const uint ButtonLeftMask = 1, ButtonRightMask = 4;
    public const int TextureAccessStreaming = 1, PixelFormatBgra32 = 372645892;
    public const int ScancodeA = 4, ScancodeC = 6, ScancodeD = 7, ScancodeE = 8, ScancodeG = 10, ScancodeI = 12, ScancodeM = 16, ScancodeO = 18, ScancodeR = 21, ScancodeS = 22, ScancodeT = 23, ScancodeW = 26, ScancodeY = 28;
    public const int Scancode1 = 30, Scancode2 = 31, Scancode3 = 32, Scancode4 = 33, Scancode5 = 34;
    public const int ScancodeTab = 43, ScancodeSpace = 44, ScancodeMinus = 45, ScancodeEquals = 46, ScancodeEscape = 41;
    public const int ScancodeF5 = 62, ScancodeF9 = 66, ScancodeRight = 79, ScancodeLeft = 80;
    [StructLayout(LayoutKind.Explicit, Size = 128)] public struct Event { [FieldOffset(0)] public uint Type; }
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
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr SDL_GetKeyboardState(out int count);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] public static extern bool SDL_GetWindowSize(IntPtr window, out int width, out int height);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_Delay(uint ms);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_DestroyTexture(IntPtr texture);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_DestroyRenderer(IntPtr renderer);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_DestroyWindow(IntPtr window);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr SDL_GetError();
    public static string Error() => Marshal.PtrToStringUTF8(SDL_GetError()) ?? "unknown SDL3 error";
}
