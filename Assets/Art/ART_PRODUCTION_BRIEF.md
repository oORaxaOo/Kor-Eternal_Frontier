# Kor-Eternal Frontier — Art Production Brief

This brief defines the quality gate for replacing the current procedurally painted placeholders with production game art. The existing PNGs are placeholders and must not be described as equivalent to the requested reference quality.

## Direction

Create an original, high-detail industrial RTS presentation for the Kor frontier: battered steel, concrete, tracked machinery, crimson Kor markings, restrained amber work lights, dusty terrain, smoke, debris, and a distinct organic crimson Blight. Aim for the visual finish of carefully art-directed pre-rendered RTS assets from the classic era, presented in a modern, high-resolution interface. Use a consistent three-quarter/isometric camera and physically coherent lighting, materials, scale, and shadows across all assets.

The user's 1536×1024 RTS screenshot in Downloads is a **composition and information-density reference only**: a large unobstructed battlefield, dense command sidebar, compact resource header, minimap, and selection/mission panel. Do not copy its SCP logo, symbols, names, units, buildings, UI text, layout pixel-for-pixel, or any other protected content. The Kor character concept in Downloads may guide Lilith's identity and palette; do not use it to design battlefield units.

## Non-negotiable art acceptance rules

- No simple vector shapes, primitive-painted silhouettes, emoji, stock icon packs, or enlarged low-resolution placeholders as final game art.
- Units and structures must read as detailed, materially lit, hand-designed assets at their actual in-game size. Silhouette, faction markings, weapon, facing, contact shadow, and scale must remain legible at 100% game zoom.
- Use original Kor and Blight designs. Do not trace or reproduce C&C, Age of Empires, SCP, or other game assets, logos, maps, or distinctive UI.
- Keep a shared camera elevation, light direction, palette, ground contact, and detail density for every unit/building in a set.
- Art source files stay at or above the sizes below. Runtime atlases may be packed/optimized, but may not upscale small source art and call it high-resolution.
- No generated labels, fake interface text, watermarks, transparent checkerboards baked into alpha assets, detached shadows, clipped geometry, or inconsistent number of frames.
- Review every delivered asset in the real game at native scale, in its correct lighting and UI context, before it passes.

## Asset batches and specifications

Produce each item as its own image task. Keep transparent alpha for cutouts. Do not ask one image task to render an entire game UI with text or many unrelated sprites.

### 1. Tactical terrain and battlefield backdrop

- 4096×4096 seamless or tileable terrain material set: dry Kor frontier soil, compacted tracks, gravel, scorched ground, and Blight-overrun soil.
- 4096×2048 environment plates for campaign/menu backdrops, with no text, insignia, or UI baked in.
- Sixteen authored terrain decals at 1024×1024: tire trails, shell craters, rubble, weeds, oil, scorch, and Blight growth.
- Style: high-detail, grounded pre-rendered RTS terrain; layered material breakup; restrained contrast that preserves unit readability.

### 2. Kor unit renders

Use separate transparent renders, not a contact sheet. For each unit, make 8 compass facings with consistent scale, camera, lighting, ground anchor, and frame alignment. Create at least idle (4 frames), walk (8 frames), attack (6 frames), and death (6 frames) before calling the unit production-ready.

- Pioneer: armored field engineer with visible pack, tool, and Kor crimson cloth/marking.
- Guardian: medium infantry with readable armor and original weapon silhouette.
- Purifier: heavy support operator with containment equipment and industrial protective gear.
- Sentinel tank: original tracked armored vehicle with readable turret/barrel and layered steel details.
- Engineer vehicle/hauler: original construction/logistics vehicle, clearly distinct from the tank.

Target source cells: infantry 512×512 minimum; vehicles 1024×1024 minimum. Deliver aligned PNG frames and a machine-readable atlas manifest containing frame size, pivot, direction, animation, and frame duration.

### 3. Blight entities

- Stalker: original asymmetrical organic creature, low stance, crimson/dark maroon tissue, visible movement appendages.
- Heavy organism: larger distinct silhouette with armored growth plates and a readable attack posture.
- Nest and growth nodes: grounded environmental structures that visibly infect surrounding terrain.

Use the same 8-direction animation and transparent-alpha requirements as Kor units. Blight must look biological and hostile, never like a recolored Kor unit.

### 4. Structures

Create separate transparent isometric renders, minimum 2048×2048 source per building, with consistent footprint, camera, lighting, and north-facing orientation. Include subtle damage states (intact, damaged, critical, destroyed) and at least two original construction stages for major buildings.

- Command habitat, barracks, alloy extractor, supply granary, power plant, research archive, defensive turret, purifier/containment station.
- Add original Kor signage as clean geometry or texture decals, not generated text.
- Deliver footprint/pivot metadata, shadow policy, and a separate UI icon crop for each structure.

### 5. UI and portraits

- Create a coherent dark stamped-steel command interface with original Kor borders, rivets, restrained crimson plates, readable cream text areas, and amber status lights.
- UI textures are clean, tileable, text-free assets. Render all interface text with the game's font system, not inside generated images.
- Produce an original Lilith bust portrait based on the user's provided character reference: no crown, same face/glasses/hood/cloak identity, authoritative advisor expression, lighting consistent with the game. Minimum 2048×2048 source, transparent background.
- Create original unit/building portraits and small resource glyphs from the corresponding production art; never substitute generic emoji.

## Ready-to-import delivery layout

```text
Assets/Art/Production/
  Terrain/                 4K textures and decals
  Units/Kor/<unit>/<anim>/ aligned direction frames
  Units/Blight/<unit>/<anim>/ aligned direction frames
  Buildings/<building>/    renders and damage/construction states
  UI/                      border/material textures and portraits
  manifests/               frame pivots, dimensions, IDs, footprints
  source/                  untouched selected full-resolution originals
```

Use stable lowercase IDs, preserve untouched source images, and derive optimized runtime PNG atlases in a reproducible build step. Keep generation prompts and selected/rejected variants beside the asset manifest for traceability.

## Release gate

Do not call the art pass complete until all of the following are reviewed in an actual captured game frame at 2560×1440 and at 1920×1080 UI scale:

1. A terrain-only battle scene has layered detail, useful negative space, and no obvious tile seams.
2. A mixed Kor formation contains multiple independently readable detailed sprites with matching camera and ground contact.
3. Kor buildings form a coherent settlement and remain legible beside units.
4. Blight growth and creatures look materially and compositionally distinct from Kor assets.
5. The selection panel portrait, minimap, resource header, build cards, and command states look like one authored product interface.
6. No asset is visibly stretched, blurred, clipped, incorrectly pivoted, or missing alpha at gameplay scale.
7. A side-by-side review confirms the composition and finish target without copied protected artwork or UI.

Until a connected bitmap-generation/illustration workflow or a commissioned art source supplies these assets, this brief is an implementation-ready art specification, not a claim that the visual target has been reached.
