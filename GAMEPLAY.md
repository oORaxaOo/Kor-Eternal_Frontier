# Gameplay scope and current implementation

## Long-term direction

Kor Eternal Frontier is a colony, civilization, and world simulation. The player develops a community from a vulnerable settlement into a resilient civilization. Population, construction, work, transport, inventory, production, research, regional development, and The Blight should influence one another.

The local map is orthogonal 2D with square tiles. Temporary symbols prioritize readability: people are dots, buildings and vehicles are squares, and hostile creatures use triangle markers.

Architectural features such as walls, floors, doors, paths, and conduits are designated per tile. Furniture, workstations, storage, and machines are placed as individual objects with footprints. Both workflows must use a shared material and worker-job backend without conflating their placement semantics.

## Currently implemented

- A 40 x 26 square tile map.
- Three colonists with independent position, movement task, carrying state, and hunger value.
- Resource deposits with finite quantities.
- Real gather, carry, and delivery trips that transfer resource from source deposit into colony inventory.
- Individual per-tile architecture construction orders with costs, work progress, and completion state.
- Footprint-based multi-tile placeable-object blueprints that remain unfinished until workers complete construction.
- A working production recipe at a completed workbench. It consumes alloy and wood when queued and creates one component only after worker labor is complete.
- Minimal top-down rendering with square tiles, object footprints, colonist dots, and clear resource markers.
- A separate deterministic strategic campaign model with basic regional development, research, turn processing, army orders, battle setup/reconciliation, and save/load checks.

## Known gaps

The strategic campaign model is not yet connected to the active game screen flow. The colony still needs a reusable general job board, job and material reservations, robust stockpile logic, broader resource and production definitions, work skills/jobs, housing-based needs, save/load, a proper research system with in-game unlocks, and larger world generation. The Blight remains a simple local spread-and-cleanse model rather than a fully interacting ecosystem.

The current slice is the start of the intended game, not a representation of the complete design.

## Implementation priority

1. Convert local state into explicit tile/object/inventory/job contracts.
2. Add material reservations, generalized hauling, and job priorities.
3. Expand recipes and tie them to workplace and research unlocks.
4. Introduce local colony persistence with schema validation.
5. Connect regional states to real local colony state and resource/person flows.
6. Expand population needs and The Blight ecology once shared simulation contracts are stable.
