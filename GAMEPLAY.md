# Gameplay scope and current implementation

## Long-term direction

Kor Eternal Frontier is a colony, civilization, and world simulation. Population, construction, work, transport, inventory, production, research, regional development, and The Blight should influence one another.

The local map uses orthogonal 2D square tiles. Placeholder symbols prioritize readability: people are dots, buildings and vehicles are squares, and hostile creatures use triangle markers.

Architectural features such as walls, floors, doors, paths, and conduits are designated per tile. Furniture, workstations, storage, and machines are placed as independent footprint-based objects. Both workflows need shared material accounting and worker tasks without conflating placement semantics.

## Currently implemented

- A 40 x 26 square tile map.
- Three colonists with independent position, movement task, cargo state, and hunger.
- Finite resource deposits.
- Real gather/carry/deliver trips that transfer resource from source deposit to inventory.
- Individual per-tile architecture construction orders with costs, work progress, and completion state.
- Footprint-based multi-tile object blueprints that remain unfinished until workers complete construction.
- Inventory-based construction and production costs.
- A functional production recipe: a completed workbench consumes alloy and wood and creates a component only after worker labor.
- Minimal orthogonal top-down rendering.
- Versioned local colony saves with structural validation and round-trip tests.
- A separate deterministic strategic campaign model with regional development, research, turns, battle reconciliation, and save/load tests.

## Known gaps

The strategic campaign and local colony are now connected through explicit logistics orders and a unified session save. Strategic Supplies transfer to local Food; Alloy transfers directly. Imports obey colony storage capacity and debit campaign stock only for accepted deliveries. Exports remove actual local Food/Alloy and credit strategic Supplies/Alloy. Strategic Energy and Knowledge remain campaign-only until the local simulation has matching consumers. The current integration is a first functional bridge, not yet a full regional settlement simulation. The colony still needs shared job ownership, job/material reservations, capacity-aware stockpiles, multiple recipe definitions and production chains, fuller needs/workplace simulation, save migration beyond the initial schema, and research that unlocks local mechanics. The world map is currently the separate nine-region campaign model, not a large connected civilization simulation. The Blight is still a simple local spread-and-cleanse model.

## Implementation priority

1. Migrate the new GridManager/CitizenAgent/JobBroker core into the active colony scheduler through an adapter, preserving verified gather/carry/build behavior and save compatibility.
2. Generalize production definitions, workplace queues, stockpile capacity and delivery rules.
3. Add research projects that unlock actual buildings and recipes.
4. Connect strategic regions to real local settlement state and resource/person flows.
5. Expand colonist needs, housing, work eligibility, and the Blight ecosystem after shared state contracts are stable.


## Live colony ecosystem integration (migration slice)

`Simulation/ColonyEcosystem.cs` is now stepped by the active `ColonySimulation`. Every visible colonist has a corresponding citizen agent; citizen hunger is synchronized with the existing save-compatible worker model, food is consumed from bounded grid storage, and citizen mood/state are shown in the HUD. Completed Garden objects register capacity-limited food-production jobs with the live job broker. Garden output reserves grid storage before work begins and is reconciled with the colony's existing pooled inventory capacity.

This is an incremental migration, not a wholesale replacement of the established construction/gathering scheduler: non-food materials and construction/recipe jobs still use the legacy inventory and task pipeline. The food grid currently maps the starter stockpile footprint to bounded storage slots; expanded storage and all-resource grid ownership remain follow-up work. Existing save schemas are unchanged; the runtime ecosystem is rebuilt from restored worker and inventory state. See `docs/colony-systems.md` for core contracts and migration boundaries.

## Integrated alpha controls

- **T** — advance one strategic campaign turn. Regional production, army orders, upkeep, research effects, and Blight spread are resolved by the campaign model.
- **I** — import up to 20 strategic Supplies as local Food and up to 10 strategic Alloy, limited by available local storage. Only accepted deliveries are charged to the campaign.
- **E** — export up to 20 local Food as strategic Supplies and up to 10 local Alloy. Only resources actually present in local inventory are transferred.
- **F5 / F9** — save/load one session containing both colony and campaign state at `~/.local/share/kor-eternal-frontier/session.json` (or under `$XDG_DATA_HOME`). Legacy colony-only and campaign-only saves are not overwritten by these controls.

The deterministic self-test checks import/export conservation, full-storage rejection, and unified disk save/load in addition to the existing colony and campaign checks.
