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
- A `WorldSimulation` coordinator that advances the local colony and regional world under one real-time clock, with pause/speed support and automatic regional updates.

## Known gaps

The local colony and temporary legacy regional model currently advance through one `WorldSimulation` clock. The gameplay interface is mouse-first: visible controls expose pause/resume, speed, save/load, zoom, construction, object placement and cancellation; middle-mouse drag pans the camera. This is not completion of the world redesign. The nine-region linked-hex model is explicitly legacy scaffolding and must not be expanded into the new world. Regional stock remains separate, the old transfer bridge remains in code but is not exposed as a gameplay key, and a continuous world-space model, canonical resources, travel-time shipments, population continuity and one complete world save are still required.

## Implementation priority

1. Migrate the new GridManager/CitizenAgent/JobBroker core into the active colony scheduler through an adapter, preserving verified gather/carry/build behavior and save compatibility.
2. Generalize production definitions, workplace queues, stockpile capacity and delivery rules.
3. Add research projects that unlock actual buildings and recipes.
4. Connect strategic regions to real local settlement state and resource/person flows.
5. Expand colonist needs, housing, work eligibility, and the Blight ecosystem after shared state contracts are stable.


## Live colony ecosystem integration (migration slice)

`Simulation/ColonyEcosystem.cs` is now stepped by the active `ColonySimulation`. Every visible colonist has a corresponding citizen agent; citizen hunger is synchronized with the existing save-compatible worker model, food is consumed from bounded grid storage, and citizen mood/state are shown in the HUD. Completed Garden objects register capacity-limited food-production jobs with the live job broker. Garden output reserves grid storage before work begins and is reconciled with the colony's existing pooled inventory capacity.

This is an incremental migration, not a wholesale replacement of the established construction/gathering scheduler: non-food materials and construction/recipe jobs still use the legacy inventory and task pipeline. The food grid maps the starter stockpile and completed storage-rack footprints to bounded storage slots while respecting the existing pooled inventory cap; all-resource grid ownership remains follow-up work. Existing save schemas are unchanged; the runtime ecosystem is rebuilt from restored worker and inventory state. See `docs/colony-systems.md` for core contracts and migration boundaries.

## Mouse-first controls

- **Pause / Resume** — pause or resume the shared simulation clock.
- **Speed − / Speed +** — change the world speed between 0.25x and 4x.
- **Save / Load** — persist or restore the current session.
- **Zoom − / Zoom +** — change camera zoom.
- **Wall / Floor / Path / Door / Conduit** — select a tile architecture mode.
- **Bench / Pod / Rack / Gen / Garden** — select a placeable object.
- **Cancel** — exit the active placement mode.
- **Left click** — select colonists or place blueprints; **right click** issues a colonist command; **middle-mouse drag** pans the camera.

The old keyboard-only import/export shortcuts are no longer part of the player-facing interface. The transfer bridge remains legacy implementation and must be replaced with real world shipments before the redesign is complete.

The deterministic self-test continues to check resource conservation and session persistence in addition to colony and regional migration checks.
