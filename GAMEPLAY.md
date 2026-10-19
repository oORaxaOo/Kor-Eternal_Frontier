# Gameplay scope and current implementation

## Long-term direction

Kor Eternal Frontier is a colony, civilization, and world simulation. Population, construction, work, transport, inventory, production, research, regional development, and The Blight should influence one another.

The local map uses orthogonal 2D square tiles. Placeholder symbols prioritize readability: people are dots, buildings and vehicles are squares, and hostile creatures use triangle markers.

Architectural features such as walls, floors, doors, paths, and conduits are designated per tile. Furniture, workstations, storage, and machines are placed as independent footprint-based objects. Both workflows need shared material accounting and worker tasks without conflating placement semantics.

## Currently implemented

- A 40 x 26 square tile map.
- Three colonists with independent position, movement task, cargo state, and hunger.
- Finite resource deposits.
- Real gather/carry/deliver trips for both legacy resources and stable-ID raw materials, with finite source quantities and shared inventory capacity.
- Individual per-tile architecture construction orders with costs, work progress, and completion state.
- Footprint-based multi-tile object blueprints that remain unfinished until workers complete construction.
- Inventory-based construction and production costs with shared storage capacity across legacy resources and stable-ID industry materials.
- Live catalogue-driven recipe queues: a completed workbench reserves actual inputs, workers travel to the station, perform recipe work, and add stage-specific outputs.
- Local research projects spend strategic Knowledge and Energy, enforce catalogue prerequisites, persist in colony schema v5, and unlock recipes and catalogue buildings only after the required technology is completed.
- All 128 catalogue building variants can be selected through a paginated mouse-first construction list, placed as footprint blueprints, gated by research, built by colonists, and saved with their stable building IDs. Construction reserves actual stable-ID masonry-chain materials, alloy and components; completed catalogue storage buildings add inventory capacity.
- All 128 catalogue unit variants are now recruitable through a paginated mouse-first roster. Recruitment requires the unit's research unlock and exact Supplies/Alloy costs, adds combat strength, persists in campaign saves, incurs recurring upkeep, and can lose a roster unit when a battle is lost.
- Advanced production recipes reserve local power capacity based on their stage. Completed generators and catalogue power buildings supply throughput; spare power increases work speed, and each Workbench runs only one active order. All 168 recipes are accessible through a paginated production catalogue.
- The catalogue now contains 204 resources/materials, 480 research nodes, 128 building variants, 128 unit variants and 168 recipes (1,108 linked definitions total). Four additional finite mineral deposits feed explicit raw inputs in the masonry, metallurgy and glassworks chains.
- Multi-stage intermediate material IDs persist through save/load; recipes reject missing inputs instead of generating free goods.
- The original component recipe remains available alongside the catalogue recipes.
- Minimal orthogonal top-down rendering.
- Versioned local colony saves with structural validation and round-trip tests.
- A separate deterministic strategic campaign model with regional development, research, turns, battle reconciliation, and save/load tests.

## Known gaps

The strategic campaign and local colony are connected through explicit logistics orders and a unified session save. Strategic Supplies transfer to local Food; Alloy transfers directly. Imports obey colony storage capacity and debit campaign stock only for accepted deliveries. Exports remove actual local Food/Alloy and credit strategic Supplies/Alloy. Strategic Energy and Knowledge are campaign resources used by the local research system. The colony supports stable-ID industry materials alongside its five legacy resources; both share storage capacity, and catalogue recipe orders consume real inputs and persist their recipe identity. Fifteen finite raw-material sites now provide ore, rare earth, conductive crystal, chemical reagents, water, biomass, recycled feedstock, clay, sand, copper ore, silica, fuel, medical precursors, contained Blight samples and exotic matter. The 204-resource catalogue connects clay to masonry, copper ore to metallurgy, silica/sand to glassworks, fuel to advanced power equipment, medical precursors to pharmaceuticals, and Blight/exotic samples to containment; all site coordinates and remaining quantities are available through a paginated Resources panel. Local research spends campaign Knowledge and Energy, checks prerequisites, persists completed technology IDs in colony schema v5, and gates recipes and catalogue buildings until their required technology is researched. All 128 catalogue building variants are now placeable and use worker-driven construction with real stable-ID material costs; storage-family buildings expand capacity when completed. Remaining gaps include extraction coverage for more catalogue materials, full power distribution/storage and machine-specific throughput, automated supply balancing, deeper role-specific battle effects and tactical formations, broader building effects, and fuller needs/workplace simulation. The world map remains a separate strategic layer rather than a fully connected civilization simulation, and The Blight is still a simplified spread-and-cleanse model.

