#!/usr/bin/env python3
"""Assemble a complete, source-backed Thief art approval sheet."""

import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageOps


TIERS = ("street", "burglar", "guild", "masterwork", "locksmith", "nightblade", "wayfarer")
ARMOR = ("coat", "hood", "boots", "charm")
ARTIFACTS = ("thief_twins_skeleton_key", "thief_twins_candles_end", "thief_bow_unlost_road")
INK = (29, 34, 41)
PANEL = (42, 49, 57)
LINE = (79, 88, 96)
WHITE = (240, 235, 223)
MUTED = (177, 186, 190)
ACCENT = (181, 106, 86)
ROOT = Path(__file__).resolve().parents[3]


def portable_path(path):
    """Store a repository-relative source path in the review receipt."""
    return path.resolve().relative_to(ROOT).as_posix()


def font(size, bold=False):
    roots = (
        "/System/Library/Fonts/Supplemental/Arial Bold.ttf" if bold else "/System/Library/Fonts/Supplemental/Arial.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf" if bold else "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
    )
    for path in roots:
        try:
            return ImageFont.truetype(path, size)
        except OSError:
            pass
    return ImageFont.load_default()


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def label(draw, xy, value, size=28, color=WHITE, bold=False):
    draw.text(xy, value, fill=color, font=font(size, bold))


