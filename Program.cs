using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Gondwana;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Scenes;
using KorTheBlight.Rendering;
using KorTheBlight.Simulation;
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
        bool engineStarted = false;
        try
        {
            if (Sdl.SDL_Init(Sdl.InitVideo) < 0) throw new InvalidOperationException(Sdl.Error());
            window = Sdl.SDL_CreateWindow("KOR-ETERNAL FRONTIER", 1100, 720, Sdl.WindowResizable);
            if (window == IntPtr.Zero) throw new InvalidOperationException(Sdl.Error());
            rendererHandle = Sdl.SDL_CreateRenderer(window, IntPtr.Zero);
            if (rendererHandle == IntPtr.Zero) throw new InvalidOperationException(Sdl.Error());

            var world = new WorldSimulation(70241);
            var simulation = world.Colony;
            bool gameStarted = false, settingsMenu = false, startPaused = false;
            string menuMessage = string.Empty;
            adapter = new SdlAdapter(rendererHandle, 1100, 720);
            painter = new ColonyRenderer();
            menuPainter = new MainMenuRenderer();
            using var host = new RenderSurfaceHost<BitmapBackbuffer>(adapter);
            using var scene = new Scene();
            host.Bind(scene, false);
            host.ViewManager.ConfigureSingleFullView();
            host.RedrawDirtyRectangleOnly = false;

            float cameraX = 0, cameraY = 0, zoom = 1;
            float worldCameraX = (float)(ContinuousWorldState.WorldExtent / 2), worldCameraY = (float)(ContinuousWorldState.WorldExtent / 2);
            long? selectedWorldEntityId = null;
            WorldResourceKind? selectedShipmentResource = null;
            ArchitectureKind architectureMode = ArchitectureKind.None;
            PlaceableKind objectType = PlaceableKind.Workbench;
            bool placingObject = false, paused = false;
            host.RenderBackbufferPostScene += canvas =>
            {
                if (gameStarted) painter.Draw(canvas, simulation, cameraX, cameraY, zoom, architectureMode, objectType, placingObject, paused, world.SpeedMultiplier, world.SpatialWorld, worldCameraX, worldCameraY, selectedWorldEntityId, selectedShipmentResource);
                else menuPainter.Draw(canvas, settingsMenu, startPaused, menuMessage);
            };
            engine = Engine.Instance;
            engine.Initialize();
            engine.Configuration.TargetFPS = 60;
            var context = new PumpSynchronizationContext();
            engine.StartTimerDriven(context);
            engineStarted = true;

            bool running = true, wasLeft = false, wasRight = false, wasMiddle = false;
            float previousMouseX = 0, previousMouseY = 0;
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
                if (gameStarted && !paused)
                {
                    while (accumulator >= .1) { world.Step(.1f); simulation = world.Colony; accumulator -= .1; }
                }
                else accumulator = 0;
                scene.FullRefreshNeeded = true;

                uint buttons = Sdl.SDL_GetMouseState(out float mouseX, out float mouseY);
                var logicalMouse = adapter.AdapterPxToScreenPx(new System.Drawing.PointF(mouseX, mouseY));
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
                            world = new WorldSimulation(70241);
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
                        if (placingObject) simulation.PlaceObjectBlueprint(objectType, tileX, tileY);
                        else if (architectureMode != ArchitectureKind.None) simulation.DesignTileConstruction(tileX, tileY, architectureMode);
                        else simulation.SelectWorkerAt(worldMouse.X, worldMouse.Y);
                    }
                    else if (logicalMouse.X >= 770 && logicalMouse.Y >= 493 && logicalMouse.Y < 650)
                    {
                        float mx = logicalMouse.X, my = logicalMouse.Y;
                        if (Hit(mx, my, 784, 501, 850, 523))
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
                        else if (worldView && Hit(mx, my, 784, 566, 838, 588)) { selectedShipmentResource = WorldResourceKind.Wood; simulation.SetMessage("Cargo selected: Wood. Select a caravan, then click a destination settlement."); }
                        else if (worldView && Hit(mx, my, 842, 566, 896, 588)) { selectedShipmentResource = WorldResourceKind.Stone; simulation.SetMessage("Cargo selected: Stone. Select a caravan, then click a destination settlement."); }
                        else if (worldView && Hit(mx, my, 900, 566, 954, 588)) { selectedShipmentResource = WorldResourceKind.Food; simulation.SetMessage("Cargo selected: Food. Select a caravan, then click a destination settlement."); }
                        else if (worldView && Hit(mx, my, 958, 566, 1012, 588)) { selectedShipmentResource = WorldResourceKind.Alloy; simulation.SetMessage("Cargo selected: Alloy. Select a caravan, then click a destination settlement."); }
                        else if (worldView && Hit(mx, my, 1016, 566, 1070, 588)) { selectedShipmentResource = null; simulation.SetMessage("Shipment selection cleared."); }
                        else if (worldView && Hit(mx, my, 784, 593, 1086, 615)) { selectedShipmentResource = null; simulation.SetMessage("Shipment selection cleared."); }
                        else if (!worldView && Hit(mx, my, 784, 566, 838, 588)) { architectureMode = ArchitectureKind.Wall; placingObject = false; }
                        else if (!worldView && Hit(mx, my, 842, 566, 896, 588)) { architectureMode = ArchitectureKind.Floor; placingObject = false; }
                        else if (!worldView && Hit(mx, my, 900, 566, 954, 588)) { architectureMode = ArchitectureKind.Path; placingObject = false; }
                        else if (!worldView && Hit(mx, my, 958, 566, 1012, 588)) { architectureMode = ArchitectureKind.Door; placingObject = false; }
                        else if (!worldView && Hit(mx, my, 1016, 566, 1070, 588)) { architectureMode = ArchitectureKind.Conduit; placingObject = false; }
                        else if (!worldView && Hit(mx, my, 784, 593, 831, 615)) { objectType = PlaceableKind.Workbench; placingObject = true; architectureMode = ArchitectureKind.None; }
                        else if (!worldView && Hit(mx, my, 835, 593, 882, 615)) { objectType = PlaceableKind.SleepingPod; placingObject = true; architectureMode = ArchitectureKind.None; }
                        else if (!worldView && Hit(mx, my, 886, 593, 933, 615)) { objectType = PlaceableKind.StorageRack; placingObject = true; architectureMode = ArchitectureKind.None; }
                        else if (!worldView && Hit(mx, my, 937, 593, 984, 615)) { objectType = PlaceableKind.Generator; placingObject = true; architectureMode = ArchitectureKind.None; }
                        else if (!worldView && Hit(mx, my, 988, 593, 1035, 615)) { objectType = PlaceableKind.Garden; placingObject = true; architectureMode = ArchitectureKind.None; }
                        else if (!worldView && Hit(mx, my, 1039, 593, 1086, 615))
                        {
                            architectureMode = ArchitectureKind.None;
                            placingObject = false;
                            simulation.SetMessage("Placement cancelled.");
                        }
                    }
                }
                if (gameStarted && inWorldView && right && !wasRight)
                {
                    if (worldView && selectedWorldEntityId.HasValue && !world.SpatialWorld.Shipments.Any(shipment => shipment.CarrierEntityId == selectedWorldEntityId.Value && shipment.Status == WorldShipmentStatus.InTransit) && world.SpatialWorld.Entities.Any(entity => entity.Id == selectedWorldEntityId.Value && entity.Kind is WorldEntityKind.Army or WorldEntityKind.Caravan))
                    {
                        world.SpatialWorld.OrderMove(selectedWorldEntityId.Value, worldMouse.X, worldMouse.Y, 240d);
                        simulation.SetMessage("World movement order issued.");
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
            Sdl.SDL_Quit();
        }
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
    public const uint InitVideo = 0x20, WindowResizable = 0x20, EventQuit = 0x100, EventWindowClose = 0x210, EventWindowResized = 0x206;
    public const uint ButtonLeftMask = 1, ButtonMiddleMask = 2, ButtonRightMask = 4;
    public const int TextureAccessStreaming = 1, PixelFormatBgra32 = 372645892;
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
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] public static extern bool SDL_GetWindowSize(IntPtr window, out int width, out int height);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_Delay(uint ms);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_DestroyTexture(IntPtr texture);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_DestroyRenderer(IntPtr renderer);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_DestroyWindow(IntPtr window);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr SDL_GetError();
    public static string Error() => Marshal.PtrToStringUTF8(SDL_GetError()) ?? "unknown SDL3 error";
}
