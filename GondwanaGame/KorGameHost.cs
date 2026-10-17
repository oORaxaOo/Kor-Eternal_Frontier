using System.Drawing;
using System.Numerics;
using Avalonia.Input;
using Gondwana.Avalonia.Hosting;
using Gondwana.Avalonia.Rendering;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Input.Keyboard;
using Gondwana.Input.Mouse;
using Gondwana.Scenes;
using Gondwana.Timers;
using KorFrontier.Gondwana.Rendering;
using KorFrontier.Gondwana.Simulation;
using SkiaSharp;
using SpriteHorizontalAlignment = Gondwana.Drawing.Sprites.HorizontalAlignment;
using SpriteVerticalAlignment = Gondwana.Drawing.Sprites.VerticalAlignment;

namespace KorFrontier.Gondwana;

internal sealed class KorGameHost : AvaloniaGpuGameHost
{
    private const int MapWidth = KorWorld.Width;
    private const int MapHeight = KorWorld.Height;
    private readonly KorWorld _world = new();
    private Tilesheet _atlas = null!;
    private SceneLayer _ground = null!;
    private SceneLayer _actors = null!;
    private readonly Dictionary<int, Sprite> _unitSprites = [];
    private readonly Dictionary<int, Sprite> _buildingSprites = [];
    private readonly Dictionary<int, int> _lastGroundFrames = [];
    private long _lastTick;
    private bool _paused;
    private int _selectionFrame;

    internal KorWorld World => _world;
    internal KorBuildKind? BuildMode { get; private set; }

    internal KorGameHost(AvaloniaGpuRenderSurfaceControl surface) : base(surface) { }

    protected override void LoadTilesheets()
    {
        _atlas = Engine.Managers.Tilesheets.LoadFromBitmap("kor-original-crimson-atlas", KorArt.CreateAtlas());
        _atlas.DefaultRegion.TileSize = new Size(KorArt.FrameSize, KorArt.FrameSize);
    }

    protected override Scene CreateInitialScene()
    {
        var scene = new Scene();
        _ground = scene.AddLayer(MapWidth, MapHeight, 64, 32, 0, 1f, CoordinateSystemTypes.IsometricRhombic);
        _actors = scene.AddLayer(MapWidth, MapHeight, 64, 32, 10, 1f, CoordinateSystemTypes.IsometricRhombic);
        for (int y = 0; y < MapHeight; y++)
        for (int x = 0; x < MapWidth; x++)
        {
            var tile = _ground[x, y]!;
            tile.CurrentFrame = _atlas[GroundFrame(_world.GroundAt(x, y)), 0];
            _lastGroundFrames[y * MapWidth + x] = GroundFrame(_world.GroundAt(x, y));
        }
        return scene;
    }

    protected override void OnSceneBound()
    {
        RenderSurface.Host.Backbuffer!.ClearColor = new SKColor(12, 17, 22);
        var view = RenderSurface.Host.ViewManager.Views[0];
        view.Camera.WorldBoundsPx = Scene!.GetWorldBoundsPx();
        view.Camera.CenterOnGrid(_ground, 20, 20);
    }

    protected override void CreateSprites()
    {
        foreach (var unit in _world.Units)
        {
            var frame = unit.Kind == KorUnitKind.Pioneer ? KorArt.Pioneer : KorArt.Warden;
            var sprite = Engine.Managers.Sprites.CreateSprite(_actors, _atlas[frame, 0], $"kor-unit-{unit.Id}");
            sprite.RenderSize = new Size(48, 48);
            sprite.HorizAlign = SpriteHorizontalAlignment.Center;
            sprite.VertAlign = SpriteVerticalAlignment.Middle;
            sprite.ZOrder = 40;
            sprite.Visible = true;
            sprite.SetPosition(new Vector2(unit.X, unit.Y));
            _unitSprites[unit.Id] = sprite;
        }

        foreach (var building in _world.Buildings)
            AddBuildingSprite(building);
    }

    protected override void OnKeyboardAdapterInitialized()
    {
        var keyboard = Engine.Input.KeyboardEventPoller!;
        keyboard.KeyDown += OnKeyDown;
        foreach (var key in new[] { Key.W, Key.A, Key.S, Key.D, Key.Up, Key.Down, Key.Left, Key.Right, Key.Escape, Key.Space, Key.B, Key.Home })
            keyboard.StartMonitoringKey((int)key, key.ToString());
    }

    protected override void OnMouseAdapterInitialized()
    {
        var mouse = Engine.Input.MouseEventPoller!;
        mouse.MouseEvent += OnMouse;
        mouse.StartMonitoringMouse();
    }

    protected override void OnEngineInitialized()
    {
        Engine.Configuration.TargetFPS = 60;
        _lastTick = HighResTimer.GetCurrentTick();
        Engine.BeforeBackgroundTasksExecute += UpdateWorld;
    }

    protected override void UnhookEvents()
    {
        if (Engine.Input.KeyboardEventPoller is not null)
            Engine.Input.KeyboardEventPoller.KeyDown -= OnKeyDown;
        if (Engine.Input.MouseEventPoller is not null)
            Engine.Input.MouseEventPoller.MouseEvent -= OnMouse;
        Engine.BeforeBackgroundTasksExecute -= UpdateWorld;
    }

    internal void SetBuildMode(KorBuildKind kind)
    {
        BuildMode = kind;
        _world.Status = $"Build mode: {kind}. Right-click a clear tile to place the construction site.";
    }

    internal void CancelBuildMode()
    {
        BuildMode = null;
        _world.Status = "Command ready · select Kor, build, and grow the frontier.";
    }

