# KOR: Eternal Frontier — Unity Edition

An original Kor RTS / settlement-builder / 4X prototype with a crimson, black and gold visual identity. No copied game assets are included.

## Open and play on NyarchLinux

1. Install Unity Hub and a Unity 6 editor (baseline: `6000.0.0f1`).
2. In Unity Hub choose **Add project from disk** and select this repository folder.
3. Wait for import. The editor bootstrap creates `Assets/Scenes/KorFrontier.unity` on first load.
4. Open that scene if needed and press **Play**.
5. For a native Linux build, select the Linux target under **File → Build Profiles** and include the KorFrontier scene.

This repository contains Unity source code, not a precompiled Linux executable. The project has not yet been compiled or run inside the Unity Editor in this environment; treat first import as an integration test.

## Controls

- WASD / arrow keys: move camera; mouse wheel: zoom; hold Shift for faster camera movement.
- Left-click: select a Kor unit or place the active building.
- Drag with the left mouse button: select multiple Kor units.
- Shift + left-click or Shift + drag: add/remove or extend the current selection.
- Right-click: move selected units or issue an attack order against a hostile.
- B: Solar Collector build mode; Space: pause.
- F5 or **SAVE**: save the current frontier; F9 or **LOAD**: restore it.
- Use the right command panel for buildings and research.

Save data is written to Unity's `Application.persistentDataPath` as `kor-frontier-save.json`. Saves include resources, research, unit/building positions, hit points, destinations and construction progress.

## Prototype systems

- Seeded terrain generation and resource deposits, Kor command core and starting units.
- Supplies, alloy, energy and knowledge economy.
- Solar Collectors, Ore Extractors, Knowledge Archives, Guardian Barracks, Defence Turrets and Relay Beacons.
- Research directives with prerequisite chains, automatic defence fire, combat and Blight expansion.
- Blight pursuit AI: hostiles seek Kor forces and close distance before attacking.
- Pioneer-driven construction: place a construction site, then move a Pioneer close enough to work on it.
- Drag-box unit selection, additive selection and group orders.
- Multi-unit selection and group movement/attack orders.
- Persistent local save/load; the sandbox can continue without a forced victory screen.

## Roadmap

The current build is an early systems prototype, not a finished commercial-scale game. It uses generated primitive models and materials. A* pathfinding, animated art, deeper logistics, diplomacy, more advanced faction AI, audio and a more complete 4X simulation remain planned. Blight pursuit is currently simple nearest-target behavior rather than strategic pathfinding.

The Kor Dynasty is the primary playable faction. The goal is classic RTS readability with settlement growth, research progression and a persistent procedural frontier. References to other games are inspirations only; this repository uses original code and placeholder art.

## License

MIT. See `LICENSE`.
