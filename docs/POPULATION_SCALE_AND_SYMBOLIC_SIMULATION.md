# Population Scale and Symbolic Simulation Design

## Product intent

Kor Eternal Frontier should use a compact, Dwarf-Fortress-inspired visual language to support simulation depth rather than visual spectacle. Tiles, short glyphs, small sprites, status colours and inspectable text should communicate a world whose residents, economy and ecology continue to change.

This is an inspiration target, not a reproduction of Dwarf Fortress' proprietary presentation or content.

## Population target

- Target settlements with thousands of individually identifiable residents.
- Every resident should have persistent identity and consequential state; distant residents may use cheaper update schedules, but must not become decorative counters.
- Needs and relationships should create decisions and trade-offs, not just passive meters.
- Residents should have jobs, homes, schedules, health, social ties, memories and reactions to local conditions as the simulation grows.
- Food, storage, housing, workplaces, transport, safety and environmental conditions must be finite, connected systems.

## Simulation architecture

1. **Authoritative state, presentation separated.** Rendering must never mutate simulation state. The same save must produce the same logical state regardless of zoom or selected view.
2. **Deterministic time slices.** Use a stable simulation clock and seeded randomness so bug reports and regression tests can reproduce world events.
3. **Simulation levels of detail.** Near-camera residents can update movement and interactions frequently. Distant residents update needs, work, travel and relationships in larger deterministic batches. LOD transitions must preserve resources and outcomes.
4. **Spatial queries.** Use spatial buckets and cached routes/targets instead of having every resident repeatedly scan the whole map or all other residents.
5. **Event-driven work.** Needs crossing thresholds, a job becoming available, a stockpile changing, or a hazard spreading should wake relevant systems; avoid expensive global polling where possible.
6. **Bounded presentation cost.** Draw only visible tiles and relevant symbols. Provide overlays and inspector panels for dense data instead of rendering every resident label simultaneously.
7. **Persistence as a contract.** Resident identity, needs, work, relationships, and world history must survive save/load. Schema changes need compatibility tests.
8. **Observable performance.** Record simulation time, pathfinding requests, active agents, queued events, render candidates and save size. Performance improvements must be measured, not assumed.

## First scalability guardrail

The citizen ecosystem uses a monotonic ID counter instead of scanning all existing residents for each new ID. Automated simulation checks create and step 10,000 distinct citizen agents in one deterministic tick. This is an agent-level scalability guardrail, not proof that 10,000 fully integrated workers meet a target frame time. The local colony pathfinder also caches up to 8,192 successful start/target routes, returns defensive copies, and invalidates routes when a completed tile or new object blueprint changes walkability. Search and cache-hit counters make reuse testable. Automated checks verify that a repeated query avoids a second breadth-first search. These guardrails catch some avoidable work, but they do not replace profiling of full worker logistics or rendering.

## Visual direction

- Orthogonal tile grid; strong terrain and architecture silhouettes.
- Restrained palette with clear semantic colour: neutral terrain, pale residents, gold priorities, crimson danger/Blight.
- Small symbols and concise labels, with optional overlays for needs, jobs, danger, traffic and resources.
- Rich inspection panels and event history provide depth without cluttering the map.
- Prioritize readability at multiple zoom levels; distant simulation should aggregate visually while retaining individual identities in the inspector.

## Next implementation milestones

1. Resident needs beyond hunger and rest, with real ways to satisfy each need.
2. Housing, household membership, social ties and schedules.
3. Expand spatial indexing and path reuse; add repeatable 1k, 5k and 10k full-worker benchmarks with tick, pathfinding, render and save metrics.
4. Visible and testable simulation LOD for off-screen residents.
5. Economic pressure propagation: shortages, job chains, transport bottlenecks and migration.
6. Ecology interactions: herds, habitat/forage, Blight pressure, danger zones and regional events.
7. Population and world-history inspectors, filters and event logs.
