# Kor Eternal Frontier — agent instructions

## Canonical project
- The only active application is the root .NET 8 project.
- The Unity prototype and duplicate nested Gondwana project have been removed. Do not recreate them.
- Gameplay rules belong in Simulation; strategic state belongs in Campaign.
- Rendering and input may translate screen positions into world coordinates. Simulation APIs must not depend on SDL, Gondwana, SkiaSharp, windows, camera offsets, or screen pixels.
- The active local renderer is orthogonal top-down: square tiles, people as dots, hostile creatures as triangles, and buildings/vehicles as simple shapes. No isometric projection or polished art pipeline is active.

## Commands
- Build: dotnet build GondwanaGame.csproj --configuration Release
- Rules and campaign checks: dotnet run --project GondwanaGame.csproj --configuration Release -- --self-test
- SDL/Gondwana startup smoke check: SDL_VIDEODRIVER=offscreen dotnet run --project GondwanaGame.csproj --configuration Release -- --smoke-test
- Linux package: ./build-linux.sh

## Engineering rules
- Simulation systems must have testable logical state and fixed-step behavior independent of frame rate.
- Keep resource accounting explicit; never create or duplicate inventory implicitly.
- Do not mark a blueprint complete unless material and work requirements are satisfied.
- Keep tile architecture orders distinct from placed furniture/machine objects as those systems are implemented.
- Preserve deterministic behavior for a fixed seed where practical; separate presentation randomness from simulation randomness.
- Any persistence schema change needs validation and round-trip tests.
- Self-test must execute both local-simulation and campaign checks.
- Prefer small, tested changes over replacing unrelated working code.
- Do not commit generated build output, temporary files, downloaded assets, or Python caches.
- Work on feature branches. Preserve unrelated user changes and avoid destructive history rewrites.
- Never claim a build, graphical launch, or test succeeded without running it and observing the result.
