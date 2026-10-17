# KOR: Eternal Frontier — Gondwana prototype

This is the new code-first, isometric 2.5D implementation track. The Unity prototype remains in the repository untouched while the Gondwana track proves the new rendering and interaction foundation.

## Run on Linux, macOS, or Windows

Requires the .NET 8 SDK.

```bash
dotnet run --project GondwanaGame/KorFrontier.Gondwana.csproj
```

The project uses the current Gondwana Avalonia desktop template and GPU rendering path. First launch restores NuGet dependencies.

## Kor identity and visual direction

The game uses an original crimson, charcoal, and warm-gold art language; sharp diamond terrain, dark industrial silhouettes, readable unit colors, and a command-centre HUD. Its interface carries the Kor values “ALLE FÜR ALLE” and “EIN KOR LÄSST KEINEN KOR ALLEIN.” Art is generated from original vector-like shapes at runtime; no assets from other games are copied.

## Current foundation

- Rhombic isometric terrain with seeded meadow, soil, crystal, and spreading Blight.
- Kor command core, Pioneer and Warden units.
- Selection, additive selection, move orders, camera panning, zoom, and smooth camera transitions.
- Solar, extractor, archive, and relay construction with resource costs and nearby Pioneer construction.
- Supplies, alloy, energy, knowledge, and an evolving Blight footprint.
- Code-first Avalonia HUD, command buttons, and responsive desktop layout.


## Standalone Linux build

The GitHub Actions workflow publishes a self-contained Linux x64 package named `kor-frontier-linux-x64`. Download the artifact from the latest successful **Gondwana prototype** workflow run, extract it, then launch with:

```bash
./launch.sh
```

The package includes the .NET runtime, so a separate .NET installation is not needed to run that published build. The source project still requires the .NET 8 SDK for development.

## Controls

- **WASD / arrows:** pan camera.
- **Mouse wheel:** zoom around pointer.
- **Left-click:** select a Kor.
- **Shift + left-click:** add/remove selection.
- **Right-click:** issue a move order, or place the selected structure.
- **B:** quick-select Solar Array build mode.
- **Home:** return camera to the command core.
- **F5 / F9:** save / load the latest frontier (`LocalApplicationData/KorFrontier/frontier-save.json`).
- **Space:** pause/resume simulation.
- **Escape:** cancel build mode.

This is an early playable foundation, not a claim of commercial completeness. CI builds are the first automated integration gate; interactive playtesting and polish remain necessary.
