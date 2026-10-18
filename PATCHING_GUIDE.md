# Development guide

There is one active application: the root .NET 8 project. Do not add another project or restore the removed Unity prototype.

## Main files

- Program.cs: window creation, input mapping, and simulation stepping.
- Simulation/OutpostSimulation.cs: the current small local simulation.
- Simulation/ScenarioBalance.cs and Data/Balance/scenario.json: starter scenario tuning.
- Campaign/CampaignSimulation.cs and Campaign/CampaignState.cs: deterministic strategic state and campaign persistence.
- Rendering/OutpostRenderer.cs: presentation-only square-grid local view.
- Rendering/OrthogonalGridProjection.cs: screen/world transforms at the input/render boundary.
- Tests/ScenarioChecks.cs and Tests/CampaignChecks.cs: checks invoked by --self-test.

## Build and test

    dotnet build GondwanaGame.csproj --configuration Release
    dotnet run --project GondwanaGame.csproj --configuration Release -- --self-test
    SDL_VIDEODRIVER=offscreen dotnet run --project GondwanaGame.csproj --configuration Release -- --smoke-test
    ./build-linux.sh

## Rules for changes

1. Make one coherent change at a time.
2. Add a behavior test with each new simulation rule.
3. Keep screen coordinates, SDL, and drawing types outside simulation classes.
4. Do not use cosmetic rendering to fake simulation state.
5. Keep tile architecture orders distinct from placeable furniture and machine objects.
6. Check the actual diff and test output before declaring completion.
7. Do not commit bin, obj, artifacts, Python caches, or temporary output.
