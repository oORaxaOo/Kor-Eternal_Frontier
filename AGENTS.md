# Project notes for coding agents

- This folder is the standalone C#/.NET 8, Gondwana 2.6.1, SDL3, and SkiaSharp game. Do not modify the adjacent Unity project when working here.
- Build: `dotnet build GondwanaGame.csproj --configuration Release`.
- Simulation checks: `dotnet run --project GondwanaGame.csproj --configuration Release -- --self-test`.
- Application smoke check: `SDL_VIDEODRIVER=offscreen dotnet run --project GondwanaGame.csproj --configuration Release -- --smoke-test` (does not verify visual output or input).
- `Campaign/` owns strategic state, deterministic map generation, turn rules, campaign persistence, and battle setup/reconciliation. `Simulation/OutpostSimulation.cs` owns tactical battle rules. `Rendering/` must remain presentation-only. `Program.cs` maps input to campaign/simulation APIs and selects screens.
- The app uses a 1280x720 16:9 adapter with a 2560x1440 Gondwana backbuffer. Painters use 1100x720 design coordinates scaled to 16:9. Pointer coordinates must be inverse-transformed by both scale factors before hit testing.
- Campaign saves use schema version 1 JSON under the XDG user data directory. Update validation and round-trip checks when changing persisted state. Never serialize SDL, Gondwana, or Skia handles.
- Campaign turns are discrete. Tactical simulation advances at a fixed 0.1-second step and pauses with Space. Do not advance strategic state inside rendering.
- Battle resource stock transfers to the tactical mission once at battle start. Resolve that active battle once; transfer only remaining tactical stocks and apply casualties/ownership before clearing the active encounter.
- Keep world generation reproducible for a seed. Separate presentation randomness from simulation randomness.
- Edit balance in `Data/Balance/scenario.json` where supported. Campaign costs and yields are still C# constants; document that when changing them.
- Original game images live in `Assets/Art/`; `Tools/generate_art.py` rebuilds the raster set with Python/Pillow. The project copies these assets to builds. Keep third-party art out unless its license and provenance have been reviewed.
- Preserve existing worktree changes. Do not clean generated files destructively, commit, or publish unless explicitly asked in the current task.
- Report which checks actually ran and whether the UI had a real display backend.