## Implementation priority

1. Extract central job assignment and resource reservations without changing verified gameplay behavior.
2. Expand raw-material extraction coverage and extend local power from shared capacity into distribution, storage, and machine-specific throughput.
3. Expand role-specific combat effects and tactical formations for recruited unit variants.
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
- **Orders**: select a colonist, open Orders → Move / gather order mode, then left-click the destination, a legacy resource deposit, or a marked stable-ID raw-material site. Workers collect up to five units per trip and deliver them to shared stockpiles. Right-click remains an optional shortcut.
- **Game**: save or load the combined colony/campaign session.
- **Mouse interaction**: left-click to select a colonist, inspect a territory, or place a selected construction/blueprint; right-click a colony tile to issue a movement/gather order to the selected colonist.

All core management actions have visible menu paths. Context-specific actions that require the world map explain when they are unavailable from the colony view. Closing the game remains available through the operating system's window controls.


## Industrial progression catalogue (foundation slice)

The mouse-first **Industry & Tech** page introduces a data-first content catalogue intended to grow into the main long-term gameplay loop. The catalogue currently validates **1,104 definitions**: 200 resource/material types, 480 research nodes, 128 building variants, 128 unit variants, and 168 production recipes. Research spans eight eras and twelve disciplines: logistics, materials science, energy, food systems, industry, computing, medicine, defence, exploration, ecology, colony systems, and Blight studies.

Production content is organised as twenty-one multi-stage industries, including timber, stoneworks, metallurgy, chemistry, electronics, power equipment, food processing, pharmaceuticals, construction components, vehicles, defence manufacturing, research instruments, water treatment, technical textiles, glassworks, optics, aerospace, biochemistry, waste recovery, robotics, and Blight containment. Each stage has its own stable material ID, so intermediate goods form real catalog-level chains instead of repeatedly producing the same generic output. Resource definitions carry groups, baseline values, and renewability; recipes define inputs, outputs, work requirements and an unlock; buildings define footprint, construction work, material requirements and upkeep; units define recruitment costs and recurring upkeep. Research prerequisites form cross-linked era ladders so higher tiers become progressively more expensive and require both discipline progress and logistics infrastructure.

**Implementation boundary:** the stable-ID material ledger, shared storage accounting, paginated live catalogue recipe queue, worker-driven production, finite raw-material extraction, research-backed recipe/building/unit unlocks, all 128 worker-built catalogue building variants, all 128 recruitable catalogue unit variants, and shared power/workstation throughput rules are connected to runtime systems. Colony saves use schema v5 and campaign saves persist the recruited unit roster; older saves remain supported. This remains an incremental runtime slice: not every catalogue material has a local extraction source, power is modeled as shared throughput rather than a full grid with storage/distribution, buildings have only limited family effects, and unit combat is represented by aggregate army strength rather than individual tactical formations.

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
- **Resources** — every legacy resource, produced stable-ID materials, remaining deposits, shared storage capacity, plus import/export actions.
- **Construction** — live architecture/object counts and status, recent tile orders, direct mouse buttons for wall, floor, path, door, conduit, workbench, sleeping pod, storage rack, generator and garden placement, plus a paginated research-gated catalogue for all 128 building variants.
- **Production** — workstation capacity and active orders, local power capacity, the legacy component batch, all paginated catalogue recipes, input/output details, shortage/power feedback and completed/pending recipe orders.
- **Research** — strategic Ward Protocol plus a paginated local technology tree with research costs, prerequisite checks, save-persistent completions, and production/building unlocks.
- **Statistics** — settlement activity, needs currently simulated, inventory/deposit totals, strategic holdings, army readiness and Blight metrics.
- **Diplomacy** — faction relationship overview and clickable relation, non-aggression and trade actions.
- **Industry & Tech** — catalogue totals and production chains, plus a paginated unit roster for recruiting researched unit variants with strategic resources and recurring upkeep.
- **Territories** — selected world territory intelligence, Blight pressure, settlement/development, treasury and claim/turn actions.

The dashboard is a first management surface built around live simulation state, not a claim that every desired system already exists. Local technology now gates recipes, catalogue buildings and unit recruitment; strategic Ward Protocol remains a separate campaign research action. Colonists have basic hunger/task/cargo state rather than full skills, health, mood, schedules or needs, and the world and regional campaign remain separate layers. Future work should expand the simulation and preserve mouse-first controls.

