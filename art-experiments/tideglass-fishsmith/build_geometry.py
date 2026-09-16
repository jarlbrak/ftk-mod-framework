#!/usr/bin/env python3
"""Build Tideglass Fishsmith from original geometry on the native Fish avatar rigs.

The generator reads only ordered bone names and inverse bind matrices from local
references. Every surface, palette, UV, and skin influence in this package is
original authoring; it does not reuse native mesh geometry, textures, UVs,
weights, or animation samples.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path
import sys

import numpy as np
from PIL import Image, ImageDraw

ROOT = next(path for path in Path(__file__).resolve().parents
            if (path / "FTKModFramework").is_dir() and (path / "tools").is_dir())
PACKAGE = ROOT / "art-experiments" / "tideglass-fishsmith"
BODY_REFERENCE = ROOT / "scratch" / "skeleton-audit" / "121366" / "reference.npz"
HAIR_TOP_REFERENCE = ROOT / "scratch" / "skeleton-audit" / "121490" / "reference.npz"
HAIR_BOTTOM_REFERENCE = ROOT / "scratch" / "skeleton-audit" / "121521" / "reference.npz"
sys.path.insert(0, str(ROOT / "tools" / "ai-model-pipeline"))
from export_ftk_glb import write_glb
from validate_glb import validate

# Deep water, oxidised metal, and bioluminescent coral. Palette cells are the
# only texture source used by all three original meshes.
PALETTE = [
    "071C2B",  # trench shadow
    "0D4050",  # blue-black steel
    "176A72",  # tideglass teal
    "2A9E91",  # seafoam scale
    "64D1B6",  # lantern mint
    "C5F2D9",  # foam highlight
    "18324B",  # wet slate
    "246E9C",  # blue scale
    "C17A38",  # oxidised copper
    "E7B65D",  # brass glint
    "EF7355",  # reef coral
    "5B3069",  # abyss violet
    "E8FBFF",  # lantern white
]

BODY_BONES = [
    "BackB_M", "Chest_M", "Scapula_R", "Shoulder_R", "Elbow_R", "Wrist_R",
    "MiddleFinger1_R", "MiddleFinger2_R", "MiddleFinger3_R",
    "ThumbFinger1_R", "ThumbFinger2_R", "ThumbFinger3_R", "Neck_M", "Head_M",
    "Hair_M", "Scapula_L", "Shoulder_L", "Elbow_L", "Wrist_L",
    "MiddleFinger1_L", "MiddleFinger2_L", "MiddleFinger3_L",
    "ThumbFinger1_L", "ThumbFinger2_L", "ThumbFinger3_L",
]
HAIR_TOP_BONES = ["Chest_M", "Scapula_R", "Neck_M", "Head_M", "Hair_M", "Scapula_L"]
HAIR_BOTTOM_BONES = ["Chest_M", "Scapula_R", "Shoulder_R", "Neck_M", "Head_M", "Scapula_L", "Shoulder_L"]


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def normalized(influences: dict[str, float] | str) -> dict[str, float]:
    result = {influences: 1.0} if isinstance(influences, str) else dict(influences)
    if not 1 <= len(result) <= 4 or any(not math.isfinite(value) or value <= 0 for value in result.values()):
        raise ValueError(f"Invalid authored weights: {result}")
    total = sum(result.values())
    return {bone: value / total for bone, value in result.items()}


class Surface:
    """Original faceted skinned sculpture, with one palette stripe per face."""

    def __init__(self, names: list[str], centers: np.ndarray) -> None:
        self.names = names
        self.B = {name: np.asarray(center, dtype=float) for name, center in zip(names, centers)}
        self.data: dict[str, list] = {
            "positions": [], "normals": [], "uvs": [], "triangles": [],
            "joints": [], "weights": [], "bone_names": names,
        }
        self.pieces: list[dict[str, object]] = []

    @staticmethod
    def color(value: int | list[int], offset: int = 0) -> int:
        return value[offset % len(value)] if isinstance(value, list) else value

    def triangle(self, points: list[np.ndarray], skins: list[dict[str, float] | str], color: int) -> None:
        points = [np.asarray(point, dtype=float) for point in points]
        cross = np.cross(points[1] - points[0], points[2] - points[0])
        length = float(np.linalg.norm(cross))
        if length < 1e-9:
            raise ValueError("Degenerate authored triangle")
        start = len(self.data["positions"])
        self.data["triangles"].append([start, start + 1, start + 2])
        uv = [(color + 0.5) / len(PALETTE), 0.5]
        for point, skin in zip(points, skins):
            values = normalized(skin)
            if any(bone not in self.B for bone in values):
                raise ValueError(f"Unknown rig bone in authored weight: {values}")
            pairs = [(self.names.index(bone), weight) for bone, weight in values.items()]
            pairs += [(0, 0.0)] * (4 - len(pairs))
            self.data["positions"].append(point.tolist())
            self.data["normals"].append((cross / length).tolist())
            self.data["uvs"].append(uv)
            self.data["joints"].append([joint for joint, _ in pairs])
            self.data["weights"].append([weight for _, weight in pairs])

    def tube(
        self, label: str, points: list[np.ndarray], radii_x: list[float], radii_z: list[float],
        skins: list[dict[str, float] | str], colors: int | list[int], *, sides: int = 8,
        axis: tuple[float, float, float] = (1.0, 0.0, 0.0),
    ) -> None:
        if not (len(points) >= 2 and len(points) == len(radii_x) == len(radii_z) == len(skins)):
            raise ValueError(label)
        start = len(self.data["positions"])
        points = [np.asarray(point, dtype=float) for point in points]
        rings: list[list[np.ndarray]] = []
        for index, point in enumerate(points):
            tangent = points[min(index + 1, len(points) - 1)] - points[max(index - 1, 0)]
            tangent /= np.linalg.norm(tangent)
            u = np.asarray(axis, dtype=float)
            u -= tangent * np.dot(u, tangent)
            if np.linalg.norm(u) < 1e-6:
                u = np.cross(tangent, np.array([0.0, 0.0, 1.0]))
            u /= np.linalg.norm(u)
            v = np.cross(tangent, u)
            rings.append([
                point + u * radii_x[index] * math.cos(step * math.tau / sides)
                + v * radii_z[index] * math.sin(step * math.tau / sides)
                for step in range(sides)
            ])
        for row in range(len(points) - 1):
            for side in range(sides):
                next_side = (side + 1) % sides
                color = self.color(colors, row + side)
                self.triangle([rings[row][side], rings[row][next_side], rings[row + 1][next_side]],
                              [skins[row], skins[row], skins[row + 1]], color)
                self.triangle([rings[row][side], rings[row + 1][next_side], rings[row + 1][side]],
                              [skins[row], skins[row + 1], skins[row + 1]], color)
        for side in range(sides):
            next_side = (side + 1) % sides
            self.triangle([points[0], rings[0][next_side], rings[0][side]], [skins[0]] * 3, self.color(colors, side))
            self.triangle([points[-1], rings[-1][side], rings[-1][next_side]], [skins[-1]] * 3, self.color(colors, side + 1))
        self.pieces.append({"name": label, "vertex_start": start, "vertex_count": len(self.data["positions"]) - start})

    def ellipsoid(
        self, label: str, center: np.ndarray, radii: tuple[float, float, float],
        skin: dict[str, float] | str, colors: int | list[int], *, sides: int = 10,
    ) -> None:
        center = np.asarray(center, dtype=float)
        factors = [0.10, 0.64, 1.0, 0.64, 0.10]
        heights = [-0.95, -0.60, 0.0, 0.60, 0.95]
        self.tube(label, [center + np.array([0.0, height * radii[1], 0.0]) for height in heights],
                  [factor * radii[0] for factor in factors], [factor * radii[2] for factor in factors],
                  [skin] * len(factors), colors, sides=sides)

    def blade(
        self, label: str, points: list[np.ndarray], widths: list[float], thicknesses: list[float],
        skins: list[dict[str, float] | str], colors: int | list[int], *, axis=(1.0, 0.0, 0.0),
    ) -> None:
        """A closed flattened fin or sail; unlike a decorative plane it has volume."""
        self.tube(label, points, widths, thicknesses, skins, colors, sides=4, axis=axis)


def write_palette(path: Path) -> None:
    tile_width, height = 72, 96
    image = Image.new("RGB", (tile_width * len(PALETTE), height), "black")
    for index, encoded in enumerate(PALETTE):
        color = tuple(int(encoded[offset:offset + 2], 16) for offset in (0, 2, 4))
        tile = Image.new("RGB", (tile_width, height), color)
        draw = ImageDraw.Draw(tile)
        light = tuple(min(255, int(channel * 1.26 + 8)) for channel in color)
        dark = tuple(max(0, int(channel * 0.52)) for channel in color)
        for shift in range(-65, 140, 18):
            draw.arc((shift, 8, shift + 58, 70), 190, 348, fill=light, width=3)
        for line in range(10, height, 24):
            draw.line((0, line, tile_width, line - 8), fill=dark, width=2)
        draw.line((0, height - 6, tile_width, height - 6), fill=dark, width=5)
        image.paste(tile, (index * tile_width, 0))
    image.save(path)


def body_surface(names: list[str], bindposes: np.ndarray) -> Surface:
    if names != BODY_BONES:
        raise ValueError("Fish body palette drifted")
    surface = Surface(names, np.linalg.inv(bindposes)[:, :3, 3])
    B = surface.B
    back, chest, neck, head, hair = (B[key] for key in ("BackB_M", "Chest_M", "Neck_M", "Head_M", "Hair_M"))

    # A heavy tideglass coat gives the body a readable silhouette above the
    # native Blacksmith clothes without relying on their surfaces for its shape.
    surface.tube(
        "Tideglass pressure coat",
        [back + np.array([0.0, -0.36, -0.015]), back + np.array([0.0, -0.06, 0.005]),
         chest + np.array([0.0, 0.16, 0.01]), chest + np.array([0.0, 0.31, 0.02])],
        [0.25, 0.32, 0.37, 0.28], [0.19, 0.25, 0.28, 0.22],
        ["BackB_M", {"BackB_M": 0.62, "Chest_M": 0.38}, "Chest_M", {"Chest_M": 0.72, "Neck_M": 0.28}],
        [0, 1, 2, 1, 3], sides=12,
    )
    surface.ellipsoid("Brass breastplate", chest + np.array([0.0, 0.035, 0.305]),
                      (0.28, 0.24, 0.070), "Chest_M", [8, 9, 8, 8, 9], sides=12)
    surface.ellipsoid("Lantern core", chest + np.array([0.0, 0.035, 0.395]),
                      (0.055, 0.075, 0.020), "Chest_M", [8, 9, 10, 9], sides=8)
    surface.tube(
        "Copper pressure collar", [chest + np.array([0.0, 0.23, 0.02]), neck + np.array([0.0, -0.025, 0.035])],
        [0.31, 0.23], [0.25, 0.19], [{"Chest_M": 0.62, "Neck_M": 0.38}, "Neck_M"], [8, 9, 8, 9], sides=12,
    )
    # The native Blacksmith helm already supplies a strong horn silhouette. Keep
    # this shell compact and dark so it reads as a fish visor beneath that helm,
    # instead of as a second oversized pale mask.
    surface.ellipsoid("Compact tideglass fish visor", head + np.array([0.0, 0.055, 0.11]),
                      (0.255, 0.265, 0.235), {"Neck_M": 0.18, "Head_M": 0.82}, [1, 2, 3, 2, 1], sides=13)
    surface.ellipsoid("Tapered tideglass snout", head + np.array([0.0, 0.015, 0.355]),
                      (0.180, 0.120, 0.070), "Head_M", [1, 2, 3, 2], sides=11)
    surface.blade("Copper gill plate right", [head + np.array([0.175, 0.035, 0.20]), head + np.array([0.245, -0.065, 0.15])],
                  [0.075, 0.020], [0.026, 0.008], ["Head_M", "Head_M"], [8, 9, 10], axis=(0.0, 1.0, 0.0))
    surface.blade("Copper gill plate left", [head + np.array([-0.175, 0.035, 0.20]), head + np.array([-0.245, -0.065, 0.15])],
                  [0.075, 0.020], [0.026, 0.008], ["Head_M", "Head_M"], [8, 9, 10], axis=(0.0, 1.0, 0.0))
    for sign in (-1, 1):
        surface.ellipsoid("Brass eye slit " + str(sign),
                          head + np.array([sign * 0.095, 0.115, 0.405]), (0.030, 0.028, 0.012),
                          "Head_M", [8, 9, 10], sides=8)
    surface.blade(
        "Back sea-mantle", [back + np.array([0.0, -0.14, -0.18]), back + np.array([0.0, 0.06, -0.27]),
                              chest + np.array([0.0, 0.22, -0.24])],
        [0.25, 0.38, 0.28], [0.055, 0.075, 0.042],
        ["BackB_M", {"BackB_M": 0.48, "Chest_M": 0.52}, "Chest_M"], [11, 1, 2, 3], axis=(1.0, 0.0, 0.0),
    )
    surface.ellipsoid("Lantern crest cap", hair + np.array([0.0, -0.19, 0.07]),
                      (0.11, 0.11, 0.08), "Hair_M", [8, 9, 10, 9], sides=8)

    for suffix, sign in (("R", 1), ("L", -1)):
        scapula, shoulder, elbow, wrist = [B[f"{part}_{suffix}"] for part in ("Scapula", "Shoulder", "Elbow", "Wrist")]
        surface.tube(
            "Scale pauldron bridge " + suffix,
            [chest + np.array([sign * 0.17, 0.19, 0.00]), scapula, shoulder],
            [0.19, 0.25, 0.23], [0.18, 0.24, 0.22],
            [{"Chest_M": 0.58, f"Scapula_{suffix}": 0.42}, {f"Scapula_{suffix}": 0.64, f"Shoulder_{suffix}": 0.36}, f"Shoulder_{suffix}"],
            [1, 2, 3, 2, 8], sides=9,
        )
        surface.ellipsoid("Scale pauldron " + suffix, shoulder + np.array([0.0, 0.025, 0.055]),
                          (0.23, 0.22, 0.22), f"Shoulder_{suffix}", [1, 2, 3, 2, 8], sides=11)
        surface.tube(
            "Segmented upper arm " + suffix,
            [shoulder, (shoulder + elbow) / 2 + np.array([0.0, 0.018, 0.055]), elbow],
            [0.175, 0.20, 0.15], [0.17, 0.19, 0.15],
            [f"Shoulder_{suffix}", {f"Shoulder_{suffix}": 0.48, f"Elbow_{suffix}": 0.52}, f"Elbow_{suffix}"],
            [1, 2, 3, 2, 8], sides=9,
        )
        surface.ellipsoid("Copper elbow " + suffix, elbow + np.array([0.0, 0.0, 0.07]),
                          (0.17, 0.18, 0.17), f"Elbow_{suffix}", [8, 9, 10, 9], sides=8)
        surface.tube(
            "Tideglass forearm " + suffix,
            [elbow, (elbow + wrist) / 2 + np.array([0.0, -0.012, 0.075]), wrist],
            [0.15, 0.19, 0.14], [0.15, 0.18, 0.14],
            [f"Elbow_{suffix}", {f"Elbow_{suffix}": 0.46, f"Wrist_{suffix}": 0.54}, f"Wrist_{suffix}"],
            [0, 1, 2, 1, 3], sides=9,
        )
        surface.ellipsoid("Forging glove " + suffix, wrist + np.array([sign * 0.075, 0.0, 0.09]),
                          (0.16, 0.13, 0.14), f"Wrist_{suffix}", [1, 2, 7, 8], sides=8)
        for finger in ("MiddleFinger", "ThumbFinger"):
            points = [B[f"{finger}{index}_{suffix}"] for index in (1, 2, 3)]
            tip = points[-1] + (points[-1] - points[-2]) * 0.60
            bones = [f"{finger}{index}_{suffix}" for index in (1, 2, 3)]
            surface.tube(
                f"Copper {finger} digits {suffix}", [points[0], points[1], points[2], tip],
                [0.070, 0.055, 0.040, 0.018], [0.063, 0.050, 0.036, 0.016],
                [{f"Wrist_{suffix}": 0.20, bones[0]: 0.80}, {bones[0]: 0.30, bones[1]: 0.70},
                 {bones[1]: 0.28, bones[2]: 0.72}, bones[2]], [8, 9, 10, 9, 3], sides=6,
            )
    return surface


def hair_top_surface(names: list[str], bindposes: np.ndarray) -> Surface:
    if names != HAIR_TOP_BONES:
        raise ValueError("Fish hair-top palette drifted")
    surface = Surface(names, np.linalg.inv(bindposes)[:, :3, 3])
    B = surface.B
    surface.blade(
        "Crested tideglass sail",
        [B["Head_M"] + np.array([0.0, 0.10, -0.015]), B["Hair_M"] + np.array([0.0, -0.26, 0.025]),
         B["Hair_M"] + np.array([0.0, -0.04, 0.09])],
        [0.13, 0.15, 0.018], [0.030, 0.034, 0.008],
        [{"Head_M": 0.75, "Hair_M": 0.25}, "Hair_M", "Hair_M"], [1, 2, 3, 2, 8], axis=(1.0, 0.0, 0.0),
    )
    surface.ellipsoid("Lantern crest socket", B["Hair_M"] + np.array([0.0, -0.18, 0.085]),
                      (0.090, 0.090, 0.065), "Hair_M", [8, 9, 10, 9], sides=8)
    surface.tube(
        "Riveted brow band", [B["Head_M"] + np.array([0.0, -0.07, 0.24]), B["Head_M"] + np.array([0.0, 0.10, 0.27])],
        [0.27, 0.29], [0.048, 0.052], ["Head_M", "Head_M"], [8, 9, 10], sides=9,
    )
    for suffix, sign in (("R", 1), ("L", -1)):
        surface.tube(
            "Tideglass shoulder harness " + suffix,
            [B["Neck_M"] + np.array([sign * 0.12, -0.03, -0.05]),
             B[f"Scapula_{suffix}"] + np.array([0.0, 0.04, -0.08]),
             B["Chest_M"] + np.array([sign * 0.19, 0.04, -0.10])],
            [0.065, 0.095, 0.070], [0.050, 0.072, 0.055],
            [{"Neck_M": 0.52, f"Scapula_{suffix}": 0.48}, f"Scapula_{suffix}",
             {f"Scapula_{suffix}": 0.43, "Chest_M": 0.57}], [1, 2, 3, 2, 8], sides=6,
        )
    return surface


def hair_bottom_surface(names: list[str], bindposes: np.ndarray) -> Surface:
    if names != HAIR_BOTTOM_BONES:
        raise ValueError("Fish hair-bottom palette drifted")
    surface = Surface(names, np.linalg.inv(bindposes)[:, :3, 3])
    B = surface.B
    surface.blade(
        "Kelp back-mantle",
        [B["Head_M"] + np.array([0.0, -0.05, -0.15]), B["Neck_M"] + np.array([0.0, -0.02, -0.24]),
         B["Chest_M"] + np.array([0.0, -0.16, -0.30])],
        [0.17, 0.29, 0.34], [0.055, 0.085, 0.075],
        [{"Head_M": 0.68, "Neck_M": 0.32}, {"Head_M": 0.36, "Neck_M": 0.64}, "Chest_M"],
        [11, 1, 2, 3, 2], axis=(1.0, 0.0, 0.0),
    )
    for suffix, sign in (("R", 1), ("L", -1)):
        scapula, shoulder = B[f"Scapula_{suffix}"], B[f"Shoulder_{suffix}"]
        surface.blade(
            "Animated reef mantle " + suffix,
            [B["Neck_M"] + np.array([sign * 0.075, -0.04, -0.15]), scapula + np.array([0.0, -0.02, -0.17]),
             shoulder + np.array([0.0, -0.04, -0.16]), shoulder + np.array([sign * 0.16, -0.15, -0.12])],
            [0.085, 0.11, 0.08, 0.022], [0.050, 0.067, 0.052, 0.010],
            [{"Neck_M": 0.50, f"Scapula_{suffix}": 0.50}, {f"Scapula_{suffix}": 0.58, f"Shoulder_{suffix}": 0.42},
             f"Shoulder_{suffix}", f"Shoulder_{suffix}"], [1, 2, 3, 2, 8], axis=(0.0, 1.0, 0.0),
        )
        surface.ellipsoid("Coral shoulder lamp " + suffix,
                          shoulder + np.array([sign * 0.10, -0.07, -0.13]), (0.085, 0.10, 0.07),
                          f"Shoulder_{suffix}", [8, 9, 10], sides=7)
    return surface


def write_asset(name: str, surface: Surface, reference, output_dir: Path, *, write_runtime: bool) -> dict[str, object]:
    source_path = output_dir / f"{name}.source.json"
    source_path.write_text(json.dumps(surface.data, separators=(",", ":")) + "\n")
    (output_dir / f"{name}.pieces.json").write_text(json.dumps(surface.pieces, indent=2) + "\n")
    used = sorted({joint for joints, weights in zip(surface.data["joints"], surface.data["weights"])
                   for joint, weight in zip(joints, weights) if weight > 0})
    if used != list(range(len(surface.names))):
        raise AssertionError((name, used, surface.names))
    report: dict[str, object] = {
        "vertices": len(surface.data["positions"]), "triangles": len(surface.data["triangles"]),
        "paletteBones": len(surface.names), "weightedPaletteBones": len(used), "allPaletteBonesWeighted": True,
    }
    if write_runtime:
        glb_path = output_dir / f"{name}.glb"
        write_glb(glb_path, surface.data, reference)
        validation = validate(glb_path, reference)
        validation.update(report)
        (output_dir / f"{name}.validation.json").write_text(json.dumps(validation, indent=2) + "\n")
        report["glbSha256"] = sha(glb_path)
    return report


def build(body_reference_path: Path, hair_top_reference_path: Path, hair_bottom_reference_path: Path,
          output_dir: Path, *, write_runtime: bool = True) -> dict[str, object]:
    output_dir.mkdir(parents=True, exist_ok=True)
    body_reference = np.load(body_reference_path, allow_pickle=False)
    hair_top_reference = np.load(hair_top_reference_path, allow_pickle=False)
    hair_bottom_reference = np.load(hair_bottom_reference_path, allow_pickle=False)
    try:
        body = body_surface(body_reference["bone_names"].tolist(), np.asarray(body_reference["bindposes"], dtype=float))
        hair_top = hair_top_surface(hair_top_reference["bone_names"].tolist(), np.asarray(hair_top_reference["bindposes"], dtype=float))
        hair_bottom = hair_bottom_surface(hair_bottom_reference["bone_names"].tolist(), np.asarray(hair_bottom_reference["bindposes"], dtype=float))
        write_palette(output_dir / "tideglass-palette.png")
        assets = {
            "tideglass-body": write_asset("tideglass-body", body, body_reference, output_dir, write_runtime=write_runtime),
            "tideglass-hair-top": write_asset("tideglass-hair-top", hair_top, hair_top_reference, output_dir, write_runtime=write_runtime),
            "tideglass-hair-bottom": write_asset("tideglass-hair-bottom", hair_bottom, hair_bottom_reference, output_dir, write_runtime=write_runtime),
        }
        report = {
            "status": "PASS_ORIGINAL_BIND_AUTHORED", "model": "Tideglass Fishsmith",
            "target": {
                "baseClass": "blacksmith", "skinset": "blacksmith_Fish", "defaultSkinType": "Fish",
                "bodyRendererPath": "playerFIsh", "bodyRendererSourceId": 121366,
                "hairTopRendererPath": "hairTop", "hairTopRendererSourceId": 121490,
                "hairBottomRendererPath": "hairBottom", "hairBottomRendererSourceId": 121521,
            },
            "authoringInputs": ["bone_names", "bindposes"], "nativeSurfaceRead": False,
            "references": {
                "body": {"path": str(body_reference_path.relative_to(ROOT)), "sha256": sha(body_reference_path)},
                "hairTop": {"path": str(hair_top_reference_path.relative_to(ROOT)), "sha256": sha(hair_top_reference_path)},
                "hairBottom": {"path": str(hair_bottom_reference_path.relative_to(ROOT)), "sha256": sha(hair_bottom_reference_path)},
            },
            "assets": assets, "paletteSha256": sha(output_dir / "tideglass-palette.png"),
            "artScope": "Original Tideglass Fishsmith V2: a compact dark tideglass fish visor beneath the native Blacksmith horn helm, tapered snout, brass eye slits and lantern core, scale pauldrons, forged gloves, and separately animated kelp-and-coral mantle.",
        }
        (output_dir / "build-report.json").write_text(json.dumps(report, indent=2) + "\n")
        return report
    finally:
        for reference in (body_reference, hair_top_reference, hair_bottom_reference):
            if hasattr(reference, "close"):
                reference.close()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--body-reference", type=Path, default=BODY_REFERENCE)
    parser.add_argument("--hair-top-reference", type=Path, default=HAIR_TOP_REFERENCE)
    parser.add_argument("--hair-bottom-reference", type=Path, default=HAIR_BOTTOM_REFERENCE)
    parser.add_argument("--output-dir", type=Path, default=PACKAGE)
    parser.add_argument("--no-runtime", action="store_true", help="Write deterministic source files without GLB export.")
    args = parser.parse_args()
    print(json.dumps(build(args.body_reference.resolve(), args.hair_top_reference.resolve(), args.hair_bottom_reference.resolve(),
                           args.output_dir.resolve(), write_runtime=not args.no_runtime), indent=2))


if __name__ == "__main__":
    main()
