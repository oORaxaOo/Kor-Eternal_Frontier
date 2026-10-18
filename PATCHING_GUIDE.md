# Development guide

There is one active application: the root .NET 8 project. Do not add another project or restore the removed Unity prototype.

## Main files

- Program.cs: window creation, input mapping, and simulation stepping.
- Simulation/ColonySimulation.cs: current colony state and gameplay rules.
- Campaign/CampaignSimulation.cs and Campaign/CampaignState.cs: deterministic strategic state and campaign persistence.
- Rendering/ColonyRenderer.cs: presentation-only square-grid view.
- Rendering/OrthogonalGridProjection.cs: screen/world transforms at the input/render boundary.
- Tests/ColonyChecks.cs and Tests/CampaignChecks.cs: checks invoked by --self-test.

## Build and test

    dotnet build GondwanaGame.csproj --configuration Release
    dotnet run --project GondwanaGame.csproj --configuration Release -- --self-test
    ./build-linux.sh

## Rules for changes

1. Make one coherent change at a time.
2. Add a behavior test with each new simulation rule.
3. Keep screen coordinates, SDL, and drawing types outside simulation classes.
4. Do not use cosmetic rendering to fake simulation state.
5. Keep tile architecture jobs distinct from placeable furniture and machine objects.
6. Conserve inventory and deposit quantities across gather/deliver.
7. Validate the actual diff and test output before declaring completion.
8. Do not commit bin, obj, artifacts, Python caches, or temporary output.
