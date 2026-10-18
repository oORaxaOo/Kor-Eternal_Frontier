# Gameplay scope and current implementation

## Long-term direction

Kor Eternal Frontier is intended to be a colony, civilization, and world simulation. The player develops a community from a vulnerable settlement into a resilient civilization. Population, construction, work, transport, inventory, production, research, regional development, and The Blight should influence one another.

The local map is orthogonal 2D with square tiles. Temporary symbols prioritize readability: people are dots, vehicles and placeable objects are simple squares, and hostile creatures use triangle markers.

Architectural features such as walls, floors, doors, paths, and conduits will be designated per tile. Furniture, workstations, storage, and machines will be placed as individual objects with footprints. Both workflows must use a shared materials and job backend without conflating their placement semantics.

## Currently implemented

- Small deterministic local scenario with one worker and one Guardian.
- Resource deposits and resource gathering.
- Purifier construction with a real resource cost and worker labor.
- Local Blight spread and purifier cleansing.
- A separate deterministic nine-region campaign model with basic development, research, turn processing, army orders, battle setup/reconciliation, and save/load checks.
- Self-tests for current local rules and the campaign model.

## Known gaps

The campaign model is not yet connected to the application screen flow. The game does not yet have a generic colony population simulation, tile-by-tile building queue, multi-tile placeable-object system, shared job scheduler, stockpiles/reservations/hauling, general production recipes, population needs, a research tree that unlocks local mechanics, or a large connected procedural world. The Blight is not yet a complex interacting ecosystem.

The nine-region campaign and the small local scenario are a foundation, not an end-state representation of the intended game.

## Implementation priority

1. Establish tile and object ownership models.
2. Separate tile construction jobs from placed-object blueprints.
3. Implement inventories, reservations, stockpiles, hauling, and job ownership.
4. Create one complete resource-to-product production chain.
5. Add population needs and persistence coverage.
6. Connect strategic regions to real local colony state.
7. Expand research and The Blight ecology only after shared contracts are stable.
