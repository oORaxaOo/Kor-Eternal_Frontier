# Gameplay pillars

## The Kor Dynasty
The Kor are the primary playable faction. Their identity is a community that grows through logistics, research, protection and collective resilience. The visual palette is crimson, black and gold.

## Core loop
1. Establish the command core and keep Pioneers alive.
2. Expand the resource economy with Solar Collectors, Extractors and Knowledge Archives.
3. Place construction sites, then keep one or more Pioneers within working distance to complete them.
4. Use research to improve the settlement and unlock later strategic systems.
5. Defend the perimeter from the Blight, whose entities attack Kor forces and spread over time.
6. Save the frontier and continue expanding instead of ending at a fixed campaign victory screen.

## Current prototype
The Unity build uses generated terrain and primitive meshes so that the game systems can be iterated without waiting for final art. Resource counters, construction sites with Pioneer work, multi-unit selection, research prerequisites, movement, simple combat, nearest-target Blight pursuit and local JSON save/load are implemented in the runtime controller.

## Controls and persistence
- Drag a rectangle to select multiple Kor units.
- Shift + click or Shift + drag adds units to the current selection.
- F5 saves and F9 loads from Unity's persistent data directory.
- Save files include resource totals, research, entities, positions, hit points, destinations and in-progress construction work.

## Next milestones
- A* pathfinding, drag selection, attack-move, unit formations and line-of-sight.
- Seed selection and world presets; larger streamed maps.
- Expand the initial research chain into a multi-branch tree with stronger unlocks and UI feedback.
- Blight ecology with nests, spread pressure, evolving threats and countermeasures.
- Production-ready Kor models, animation, audio, UI and Linux player builds.
- A deeper 4X layer: territory, logistics, settlement policies, diplomacy and long-term world events.
