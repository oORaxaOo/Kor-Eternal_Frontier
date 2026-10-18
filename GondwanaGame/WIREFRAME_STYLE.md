# Kor-Eternal Frontier — Wireframe Visual Foundation

## Purpose

The current standalone scenario uses an original dark industrial wireframe treatment drawn with SkiaSharp. The intention is to establish a scalable visual language for the future colony simulation without requiring a unique painted sprite for every early building and prop.

This document describes the current implementation, not a claim that the full final art direction is complete.

## Visual principles

- **Kor engineering:** dark, filled surfaces with clean crimson contours and deliberate technical details.
- **Terrain:** restrained blue-green survey lines and a subtle drafting grid.
- **Critical information:** muted gold for selection, resource emphasis, and containment status.
- **Blight:** transparent dark-crimson contamination over terrain, branching red paths, and irregular organic creature contours.
- **Readability first:** line density should stay low enough that workers, buildings, and pathing targets remain easy to distinguish.
- **Consistent geometry:** buildings should use the same isometric ground anchor as the terrain grid so they sit on tiles rather than floating between them.

## Current palette

The primary values are defined near the top of `Rendering/OutpostRenderer.cs`:

| Name | Purpose |
| --- | --- |
| `Charcoal` | Main background |
| `Crimson` | Primary Kor accent |
| `Gold` | Important UI and status accent |
| `PanelEdge` | UI boundaries |
| `TerrainWire` | Isometric terrain grid |
| `KorWire` | Kor structure contours |
| `BlightWire` | Blight creature contours |
| `SurfaceFill` | Reserved dark surface fill token; currently not used throughout all drawing methods |

Many secondary colors are still local to individual draw methods. Centralizing every minor highlight is a future cleanup task, not a prerequisite for this prototype pass.

## Implemented in the current foundation

- Dark drafting-grid backdrop.
- Isometric terrain tiles with restrained survey marks.
- Infection overlays using branching wire-like lines instead of a single simple red blob.
- Outlined industrial rocks and resource markers.
- A wireframe-prism helper used by the command hub and purifier structures.
- Additional structural contours on worker and Guardian silhouettes.
- Crimson wire contours on Blight creatures.
- Refined top and bottom HUD framing.

## Current limitations

- The playable map remains a small fixed scenario.
- The screen layout still uses a 1100×720 logical composition.
- Several art elements are still vector-like primitive constructions, not production-ready hand-painted assets.
- The renderer has not yet been visually tested on a real desktop after this change.
- No full colony population, production-chain, strategic world map, or save/load system is implemented in this initial slice.

## Verification

From the repository root:

```sh
dotnet build GondwanaGame/GondwanaGame.csproj --configuration Release
dotnet run --project GondwanaGame/GondwanaGame.csproj --configuration Release -- --self-test
```

A passing build and simulation check do not prove visual quality. Launch on a desktop display and inspect the scene at the ordinary gameplay zoom before finalizing line widths, colors, or entity scale.