    internal void CenterOnCore()
    {
        var view = RenderSurface.Host.ViewManager.Views[0];
        view.Camera.AnimateCenterOnGrid(_ground, 20, 20, 8f);
    }

    private void OnKeyDown(KeyDownEventArgs args)
    {
        if (args.KeyAction != KeyAction.Pressed && args.KeyAction != KeyAction.Repeated)
            return;
        if (!Enum.TryParse(args.KeyConfig.Key, true, out Key key))
            return;

        var camera = RenderSurface.Host.ViewManager.Views[0].Camera;
        var position = camera.PositionPx;
        const float pan = 92f;
        switch (key)
        {
            case Key.W:
            case Key.Up: camera.PanTo(new PointF(position.X, position.Y - pan), 8f); break;
            case Key.S:
            case Key.Down: camera.PanTo(new PointF(position.X, position.Y + pan), 8f); break;
            case Key.A:
            case Key.Left: camera.PanTo(new PointF(position.X - pan, position.Y), 8f); break;
            case Key.D:
            case Key.Right: camera.PanTo(new PointF(position.X + pan, position.Y), 8f); break;
            case Key.Escape: CancelBuildMode(); break;
            case Key.Space: _paused = !_paused; _world.Status = _paused ? "Simulation paused." : "Simulation resumed."; break;
            case Key.B: SetBuildMode(KorBuildKind.Solar); break;
            case Key.Home: CenterOnCore(); break;
        }
    }

    private void OnMouse(global::Gondwana.Input.Mouse.MouseEventArgs args)
    {
        var view = RenderSurface.Host.ViewManager.Views[0];
        if (args.ScrollDelta != 0)
        {
            float target = Math.Clamp(view.Viewport.Zoom + (args.ScrollDelta > 0 ? 0.12f : -0.12f), 0.55f, 1.5f);
            view.ZoomAroundScreenPoint(_ground, new PointF(args.CurrentPosition.X, args.CurrentPosition.Y), target, 0.18f);
        }

        if (!args.LeftButtonJustPressed && !args.RightButtonJustPressed)
            return;

        var grid = view.ScreenPxToGrid(_ground, new PointF(args.CurrentPosition.X, args.CurrentPosition.Y));
        int x = (int)MathF.Round(grid.X);
        int y = (int)MathF.Round(grid.Y);
        if (x < 0 || y < 0 || x >= MapWidth || y >= MapHeight)
            return;

        if (args.LeftButtonJustPressed)
        {
            _world.SelectAt(grid.X, grid.Y, args.IsShift);
            return;
        }

        if (BuildMode is { } build)
        {
            if (_world.TryBuild(build, x, y))
            {
                var newBuilding = _world.Buildings.OrderByDescending(b => b.Id).First();
                AddBuildingSprite(newBuilding);
                BuildMode = null;
            }
        }
        else
        {
            _world.OrderSelected(x, y);
        }
    }

    private void UpdateWorld()
    {
        long tick = HighResTimer.GetCurrentTick();
        float dt = Math.Clamp(HighResTimer.GetDuration(_lastTick, tick), 0f, 0.05f);
        _lastTick = tick;
        if (dt <= 0f || _paused) return;

        _world.Update(dt);
        UpdateGround();
        UpdateSprites();
    }

    private void UpdateGround()
    {
        for (int y = 0; y < MapHeight; y++)
        for (int x = 0; x < MapWidth; x++)
        {
            int index = y * MapWidth + x;
            int frame = GroundFrame(_world.GroundAt(x, y));
            if (_lastGroundFrames[index] == frame) continue;
            _ground[x, y]!.CurrentFrame = _atlas[frame, 0];
            _lastGroundFrames[index] = frame;
        }
    }

    private void UpdateSprites()
    {
        foreach (var unit in _world.Units)
        {
            if (!_unitSprites.TryGetValue(unit.Id, out var sprite)) continue;
            sprite.SetPosition(new Vector2(unit.X, unit.Y));
            int frame = unit.Selected
                ? (unit.Kind == KorUnitKind.Pioneer ? KorArt.PioneerSelected : KorArt.WardenSelected)
                : (unit.Kind == KorUnitKind.Pioneer ? KorArt.Pioneer : KorArt.Warden);
            sprite.CurrentFrame = _atlas[frame, 0];
            sprite.ZOrder = 40 + (int)((unit.X + unit.Y) * 10);
        }
        foreach (var building in _world.Buildings)
        {
            if (!_buildingSprites.TryGetValue(building.Id, out var sprite)) continue;
            sprite.SetPosition(new Vector2(building.X, building.Y));
            sprite.Visible = true;
        }
    }

    private void AddBuildingSprite(KorBuilding building)
    {
        int frame = building.Kind switch
        {
            KorBuildKind.Solar => KorArt.Solar,
            KorBuildKind.Extractor => KorArt.Extractor,
            KorBuildKind.Archive => KorArt.Archive,
            _ => KorArt.Relay
        };
        var sprite = Engine.Managers.Sprites.CreateSprite(_actors, _atlas[frame, 0], $"kor-building-{building.Id}");
        sprite.RenderSize = new Size(58, 58);
        sprite.HorizAlign = SpriteHorizontalAlignment.Center;
        sprite.VertAlign = SpriteVerticalAlignment.Middle;
        sprite.ZOrder = 20;
        sprite.Visible = true;
        sprite.SetPosition(new Vector2(building.X, building.Y));
        _buildingSprites[building.Id] = sprite;
    }

    private static int GroundFrame(GroundKind ground) => ground switch
    {
        GroundKind.Soil => KorArt.Soil,
        GroundKind.Crystal => KorArt.Crystal,
        GroundKind.Blight => KorArt.Blight,
        _ => KorArt.Meadow
    };
}
