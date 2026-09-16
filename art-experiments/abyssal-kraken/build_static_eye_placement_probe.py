#!/usr/bin/env python3
"""Build an original static-eye placement probe for the modern Kraken.

This is an isolated calibration artifact. It uses colored, low-poly markers in
the selected MeshFilter's local space to map which authored offsets survive
occlusion and where they project relative to our original V2 head. It never
opens the native static mesh or uses its surface data.
"""
from __future__ import annotations

import hashlib
import json
import struct
import sys
from pathlib import Path

import numpy as np
from PIL import Image

OUT = Path(__file__).resolve().parent
ROOT = OUT.parent.parent
sys.path.insert(0, str(ROOT / "tools/ai-model-pipeline"))
from export_ftk_glb import write_static_glb
from build_static_eye_v4 import StaticSurface, accessor, parse_glb
from build_static_eye_v5 import derive_head_core_anchor


ASSET = "abyssal-crown-kraken-eye-placement-probe-v1"
GLB = OUT / f"{ASSET}.glb"
TEXTURE = OUT / f"{ASSET}.png"
SOURCE = OUT / f"{ASSET}.source.json"
PIECES = OUT / f"{ASSET}.pieces.json"
VALIDATION = OUT / f"{ASSET}.validation.json"


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def paint_texture() -> None:
    """Write a six-color original atlas with one band per named marker."""
    colors = [
        (33, 198, 208),   # P0 target pivot
        (235, 75, 163),   # P1 V2 core anchor
        (239, 181, 53),   # P2 local +Z
        (228, 94, 51),    # P3 local -Z
        (109, 221, 87),   # P4 local +X
        (148, 94, 235),   # P5 local +Y
    ]
    pixels = np.zeros((384, 384, 3), dtype=np.uint8)
    for index, color in enumerate(colors):
        low = index * pixels.shape[0] // len(colors)
        high = (index + 1) * pixels.shape[0] // len(colors)
        pixels[low:high] = color
        # Intentional pale diagonal lets a marker's rotation remain visible.
        for x in range(pixels.shape[1]):
            y = low + (x * 17 + index * 13) % max(1, high - low)
            pixels[max(low, y - 1):min(high, y + 2), x] = np.minimum(255, np.array(color) + 35)
    Image.fromarray(pixels, "RGB").save(TEXTURE)


def validate() -> dict:
    root, binary = parse_glb(GLB)
    assert len(root["meshes"]) == 1 and "skins" not in root
    primitive = root["meshes"][0]["primitives"]
    assert len(primitive) == 1
    primitive = primitive[0]
    attributes = primitive["attributes"]
    assert primitive.get("mode", 4) == 4
    assert set(attributes) == {"POSITION", "NORMAL", "TEXCOORD_0"}
    assert "JOINTS_0" not in attributes and "WEIGHTS_0" not in attributes
    positions = accessor(root, binary, attributes["POSITION"], lambda n: (n, 3), "<f4")
    normals = accessor(root, binary, attributes["NORMAL"], lambda n: (n, 3), "<f4")
    uvs = accessor(root, binary, attributes["TEXCOORD_0"], lambda n: (n, 2), "<f4")
    indices = accessor(root, binary, primitive["indices"], lambda n: (n,), "<u2")
    source = json.loads(SOURCE.read_text())
    assert np.allclose(positions, np.asarray(source["positions"], dtype=np.float32))
    assert np.allclose(normals, np.asarray(source["normals"], dtype=np.float32))
    assert np.allclose(uvs, np.asarray(source["uvs"], dtype=np.float32))
    assert np.array_equal(indices.reshape(-1, 3), np.asarray(source["triangles"], dtype=np.uint16))
    return {
        "status": "PASS_ABYSSAL_KRAKEN_STATIC_EYE_PLACEMENT_PROBE_CONTRACT",
        "asset": ASSET,
        "vertices": int(len(positions)),
        "triangles": int(len(indices) // 3),
        "bounds": {"min": positions.min(axis=0).tolist(), "max": positions.max(axis=0).tolist()},
        "glbSha256": sha(GLB),
        "textureSha256": sha(TEXTURE),
        "sourceSha256": sha(SOURCE),
        "noSkinAttributes": True,
        "scope": "Static GLB contract and named placement markers only. A fresh isolated screenshot is needed to determine visibility and screen projection.",
    }


def main() -> None:
    anchor, anchor_info = derive_head_core_anchor()
    # Offsets intentionally span target pivot, the own-head anchor, and three
    # eye-local axes. They are authored values, not native static mesh data.
    markers = [
        ("P0 target pivot cyan", np.array([0.0, 0.0, 0.0]), .23),
        ("P1 V2 head-core magenta", anchor, .23),
        ("P2 head-core plus-Z gold", anchor + np.array([0.0, 0.0, 1.25]), .19),
        ("P3 head-core minus-Z orange", anchor + np.array([0.0, 0.0, -1.25]), .19),
        ("P4 head-core plus-X lime", anchor + np.array([1.00, 0.0, 0.0]), .19),
        ("P5 head-core plus-Y violet", anchor + np.array([0.0, 1.00, 0.0]), .19),
    ]
    surface = StaticSurface()
    atlas_height = 1.0 / len(markers)
    for index, (label, center, radius) in enumerate(markers):
        surface.ellipsoid(
            label, center, [radius, radius, radius], rings=5, segments=8,
            atlas=(0.0, index * atlas_height + .006, 1.0, (index + 1) * atlas_height - .006),
        )
    data = surface.data()
    data["placementProbe"] = {
        "markers": [{"label": label, "eyeLocalCenter": center.tolist(), "radius": radius} for label, center, radius in markers],
        "anchor": anchor_info,
        "authoringPurpose": "Map live screen placement and occlusion for a MeshRenderer child without reading its native mesh surface.",
    }
    data["authoringBoundary"] = (
        "Original colored calibration markers. No native static mesh surface, bounds, texture pixels, UVs, skin weights, or animation data are read. "
        "The V2 head-core reference uses permitted palette metadata and our own original V2 landmark."
    )
    paint_texture()
    SOURCE.write_text(json.dumps(data, separators=(",", ":")) + "\n")
    PIECES.write_text(json.dumps(surface.pieces, indent=2) + "\n")
    write_static_glb(GLB, data)
    result = validate()
    result["piecesSha256"] = sha(PIECES)
    result["generatorSha256"] = sha(Path(__file__))
    VALIDATION.write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
