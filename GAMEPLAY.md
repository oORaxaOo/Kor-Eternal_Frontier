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


## Mouse-first menus and submenus

The command UI now provides mouse-accessible menus and nested submenus; keyboard shortcuts remain optional conveniences rather than requirements for the core command set.

- **Colony**: switch between colony and strategic world map, pause/resume, and open camera controls.
- **Colony → Camera controls**: pan the view and zoom in/out using clickable menu items.
- **Build → Architecture**: choose Wall, Floor, Path, Door, or Conduit, then left-click a tile to place the construction order.
- **Build → Buildings & objects**: choose Workbench, Sleeping Pod, Storage Rack, Generator, or Garden, then left-click a tile to place the blueprint.
- **Empire**: advance the strategic turn, open logistics, or claim a selected frontier territory while on the world map.
- **Empire → Logistics**: import strategic supplies or export colony resources.
- **Diplomacy**: improve relations, offer a non-aggression pact, or propose trade while on the strategic world map.
- **Orders**: select a colonist, open Orders → Move / gather order mode, then left-click the destination or a resource deposit. Right-click remains an optional shortcut.
- **Game**: save or load the combined colony/campaign session.
- **Mouse interaction**: left-click to select a colonist, inspect a territory, or place a selected construction/blueprint; right-click a colony tile to issue a movement/gather order to the selected colonist.

All core management actions have visible menu paths. Context-specific actions that require the world map explain when they are unavailable from the colony view. Closing the game remains available through the operating system's window controls.


## Industrial progression catalogue (foundation slice)

The mouse-first **Industry & Tech** page introduces a data-first content catalogue intended to grow into the main long-term gameplay loop. The catalogue currently validates **1,104 definitions**: 200 resource/material types, 480 research nodes, 128 building variants, 128 unit variants, and 168 production recipes. Research spans eight eras and twelve disciplines: logistics, materials science, energy, food systems, industry, computing, medicine, defence, exploration, ecology, colony systems, and Blight studies.

Production content is organised as twenty-one multi-stage industries, including timber, stoneworks, metallurgy, chemistry, electronics, power equipment, food processing, pharmaceuticals, construction components, vehicles, defence manufacturing, research instruments, water treatment, technical textiles, glassworks, optics, aerospace, biochemistry, waste recovery, robotics, and Blight containment. Each stage has its own stable material ID, so intermediate goods form real catalog-level chains instead of repeatedly producing the same generic output. Resource definitions carry groups, baseline values, and renewability; recipes define inputs, outputs, work requirements and an unlock; buildings define footprint, construction work, material requirements and upkeep; units define recruitment costs and recurring upkeep. Research prerequisites form cross-linked era ladders so higher tiers become progressively more expensive and require both discipline progress and logistics infrastructure.

**Important implementation boundary:** this commit adds and validates the catalogue and exposes a mouse-visible overview page. It does not yet replace the live colony inventory, current single component recipe, Ward Protocol research action, unit recruitment or save format. The next integration slices must migrate live inventory/jobs to stable resource IDs, route all recipe queues through the catalogue, persist research/unlocks, and apply building/unit upkeep to the simulation. This boundary is deliberate: showing hundreds of entries must not be mistaken for hundreds of already playable mechanics.

### Intended pressure loop

1. Survey and extract finite local deposits; expansion opens access to new resource families.
2. Convert raw resources through multi-step industrial chains, each consuming labour, machine capacity, energy and intermediate stock.
3. Research higher eras to unlock more efficient tools and advanced buildings, while research itself consumes increasingly scarce Knowledge and Energy.
4. Maintain housing, farms, power, storage, logistics, medical and defence infrastructure; every additional system adds upkeep and competes for workers and materials.
5. Recover waste and recycle intermediate products, negotiate or trade for shortages, and decide which production chains deserve limited space and power.
6. As The Blight pressures territory and supply routes, choose between immediate survival, expansion, research and long-term industrial resilience.

The intended challenge is not merely accumulating a larger stockpile. It is keeping an increasingly interconnected economy balanced while every new capability creates both power and a new obligation.

## Mouse-first colony management dashboard

The **Manage** command-bar entry opens a nine-section dashboard over the playfield. Its left navigation keeps management categories consistently accessible while the main area displays current simulation data.

- **Overview** — local population, pending work, construction completion, storage, campaign stockpiles, controlled regions and recent simulation events.
- **Population** — colonist roster, hunger, current task, carried cargo and selection state.
- **Resources** — every local inventory resource, remaining resource deposits, storage capacity, plus import/export actions.
- **Construction** — live architecture/object counts and status, recent tile orders, and direct mouse buttons for wall, floor, path, door, conduit, workbench, sleeping pod, storage rack, generator and garden placement.
- **Production** — workstation status and direct queue buttons for the currently implemented component recipe.
- **Research** — strategic Ward Protocol status, costs, prerequisites and a clickable research action.
- **Statistics** — settlement activity, needs currently simulated, inventory/deposit totals, strategic holdings, army readiness and Blight metrics.
- **Diplomacy** — faction relationship overview and clickable relation, non-aggression and trade actions.
- **Territories** — selected world territory intelligence, Blight pressure, settlement/development, treasury and claim/turn actions.

The dashboard is a first management surface built around existing simulation state, not a claim that every desired system already exists. Current implemented research is one strategic project; production has one recipe; colonists have basic hunger/task/cargo state rather than full skills, health, mood, schedules or needs; the world and regional campaign remain separate layers. Future work should expand simulation systems and add corresponding data/action pages without replacing the mouse-first navigation pattern.

