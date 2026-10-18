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
            using var host = new RenderSurfaceHost<BitmapBackbuffer>(adapter);
            using var scene = new Scene();
            host.Bind(scene, false);
            host.ViewManager.ConfigureSingleFullView();
            host.RedrawDirtyRectangleOnly = false;

            float cameraX = 0, cameraY = 0, zoom = 1;
            ArchitectureKind architectureMode = ArchitectureKind.None;
            PlaceableKind objectType = PlaceableKind.Workbench;
            bool placingObject = false, paused = false, worldMapMode = false;
            host.RenderBackbufferPostScene += canvas =>
            {
                if (worldMapMode) worldPainter.Draw(canvas, worldMap);
                else painter.Draw(canvas, simulation, cameraX, cameraY, zoom, architectureMode, objectType, placingObject, paused, campaign);
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
            bool wasT = false, wasI = false, wasE = false, wasM = false, wasY = false, wasG = false, wasR = false;
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
                bool leftDown = IsDown(keys, Sdl.ScancodeLeft);
                bool rightDown = IsDown(keys, Sdl.ScancodeRight);

                if (oneDown && !was1) { architectureMode = ArchitectureKind.Wall; placingObject = false; }
                if (twoDown && !was2) { architectureMode = ArchitectureKind.Floor; placingObject = false; }
                if (threeDown && !was3) { architectureMode = ArchitectureKind.Path; placingObject = false; }
                if (fourDown && !was4) { architectureMode = ArchitectureKind.Door; placingObject = false; }
                if (fiveDown && !was5) { architectureMode = ArchitectureKind.Conduit; placingObject = false; }
                if (objectDown && !wasObject) { placingObject = !placingObject; if (placingObject) architectureMode = ArchitectureKind.None; }
                if (tabDown && !wasTab)
                {
                    objectType = NextPlaceable(objectType);
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
                wasT = tDown; wasI = iDown; wasE = eDown; wasM = mDown; wasY = yDown; wasG = gDown; wasR = rDown;

                uint buttons = Sdl.SDL_GetMouseState(out float mouseX, out float mouseY);
                var logicalMouse = adapter.AdapterPxToScreenPx(new System.Drawing.PointF(mouseX, mouseY));
                var worldMouse = OrthogonalGridProjection.ScreenToWorld(logicalMouse.X, logicalMouse.Y, cameraX, cameraY, zoom);
                bool inWorldView = logicalMouse.X < 770 && logicalMouse.Y >= 62 && logicalMouse.Y < 650;
                bool left = (buttons & Sdl.ButtonLeftMask) != 0, right = (buttons & Sdl.ButtonRightMask) != 0;
                if (worldMapMode && left && !wasLeft && logicalMouse.X >= 24 && logicalMouse.X < 24 + WorldMapSimulation.Width * 29 &&
                    logicalMouse.Y >= 82 && logicalMouse.Y < 82 + WorldMapSimulation.Height * 29)
                {
                    int tx = (int)((logicalMouse.X - 24) / 29), ty = (int)((logicalMouse.Y - 82) / 29);
                    worldMap.SelectAt(tx, ty);
                }
                else if (!worldMapMode && inWorldView && left && !wasLeft)
                {
                    int tileX = (int)MathF.Floor(worldMouse.X), tileY = (int)MathF.Floor(worldMouse.Y);
                    if (placingObject) simulation.PlaceObjectBlueprint(objectType, tileX, tileY);
                    else if (architectureMode != ArchitectureKind.None) simulation.DesignTileConstruction(tileX, tileY, architectureMode);
                    else simulation.SelectWorkerAt(worldMouse.X, worldMouse.Y);
                }
                if (!worldMapMode && inWorldView && right && !wasRight) simulation.CommandSelectedTo(worldMouse.X, worldMouse.Y);
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
            painter?.Dispose(); worldPainter?.Dispose(); adapter?.Dispose();
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
    public const int ScancodeA = 4, ScancodeD = 7, ScancodeE = 8, ScancodeG = 10, ScancodeI = 12, ScancodeM = 16, ScancodeO = 18, ScancodeR = 21, ScancodeS = 22, ScancodeT = 23, ScancodeW = 26, ScancodeY = 28;
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
