# DFHack-inspired construction and logistics notes

This document records design patterns studied from DFHack's public documentation and how they apply to Kor Eternal Frontier. The game implementation is original; no DFHack source files or assets are copied into this repository.

## Reference material

- DFHack `buildingplan` documentation: https://docs.dfhack.org/en/stable/docs/tools/buildingplan.html
- DFHack Lua API reference: https://docs.dfhack.org/en/stable/docs/dev/Lua%20API.html
- DFHack source and licensing notes: https://github.com/DFHack/dfhack and https://github.com/DFHack/dfhack/blob/develop/LICENSE.rst

DFHack is a Dwarf Fortress extension/modding toolkit, not a drop-in colony simulation engine. Its useful contribution here is the interaction and workflow design around planned construction, missing materials, queues, and automation—not wholesale integration of its memory-hacking runtime.

## Implemented in this change

- The build HUD previews material recipes from the same authoritative cost functions used by the construction scheduler.
- The preview distinguishes **MATERIALS READY** from **WAITING** and lists the missing quantity by resource.
- Tile blueprints waiting for materials are now visibly different on the map: crimson outline and fill, with an exclamation marker at normal settlement zoom. Gold remains reserved for active construction.
- Planning is still distinct from physical construction. A blueprint can be designated before the stockpile contains its recipe, but no worker may begin the build until the scheduler successfully consumes the complete material cost. This follows the useful separation in DFHack buildingplan between design intent and executable work.

## Newly implemented work-order management slice

- Construction and component-production orders have player-set priorities from 1 (low) to 5 (critical). The live scheduler selects higher-priority eligible work first across these three order families, and automatic hauling prioritizes resources required by the most urgent blocked construction order before considering lower-priority shortages.
- Clicking an unfinished blueprint, architecture order, or workbench with queued component production opens an inspector with current priority and mouse controls to adjust it.
- The inspector reports actionable state: missing material quantities, whether materials are ready, whether work is already in progress, or when no accessible work tile was found. Queued production reports its committed inputs and progress.
- Priorities are stored in colony save schema v5 and validated; legacy supported save versions default missing priorities to 3.
- Automated regression checks cover selection, priority bounds, scheduling order, diagnostic availability, and persistence.

This is a coherent first slice, not a claim that all Dwarf Fortress systems are integrated. Gathering, hauling, citizen needs, ecology, and all remaining job types still need a single shared broker and richer queue tooling.

## Existing mechanics retained

- Each tile/object has an independent construction order and status.
- Material consumption is checked and applied through the canonical inventory ledger; a failed material check does not consume a partial recipe.
- Workers gather finite deposits, carry loads, and deliver them to bounded stockpiles.
- The job scheduler automatically seeks materials required by pending construction orders.
- Inventory, worker cargo, deposits, construction state, and work progress are part of save/load.

## Next engineering slices

1. Give every planned object a visible status and exact missing-material breakdown, not just the current build selection.
2. Add a construction queue/inspector with filtering, cancellation, and priority controls.
3. Add material reservations so multiple orders cannot all appear to own the same available stock; preserve reservations through save/load.
4. Add configurable production/work orders with target stock levels, input costs, output storage checks, and cancellation/refund rules.
5. Improve stockpile zoning and per-resource storage capacity, while keeping one canonical inventory ledger.
6. Add regression scenarios for zero-material starts, concurrent orders competing for the same material, delivery capacity, cancellation, and save/load round trips.

## Validation

The repository's canonical checks are `dotnet build GondwanaGame.csproj --configuration Release` and `dotnet run --project GondwanaGame.csproj --configuration Release -- --self-test`. CI also packages the Linux x86_64 game and smoke-tests the graphical startup path.