def image_tile(canvas, draw, path, box, caption, files, missing, key, background=PANEL):
    x, y, w, h = box
    draw.rounded_rectangle((x, y, x + w, y + h), radius=12, fill=background, outline=LINE, width=2)
    label(draw, (x + 16, y + 12), caption, 24, WHITE, True)
    inner = (x + 12, y + 48, w - 24, h - 60)
    if not path or not path.exists():
        missing.append(key)
        label(draw, (x + 20, y + h // 2), "RENDER PENDING", 22, ACCENT, True)
        return
    source = Image.open(path).convert("RGBA")
    source.thumbnail((inner[2], inner[3]), Image.Resampling.LANCZOS)
    ix = inner[0] + (inner[2] - source.width) // 2
    iy = inner[1] + (inner[3] - source.height) // 2
    canvas.alpha_composite(source, (ix, iy))
    files[key] = {"path": portable_path(path), "sha256": digest(path)}


def icon_tile(canvas, draw, path, box, caption, files, missing, key):
    x, y, w, h = box
    draw.rounded_rectangle((x, y, x + w, y + h), radius=10, fill=PANEL, outline=LINE, width=2)
    if not path.exists():
        missing.append(key)
        label(draw, (x + 10, y + 20), "MISSING", 18, ACCENT, True)
    else:
        source = Image.open(path).convert("RGBA")
        source = ImageOps.contain(source, (w - 24, h - 58), Image.Resampling.LANCZOS)
        canvas.alpha_composite(source, (x + (w - source.width) // 2, y + 8))
        files[key] = {"path": portable_path(path), "sha256": digest(path)}
    label(draw, (x + 8, y + h - 42), caption, 18, WHITE)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", type=Path, default=Path("marketplace/packages/thief"))
    parser.add_argument("--renders", type=Path, required=True,
                        help="Directory with armor-TIER-{male,female}.png and weapon-ID.png")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--require-complete", action="store_true")
    args = parser.parse_args()

    package = args.package.resolve()
    render_dir = args.renders.resolve()
    entries = json.loads((package / "content.json").read_text())["entries"]
    by_id = {entry["id"]: entry for entry in entries}
    icons = {entry["icon"] for entry in entries if entry.get("icon")}
    assert len(entries) == 54 and len(icons) == 46, "Thief inventory changed; update board coverage"

    width, row_height, margin = 4350, 635, 40
    title_height, artifact_height, footer_height = 175, 610, 270
    height = title_height + row_height * len(TIERS) + artifact_height + footer_height
    canvas = Image.new("RGBA", (width, height), INK + (255,))
    draw = ImageDraw.Draw(canvas)
    files, missing = {}, []

    label(draw, (margin, 35), "THIEF  |  COMPLETE ART REVIEW", 64, WHITE, True)
    label(draw, (margin, 112), "Exported mesh renders and every distinct package icon  •  approval requested", 28, MUTED)

    for index, tier in enumerate(TIERS):
        y = title_height + index * row_height
        draw.rectangle((0, y, width, y + row_height), fill=INK if index % 2 == 0 else (34, 40, 47))
        label(draw, (margin, y + 20), f"{index + 1:02d}  {tier.upper()}", 40, WHITE, True)
        armor_y = y + 86
        for sex, x in (("male", 40), ("female", 610)):
            key = f"armor-{tier}-{sex}"
            image_tile(canvas, draw, render_dir / f"{key}.png", (x, armor_y, 540, 490),
                       f"Full set  •  {sex}", files, missing, key)
        for family, x in (("twins", 1180), ("bow", 1740)):
            item_id = f"thief_{family}_{tier}"
            if item_id not in by_id:
                raise ValueError(f"Missing entry {item_id}")
            key = f"weapon-{item_id}"
            image_tile(canvas, draw, render_dir / f"{key}.png", (x, armor_y, 540, 490),
                       by_id[item_id]["displayName"], files, missing, key)
        icon_ids = [f"thief_twins_{tier}", f"thief_bow_{tier}"] + [f"thief_{family}_{tier}" for family in ARMOR]
        for j, item_id in enumerate(icon_ids):
            entry = by_id[item_id]
            key = "icon-" + item_id
            icon_tile(canvas, draw, package / entry["icon"],
                      (2315 + (j % 3) * 660, armor_y + (j // 3) * 245, 630, 230),
                      entry["displayName"], files, missing, key)

    y = title_height + len(TIERS) * row_height
    label(draw, (margin, y + 20), "ARTIFACT WEAPONS", 40, WHITE, True)
    for j, item_id in enumerate(ARTIFACTS):
        entry = by_id[item_id]
        x = margin + j * 1430
        key = f"weapon-{item_id}"
        image_tile(canvas, draw, render_dir / f"{key}.png", (x, y + 84, 820, 455),
                   entry["displayName"], files, missing, key)
        icon_tile(canvas, draw, package / entry["icon"], (x + 835, y + 84, 565, 455),
                  entry["displayName"], files, missing, "icon-" + item_id)

    y += artifact_height
    label(draw, (margin, y + 16), "CLASS ICON", 38, WHITE, True)
    class_entry = by_id["thief"]
    icon_tile(canvas, draw, package / class_entry["icon"], (margin, y + 70, 260, 170),
              "Slip Away", files, missing, "icon-thief-slip-away")
    label(draw, (345, y + 100), "All 45 equipment icons + Slip Away shown above.", 28, MUTED)
    label(draw, (345, y + 145), "Other action entries reuse their associated weapon icon.", 28, MUTED)

    expected = 7 * (2 + 2 + 6) + 3 * 2 + 1
    assert len(files) + len(missing) == expected
    args.output.parent.mkdir(parents=True, exist_ok=True)
    canvas.convert("RGB").save(args.output, quality=95)
    report = {
        "schema": "ftkmf.thief-art-approval-board.v1",
        "packageContentSha256": digest(package / "content.json"),
        "image": portable_path(args.output),
        "imageSha256": digest(args.output),
        "expectedTiles": expected,
        "renderedTiles": len(files),
        "missing": missing,
        "sources": files,
        "scope": "Presentation of authored exported geometry and package icons; native fit and motion are separate evidence.",
    }
    args.output.with_suffix(".json").write_text(json.dumps(report, indent=2) + "\n")
    print(f"Board: {args.output}  tiles: {len(files)}/{expected}  missing: {len(missing)}")
    if args.require_complete and missing:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
