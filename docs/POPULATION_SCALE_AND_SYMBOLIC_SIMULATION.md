# Population Scale and Symbolic Simulation Design

## Product intent

Kor Eternal Frontier should use a compact, Dwarf-Fortress-inspired visual language to support simulation depth rather than visual spectacle. Tiles, short glyphs, small sprites, status colours and inspectable text should communicate a world whose residents, economy and ecology continue to change.

These are design inspirations, not claims about the private source architecture of the reference games.

## Population target

- Target settlements with thousands of individually identifiable residents.
- Every resident should have persistent identity and consequential state; distant residents may use cheaper update schedules, but must not become decorative counters.
- Needs and relationships should create decisions and trade-offs, not just passive meters.
- Residents should have jobs, homes, schedules, health, social ties, memories and reactions to local conditions as the simulation grows.
- Food, storage, housing, workplaces, transport, safety and environmental conditions must be finite, connected systems.

## Combined simulation architecture

1. **Authoritative state, presentation separated.** Rendering must never mutate simulation state. The same save must produce the same logical state regardless of zoom or selected view.
2. **Deterministic time slices.** Use a stable simulation clock and seeded randomness so bug reports and regression tests can reproduce world events.
3. **Simulation levels of detail.** Near-camera residents can update movement and interactions frequently. Distant residents update needs, work, travel and relationships in larger deterministic batches. LOD transitions must preserve identities, inventory and outcomes.
4. **Spatial queries.** Add spatial buckets and cached routes/targets instead of having every resident repeatedly scan the whole map or all other residents.
5. **Event-driven work.** Needs crossing thresholds, a job becoming available, a stockpile changing, or a hazard spreading should wake relevant systems. A deterministic bounded event queue is now available as infrastructure; subsystem handlers are not yet connected.
6. **Shared-destination movement.** Add multi-source flow fields for mass travel to common destinations such as dining, storage or rally points. Preserve exact reachability checks and reservations for individual jobs.
7. **Material simulation.** Add explicit tile materials, temperature and bounded fluid updates incrementally. A 3D voxel world, pressure-driven fluids and body-part damage are future systems, not current functionality.
8. **Bounded presentation cost.** Draw only visible tiles and relevant symbols. Provide overlays and inspector panels for dense data instead of rendering every resident label simultaneously.
9. **Persistence as a contract.** Resident identity, needs, work, relationships and world history must survive save/load. Schema changes need compatibility tests.
10. **Observable performance.** Record simulation time, pathfinding requests, active agents, queued events, render candidates and save size. Performance improvements must be measured, not assumed.

## First scalability guardrail

The citizen ecosystem uses a monotonic ID counter instead of scanning all existing residents for each new ID. Automated simulation checks create and step 10,000 distinct citizen agents in one deterministic tick. This is an agent-level scalability guardrail, not proof that 10,000 fully integrated workers meet a target frame time. The local colony pathfinder caches successful start/target routes and exposes search/cache-hit counters. These guardrails catch some avoidable work, but they do not replace profiling of full worker logistics or rendering.

## Visual direction

- Orthogonal tile grid; strong terrain and architecture silhouettes.
- Restrained palette with clear semantic colour: neutral terrain, pale residents, gold priorities, crimson danger/Blight.
- Small symbols and concise labels, with optional overlays for needs, jobs, danger, traffic and resources.
- Rich inspection panels and event history provide depth without cluttering the map.
- Prioritize readability at multiple zoom levels; distant simulation should aggregate visually while retaining individual identities in the inspector.

## Next implementation milestones

1. Connect deterministic events to job availability, stock changes and need thresholds.
2. Add spatial buckets and repeatable 1k, 5k and 10k end-to-end benchmarks with tick, pathfinding, render and save metrics.
3. Add shared-destination flow fields and verify route invalidation when obstacles change.
4. Expand resident needs, housing, household membership, social ties, schedules and memories.
5. Add tile material definitions, temperature exchange and bounded fluid movement.
6. Add seeded incident scheduling and ecology interactions: herds, habitat/forage, Blight pressure and danger zones.
7. Add population and world-history inspectors, filters and event logs.
