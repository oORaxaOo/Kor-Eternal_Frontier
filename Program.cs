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
        ScenarioBalance balance;
        try { balance = ScenarioBalance.Load(Path.Combine(AppContext.BaseDirectory, "Data", "Balance", "scenario.json")); }
        catch (InvalidDataException ex) { Console.Error.WriteLine($"KOR-ETERNAL FRONTIER configuration error: {ex.Message}"); return 2; }
        IntPtr window = IntPtr.Zero, rendererHandle = IntPtr.Zero;
        SdlAdapter? adapter = null;
        OutpostRenderer? painter = null;
        Engine? engine = null;
        bool engineStarted = false;
        try
        {
            if (Sdl.SDL_Init(Sdl.InitVideo) < 0) throw new InvalidOperationException(Sdl.Error());
            window = Sdl.SDL_CreateWindow("KOR-ETERNAL FRONTIER", 1100, 720, Sdl.WindowResizable);
            if (window == IntPtr.Zero) throw new InvalidOperationException(Sdl.Error());
            rendererHandle = Sdl.SDL_CreateRenderer(window, IntPtr.Zero);
            if (rendererHandle == IntPtr.Zero) throw new InvalidOperationException(Sdl.Error());

            var simulation = new OutpostSimulation(balance);
            adapter = new SdlAdapter(rendererHandle, 1100, 720);
            painter = new OutpostRenderer();
            using var host = new RenderSurfaceHost<BitmapBackbuffer>(adapter);
            using var scene = new Scene();
            host.Bind(scene, false);
            host.ViewManager.ConfigureSingleFullView();
            host.RedrawDirtyRectangleOnly = false;

            float cameraX = 0, cameraY = 0, zoom = 1;
            bool buildMode = false;
            host.RenderBackbufferPostScene += canvas => painter.Draw(canvas, simulation, cameraX, cameraY, zoom, buildMode);
            engine = Engine.Instance;
            engine.Initialize();
            engine.Configuration.TargetFPS = 60;
            var context = new PumpSynchronizationContext();
            engine.StartTimerDriven(context);
            engineStarted = true;

            bool running = true, wasLeft = false, wasRight = false, wasBuild = false, wasEscape = false;
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
                while (accumulator >= .1) { simulation.Step(.1f); accumulator -= .1; }
                scene.FullRefreshNeeded = true;

                IntPtr keys = Sdl.SDL_GetKeyboardState(out _);
                float panSpeed = 300 * (float)frame;
                if (IsDown(keys, Sdl.ScancodeW)) cameraY = Math.Min(240, cameraY + panSpeed);
                if (IsDown(keys, Sdl.ScancodeS)) cameraY = Math.Max(-240, cameraY - panSpeed);
                if (IsDown(keys, Sdl.ScancodeA)) cameraX = Math.Min(350, cameraX + panSpeed);
                if (IsDown(keys, Sdl.ScancodeD)) cameraX = Math.Max(-350, cameraX - panSpeed);
                if (IsDown(keys, Sdl.ScancodeEquals)) zoom = Math.Min(1.7f, zoom + (float)frame * .65f);
                if (IsDown(keys, Sdl.ScancodeMinus)) zoom = Math.Max(.65f, zoom - (float)frame * .65f);

                bool buildDown = IsDown(keys, Sdl.ScancodeB), escapeDown = IsDown(keys, Sdl.ScancodeEscape);
                if (buildDown && !wasBuild) { buildMode = !buildMode; simulation.SetMessage(buildMode ? "Lilith: Select open ground for a purifier foundation." : "Placement cancelled."); }
                if (escapeDown && !wasEscape && buildMode) { buildMode = false; simulation.SetMessage("Purifier placement cancelled."); }
                wasBuild = buildDown; wasEscape = escapeDown;

                uint buttons = Sdl.SDL_GetMouseState(out float mouseX, out float mouseY);
                var logicalMouse = adapter.AdapterPxToScreenPx(new System.Drawing.PointF(mouseX, mouseY));
                var worldMouse = OrthogonalGridProjection.ScreenToWorld(logicalMouse.X, logicalMouse.Y, cameraX, cameraY, zoom);
                bool left = (buttons & Sdl.ButtonLeftMask) != 0, right = (buttons & Sdl.ButtonRightMask) != 0;
                if (left && !wasLeft)
                {
                    if (buildMode) { simulation.PlacePurifier(worldMouse.X, worldMouse.Y); buildMode = false; }
                    else simulation.SelectUnit(worldMouse.X, worldMouse.Y);
                }
                if (right && !wasRight) simulation.Command(worldMouse.X, worldMouse.Y);
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
            painter?.Dispose(); adapter?.Dispose();
            if (rendererHandle != IntPtr.Zero) Sdl.SDL_DestroyRenderer(rendererHandle);
            if (window != IntPtr.Zero) Sdl.SDL_DestroyWindow(window);
            Sdl.SDL_Quit();
        }
    }

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
    public const int ScancodeA = 4, ScancodeB = 5, ScancodeD = 7, ScancodeS = 22, ScancodeW = 26, ScancodeMinus = 45, ScancodeEquals = 46, ScancodeEscape = 41;
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
