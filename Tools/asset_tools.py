#!/usr/bin/env python3
"""Build the readable art manifest, contact sheet, and validate the raster library."""
from pathlib import Path
import argparse
import json
import sys
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "Assets" / "Art"
MANIFEST = ROOT / "Assets" / "Metadata" / "art_manifest.json"
PREVIEW = ROOT / "Assets" / "Previews" / "contact-sheet.png"

def manifest_entries():
    categories = {
        "ground-": "terrain", "kor-": "kor", "blight-": "blight",
        "alloy-": "resource", "supply-": "resource", "terrain-": "environment",
        "region-": "strategic-map", "strategic-": "strategic-map", "menu-": "interface",
    }
    entries = []
    for path in sorted(ART.glob("*.png")):
        with Image.open(path) as image:
            width, height = image.size
        stem = path.stem
        category = next((value for prefix, value in categories.items() if stem.startswith(prefix)), "interface")
        if stem.startswith("ground-"):
            anchor = [width / 2, height / 2]
        elif stem in ("strategic-map", "kor-hud-metal", "kor-button-red", "kor-button-steel", "menu-foundry-2560"):
            anchor = [0, 0]
        else:
            anchor = [width / 2, height - 18]
        entries.append({
            "assetId": stem,
            "file": f"Assets/Art/{path.name}",
            "category": category,
            "width": width,
            "height": height,
            "anchor": anchor,
            "directions": ["SE"],
            "animations": [{"name": "idle", "frames": [0], "frameDurationMs": 1000, "loop": True}],
            "provenance": "Original Kor-Eternal Frontier artwork generated from Tools/generate_art.py",
        })
    return entries

def build_manifest():
    MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    value = {
        "schemaVersion": 1,
        "projection": {"type": "2.5D isometric", "tileWidth": 54, "tileHeight": 27, "anchorConvention": "bottom-center ground contact; terrain diamonds centered"},
        "palette": {"crimson": "#A42730", "industrialRed": "#962B2D", "charcoal": "#0C0F11", "graphite": "#292F2C", "weatheredSteel": "#777C70", "concrete": "#555B55", "warmGold": "#D9AB4F", "terrain": "weathered olive, stone, and earth", "blight": "dark crimson and organic red"},
        "directionOrder": ["N", "NE", "E", "SE", "S", "SW", "W", "NW"],
        "assets": manifest_entries(),
    }
    MANIFEST.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")

def validate():
    if not MANIFEST.exists():
        print(f"Missing manifest: {MANIFEST.relative_to(ROOT)}. Run: python3 Tools/asset_tools.py --write-manifest", file=sys.stderr)
        return 1
    try:
        data = json.loads(MANIFEST.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        print(f"Invalid manifest {MANIFEST}: {error}", file=sys.stderr)
        return 1
    errors = []
    ids = set()
    listed_files = set()
    for asset in data.get("assets", []):
        name = asset.get("assetId", "<missing id>")
        file = ROOT / asset.get("file", "")
        if name in ids:
            errors.append(f"{name}: duplicate assetId")
        ids.add(name)
        listed_files.add(file.resolve())
        if not file.is_file():
            errors.append(f"{name}: missing file {file}")
            continue
        try:
            with Image.open(file) as image:
                image.verify()
            with Image.open(file) as image:
                if image.size != (asset.get("width"), asset.get("height")):
                    errors.append(f"{name}: manifest says {asset.get('width')}x{asset.get('height')}, image is {image.width}x{image.height}")
                if image.mode not in ("RGBA", "LA", "RGB", "L"):
                    errors.append(f"{name}: unsupported image mode {image.mode}")
                if asset.get("category") not in ("terrain", "kor", "blight", "resource", "environment", "strategic-map", "interface"):
                    errors.append(f"{name}: invalid category {asset.get('category')}")
                # Assets other than baked strategic/interface backgrounds should retain alpha.
                if asset.get("category") not in ("strategic-map", "interface") and "A" not in image.getbands():
                    errors.append(f"{name}: expected an alpha channel for sprite transparency")
                animations = asset.get("animations")
                if not isinstance(animations, list) or not animations:
                    errors.append(f"{name}: animations must be a non-empty list")
                for animation in animations or []:
                    if animation.get("frameDurationMs", 0) <= 0 or not animation.get("frames"):
                        errors.append(f"{name}: animation {animation.get('name')} needs frames and positive timing")
                if asset.get("directions") != ["SE"]:
                    errors.append(f"{name}: unsupported directions; this library currently has authored SE views only")
        except Exception as error:
            errors.append(f"{name}: cannot read image: {error}")
    for file in ART.glob("*.png"):
        if file.resolve() not in listed_files:
            errors.append(f"Unregistered image: {file.relative_to(ROOT)}")
    if errors:
        print("Asset validation failed:")
        for error in errors:
            print(f"  - {error}")
        return 1
    print(f"Validated {len(ids)} manifest entries and PNG files.")
    return 0

def contact_sheet():
    data = json.loads(MANIFEST.read_text(encoding="utf-8"))
    items = []
    for asset in data["assets"]:
        path = ROOT / asset["file"]
        if asset["category"] in ("interface", "strategic-map") and path.stem not in ("strategic-map",):
            continue
        with Image.open(path) as source:
            image = source.convert("RGBA")
            image.thumbnail((220, 170), Image.Resampling.LANCZOS)
        items.append((asset["assetId"], image.copy()))
    cell_w, cell_h, columns = 260, 220, 4
    rows = (len(items) + columns - 1) // columns
    sheet = Image.new("RGB", (cell_w * columns, cell_h * rows), (24, 29, 29))
    draw = ImageDraw.Draw(sheet)
    for index, (label, sprite) in enumerate(items):
        x, y = (index % columns) * cell_w, (index // columns) * cell_h
        draw.rectangle((x+8, y+8, x+cell_w-8, y+cell_h-30), fill=(43,49,48))
        sheet.paste(sprite, (x+(cell_w-sprite.width)//2, y+12+(170-sprite.height)//2), sprite)
        draw.text((x+12, y+cell_h-25), label, fill=(230,218,190))
    PREVIEW.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(PREVIEW)
    print(f"Wrote {PREVIEW.relative_to(ROOT)} ({sheet.width}x{sheet.height}, {len(items)} assets)")

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--write-manifest", action="store_true", help="Rebuild metadata from source PNG dimensions")
    parser.add_argument("--contact-sheet", action="store_true", help="Write the review contact sheet")
    args = parser.parse_args()
    if args.write_manifest:
        build_manifest()
        print(f"Wrote {MANIFEST.relative_to(ROOT)}")
    if args.contact_sheet:
        if not MANIFEST.exists(): build_manifest()
        contact_sheet()
    return validate()

if __name__ == "__main__":
    raise SystemExit(main())
