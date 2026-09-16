#!/usr/bin/env python3
"""Build Wildbloom Herbalist from original geometry on three exact player rigs.

Only the selected rig's bone names and inverse bind matrices are read from the
local reference files. All surfaces, UVs, weights, palette, and names below are
original authoring. No native mesh surface, texture, UV, skin weight, or motion
sample is consulted.
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
PACKAGE = ROOT / "art-experiments" / "wildbloom-herbalist"
BODY_REFERENCE = ROOT / "scratch" / "skeleton-audit" / "121202" / "reference.npz"
HAIR_TOP_REFERENCE = ROOT / "scratch" / "skeleton-audit" / "121088" / "reference.npz"
HAIR_BOTTOM_REFERENCE = ROOT / "scratch" / "skeleton-audit" / "121003" / "reference.npz"
sys.path.insert(0, str(ROOT / "tools" / "ai-model-pipeline"))
from export_ftk_glb import write_glb
from validate_glb import validate

PALETTE = [
    "10251F",  # woodland shadow
    "1C4A39",  # deep leaf
    "2E7251",  # fern
    "5C9A68",  # sunlit moss
    "9ECF92",  # new growth
    "DBE7B2",  # linen bloom
    "8E5C3B",  # bark leather
    "C4874D",  # terracotta seed
    "E0A95C",  # pollen gold
    "F4D790",  # sunpetal
    "8C3850",  # berry blossom
    "3F273D",  # twilight plum
]

BODY_BONES = [
    "Chest_M", "Scapula_R", "Shoulder_R", "Elbow_R", "Wrist_R",
    "MiddleFinger1_R", "MiddleFinger2_R", "MiddleFinger3_R",
    "ThumbFinger1_R", "ThumbFinger2_R", "ThumbFinger3_R", "Neck_M",
    "Head_M", "Hair_M", "Scapula_L", "Shoulder_L", "Elbow_L",
    "Wrist_L", "MiddleFinger1_L", "MiddleFinger2_L", "MiddleFinger3_L",
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
    """Original faceted skinned surface with one palette stripe per face."""

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
        factors = [0.12, 0.66, 1.0, 0.66, 0.12]
        heights = [-0.94, -0.60, 0.0, 0.60, 0.94]
        self.tube(label, [center + np.array([0.0, height * radii[1], 0.0]) for height in heights],
                  [factor * radii[0] for factor in factors], [factor * radii[2] for factor in factors],
                  [skin] * len(factors), colors, sides=sides)

    def leaf(
        self, label: str, base: np.ndarray, direction: np.ndarray, width: float, length: float,
        skin: dict[str, float] | str, colors: int | list[int],
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
        center = base + direction * length * 0.52
        tip = base + direction * length
        front = center + up * width * 0.27
        back = center - up * width * 0.27
        left = center - side * width
        right = center + side * width
        for first, second, color in ((left, front, 0), (front, right, 1), (right, back, 2), (back, left, 3)):
            self.triangle([base, first, tip], [skin] * 3, self.color(colors, color))
            self.triangle([tip, second, base], [skin] * 3, self.color(colors, color + 1))
        self.pieces.append({"name": label, "vertex_start": start, "vertex_count": len(self.data["positions"]) - start})


def write_palette(path: Path) -> None:
    tile_width, height = 72, 96
    image = Image.new("RGB", (tile_width * len(PALETTE), height), "black")
    for index, encoded in enumerate(PALETTE):
        color = tuple(int(encoded[offset:offset + 2], 16) for offset in (0, 2, 4))
        tile = Image.new("RGB", (tile_width, height), color)
        draw = ImageDraw.Draw(tile)
        highlight = tuple(min(255, int(channel * 1.18)) for channel in color)
        shadow = tuple(max(0, int(channel * 0.61)) for channel in color)
        for shift in range(-100, 170, 18):
            draw.line([(shift, 0), (shift + 82, height)], fill=highlight, width=2)
        draw.line([(0, height - 5), (tile_width, height - 5)], fill=shadow, width=4)
        image.paste(tile, (index * tile_width, 0))
    image.save(path)


def body_surface(names: list[str], bindposes: np.ndarray) -> Surface:
    if names != BODY_BONES:
        raise ValueError("Herbalist Female body palette drifted")
    surface = Surface(names, np.linalg.inv(bindposes)[:, :3, 3])
    B = surface.B
    chest, neck, head, hair = (B[key] for key in ("Chest_M", "Neck_M", "Head_M", "Hair_M"))
    surface.tube(
        "Barkweave tunic core",
        [chest + np.array([0.0, -0.58, 0.00]), chest + np.array([0.0, -0.22, 0.025]), chest + np.array([0.0, 0.14, 0.02])],
        [0.31, 0.40, 0.43], [0.23, 0.27, 0.28], ["Chest_M"] * 3, [0, 1, 2, 3, 1, 2], sides=11,
    )
    surface.ellipsoid("Moss mantle", chest + np.array([0.0, 0.00, 0.22]), (0.43, 0.39, 0.13), "Chest_M", [1, 2, 3, 4, 3, 2], sides=12)
    surface.ellipsoid("Sunseed medallion", chest + np.array([0.0, 0.035, 0.365]), (0.105, 0.15, 0.035), "Chest_M", [7, 8, 9, 8], sides=8)
    surface.tube(
        "Petal collar", [chest + np.array([0.0, 0.17, -0.005]), neck + np.array([0.0, -0.02, 0.03])],
        [0.34, 0.25], [0.25, 0.21], [{"Chest_M": 0.68, "Neck_M": 0.32}, "Neck_M"], [5, 4, 10, 11], sides=10,
    )
    surface.ellipsoid("Willow face", head + np.array([0.0, 0.11, 0.095]), (0.265, 0.31, 0.245), {"Neck_M": 0.18, "Head_M": 0.82}, [5, 6, 4, 3, 4, 6], sides=12)
    surface.ellipsoid("Herbalist veil", head + np.array([0.0, 0.095, 0.34]), (0.175, 0.19, 0.052), "Head_M", [5, 8, 9, 8], sides=8)
    for sign in (-1, 1):
        surface.ellipsoid("Dewglass eye " + str(sign), head + np.array([sign * 0.093, 0.17, 0.402]), (0.038, 0.036, 0.016), "Head_M", [3, 4, 9], sides=7)
        surface.leaf("Crown leaf " + str(sign), hair + np.array([sign * 0.12, -0.10, 0.065]), np.array([sign * 0.25, 0.91, 0.18]), 0.075, 0.31, "Hair_M", [2, 3, 4, 8])
    surface.leaf("Crown bloom", hair + np.array([0.0, -0.08, 0.11]), np.array([0.0, 1.0, 0.13]), 0.09, 0.34, "Hair_M", [8, 9, 10, 4])
    surface.tube(
        "Trailing herb mantle", [chest + np.array([0.0, -0.49, -0.20]), chest + np.array([0.0, -0.12, -0.285]), chest + np.array([0.0, 0.20, -0.23])],
        [0.33, 0.43, 0.36], [0.075, 0.090, 0.075], ["Chest_M"] * 3, [11, 10, 1, 2], sides=9,
    )
    for suffix, sign in (("R", 1), ("L", -1)):
        scapula, shoulder, elbow, wrist = [B[f"{part}_{suffix}"] for part in ("Scapula", "Shoulder", "Elbow", "Wrist")]
        surface.tube(
            "Vine shoulder bridge " + suffix,
            [chest + np.array([sign * 0.16, 0.19, 0.00]), scapula, shoulder], [0.20, 0.25, 0.23], [0.19, 0.24, 0.23],
            [{"Chest_M": 0.60, f"Scapula_{suffix}": 0.40}, {f"Scapula_{suffix}": 0.68, f"Shoulder_{suffix}": 0.32}, f"Shoulder_{suffix}"],
            [1, 2, 3, 4, 2, 8], sides=9,
        )
        surface.ellipsoid("Petaled shoulder " + suffix, shoulder + np.array([0.0, 0.01, 0.03]), (0.26, 0.25, 0.25), f"Shoulder_{suffix}", [2, 3, 4, 9], sides=10)
        surface.tube(
            "Linen upper arm " + suffix, [shoulder, (shoulder + elbow) / 2 + np.array([0.0, 0.02, 0.05]), elbow],
            [0.18, 0.20, 0.16], [0.18, 0.20, 0.16], [f"Shoulder_{suffix}", {f"Shoulder_{suffix}": 0.48, f"Elbow_{suffix}": 0.52}, f"Elbow_{suffix}"],
            [5, 6, 3, 4, 5, 10], sides=9,
        )
        surface.ellipsoid("Thorn elbow " + suffix, elbow + np.array([0.0, 0.0, 0.065]), (0.17, 0.18, 0.17), f"Elbow_{suffix}", [6, 7, 8, 7], sides=8)
        surface.tube(
            "Wrapped forearm " + suffix, [elbow, (elbow + wrist) / 2 + np.array([0.0, -0.01, 0.07]), wrist],
            [0.16, 0.20, 0.15], [0.16, 0.19, 0.15], [f"Elbow_{suffix}", {f"Elbow_{suffix}": 0.46, f"Wrist_{suffix}": 0.54}, f"Wrist_{suffix}"],
            [0, 1, 2, 3, 4, 5], sides=9,
        )
        surface.ellipsoid("Root glove " + suffix, wrist + np.array([sign * 0.07, 0.0, 0.075]), (0.17, 0.13, 0.15), f"Wrist_{suffix}", [6, 7, 2, 3], sides=8)
        for finger in ("MiddleFinger", "ThumbFinger"):
            points = [B[f"{finger}{index}_{suffix}"] for index in (1, 2, 3)]
            tip = points[-1] + (points[-1] - points[-2]) * 0.55
            bones = [f"{finger}{index}_{suffix}" for index in (1, 2, 3)]
            surface.tube(
                f"{finger} root glove {suffix}", [points[0], points[1], points[2], tip],
                [0.067, 0.055, 0.041, 0.019], [0.062, 0.051, 0.038, 0.017],
                [{f"Wrist_{suffix}": 0.18, bones[0]: 0.82}, {bones[0]: 0.30, bones[1]: 0.70}, {bones[1]: 0.30, bones[2]: 0.70}, bones[2]],
                [6, 7, 8, 9, 7], sides=6,
            )
    return surface


def hair_top_surface(names: list[str], bindposes: np.ndarray) -> Surface:
    if names != HAIR_TOP_BONES:
        raise ValueError("Herbalist Female hair-top palette drifted")
    surface = Surface(names, np.linalg.inv(bindposes)[:, :3, 3])
    B = surface.B
    surface.tube(
        "Woven blossom crown", [B["Head_M"] + np.array([0.0, -0.12, -0.01]), B["Head_M"] + np.array([0.0, 0.15, 0.02]), B["Hair_M"] + np.array([0.0, -0.08, 0.03])],
        [0.31, 0.35, 0.22], [0.29, 0.33, 0.22], [{"Neck_M": 0.20, "Head_M": 0.80}, "Head_M", {"Head_M": 0.35, "Hair_M": 0.65}], [10, 4, 5, 3, 2, 10], sides=11,
    )
    surface.leaf("High blossom", B["Hair_M"] + np.array([0.0, -0.06, 0.12]), np.array([0.0, 1.0, 0.12]), 0.09, 0.36, "Hair_M", [8, 9, 10, 4])
    for suffix, sign in (("R", 1), ("L", -1)):
        surface.tube(
            "Woven crown shoulder seam " + suffix,
            [B["Neck_M"] + np.array([sign * 0.12, -0.03, -0.05]), B[f"Scapula_{suffix}"] + np.array([0.0, 0.04, -0.08]), B["Chest_M"] + np.array([sign * 0.18, 0.05, -0.10])],
            [0.070, 0.092, 0.072], [0.056, 0.073, 0.058], [{"Neck_M": 0.52, f"Scapula_{suffix}": 0.48}, f"Scapula_{suffix}", {f"Scapula_{suffix}": 0.44, "Chest_M": 0.56}], [1, 2, 3, 4], sides=6,
        )
    return surface


def hair_bottom_surface(names: list[str], bindposes: np.ndarray) -> Surface:
    if names != HAIR_BOTTOM_BONES:
        raise ValueError("Herbalist Female hair-bottom palette drifted")
    surface = Surface(names, np.linalg.inv(bindposes)[:, :3, 3])
    B = surface.B
    surface.tube(
        "Trailing petal braid", [B["Head_M"] + np.array([0.0, -0.04, -0.13]), B["Neck_M"] + np.array([0.0, -0.03, -0.22]), B["Chest_M"] + np.array([0.0, -0.10, -0.23])],
        [0.15, 0.25, 0.29], [0.070, 0.095, 0.090], [{"Head_M": 0.70, "Neck_M": 0.30}, {"Head_M": 0.42, "Neck_M": 0.58}, "Chest_M"], [10, 11, 5, 4], sides=9,
    )
    for suffix, sign in (("R", 1), ("L", -1)):
        scapula, shoulder = B[f"Scapula_{suffix}"], B[f"Shoulder_{suffix}"]
        surface.tube(
            "Animated shoulder braid " + suffix,
            [B["Neck_M"] + np.array([sign * 0.075, -0.04, -0.14]), scapula + np.array([0.0, -0.02, -0.15]), shoulder + np.array([0.0, -0.015, -0.13]), shoulder + np.array([sign * 0.075, -0.09, -0.10])],
            [0.070, 0.085, 0.070, 0.035], [0.054, 0.066, 0.055, 0.028],
            [{"Neck_M": 0.50, f"Scapula_{suffix}": 0.50}, {f"Scapula_{suffix}": 0.58, f"Shoulder_{suffix}": 0.42}, f"Shoulder_{suffix}", f"Shoulder_{suffix}"], [6, 7, 8, 10, 7], sides=6,
        )
        surface.leaf("Shoulder bloom " + suffix, shoulder + np.array([sign * 0.075, -0.05, -0.13]), np.array([sign * 0.32, -0.45, -0.18]), 0.050, 0.17, f"Shoulder_{suffix}", [8, 9, 10, 4])
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
        write_palette(output_dir / "wildbloom-palette.png")
        assets = {
            "wildbloom-body": write_asset("wildbloom-body", body, body_reference, output_dir, write_runtime=write_runtime),
            "wildbloom-hair-top": write_asset("wildbloom-hair-top", hair_top, hair_top_reference, output_dir, write_runtime=write_runtime),
            "wildbloom-hair-bottom": write_asset("wildbloom-hair-bottom", hair_bottom, hair_bottom_reference, output_dir, write_runtime=write_runtime),
        }
        report = {
            "status": "PASS_ORIGINAL_BIND_AUTHORED",
            "model": "Wildbloom Herbalist",
            "target": {
                "baseClass": "blacksmith",
                "skinset": "herbalist_Female",
                "bodyRendererPath": "player_Herbalist",
                "bodyRendererSourceId": 121202,
                "hairTopRendererPath": "hairTop",
                "hairTopRendererSourceId": 121088,
                "hairBottomRendererPath": "hairBottom",
                "hairBottomRendererSourceId": 121003,
            },
            "authoringInputs": ["bone_names", "bindposes"],
            "nativeSurfaceRead": False,
            "references": {
                "body": {"path": str(body_reference_path.relative_to(ROOT)), "sha256": sha(body_reference_path)},
                "hairTop": {"path": str(hair_top_reference_path.relative_to(ROOT)), "sha256": sha(hair_top_reference_path)},
                "hairBottom": {"path": str(hair_bottom_reference_path.relative_to(ROOT)), "sha256": sha(hair_bottom_reference_path)},
            },
            "assets": assets,
            "paletteSha256": sha(output_dir / "wildbloom-palette.png"),
            "artScope": "Original woodland healer: barkweave tunic, moss mantle, petal collar, blossom crown, and independently animated shoulder braids.",
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
