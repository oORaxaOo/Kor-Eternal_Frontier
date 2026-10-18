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

1. Extract central job assignment and resource reservations without changing verified gameplay behavior.
2. Generalize production definitions, workplace queues, stockpile capacity and delivery rules.
3. Add research projects that unlock actual buildings and recipes.
4. Connect strategic regions to real local settlement state and resource/person flows.
5. Expand colonist needs, housing, work eligibility, and the Blight ecosystem after shared state contracts are stable.


## Integrated alpha controls

- **T** — advance one strategic campaign turn. Regional production, army orders, upkeep, research effects, and Blight spread are resolved by the campaign model.
- **I** — import up to 20 strategic Supplies as local Food and up to 10 strategic Alloy, limited by available local storage. Only accepted deliveries are charged to the campaign.
- **E** — export up to 20 local Food as strategic Supplies and up to 10 local Alloy. Only resources actually present in local inventory are transferred.
- **F5 / F9** — save/load one session containing both colony and campaign state at `~/.local/share/kor-eternal-frontier/session.json` (or under `$XDG_DATA_HOME`). Legacy colony-only and campaign-only saves are not overwritten by these controls.

The deterministic self-test checks import/export conservation, full-storage rejection, and unified disk save/load in addition to the existing colony and campaign checks.


## Strategic world map and diplomacy (new vertical slice)

Press **M** to move between the colony and a separate continental strategy view. The world layer contains 468 persistent territories with faction ownership, settlements/capitals, local development and supplies, explored state, and Blight pressure. Six active powers plus unclaimed frontier land are generated from a fixed seed: the Kor, Ashen Compact, Free Marches, Helion League, Verdant Covenant, and the Blight. The empire has its own Supplies/Alloy treasury; Kor-controlled territories generate strategic income each world turn. Territory ownership is visualized directly on the map; the side panel provides selected-territory intelligence and a faction legend.

- Click a territory to inspect it; **Left/Right Arrow** cycles territory selection.
- **C** claims an adjacent unclaimed territory as a Kor outpost for 40 Supplies and 25 Alloy; Kor territories generate treasury income on world turns.
- **R** sends a diplomatic mission to improve Kor relations.
- **Y** proposes a non-aggression pact when relations are sufficient.
- **G** proposes a trade pact after relations improve enough.
- **T** advances the strategic campaign turn and, while the world map is open, advances the world layer too.
- World state is saved to `~/.local/share/kor-eternal-frontier/world-map.json` (or under `$XDG_DATA_HOME`) after diplomacy and world-turn actions.

The Blight spreads across eligible neighboring territories on world turns, gradually raising pressure before consuming a territory. The current implementation is a foundation slice: territory-level diplomacy, local development and faction AI are deliberately simpler than the intended final simulation. Full treaties between every faction, wars/alliances, trade-route logistics, regional armies, settlement-to-colony transitions, and synchronized world-map saves inside the unified F5/F9 session remain follow-up work.
