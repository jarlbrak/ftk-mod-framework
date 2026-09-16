#!/usr/bin/env python3
"""Build Hearthveil, an original Blacksmith Female avatar set.

Only the selected player rig names and inverse bind matrices are read from the
ignored local references.  The body, crown, hood, palette, UVs, normals, and
weights below are original work; no native surface, texture, UV, weight, or
animation data informs their shape.
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


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir() and (path / "tools").is_dir())
PACKAGE = ROOT / "art-experiments" / "hearthveil-blacksmith"
BODY_REFERENCE = ROOT / "scratch" / "skeleton-audit" / "121067" / "reference.npz"
HAIR_REFERENCE = ROOT / "scratch" / "skeleton-audit" / "121083" / "reference.npz"
DEFAULT_ARMOR_REFERENCE = ROOT / "scratch" / "all-raw-rig-audit" / "121248" / "reference.npz"
BOOTS_REFERENCE = ROOT / "scratch" / "all-raw-rig-audit" / "121113" / "reference.npz"
GAMBESON_REFERENCE = ROOT / "scratch" / "all-raw-rig-audit" / "121211" / "reference.npz"
sys.path.insert(0, str(ROOT / "tools" / "ai-model-pipeline"))
from export_ftk_glb import write_glb
from validate_glb import validate


PALETTE = [
    "151D26",  # forge shadow
    "273743",  # blue slate
    "3F5B63",  # weathered teal metal
    "608077",  # cool verdigris
    "9DB3A2",  # pale patina
    "D1C49D",  # bone cloth
    "7B5138",  # aged leather
    "B87843",  # hammered copper
    "E4A253",  # furnace amber
    "F5D77D",  # lamp gold
    "7B3440",  # hearth red
    "3B2330",  # hood plum
]
BODY_BONES = [
    "Chest_M", "Scapula_R", "Shoulder_R", "Elbow_R", "Wrist_R",
    "MiddleFinger1_R", "MiddleFinger2_R", "MiddleFinger3_R",
    "ThumbFinger1_R", "ThumbFinger2_R", "ThumbFinger3_R", "Neck_M",
    "Head_M", "Hair_M", "Scapula_L", "Shoulder_L", "Elbow_L",
    "Wrist_L", "MiddleFinger1_L", "MiddleFinger2_L", "MiddleFinger3_L",
    "ThumbFinger1_L", "ThumbFinger2_L", "ThumbFinger3_L",
]
HAIR_BONES = ["Chest_M", "Scapula_R", "Neck_M", "Head_M", "Hair_M", "Scapula_L"]
DEFAULT_ARMOR_BONES = [
    "Root_M", "BackA_M", "BackB_M", "Chest_M", "Scapula_R", "Shoulder_R", "Elbow_R", "Wrist_R", "Neck_M",
    "Scapula_L", "Shoulder_L", "Elbow_L", "Wrist_L", "Hip_R", "Knee_R", "Ankle_R", "Hip_L", "Knee_L", "Ankle_L",
]
BOOTS_BONES = [
    "Hip_R", "Knee_R", "Ankle_R", "MiddleToe1_R", "MiddleToe2_R",
    "Hip_L", "Knee_L", "Ankle_L", "MiddleToe1_L", "MiddleToe2_L",
]
GAMBESON_BONES = [
    "Root_M", "BackA_M", "BackB_M", "Chest_M", "Scapula_R", "Shoulder_R", "Elbow_R", "Wrist_R",
    "ThumbFinger1_R", "ThumbFinger2_R", "Neck_M", "Head_M", "Scapula_L", "Shoulder_L", "Elbow_L", "Wrist_L",
    "ThumbFinger1_L", "ThumbFinger2_L", "Hip_R", "Knee_R", "Ankle_R", "MiddleToe1_R", "MiddleToe2_R",
    "Hip_L", "Knee_L", "Ankle_L", "MiddleToe1_L", "MiddleToe2_L",
]


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def normalized(skin: dict[str, float] | str) -> dict[str, float]:
    values = {skin: 1.0} if isinstance(skin, str) else dict(skin)
    if not 1 <= len(values) <= 4:
        raise ValueError(f"Expected one to four original weights: {values}")
    if any(not math.isfinite(weight) or weight <= 0 for weight in values.values()):
        raise ValueError(f"Invalid original weight: {values}")
    total = sum(values.values())
    return {name: weight / total for name, weight in values.items()}


class Surface:
    """Faceted original geometry with explicit palette and skin assignment."""

    def __init__(self, names: list[str], centers: np.ndarray):
        self.names = names
        self.B = {name: np.asarray(center, dtype=float) for name, center in zip(names, centers)}
        self.data: dict[str, list] = {
            "positions": [], "normals": [], "uvs": [], "triangles": [],
            "joints": [], "weights": [], "bone_names": names,
        }
        self.pieces: list[dict[str, object]] = []

    @staticmethod
    def _color(index: int | list[int], offset: int = 0) -> int:
        return index[offset % len(index)] if isinstance(index, list) else index

    def triangle(
        self,
        points: list[np.ndarray],
        skins: list[dict[str, float] | str],
        color: int,
    ) -> None:
        points = [np.asarray(point, dtype=float) for point in points]
        cross = np.cross(points[1] - points[0], points[2] - points[0])
        length = float(np.linalg.norm(cross))
        if length < 1e-9:
            raise ValueError("Degenerate original triangle")
        start = len(self.data["positions"])
        self.data["triangles"].append([start, start + 1, start + 2])
        # A horizontal authored stripe keeps the source and runtime V origins
        # immaterial while preserving deliberately distinct palette colors.
        uv = [(color + 0.5) / len(PALETTE), 0.5]
        for point, skin in zip(points, skins):
            influences = normalized(skin)
            if any(name not in self.B for name in influences):
                raise ValueError(f"Unknown player bone: {influences}")
            pairs = [(self.names.index(name), weight) for name, weight in influences.items()]
            pairs += [(0, 0.0)] * (4 - len(pairs))
            self.data["positions"].append(point.tolist())
            self.data["normals"].append((cross / length).tolist())
            self.data["uvs"].append(uv)
            self.data["joints"].append([joint for joint, _ in pairs])
            self.data["weights"].append([weight for _, weight in pairs])

    def tube(
        self,
        label: str,
        points: list[np.ndarray],
        radii_x: list[float],
        radii_z: list[float],
        skins: list[dict[str, float] | str],
        colors: int | list[int],
        *,
        sides: int = 8,
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
                color = self._color(colors, row + side)
                self.triangle(
                    [rings[row][side], rings[row][next_side], rings[row + 1][next_side]],
                    [skins[row], skins[row], skins[row + 1]], color,
                )
                self.triangle(
                    [rings[row][side], rings[row + 1][next_side], rings[row + 1][side]],
                    [skins[row], skins[row + 1], skins[row + 1]], color,
                )
        for side in range(sides):
            next_side = (side + 1) % sides
            self.triangle([points[0], rings[0][next_side], rings[0][side]], [skins[0]] * 3, self._color(colors, side))
            self.triangle([points[-1], rings[-1][side], rings[-1][next_side]], [skins[-1]] * 3, self._color(colors, side + 1))
        self.pieces.append({"name": label, "vertex_start": start, "vertex_count": len(self.data["positions"]) - start})

    def ellipsoid(
        self,
        label: str,
        center: np.ndarray,
        radii: tuple[float, float, float],
        skin: dict[str, float] | str,
        colors: int | list[int],
        *,
        sides: int = 10,
    ) -> None:
        center = np.asarray(center, dtype=float)
        factors = [0.12, 0.66, 1.0, 0.66, 0.12]
        heights = [-0.94, -0.60, 0.0, 0.60, 0.94]
        self.tube(
            label,
            [center + np.array([0.0, height * radii[1], 0.0]) for height in heights],
            [factor * radii[0] for factor in factors],
            [factor * radii[2] for factor in factors],
            [skin] * len(factors), colors, sides=sides,
        )

    def fin(
        self,
        label: str,
        base: np.ndarray,
        direction: np.ndarray,
        width: float,
        length: float,
        skin: dict[str, float] | str,
        colors: int | list[int],
    ) -> None:
        start = len(self.data["positions"])
        base = np.asarray(base, dtype=float)
        direction = np.asarray(direction, dtype=float)
        direction /= np.linalg.norm(direction)
        side = np.cross(direction, np.array([0.0, 1.0, 0.0]))
        if np.linalg.norm(side) < 1e-6:
            side = np.array([1.0, 0.0, 0.0])
        side /= np.linalg.norm(side)
        up = np.cross(side, direction)
        corners = [
            base - side * width,
            base + side * width,
            base + up * width * 0.45,
            base - up * width * 0.45,
        ]
        tip = base + direction * length
        for index in range(4):
            self.triangle([corners[index], corners[(index + 1) % 4], tip], [skin] * 3, self._color(colors, index))
        self.triangle([corners[0], corners[2], corners[1]], [skin] * 3, self._color(colors, 0))
        self.triangle([corners[0], corners[3], corners[2]], [skin] * 3, self._color(colors, 1))
        self.pieces.append({"name": label, "vertex_start": start, "vertex_count": len(self.data["positions"]) - start})


def write_palette(path: Path) -> None:
    width = 72 * len(PALETTE)
    image = Image.new("RGB", (width, 96), "black")
    for index, encoded in enumerate(PALETTE):
        color = tuple(int(encoded[offset:offset + 2], 16) for offset in (0, 2, 4))
        tile = Image.new("RGB", (72, 96), color)
        draw = ImageDraw.Draw(tile)
        highlight = tuple(min(255, int(component * 1.18)) for component in color)
        shadow = tuple(max(0, int(component * 0.66)) for component in color)
        for shift in range(-90, 160, 20):
            draw.line([(shift, 0), (shift + 78, 96)], fill=highlight, width=2)
        draw.line([(0, 92), (72, 92)], fill=shadow, width=4)
        image.paste(tile, (index * 72, 0))
    image.save(path)


def body_surface(names: list[str], bindposes: np.ndarray) -> Surface:
    assert names == BODY_BONES, "Blacksmith Female body palette drifted"
    surface = Surface(names, np.linalg.inv(bindposes)[:, :3, 3])
    B = surface.B
    chest, neck, head, hair = B["Chest_M"], B["Neck_M"], B["Head_M"], B["Hair_M"]

    surface.tube(
        "Long forge-apron torso",
        [chest + np.array([0.0, -0.58, 0.01]), chest + np.array([0.0, -0.23, 0.04]), chest + np.array([0.0, 0.16, 0.02])],
        [0.30, 0.39, 0.42], [0.22, 0.25, 0.27], ["Chest_M"] * 3,
        [0, 1, 2, 3, 1, 2], sides=10,
    )
    surface.ellipsoid("Hammered breastplate", chest + np.array([0.0, 0.00, 0.22]), (0.42, 0.40, 0.14), "Chest_M", [1, 2, 3, 4, 3, 2], sides=12)
    surface.ellipsoid("Furnace heart", chest + np.array([0.0, 0.04, 0.37]), (0.115, 0.17, 0.035), "Chest_M", [7, 8, 9, 8], sides=8)
    surface.tube(
        "High cloth collar", [chest + np.array([0.0, 0.17, 0.00]), neck + np.array([0.0, -0.03, 0.03])],
        [0.34, 0.25], [0.25, 0.22], [{"Chest_M": 0.70, "Neck_M": 0.30}, "Neck_M"], [10, 11, 10, 6], sides=10,
    )
    surface.ellipsoid("Faceted forge mask", head + np.array([0.0, 0.12, 0.10]), (0.27, 0.32, 0.25), {"Neck_M": 0.18, "Head_M": 0.82}, [0, 1, 2, 3, 2, 1], sides=12)
    surface.ellipsoid("Copper face plate", head + np.array([0.0, 0.10, 0.34]), (0.18, 0.20, 0.055), "Head_M", [6, 7, 8, 9], sides=8)
    for sign in (-1, 1):
        surface.ellipsoid("Lantern eye " + str(sign), head + np.array([sign * 0.095, 0.17, 0.405]), (0.040, 0.038, 0.018), "Head_M", [8, 9], sides=7)
        surface.fin("Crown wing " + str(sign), hair + np.array([sign * 0.13, -0.10, 0.09]), np.array([sign * 0.16, 0.97, 0.15]), 0.075, 0.30, "Hair_M", [6, 7, 8, 9])
    surface.fin("Crown flame", hair + np.array([0.0, -0.09, 0.12]), np.array([0.0, 1.0, 0.10]), 0.09, 0.39, "Hair_M", [7, 8, 9, 6])
    surface.tube(
        "Back mantle", [chest + np.array([0.0, -0.50, -0.20]), chest + np.array([0.0, -0.10, -0.29]), chest + np.array([0.0, 0.20, -0.24])],
        [0.33, 0.43, 0.36], [0.075, 0.090, 0.075], ["Chest_M"] * 3, [11, 10, 11, 3], sides=9,
    )

    for suffix, sign in (("R", 1), ("L", -1)):
        scapula, shoulder, elbow, wrist = [B[f"{name}_{suffix}"] for name in ("Scapula", "Shoulder", "Elbow", "Wrist")]
        surface.tube(
            "Layered shoulder bridge " + suffix,
            [chest + np.array([sign * 0.16, 0.19, 0.00]), scapula, shoulder],
            [0.22, 0.27, 0.25], [0.22, 0.26, 0.25],
            [{"Chest_M": 0.62, f"Scapula_{suffix}": 0.38}, {f"Scapula_{suffix}": 0.68, f"Shoulder_{suffix}": 0.32}, f"Shoulder_{suffix}"],
            [2, 3, 4, 3, 2, 7], sides=9,
        )
        surface.ellipsoid("Copper shoulder " + suffix, shoulder + np.array([0.0, 0.01, 0.03]), (0.28, 0.27, 0.28), f"Shoulder_{suffix}", [1, 2, 3, 7], sides=10)
        surface.tube(
            "Sleeved upper arm " + suffix, [shoulder, (shoulder + elbow) / 2 + np.array([0.0, 0.02, 0.055]), elbow],
            [0.19, 0.21, 0.17], [0.19, 0.21, 0.17],
            [f"Shoulder_{suffix}", {f"Shoulder_{suffix}": 0.48, f"Elbow_{suffix}": 0.52}, f"Elbow_{suffix}"],
            [6, 7, 8, 7, 6, 2], sides=9,
        )
        surface.ellipsoid("Elbow lantern " + suffix, elbow + np.array([0.0, 0.00, 0.06]), (0.18, 0.19, 0.18), f"Elbow_{suffix}", [0, 1, 2, 3], sides=8)
        surface.tube(
            "Forged forearm " + suffix, [elbow, (elbow + wrist) / 2 + np.array([0.0, -0.01, 0.07]), wrist],
            [0.17, 0.21, 0.16], [0.17, 0.20, 0.16],
            [f"Elbow_{suffix}", {f"Elbow_{suffix}": 0.46, f"Wrist_{suffix}": 0.54}, f"Wrist_{suffix}"],
            [1, 2, 3, 4, 3, 2], sides=9,
        )
        surface.ellipsoid("Gloved palm " + suffix, wrist + np.array([sign * 0.075, 0.0, 0.075]), (0.18, 0.14, 0.16), f"Wrist_{suffix}", [0, 1, 2, 3], sides=8)
        for finger in ("MiddleFinger", "ThumbFinger"):
            chain = [B[f"{finger}{index}_{suffix}"] for index in (1, 2, 3)]
            endpoint = chain[-1] + (chain[-1] - chain[-2]) * 0.55
            bone = [f"{finger}{index}_{suffix}" for index in (1, 2, 3)]
            surface.tube(
                f"{finger} articulated glove {suffix}", [chain[0], chain[1], chain[2], endpoint],
                [0.070, 0.057, 0.043, 0.020], [0.065, 0.053, 0.040, 0.018],
                [{f"Wrist_{suffix}": 0.18, bone[0]: 0.82}, {bone[0]: 0.30, bone[1]: 0.70}, {bone[1]: 0.30, bone[2]: 0.70}, bone[2]],
                [5, 6, 7, 8, 7], sides=6,
            )
    return surface


def hair_top_surface(names: list[str], bindposes: np.ndarray) -> Surface:
    assert names == HAIR_BONES, "Blacksmith Female hair-top palette drifted"
    surface = Surface(names, np.linalg.inv(bindposes)[:, :3, 3])
    B = surface.B
    surface.tube(
        "Crown hood", [B["Head_M"] + np.array([0.0, -0.12, -0.015]), B["Head_M"] + np.array([0.0, 0.15, 0.02]), B["Hair_M"] + np.array([0.0, -0.08, 0.03])],
        [0.31, 0.35, 0.22], [0.29, 0.33, 0.22], [{"Neck_M": 0.20, "Head_M": 0.80}, "Head_M", {"Head_M": 0.35, "Hair_M": 0.65}], [11, 10, 3, 4, 3, 11], sides=11,
    )
    surface.fin("Top crown flame", B["Hair_M"] + np.array([0.0, -0.06, 0.12]), np.array([0.0, 1.0, 0.12]), 0.085, 0.36, "Hair_M", [6, 7, 8, 9])
    for suffix, sign in (("R", 1), ("L", -1)):
        surface.tube(
            "Hood shoulder seam " + suffix,
            [B["Neck_M"] + np.array([sign * 0.12, -0.03, -0.05]), B[f"Scapula_{suffix}"] + np.array([0.0, 0.04, -0.08]), B["Chest_M"] + np.array([sign * 0.18, 0.05, -0.10])],
            [0.075, 0.095, 0.075], [0.060, 0.075, 0.060],
            [{"Neck_M": 0.52, f"Scapula_{suffix}": 0.48}, f"Scapula_{suffix}", {f"Scapula_{suffix}": 0.44, "Chest_M": 0.56}], [10, 11, 3, 2], sides=6,
        )
    return surface


def hair_bottom_surface(names: list[str], bindposes: np.ndarray) -> Surface:
    assert names == HAIR_BONES, "Blacksmith Female hair-bottom palette drifted"
    surface = Surface(names, np.linalg.inv(bindposes)[:, :3, 3])
    B = surface.B
    surface.tube(
        "Back hood drape", [B["Hair_M"] + np.array([0.0, -0.04, -0.15]), B["Head_M"] + np.array([0.0, 0.00, -0.25]), B["Neck_M"] + np.array([0.0, -0.08, -0.23])],
        [0.16, 0.29, 0.34], [0.07, 0.10, 0.10], [{"Hair_M": 0.72, "Head_M": 0.28}, {"Head_M": 0.55, "Neck_M": 0.45}, "Neck_M"], [11, 10, 3, 2], sides=9,
    )
    for suffix, sign in (("R", 1), ("L", -1)):
        surface.tube(
            "Shoulder braid " + suffix,
            [B["Neck_M"] + np.array([sign * 0.08, -0.04, -0.15]), B[f"Scapula_{suffix}"] + np.array([0.0, -0.02, -0.15]), B["Chest_M"] + np.array([sign * 0.21, -0.08, -0.14])],
            [0.070, 0.085, 0.060], [0.055, 0.066, 0.050],
            [{"Neck_M": 0.55, f"Scapula_{suffix}": 0.45}, f"Scapula_{suffix}", {f"Scapula_{suffix}": 0.40, "Chest_M": 0.60}], [6, 7, 8, 7, 6], sides=6,
        )
    surface.fin("Lower hood tail", B["Chest_M"] + np.array([0.0, -0.19, -0.19]), np.array([0.0, -0.82, -0.12]), 0.12, 0.28, "Chest_M", [10, 11, 3, 2])
    return surface


def default_armor_surface(names: list[str], bindposes: np.ndarray) -> Surface:
    """Author the unequipped Blacksmith garment around its 19-bone apparel rig."""
    assert names == DEFAULT_ARMOR_BONES, "Blacksmith Female default-armor palette drifted"
    surface = Surface(names, np.linalg.inv(bindposes)[:, :3, 3])
    B = surface.B
    surface.tube(
        "Forge spine", [B[bone] + np.array([0.0, 0.0, -0.14]) for bone in ("Root_M", "BackA_M", "BackB_M", "Chest_M")],
        [0.17, 0.20, 0.24, 0.27], [0.13, 0.15, 0.17, 0.19],
        ["Root_M", "BackA_M", "BackB_M", "Chest_M"], [11, 10, 3, 2, 6], sides=8,
    )
    surface.ellipsoid("Copper furnace pack", B["BackB_M"] + np.array([0.0, 0.04, -0.28]), (0.28, 0.32, 0.14), "BackB_M", [0, 1, 2, 7, 8], sides=10)
    surface.ellipsoid("Forged chest mantle", B["Chest_M"] + np.array([0.0, -0.01, 0.18]), (0.37, 0.31, 0.15), "Chest_M", [1, 2, 3, 7, 8], sides=10)
    surface.tube(
        "Neck clasp", [B["Chest_M"] + np.array([0.0, 0.14, 0.02]), B["Neck_M"] + np.array([0.0, -0.02, 0.05])],
        [0.25, 0.20], [0.20, 0.17], [{"Chest_M": 0.65, "Neck_M": 0.35}, "Neck_M"], [7, 8, 9, 6], sides=8,
    )
    for suffix, sign in (("R", 1), ("L", -1)):
        scapula, shoulder, elbow, wrist = [B[f"{part}_{suffix}"] for part in ("Scapula", "Shoulder", "Elbow", "Wrist")]
        surface.tube(
            "Shoulder harness " + suffix,
            [B["Chest_M"] + np.array([sign * 0.14, 0.14, 0.01]), scapula, shoulder],
            [0.15, 0.21, 0.20], [0.13, 0.19, 0.18],
            [{"Chest_M": 0.60, f"Scapula_{suffix}": 0.40}, {f"Scapula_{suffix}": 0.60, f"Shoulder_{suffix}": 0.40}, f"Shoulder_{suffix}"],
            [6, 7, 8, 7, 2], sides=7,
        )
        surface.ellipsoid("Rivet shoulder " + suffix, shoulder + np.array([0.0, 0.01, 0.02]), (0.23, 0.20, 0.22), f"Shoulder_{suffix}", [1, 2, 7, 8], sides=9)
        surface.tube(
            "Articulated bracer " + suffix,
            [shoulder, (shoulder + elbow) / 2 + np.array([0.0, 0.00, 0.05]), elbow, wrist],
            [0.15, 0.16, 0.14, 0.12], [0.14, 0.15, 0.13, 0.11],
            [f"Shoulder_{suffix}", {f"Shoulder_{suffix}": 0.46, f"Elbow_{suffix}": 0.54}, f"Elbow_{suffix}", f"Wrist_{suffix}"],
            [0, 1, 2, 3, 7, 8], sides=7,
        )
        hip, knee, ankle = [B[f"{part}_{suffix}"] for part in ("Hip", "Knee", "Ankle")]
        surface.tube(
            "Split apron legguard " + suffix,
            [B["Root_M"] + np.array([sign * 0.13, -0.02, 0.05]), hip, knee, ankle],
            [0.22, 0.23, 0.19, 0.15], [0.17, 0.18, 0.15, 0.12],
            [{"Root_M": 0.48, f"Hip_{suffix}": 0.52}, f"Hip_{suffix}", {f"Hip_{suffix}": 0.35, f"Knee_{suffix}": 0.65}, {f"Knee_{suffix}": 0.36, f"Ankle_{suffix}": 0.64}],
            [10, 11, 6, 7, 2, 3], sides=8,
        )
        surface.ellipsoid("Knee ember plate " + suffix, knee + np.array([0.0, 0.01, 0.11]), (0.20, 0.17, 0.10), f"Knee_{suffix}", [1, 2, 7, 8], sides=8)
    return surface


def boots_surface(names: list[str], bindposes: np.ndarray) -> Surface:
    """Author tall plated boots around the exact ten-bone boots renderer."""
    assert names == BOOTS_BONES, "Blacksmith Female boots palette drifted"
    surface = Surface(names, np.linalg.inv(bindposes)[:, :3, 3])
    B = surface.B
    for suffix, sign in (("R", 1), ("L", -1)):
        hip, knee, ankle, toe_one, toe_two = [B[f"{part}_{suffix}"] for part in ("Hip", "Knee", "Ankle", "MiddleToe1", "MiddleToe2")]
        surface.tube(
            "Tall soot boot " + suffix, [hip, knee, ankle, toe_one, toe_two],
            [0.17, 0.17, 0.18, 0.20, 0.17], [0.14, 0.14, 0.16, 0.22, 0.19],
            [f"Hip_{suffix}", {f"Hip_{suffix}": 0.40, f"Knee_{suffix}": 0.60}, {f"Knee_{suffix}": 0.38, f"Ankle_{suffix}": 0.62}, {f"Ankle_{suffix}": 0.34, f"MiddleToe1_{suffix}": 0.66}, {f"MiddleToe1_{suffix}": 0.30, f"MiddleToe2_{suffix}": 0.70}],
            [0, 1, 2, 3, 6, 7], sides=8,
        )
        surface.ellipsoid("Copper toe cap " + suffix, toe_two + np.array([0.0, 0.01, 0.06]), (0.20, 0.10, 0.20), f"MiddleToe2_{suffix}", [6, 7, 8, 9], sides=8)
        surface.fin("Boot spur " + suffix, ankle + np.array([sign * 0.08, -0.02, -0.14]), np.array([sign * 0.24, 0.0, -0.32]), 0.045, 0.21, f"Ankle_{suffix}", [7, 8, 9])
    return surface


def gambeson_surface(names: list[str], bindposes: np.ndarray) -> Surface:
    """Author the equipped cloth-armor branch around its 28-bone renderer."""
    assert names == GAMBESON_BONES, "Blacksmith Female gambeson palette drifted"
    surface = Surface(names, np.linalg.inv(bindposes)[:, :3, 3])
    B = surface.B
    surface.tube(
        "Quilted forge coat core", [B[bone] + np.array([0.0, 0.0, 0.015]) for bone in ("Root_M", "BackA_M", "BackB_M", "Chest_M")],
        [0.34, 0.38, 0.40, 0.42], [0.25, 0.28, 0.30, 0.31],
        ["Root_M", "BackA_M", "BackB_M", "Chest_M"], [11, 10, 6, 5, 7, 8], sides=11,
    )
    surface.ellipsoid("Ember tabard", B["Chest_M"] + np.array([0.0, -0.03, 0.30]), (0.28, 0.37, 0.07), "Chest_M", [10, 7, 8, 9, 8], sides=10)
    surface.tube(
        "High hood", [B["Chest_M"] + np.array([0.0, 0.15, -0.01]), B["Neck_M"], B["Head_M"] + np.array([0.0, 0.05, -0.02])],
        [0.29, 0.25, 0.31], [0.24, 0.21, 0.28], [{"Chest_M": 0.64, "Neck_M": 0.36}, "Neck_M", {"Neck_M": 0.28, "Head_M": 0.72}], [11, 10, 3, 4, 10], sides=10,
    )
    surface.ellipsoid("Hooded forge visor", B["Head_M"] + np.array([0.0, 0.08, 0.29]), (0.20, 0.16, 0.050), "Head_M", [0, 1, 2, 7, 8], sides=8)
    for suffix, sign in (("R", 1), ("L", -1)):
        scapula, shoulder, elbow, wrist = [B[f"{part}_{suffix}"] for part in ("Scapula", "Shoulder", "Elbow", "Wrist")]
        thumb_one, thumb_two = [B[f"ThumbFinger{index}_{suffix}"] for index in (1, 2)]
        surface.tube(
            "Quilted sleeve " + suffix,
            [B["Chest_M"] + np.array([sign * 0.14, 0.15, 0.0]), scapula, shoulder, elbow, wrist],
            [0.18, 0.22, 0.22, 0.18, 0.14], [0.16, 0.20, 0.20, 0.16, 0.12],
            [{"Chest_M": 0.55, f"Scapula_{suffix}": 0.45}, f"Scapula_{suffix}", {f"Scapula_{suffix}": 0.36, f"Shoulder_{suffix}": 0.64}, {f"Shoulder_{suffix}": 0.38, f"Elbow_{suffix}": 0.62}, {f"Elbow_{suffix}": 0.34, f"Wrist_{suffix}": 0.66}],
            [5, 6, 10, 11, 7, 8], sides=8,
        )
        thumb_end = thumb_two + (thumb_two - thumb_one) * 0.75
        surface.tube(
            "Warm leather thumb " + suffix, [wrist, thumb_one, thumb_two, thumb_end],
            [0.10, 0.075, 0.055, 0.026], [0.09, 0.067, 0.050, 0.022],
            [{f"Wrist_{suffix}": 0.35, f"ThumbFinger1_{suffix}": 0.65}, {f"ThumbFinger1_{suffix}": 0.30, f"ThumbFinger2_{suffix}": 0.70}, f"ThumbFinger2_{suffix}", f"ThumbFinger2_{suffix}"],
            [6, 7, 8, 7, 6], sides=6,
        )
        hip, knee, ankle, toe_one, toe_two = [B[f"{part}_{suffix}"] for part in ("Hip", "Knee", "Ankle", "MiddleToe1", "MiddleToe2")]
        surface.tube(
            "Quilted trouser " + suffix, [B["Root_M"] + np.array([sign * 0.15, 0.0, 0.02]), hip, knee, ankle, toe_one, toe_two],
            [0.25, 0.24, 0.20, 0.17, 0.19, 0.16], [0.19, 0.18, 0.15, 0.14, 0.19, 0.17],
            [{"Root_M": 0.45, f"Hip_{suffix}": 0.55}, f"Hip_{suffix}", {f"Hip_{suffix}": 0.36, f"Knee_{suffix}": 0.64}, {f"Knee_{suffix}": 0.36, f"Ankle_{suffix}": 0.64}, {f"Ankle_{suffix}": 0.34, f"MiddleToe1_{suffix}": 0.66}, {f"MiddleToe1_{suffix}": 0.31, f"MiddleToe2_{suffix}": 0.69}],
            [10, 11, 5, 6, 2, 3], sides=9,
        )
        surface.ellipsoid("Knee quilt plate " + suffix, knee + np.array([0.0, 0.0, 0.115]), (0.19, 0.17, 0.085), f"Knee_{suffix}", [6, 7, 8, 9], sides=8)
    return surface


def write_asset(name: str, surface: Surface, reference, output_dir: Path, *, write_runtime: bool) -> dict:
    source_path = output_dir / f"{name}.source.json"
    source_path.write_text(json.dumps(surface.data, separators=(",", ":")) + "\n")
    (output_dir / f"{name}.pieces.json").write_text(json.dumps(surface.pieces, indent=2) + "\n")
    used = sorted({joint for joints, weights in zip(surface.data["joints"], surface.data["weights"]) for joint, weight in zip(joints, weights) if weight > 0})
    assert used == list(range(len(surface.names))), (name, used, surface.names)
    report: dict[str, object] = {
        "vertices": len(surface.data["positions"]),
        "triangles": len(surface.data["triangles"]),
        "paletteBones": len(surface.names),
        "weightedPaletteBones": len(used),
        "allPaletteBonesWeighted": True,
    }
    if write_runtime:
        glb_path = output_dir / f"{name}.glb"
        write_glb(glb_path, surface.data, reference)
        validation = validate(glb_path, reference)
        validation.update(report)
        (output_dir / f"{name}.validation.json").write_text(json.dumps(validation, indent=2) + "\n")
        report["glbSha256"] = sha(glb_path)
    return report


def build(
    body_reference_path: Path,
    hair_reference_path: Path,
    default_armor_reference_path: Path,
    boots_reference_path: Path,
    gambeson_reference_path: Path,
    output_dir: Path,
    *,
    write_runtime: bool = True,
) -> dict:
    output_dir.mkdir(parents=True, exist_ok=True)
    body_reference = np.load(body_reference_path, allow_pickle=False)
    hair_reference = np.load(hair_reference_path, allow_pickle=False)
    default_armor_reference = np.load(default_armor_reference_path, allow_pickle=False)
    boots_reference = np.load(boots_reference_path, allow_pickle=False)
    gambeson_reference = np.load(gambeson_reference_path, allow_pickle=False)
    try:
        body = body_surface(body_reference["bone_names"].tolist(), np.asarray(body_reference["bindposes"], dtype=float))
        hair_top = hair_top_surface(hair_reference["bone_names"].tolist(), np.asarray(hair_reference["bindposes"], dtype=float))
        hair_bottom = hair_bottom_surface(hair_reference["bone_names"].tolist(), np.asarray(hair_reference["bindposes"], dtype=float))
        default_armor = default_armor_surface(default_armor_reference["bone_names"].tolist(), np.asarray(default_armor_reference["bindposes"], dtype=float))
        boots = boots_surface(boots_reference["bone_names"].tolist(), np.asarray(boots_reference["bindposes"], dtype=float))
        gambeson = gambeson_surface(gambeson_reference["bone_names"].tolist(), np.asarray(gambeson_reference["bindposes"], dtype=float))
        write_palette(output_dir / "hearthveil-palette.png")
        assets = {
            "hearthveil-body": write_asset("hearthveil-body", body, body_reference, output_dir, write_runtime=write_runtime),
            "hearthveil-hair-top": write_asset("hearthveil-hair-top", hair_top, hair_reference, output_dir, write_runtime=write_runtime),
            "hearthveil-hair-bottom": write_asset("hearthveil-hair-bottom", hair_bottom, hair_reference, output_dir, write_runtime=write_runtime),
            "hearthveil-default-armor": write_asset("hearthveil-default-armor", default_armor, default_armor_reference, output_dir, write_runtime=write_runtime),
            "hearthveil-boots": write_asset("hearthveil-boots", boots, boots_reference, output_dir, write_runtime=write_runtime),
            "hearthveil-gambeson": write_asset("hearthveil-gambeson", gambeson, gambeson_reference, output_dir, write_runtime=write_runtime),
        }
        report = {
            "status": "PASS_ORIGINAL_BIND_AUTHORED",
            "model": "Hearthveil Blacksmith",
            "target": {
                "baseClass": "blacksmith",
                "skinset": "blacksmith_Female",
                "bodyRendererPath": "playerBlacksmith",
                "bodyRendererSourceId": 121067,
                "hairTopRendererPath": "hairTop",
                "hairBottomRendererPath": "hairBottom",
                "hairTopRendererSourceId": 121083,
                "hairBottomRendererSourceId": 121178,
                "hairBottomRepresentativeBindingSourceId": 121083,
                "defaultArmorRendererPath": "armorBlacksmithF(Clone)",
                "defaultArmorRendererSourceId": 121248,
                "bootsRendererPath": "bootsBlacksmith(Clone)",
                "bootsRendererSourceId": 121113,
                "gambesonRendererPath": "armorGambesonF(Clone)",
                "gambesonRendererSourceId": 121211,
            },
            "authoringInputs": ["bone_names", "bindposes"],
            "nativeSurfaceRead": False,
            "references": {
                "body": {"path": str(body_reference_path.relative_to(ROOT)), "sha256": sha(body_reference_path)},
                "hair": {"path": str(hair_reference_path.relative_to(ROOT)), "sha256": sha(hair_reference_path)},
                "defaultArmor": {"path": str(default_armor_reference_path.relative_to(ROOT)), "sha256": sha(default_armor_reference_path)},
                "boots": {"path": str(boots_reference_path.relative_to(ROOT)), "sha256": sha(boots_reference_path)},
                "gambeson": {"path": str(gambeson_reference_path.relative_to(ROOT)), "sha256": sha(gambeson_reference_path)},
            },
            "assets": assets,
            "paletteSha256": sha(output_dir / "hearthveil-palette.png"),
            "artScope": "Original forge-hood, mask, apron, sleeve, glove, braid, default armor, boot, and gambeson design. Studio and live review remain separate gates.",
        }
        (output_dir / "build-report.json").write_text(json.dumps(report, indent=2) + "\n")
        return report
    finally:
        for reference in (body_reference, hair_reference, default_armor_reference, boots_reference, gambeson_reference):
            if hasattr(reference, "close"):
                reference.close()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--body-reference", type=Path, default=BODY_REFERENCE)
    parser.add_argument("--hair-reference", type=Path, default=HAIR_REFERENCE)
    parser.add_argument("--default-armor-reference", type=Path, default=DEFAULT_ARMOR_REFERENCE)
    parser.add_argument("--boots-reference", type=Path, default=BOOTS_REFERENCE)
    parser.add_argument("--gambeson-reference", type=Path, default=GAMBESON_REFERENCE)
    parser.add_argument("--output-dir", type=Path, default=PACKAGE)
    parser.add_argument("--no-runtime", action="store_true", help="Write source, pieces, and palette only.")
    args = parser.parse_args()
    result = build(
        args.body_reference.resolve(),
        args.hair_reference.resolve(),
        args.default_armor_reference.resolve(),
        args.boots_reference.resolve(),
        args.gambeson_reference.resolve(),
        args.output_dir.resolve(),
        write_runtime=not args.no_runtime,
    )
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
