# KOR-ETERNAL FRONTIER

KOR-ETERNAL FRONTIER is a single-player settlement RTS foundation built with Gondwana, SDL3, and C#/.NET 8. This standalone project is separate from the preserved Unity prototype.

## Current status

The current playable scenario includes an isometric map, a selectable worker, right-click movement and deposit gathering, resource counters, purifier placement and construction, a spreading red Blight, purifier containment, and a win/loss objective. The worker uses a safe direct-movement fallback: it stops when its route meets an obstacle and asks for a new order.

The visual foundation uses original Skia-drawn Kor structures, a worker, terrain, Blight growth, a compact HUD, and Lilith's advisor treatment. This is an early playable slice, not a finished campaign or a feature-complete RTS.

## Platform and requirements

- Linux x86_64 (NyarchLinux/Arch Linux supported target)
- SDL3 video runtime available as `libSDL3.so.0`
- .NET 8 SDK to build or run from source
- The published build bundles .NET and SkiaSharp's Linux native renderer; SDL3 and a working desktop display backend remain system requirements.

## Build and launch

From this directory, run `./build-linux.sh` to create a self-contained Linux x86_64 publish. Then use `./launch-kor.sh`. The launcher works from any current directory and reports when the package has not been built yet.

For a development build, run `./run-dev.sh`. The equivalent repository-root commands are:

```sh
dotnet build GondwanaGame/GondwanaGame.csproj --configuration Release
dotnet run --project GondwanaGame/GondwanaGame.csproj --configuration Release
dotnet run --project GondwanaGame/GondwanaGame.csproj --configuration Release -- --self-test
SDL_VIDEODRIVER=offscreen dotnet run --project GondwanaGame/GondwanaGame.csproj --configuration Release -- --smoke-test
dotnet publish GondwanaGame/GondwanaGame.csproj --configuration Release --runtime linux-x64 --self-contained true --output GondwanaGame/artifacts/linux-x64
```

## Controls

- Left-click the worker to select or deselect it.
- Right-click an alloy or supply deposit to move there and gather. Right-click open ground to move.
- Press **B**, then left-click open ground to place a purifier. Press **Escape** to cancel placement.
- Use **WASD** to pan and **+ / -** to zoom.
- Build two purifiers to win. The command shelter loses health if Blight reaches it.

## Content and user data

Scenario balance is in `Data/Balance/scenario.json`; it is copied beside the application on build and publish. Saves, a settings menu, and a separate user-profile directory are not implemented yet.

## Troubleshooting

- If launch reports SDL initialization or “No available video device,” verify that SDL3 is installed and start from a graphical desktop session.
- If the executable is missing, run `./build-linux.sh` first.
- If the balance JSON is invalid, startup prints a configuration error with the file path and problem.
- `--self-test` runs deterministic simulation and gameplay checks without opening a window.
- `--smoke-test` runs the SDL/Gondwana loop for two seconds and checks clean engine shutdown. With SDL's offscreen driver, it checks startup without a desktop but cannot verify the image or mouse/keyboard play.

## Verification limits

The Release build and Linux x86_64 self-contained publish have been verified. The current development environment has SDL3 but no usable graphical display backend, so mouse/keyboard play and visual composition still need a real desktop playtest.
