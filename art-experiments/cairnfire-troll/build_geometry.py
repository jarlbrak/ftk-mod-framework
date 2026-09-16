#!/usr/bin/env python3
"""Build the original Cairnfire Troll body from Troll B bind metadata only.

The source generator intentionally reads only ``bone_names`` and ``bindposes``
from the local reference. It never reads a native surface, texture, UV, weight,
or animation sample. The resulting body is a cohesive low-poly basalt-and-copper
troll that uses every bone in the exact 37-joint palette.
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
PACKAGE = ROOT / "art-experiments" / "cairnfire-troll"
sys.path.insert(0, str(ROOT / "tools" / "ai-model-pipeline"))
from export_ftk_glb import write_glb
from validate_glb import validate


# Original hand-selected palette. Each tile is a small authored paint field,
# never sampled from a game asset.
PALETTE = [
    "121A22",  # deep charcoal recess
    "22313B",  # blue-black basalt
    "344C58",  # slate face
    "54717A",  # weathered blue stone
    "7D9898",  # cold edge
    "B8C9BF",  # pale chipped edge
    "D3B774",  # old brass
    "B66B3C",  # copper
    "E18B43",  # ember amber
    "F3C66A",  # rune gold
    "7B3E35",  # iron red
    "4D2733",  # cloak shadow
    "2E6D6A",  # oxidized teal
    "5BA69D",  # sea-glass patina
    "A34E45",  # muted ember red
    "D8D3BF",  # ash cloth
]
EXPECTED_BONES = [
    "Root_M", "BackA_M", "BackB_M", "Chest_M", "Scapula_R", "Shoulder_R", "Elbow_R", "Wrist_R",
    "MiddleFinger1_R", "MiddleFinger2_R", "MiddleFinger3_R", "ThumbFinger1_R", "ThumbFinger2_R", "ThumbFinger3_R",
    "Neck_M", "Head_M", "Hair_M", "Scapula_L", "Shoulder_L", "Elbow_L", "Wrist_L",
    "MiddleFinger1_L", "MiddleFinger2_L", "MiddleFinger3_L", "ThumbFinger1_L", "ThumbFinger2_L", "ThumbFinger3_L",
    "Hip_R", "Knee_R", "Ankle_R", "MiddleToe1_R", "MiddleToe2_R", "Hip_L", "Knee_L", "Ankle_L", "MiddleToe1_L", "MiddleToe2_L",
]


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def normalized(values: dict[str, float] | str) -> dict[str, float]:
    values = {values: 1.0} if isinstance(values, str) else dict(values)
    if not 1 <= len(values) <= 4 or any(not math.isfinite(weight) or weight <= 0 for weight in values.values()):
        raise ValueError(f"Invalid original skin weights: {values}")
    total = sum(values.values())
    return {name: weight / total for name, weight in values.items()}


class Surface:
    def __init__(self, names: list[str], centers: np.ndarray):
        self.names = names
        self.B = {name: np.asarray(center, dtype=float) for name, center in zip(names, centers)}
        self.data: dict[str, list] = {key: [] for key in ("positions", "normals", "uvs", "triangles", "joints", "weights")}
        self.data["bone_names"] = names
        self.pieces: list[dict[str, object]] = []

    def triangle(self, points: list[np.ndarray], skins: list[dict[str, float] | str], color: int) -> None:
        points = [np.asarray(point, dtype=float) for point in points]
        cross = np.cross(points[1] - points[0], points[2] - points[0])
        length = float(np.linalg.norm(cross))
        if length < 1e-9:
            raise ValueError("Degenerate original triangle")
        start = len(self.data["positions"])
        self.data["triangles"].append([start, start + 1, start + 2])
        uv = [(color % 4 + 0.5) / 4, 1 - (color // 4 + 0.5) / 4]
        for point, skin in zip(points, skins):
            influence = normalized(skin)
            if any(name not in self.B for name in influence):
                raise ValueError(f"Unknown Troll B bone in original geometry: {influence}")
            pairs = [(self.names.index(name), weight) for name, weight in influence.items()]
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
        widths: list[float],
        depths: list[float],
        skins: list[dict[str, float] | str],
        colors: int | list[int],
        *,
        sides: int = 10,
        axis: tuple[float, float, float] = (1.0, 0.0, 0.0),
    ) -> None:
        if not (len(points) == len(widths) == len(depths) == len(skins) and len(points) >= 2):
            raise ValueError(label)
        start = len(self.data["positions"])
        points = [np.asarray(point, dtype=float) for point in points]
        rings = []
        for index, point in enumerate(points):
            tangent = points[min(index + 1, len(points) - 1)] - points[max(index - 1, 0)]
            tangent /= np.linalg.norm(tangent)
            u = np.asarray(axis, dtype=float)
            u -= tangent * np.dot(u, tangent)
            if np.linalg.norm(u) < 1e-6:
                u = np.cross(tangent, [0.0, 0.0, 1.0])
            u /= np.linalg.norm(u)
            v = np.cross(tangent, u)
            rings.append([
                point + u * widths[index] * math.cos(step * 2 * math.pi / sides) + v * depths[index] * math.sin(step * 2 * math.pi / sides)
                for step in range(sides)
            ])
        def color_at(index: int) -> int:
            return colors[index % len(colors)] if isinstance(colors, list) else colors
        for row in range(len(points) - 1):
            for side in range(sides):
                next_side = (side + 1) % sides
                color = color_at(row + side)
                self.triangle([rings[row][side], rings[row][next_side], rings[row + 1][next_side]], [skins[row], skins[row], skins[row + 1]], color)
                self.triangle([rings[row][side], rings[row + 1][next_side], rings[row + 1][side]], [skins[row], skins[row + 1], skins[row + 1]], color)
        for side in range(sides):
            next_side = (side + 1) % sides
            self.triangle([points[0], rings[0][next_side], rings[0][side]], [skins[0], skins[0], skins[0]], color_at(side))
            self.triangle([points[-1], rings[-1][side], rings[-1][next_side]], [skins[-1], skins[-1], skins[-1]], color_at(side + 1))
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
        factors = [0.14, 0.70, 1.0, 0.70, 0.14]
        levels = [-0.96, -0.63, 0.0, 0.63, 0.96]
        self.tube(
            label,
            [center + np.array([0.0, level * radii[1], 0.0]) for level in levels],
            [factor * radii[0] for factor in factors],
            [factor * radii[2] for factor in factors],
            [skin] * len(factors),
            colors,
            sides=sides,
        )

    def fin(self, label: str, base: np.ndarray, direction: np.ndarray, width: float, height: float, skin: dict[str, float] | str, colors: int | list[int]) -> None:
        """Make a small faceted, closed crystal fin in original local space."""
        start = len(self.data["positions"])
        base = np.asarray(base, dtype=float)
        direction = np.asarray(direction, dtype=float)
        direction /= np.linalg.norm(direction)
        sideways = np.cross(direction, np.array([0.0, 1.0, 0.0]))
        if np.linalg.norm(sideways) < 1e-6:
            sideways = np.array([1.0, 0.0, 0.0])
        sideways /= np.linalg.norm(sideways)
        up = np.cross(sideways, direction)
        corners = [
            base - sideways * width,
            base + sideways * width,
            base + up * width * 0.5,
            base - up * width * 0.5,
        ]
        tip = base + direction * height
        for index in range(4):
            self.triangle([corners[index], corners[(index + 1) % 4], tip], [skin, skin, skin], colors[index % len(colors)] if isinstance(colors, list) else colors)
        self.triangle([corners[0], corners[2], corners[1]], [skin, skin, skin], colors[0] if isinstance(colors, list) else colors)
        self.triangle([corners[0], corners[3], corners[2]], [skin, skin, skin], colors[1] if isinstance(colors, list) else colors)
        self.pieces.append({"name": label, "vertex_start": start, "vertex_count": len(self.data["positions"]) - start})


def write_palette(path: Path) -> None:
    image = Image.new("RGB", (512, 512), "black")
    for index, encoded in enumerate(PALETTE):
        color = tuple(int(encoded[offset:offset + 2], 16) for offset in (0, 2, 4))
        x0 = (index % 4) * 128
        # Source UVs are top-origin and the runtime reverses V when assigning
        # Unity Mesh.uv. Store authored tiles bottom-to-top so their in-game
        # palette index remains the deliberate PALETTE order.
        y0 = (3 - index // 4) * 128
        tile = Image.new("RGB", (128, 128), color)
        draw = ImageDraw.Draw(tile)
        # Original subtle diagonal chisel marks retain a handmade palette when
        # close camera views land between flat-shaded facets.
        accent = tuple(min(255, int(component * 1.13)) for component in color)
        for shift in range(-128, 256, 24):
            draw.line([(shift, 0), (shift + 96, 128)], fill=accent, width=2)
        image.paste(tile, (x0, y0))
    image.save(path)


def build(reference_path: Path, output_dir: Path, *, write_runtime: bool = True) -> dict:
    output_dir.mkdir(parents=True, exist_ok=True)
    reference = np.load(reference_path, allow_pickle=False)
    names = reference["bone_names"].tolist()
    bindposes = np.asarray(reference["bindposes"], dtype=float)
    assert names == EXPECTED_BONES, "Troll B palette drifted"
    assert bindposes.shape == (len(names), 4, 4)
    centers = np.linalg.inv(bindposes)[:, :3, 3]
    surface = Surface(names, centers)
    B = surface.B

    # Cohesive torso, overlapping joint collars, and short mantle panels keep
    # the silhouette intact while retaining deliberate animation weight blends.
    surface.tube(
        "Cairnfire continuous torso",
        [B["Root_M"], B["BackA_M"], B["BackB_M"], B["Chest_M"]],
        [0.52, 0.61, 0.68, 0.73], [0.38, 0.43, 0.46, 0.48],
        [{"Root_M": 0.78, "BackA_M": 0.22}, {"Root_M": 0.28, "BackA_M": 0.72}, {"BackA_M": 0.34, "BackB_M": 0.66}, {"BackB_M": 0.28, "Chest_M": 0.72}],
        [1, 2, 3, 4, 2, 3, 5, 4, 3, 2], sides=12,
    )
    surface.ellipsoid("Low basalt pelvis", B["Root_M"] + np.array([0.0, 0.02, 0.02]), (0.58, 0.31, 0.42), "Root_M", [0, 1, 2, 3], sides=12)
    surface.ellipsoid("Layered chest plate", B["Chest_M"] + np.array([0.0, -0.02, 0.35]), (0.56, 0.42, 0.16), "Chest_M", [2, 3, 4, 5, 4, 3], sides=12)
    surface.ellipsoid("Cairnfire heart", B["Chest_M"] + np.array([0.0, 0.02, 0.52]), (0.12, 0.19, 0.045), "Chest_M", [6, 7, 8, 9], sides=8)
    surface.tube("Weathered neck collar", [B["Chest_M"] + np.array([0.0, 0.17, 0.0]), B["Neck_M"] + np.array([0.0, -0.03, 0.02])], [0.43, 0.31], [0.38, 0.30], [{"Chest_M": 0.65, "Neck_M": 0.35}, "Neck_M"], [6, 7, 6, 12], sides=12)
    surface.ellipsoid("Cairnfire head", B["Head_M"] + np.array([0.0, 0.10, 0.10]), (0.49, 0.47, 0.43), "Head_M", [1, 2, 3, 4, 3, 2], sides=12)
    surface.ellipsoid("Heavy lower jaw", B["Head_M"] + np.array([0.0, -0.13, 0.32]), (0.31, 0.18, 0.20), "Head_M", [0, 1, 2, 3], sides=10)
    for side in (-1, 1):
        surface.ellipsoid("Ember eye " + str(side), B["Head_M"] + np.array([side * 0.18, 0.14, 0.49]), (0.065, 0.052, 0.030), "Head_M", [8, 9], sides=8)
        surface.tube("Carved brow " + str(side), [B["Head_M"] + np.array([side * 0.04, 0.26, 0.44]), B["Head_M"] + np.array([side * 0.34, 0.21, 0.34])], [0.060, 0.050], [0.075, 0.068], ["Head_M", "Head_M"], [4, 5, 4], sides=6, axis=(0.0, 0.0, 1.0))
        surface.fin("Crown shard " + str(side), B["Hair_M"] + np.array([side * 0.23, -0.10, 0.10]), np.array([side * 0.25, 0.96, 0.10]), 0.12, 0.44, "Hair_M", [6, 8, 9, 7])
    surface.fin("Crown shard center", B["Hair_M"] + np.array([0.0, -0.08, 0.15]), np.array([0.0, 1.0, 0.10]), 0.15, 0.55, "Hair_M", [7, 8, 9, 6])
    surface.tube("Back mantle", [B["Chest_M"] + np.array([0.0, -0.10, -0.32]), B["BackB_M"] + np.array([0.0, -0.08, -0.38]), B["BackA_M"] + np.array([0.0, -0.11, -0.32])], [0.51, 0.55, 0.46], [0.09, 0.10, 0.08], ["Chest_M", {"Chest_M": 0.45, "BackB_M": 0.55}, {"BackB_M": 0.45, "BackA_M": 0.55}], [11, 10, 11, 12, 11, 10], sides=10)

    for suffix, sign in (("R", 1), ("L", -1)):
        scapula, shoulder, elbow, wrist = [B[name + "_" + suffix] for name in ("Scapula", "Shoulder", "Elbow", "Wrist")]
        surface.tube(
            "Shoulder bridge " + suffix,
            [B["Chest_M"] + np.array([sign * 0.23, 0.26, 0.02]), scapula, shoulder],
            [0.31, 0.35, 0.32], [0.30, 0.34, 0.31],
            [{"Chest_M": 0.65, "Scapula_" + suffix: 0.35}, {"Scapula_" + suffix: 0.72, "Shoulder_" + suffix: 0.28}, "Shoulder_" + suffix],
            [3, 4, 5, 4, 3, 6], sides=10,
        )
        surface.ellipsoid("Shoulder cairn " + suffix, shoulder, (0.34, 0.33, 0.35), "Shoulder_" + suffix, [1, 2, 3, 4], sides=10)
        surface.tube(
            "Upper arm " + suffix,
            [shoulder, (shoulder + elbow) / 2 + np.array([0.0, 0.04, 0.06]), elbow],
            [0.25, 0.27, 0.22], [0.25, 0.27, 0.22],
            ["Shoulder_" + suffix, {"Shoulder_" + suffix: 0.48, "Elbow_" + suffix: 0.52}, "Elbow_" + suffix],
            [1, 2, 3, 4, 3, 2], sides=10,
        )
        surface.ellipsoid("Elbow knot " + suffix, elbow, (0.23, 0.24, 0.23), "Elbow_" + suffix, [0, 1, 2, 3], sides=9)
        surface.tube(
            "Forearm " + suffix,
            [elbow, (elbow + wrist) / 2 + np.array([0.0, -0.01, 0.07]), wrist],
            [0.23, 0.28, 0.22], [0.23, 0.27, 0.22],
            ["Elbow_" + suffix, {"Elbow_" + suffix: 0.46, "Wrist_" + suffix: 0.54}, "Wrist_" + suffix],
            [2, 3, 4, 5, 4, 3], sides=10,
        )
        surface.ellipsoid("Hammer palm " + suffix, wrist + np.array([sign * 0.09, 0.0, 0.08]), (0.23, 0.18, 0.20), "Wrist_" + suffix, [1, 2, 3, 4], sides=9)
        for finger in ("MiddleFinger", "ThumbFinger"):
            chain = [B[f"{finger}{index}_{suffix}"] for index in (1, 2, 3)]
            endpoint = chain[-1] + (chain[-1] - chain[-2]) * 0.74
            finger_names = [f"{finger}{index}_{suffix}" for index in (1, 2, 3)]
            surface.tube(
                f"{finger} talon {suffix}",
                [chain[0], chain[1], chain[2], endpoint],
                [0.092, 0.075, 0.058, 0.028], [0.088, 0.071, 0.054, 0.025],
                [{"Wrist_" + suffix: 0.20, finger_names[0]: 0.80}, {finger_names[0]: 0.28, finger_names[1]: 0.72}, {finger_names[1]: 0.30, finger_names[2]: 0.70}, finger_names[2]],
                [4, 5, 6, 4, 3], sides=6,
            )

        hip, knee, ankle = [B[name + "_" + suffix] for name in ("Hip", "Knee", "Ankle")]
        toe_one, toe_two = B["MiddleToe1_" + suffix], B["MiddleToe2_" + suffix]
        surface.ellipsoid("Hip guard " + suffix, hip, (0.30, 0.29, 0.30), "Hip_" + suffix, [1, 2, 3, 4], sides=10)
        surface.tube(
            "Thigh " + suffix,
            [hip, (hip + knee) / 2 + np.array([0.0, 0.0, 0.07]), knee],
            [0.28, 0.30, 0.23], [0.27, 0.29, 0.23],
            ["Hip_" + suffix, {"Hip_" + suffix: 0.48, "Knee_" + suffix: 0.52}, "Knee_" + suffix],
            [1, 2, 3, 4, 3, 2], sides=10,
        )
        surface.ellipsoid("Knee shield " + suffix, knee + np.array([0.0, 0.0, 0.11]), (0.25, 0.23, 0.20), "Knee_" + suffix, [2, 3, 4, 5], sides=9)
        surface.tube(
            "Shin " + suffix,
            [knee, (knee + ankle) / 2 + np.array([0.0, 0.0, 0.06]), ankle],
            [0.24, 0.25, 0.20], [0.23, 0.24, 0.20],
            ["Knee_" + suffix, {"Knee_" + suffix: 0.46, "Ankle_" + suffix: 0.54}, "Ankle_" + suffix],
            [1, 2, 3, 4, 3, 2], sides=10,
        )
        surface.ellipsoid("Ankle stone " + suffix, ankle, (0.24, 0.20, 0.22), "Ankle_" + suffix, [0, 1, 2, 3], sides=9)
        surface.tube(
            "Forward foot " + suffix,
            [ankle, toe_one, toe_two, toe_two + (toe_two - toe_one) * 0.44],
            [0.23, 0.19, 0.12, 0.045], [0.27, 0.23, 0.16, 0.045],
            [{"Ankle_" + suffix: 0.65, "MiddleToe1_" + suffix: 0.35}, {"MiddleToe1_" + suffix: 0.72, "MiddleToe2_" + suffix: 0.28}, "MiddleToe2_" + suffix, "MiddleToe2_" + suffix],
            [2, 3, 4, 5, 4], sides=8,
        )

    asset_name = "cairnfire-trollb"
    write_palette(output_dir / f"{asset_name}.png")
    source_path = output_dir / f"{asset_name}.source.json"
    source_path.write_text(json.dumps(surface.data, separators=(",", ":")) + "\n")
    (output_dir / f"{asset_name}.pieces.json").write_text(json.dumps(surface.pieces, indent=2) + "\n")
    # Compute this without consulting a native surface. Every named palette bone
    # must have at least one original, positively weighted vertex.
    used_indices = sorted({joint for joints, weights in zip(surface.data["joints"], surface.data["weights"]) for joint, weight in zip(joints, weights) if weight > 0})
    assert used_indices == list(range(len(surface.names))), (used_indices, surface.names)
    report = {
        "status": "PASS_ORIGINAL_BIND_AUTHORED",
        "model": "Cairnfire Troll B",
        "nativeChassis": "trollB",
        "targetRendererId": 121256,
        "rendererPath": "enTroll01",
        "referenceSha256": sha(reference_path),
        "authoringInputs": ["bone_names", "bindposes"],
        "nativeSurfaceRead": False,
        "vertices": len(surface.data["positions"]),
        "triangles": len(surface.data["triangles"]),
        "paletteBones": len(surface.names),
        "weightedPaletteBones": len(used_indices),
        "allPaletteBonesWeighted": used_indices == list(range(len(surface.names))),
        "artScope": "Original cohesive basalt, copper, and ember design. Studio and live review remain separate gates.",
    }
    if write_runtime:
        glb_path = output_dir / f"{asset_name}.glb"
        write_glb(glb_path, surface.data, reference)
        validation = validate(glb_path, reference)
        validation.update(report)
        (output_dir / f"{asset_name}.validation.json").write_text(json.dumps(validation, indent=2) + "\n")
        report["glbSha256"] = sha(glb_path)
        report["textureSha256"] = sha(output_dir / f"{asset_name}.png")
    (output_dir / "build-report.json").write_text(json.dumps(report, indent=2) + "\n")
    if hasattr(reference, "close"):
        reference.close()
    return report


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--reference", type=Path, default=ROOT / "scratch" / "trollb-reference" / "reference.npz")
    parser.add_argument("--output-dir", type=Path, default=PACKAGE)
    parser.add_argument("--no-runtime", action="store_true", help="Write source and palette only, for provenance checks.")
    args = parser.parse_args()
    report = build(args.reference.resolve(), args.output_dir.resolve(), write_runtime=not args.no_runtime)
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
