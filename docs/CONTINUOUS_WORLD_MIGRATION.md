# Mouse-first continuous world: migration contract

## Product direction

KOR: Eternal Frontier is a single continuous real-time simulation. The settlement is one zoom level of the world, not a separate mode. Time, population, resources, movement, trade, conflict and hazards continue in one shared state as the camera moves between scales.

## Legacy campaign map: do not extend

The old campaign model is a small fixed set of named regions positioned on a linked hex layout. It uses adjacency checks, campaign turns, region-level production ticks and separate strategic resource counters. That representation was created for turn-based play and is not the target architecture.

It may remain temporarily as a migration adapter so existing saves and tests can still run, but:
- do not add features that depend on its hex coordinates or adjacency graph;
- do not build the new world renderer around those regions;
- do not use its turn counter as a player-facing clock;
- do not treat its resource counters as a second canonical inventory;
- do not add instant import/export between colony and world.

## Target world representation

- Continuous world-space coordinates and spatial queries are the primary representation.
- Settlements, people, roads, resource deposits, vehicles, armies, weather/hazards and points of interest are entities placed in that space.
- Local detailed simulation is loaded around the camera/active settlement. Distant areas use coarse continuous simulation but remain part of the same world state and clock.
- Travel is represented by moving entities and elapsed time, not by adjacency hops or end-turn completion.
- Resource ownership is canonical. A shipment reserves/debits goods at dispatch, tracks a route and travel progress, then delivers or resolves loss/return explicitly.
- Camera zoom/pan changes level of detail and presentation only; it never changes simulation mode.
- Saves serialize one versioned world state, including the clock, local settlement, world entities, shipments, population, factions and outstanding orders.

## Mouse-first interaction

Visible menus are the primary control surface. Pause/resume, speed, save/load, construction selection, object placement and cancellation must be clickable. Contextual left/right mouse interactions are supported; middle-button drag pans the view. Keyboard shortcuts are optional conveniences only and cannot be required to play.

## Migration sequence

1. Replace keyboard-only gameplay controls with visible mouse-operated menus.
2. Introduce and test a continuous world-space model that does not depend on hex adjacency.
3. Move population, resources and logistics into shared canonical state.
4. Replace interval-based region turns with continuous world systems and travel-time entities.
5. Connect camera zoom/LOD to the same world state and remove the legacy campaign adapter after save migration is covered.
6. Validate builds, deterministic simulation tests, resource conservation, save round-trips and an actual runtime smoke test.
