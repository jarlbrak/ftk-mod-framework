#!/usr/bin/env python3
"""Build the original Mireglass Croaker asset for FTK's exact acidBlobB route.

The authoring phase uses only ordered bone names and inverse bind matrices.
After export, the independent validator reads the local reference to reject a
malformed binary/skin contract; it never feeds a native surface, UV, material,
skin weight, animation sample, or texture into authoring. Every emitted vertex,
triangle, normal, UV, influence, palette pixel, and named sculpture piece is
original authoring.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
PACKAGE = ROOT / "art-experiments" / "mireglass-croaker"
DEFAULT_REFERENCE = ROOT / "scratch" / "acidblob-b-reference-v1" / "reference.npz"
sys.path.insert(0, str(ROOT / "tools" / "ai-model-pipeline"))
from export_ftk_glb import write_glb
from validate_glb import validate


PALETTE = [
    "10271F",  # peat shadow
    "1D4B34",  # moss dark
    "39745A",  # marsh jade
    "55A97C",  # rain teal
    "9DE49B",  # acid mint
    "D3F7AC",  # lantern lime
    "D57A38",  # oxidized amber
    "F6BB62",  # brass gold
    "D8C69B",  # pale reed bone
    "493552",  # dusk violet
    "1A1020",  # eye black
    "E7F8E1",  # wet highlight
]

EXPECTED_BONES = [
    "Root_M", "Hips", "RHip", "RKnee", "RFoot", "joint8", "LHip", "LKnee",
    "LFoot", "joint19", "Tail_1", "Tail_2", "Tail_3", "Tail_End", "UpperJaw",
    "joint2", "joint4", "RUpper_1", "RUpper_2", "joint25", "LUpper_1",
    "LUpper_2", "joint31", "Head", "joint22", "LowerJaw", "RLower_1",
    "RLower_2", "joint28", "LLower_1", "LLower_2", "joint34",
]


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def binding_only(reference_path: Path) -> tuple[list[str], np.ndarray]:
    """Read the two permitted local-reference fields and nothing else."""
    with np.load(reference_path, allow_pickle=False) as reference:
        names = reference["bone_names"].tolist()
        bindposes = np.asarray(reference["bindposes"], dtype=float)
    if names != EXPECTED_BONES:
        raise ValueError("acidBlobB ordered palette changed; re-extract and inspect the route")
    if bindposes.shape != (len(names), 4, 4) or not np.isfinite(bindposes).all():
        raise ValueError("acidBlobB inverse bind matrix contract changed")
    return names, bindposes


def normalized(influences: str | dict[str, float]) -> dict[str, float]:
    values = {influences: 1.0} if isinstance(influences, str) else dict(influences)
    if not 1 <= len(values) <= 4 or any(not math.isfinite(weight) or weight <= 0 for weight in values.values()):
        raise ValueError(f"invalid original influence set: {values}")
    total = sum(values.values())
    return {bone: weight / total for bone, weight in values.items()}


class Surface:
    """A faceted, UV-textured original sculpture with explicit authored weights."""

    def __init__(self, names: list[str], bindposes: np.ndarray) -> None:
        self.names = names
        self.centers = np.linalg.inv(bindposes)[:, :3, 3]
        self.B = dict(zip(names, self.centers))
        self.data: dict[str, list] = {
            "positions": [], "normals": [], "uvs": [], "triangles": [],
            "joints": [], "weights": [], "bone_names": names,
        }
        self.pieces: list[dict[str, object]] = []

    @staticmethod
    def palette_value(value: int | list[int], index: int = 0) -> int:
        return value[index % len(value)] if isinstance(value, list) else value

    def triangle(self, points: list[np.ndarray], skins: list[str | dict[str, float]], color: int) -> None:
        vertices = [np.asarray(point, dtype=float) for point in points]
        normal = np.cross(vertices[1] - vertices[0], vertices[2] - vertices[0])
        length = float(np.linalg.norm(normal))
        if length <= 1e-9:
            raise ValueError("degenerate original triangle")
        start = len(self.data["positions"])
        self.data["triangles"].append([start, start + 1, start + 2])
        uv = [(color + 0.5) / len(PALETTE), 0.5]
        for point, skin in zip(vertices, skins):
            authored = normalized(skin)
            if any(bone not in self.B for bone in authored):
                raise ValueError(f"unknown authored rig bone: {authored}")
            pairs = [(self.names.index(bone), weight) for bone, weight in authored.items()]
            pairs += [(0, 0.0)] * (4 - len(pairs))
            self.data["positions"].append(point.tolist())
            self.data["normals"].append((normal / length).tolist())
            self.data["uvs"].append(uv)
            self.data["joints"].append([joint for joint, _ in pairs])
            self.data["weights"].append([weight for _, weight in pairs])

    def tube(
        self,
        label: str,
        points: list[np.ndarray],
        radii_x: list[float],
        radii_z: list[float],
        skins: list[str | dict[str, float]],
        colors: int | list[int],
        *,
        sides: int = 8,
        axis: tuple[float, float, float] = (1.0, 0.0, 0.0),
    ) -> None:
        if not (len(points) >= 2 and len(points) == len(radii_x) == len(radii_z) == len(skins)):
            raise ValueError(f"malformed tube: {label}")
        start = len(self.data["positions"])
        rows = [np.asarray(point, dtype=float) for point in points]
        rings: list[list[np.ndarray]] = []
        for index, center in enumerate(rows):
            tangent = rows[min(index + 1, len(rows) - 1)] - rows[max(index - 1, 0)]
            magnitude = float(np.linalg.norm(tangent))
            if magnitude <= 1e-8:
                raise ValueError(f"coincident tube controls: {label}")
            tangent /= magnitude
            u = np.asarray(axis, dtype=float)
            u -= tangent * np.dot(u, tangent)
            if np.linalg.norm(u) <= 1e-6:
                u = np.cross(tangent, np.array([0.0, 0.0, 1.0]))
            u /= np.linalg.norm(u)
            v = np.cross(tangent, u)
            rings.append([
                center + u * radii_x[index] * math.cos(step * math.tau / sides)
                + v * radii_z[index] * math.sin(step * math.tau / sides)
                for step in range(sides)
            ])
        for row in range(len(rows) - 1):
            for side in range(sides):
                following = (side + 1) % sides
                color = self.palette_value(colors, row + side)
                self.triangle(
                    [rings[row][side], rings[row][following], rings[row + 1][following]],
                    [skins[row], skins[row], skins[row + 1]], color,
                )
                self.triangle(
                    [rings[row][side], rings[row + 1][following], rings[row + 1][side]],
                    [skins[row], skins[row + 1], skins[row + 1]], color,
                )
        for side in range(sides):
            following = (side + 1) % sides
            self.triangle([rows[0], rings[0][following], rings[0][side]], [skins[0]] * 3,
                          self.palette_value(colors, side))
            self.triangle([rows[-1], rings[-1][side], rings[-1][following]], [skins[-1]] * 3,
                          self.palette_value(colors, side + 1))
        self.pieces.append({"name": label, "vertex_start": start,
                            "vertex_count": len(self.data["positions"]) - start})

    def ellipsoid(
        self,
        label: str,
        center: np.ndarray,
        radii: tuple[float, float, float],
        skin: str | dict[str, float],
        colors: int | list[int],
        *,
        sides: int = 10,
    ) -> None:
        center = np.asarray(center, dtype=float)
        fractions = [0.08, 0.54, 1.0, 0.54, 0.08]
        heights = [-0.95, -0.60, 0.0, 0.60, 0.95]
        self.tube(
            label,
            [center + np.array([0.0, height * radii[1], 0.0]) for height in heights],
            [fraction * radii[0] for fraction in fractions],
            [fraction * radii[2] for fraction in fractions],
            [skin] * len(fractions), colors, sides=sides,
        )

    def blade(
        self,
        label: str,
        points: list[np.ndarray],
        widths: list[float],
        thicknesses: list[float],
        skins: list[str | dict[str, float]],
        colors: int | list[int],
        *,
        axis: tuple[float, float, float],
    ) -> None:
        """A closed low-poly sail instead of a zero-thickness decorative plane."""
        self.tube(label, points, widths, thicknesses, skins, colors, sides=4, axis=axis)

    def all_palette_bones_weighted(self) -> bool:
        used = {self.names[joint] for row, weights in zip(self.data["joints"], self.data["weights"])
                for joint, weight in zip(row, weights) if weight > 0}
        return used == set(self.names)


def write_palette(path: Path) -> None:
    tile_width, height = 72, 96
    image = Image.new("RGB", (tile_width * len(PALETTE), height), "black")
    for index, encoded in enumerate(PALETTE):
        color = tuple(int(encoded[offset:offset + 2], 16) for offset in (0, 2, 4))
        tile = Image.new("RGB", (tile_width, height), color)
        draw = ImageDraw.Draw(tile)
        light = tuple(min(255, int(channel * 1.18 + 20)) for channel in color)
        dark = tuple(max(0, int(channel * 0.48)) for channel in color)
        for offset in range(-60, 120, 19):
            draw.arc((offset, 8, offset + 58, 72), 195, 345, fill=light, width=3)
        for y in range(13, height, 23):
            draw.line((0, y, tile_width, y - 10), fill=dark, width=2)
        draw.line((0, height - 6, tile_width, height - 6), fill=dark, width=5)
        image.paste(tile, (index * tile_width, 0))
    image.save(path)


def body_surface(names: list[str], bindposes: np.ndarray) -> Surface:
    surface = Surface(names, bindposes)
    B = surface.B
    root, hips = B["Root_M"], B["Hips"]
    upper_jaw, head, lower_jaw = B["UpperJaw"], B["Head"], B["LowerJaw"]

    # A pond-dragon silhouette: a broad wet belly, a layered shell, two paddling
    # haunches, a low tail, and a crown that keeps the active facial joints clear.
    surface.ellipsoid("Peat belly", root + np.array([0.0, 0.34, -0.13]),
                      (0.54, 0.38, 0.52), {"Root_M": 0.72, "Hips": 0.28},
                      [0, 1, 2, 1, 0], sides=12)
    surface.ellipsoid("Mireglass core", hips + np.array([0.0, 0.18, 0.04]),
                      (0.76, 0.61, 0.73), "Hips", [1, 2, 3, 2, 4, 2], sides=14)
    surface.tube(
        "Layered back shell",
        [hips + np.array([0.0, 0.35, -0.23]), B["Tail_1"] + np.array([0.0, 0.31, -0.02]),
         B["Tail_2"] + np.array([0.0, 0.22, 0.03]), B["Tail_3"] + np.array([0.0, 0.15, 0.04]),
         B["Tail_End"] + np.array([0.0, 0.09, 0.04])],
        [0.49, 0.47, 0.37, 0.24, 0.08], [0.42, 0.37, 0.29, 0.18, 0.06],
        [{"Hips": 0.50, "Tail_1": 0.50}, "Tail_1", {"Tail_1": 0.42, "Tail_2": 0.58},
         {"Tail_2": 0.35, "Tail_3": 0.65}, "Tail_End"], [0, 1, 2, 3, 1], sides=11,
    )
    surface.blade(
        "Lantern reed dorsal sail",
        [hips + np.array([0.0, 0.63, -0.32]), B["Tail_1"] + np.array([0.0, 0.66, -0.18]),
         B["Tail_2"] + np.array([0.0, 0.55, -0.06]), B["Tail_3"] + np.array([0.0, 0.36, 0.02])],
        [0.15, 0.21, 0.17, 0.05], [0.045, 0.055, 0.040, 0.012],
        [{"Hips": 0.46, "Tail_1": 0.54}, "Tail_1", {"Tail_1": 0.34, "Tail_2": 0.66},
         {"Tail_2": 0.38, "Tail_3": 0.62}], [4, 5, 4, 6], axis=(1.0, 0.0, 0.0),
    )
    surface.ellipsoid("Tail lantern bead", B["Tail_End"] + np.array([0.0, 0.05, 0.02]),
                      (0.105, 0.105, 0.105), "Tail_End", [4, 5, 7, 5], sides=8)

    for side, sign, toe in (("R", 1.0, "joint8"), ("L", -1.0, "joint19")):
        hip, knee, foot = B[f"{side}Hip"], B[f"{side}Knee"], B[f"{side}Foot"]
        surface.ellipsoid(f"{side} hip shield", hip + np.array([sign * 0.05, 0.03, 0.04]),
                          (0.30, 0.27, 0.28), f"{side}Hip", [1, 2, 3, 2], sides=10)
        surface.tube(
            f"{side} reed haunch",
            [hip, (hip + knee) / 2 + np.array([sign * 0.04, 0.02, 0.03]), knee,
             (knee + foot) / 2 + np.array([0.0, 0.0, 0.08]), foot],
            [0.27, 0.25, 0.20, 0.16, 0.18], [0.26, 0.24, 0.19, 0.15, 0.17],
            [f"{side}Hip", {f"{side}Hip": 0.46, f"{side}Knee": 0.54}, f"{side}Knee",
             {f"{side}Knee": 0.42, f"{side}Foot": 0.58}, f"{side}Foot"],
            [1, 2, 3, 2, 4], sides=9,
        )
        surface.ellipsoid(f"{side} webbed foot", foot + np.array([0.0, 0.01, 0.19]),
                          (0.30, 0.115, 0.31), f"{side}Foot", [2, 3, 4, 3], sides=10)
        surface.blade(
            f"{side} toe glow",
            [foot + np.array([0.0, 0.02, 0.26]), B[toe] + np.array([0.0, 0.01, 0.03])],
            [0.09, 0.018], [0.035, 0.008], [f"{side}Foot", toe], [4, 5, 7],
            axis=(sign, 0.0, 0.0),
        )
        surface.ellipsoid(f"{side} toe cap", B[toe] + np.array([0.0, 0.0, 0.015]),
                          (0.045, 0.045, 0.045), toe, [7, 8, 7], sides=7)

    surface.tube(
        "Gilled neck bridge",
        [hips + np.array([0.0, 0.43, 0.13]), upper_jaw + np.array([0.0, 0.08, -0.01]),
         head + np.array([0.0, -0.21, -0.04])],
        [0.45, 0.41, 0.31], [0.37, 0.34, 0.27],
        [{"Hips": 0.58, "UpperJaw": 0.42}, "UpperJaw", {"UpperJaw": 0.32, "Head": 0.68}],
        [0, 1, 2, 3], sides=11,
    )
    # Keep the face broad and legible from the combat three-quarter view. The
    # jaw-chain bones decorate cheeks and mouth corners rather than becoming
    # long free-floating whiskers in the bind pose.
    surface.ellipsoid("Crowned marsh skull", head + np.array([0.0, -0.02, 0.03]),
                      (0.55, 0.47, 0.50), {"UpperJaw": 0.18, "Head": 0.82}, [1, 2, 3, 4, 2], sides=13)
    surface.ellipsoid("Broad jade muzzle", head + np.array([0.0, -0.21, 0.43]),
                      (0.47, 0.24, 0.17), {"UpperJaw": 0.72, "Head": 0.28}, [2, 3, 4, 3], sides=12)
    surface.ellipsoid("Lower copper smile", head + np.array([0.0, -0.47, 0.43]),
                      (0.42, 0.13, 0.16), {"LowerJaw": 0.83, "Head": 0.17}, [6, 7, 6], sides=11)
    surface.ellipsoid("Deep smiling mouth seam", head + np.array([0.0, -0.35, 0.605]),
                      (0.33, 0.040, 0.020), "Head", 10, sides=10)
    surface.ellipsoid("Amber throat lantern", head + np.array([0.0, -0.34, 0.56]),
                      (0.15, 0.105, 0.045), "UpperJaw", [6, 7, 5, 7], sides=9)
    surface.tube(
        "Upper gill muzzle",
        [head + np.array([0.0, -0.17, 0.35]), head + np.array([0.0, -0.10, 0.56]),
         head + np.array([0.0, -0.13, 0.69])],
        [0.24, 0.19, 0.09], [0.13, 0.10, 0.045],
        ["UpperJaw", {"UpperJaw": 0.35, "joint2": 0.65}, "joint4"], [2, 3, 4, 3], sides=9,
    )
    surface.ellipsoid("Lower moss jaw", lower_jaw + np.array([0.0, 0.02, 0.22]),
                      (0.47, 0.20, 0.32), "LowerJaw", [0, 1, 2, 1], sides=11)
    for sign in (-1.0, 1.0):
        surface.ellipsoid(f"Lantern eye {sign:+.0f}", head + np.array([sign * 0.29, 0.14, 0.48]),
                          (0.17, 0.17, 0.060), "Head", [4, 5, 7, 5], sides=9)
        surface.ellipsoid(f"Ink pupil {sign:+.0f}", head + np.array([sign * 0.29, 0.14, 0.544]),
                          (0.050, 0.108, 0.015), "Head", 10, sides=8)
        surface.ellipsoid(f"Eye rain glint {sign:+.0f}", head + np.array([sign * 0.255, 0.200, 0.563]),
                          (0.025, 0.029, 0.010), "Head", 11, sides=7)
        surface.blade(
            f"Leaf brow {sign:+.0f}",
            [head + np.array([sign * 0.18, 0.31, 0.24]), head + np.array([sign * 0.43, 0.20, 0.18])],
            [0.10, 0.018], [0.035, 0.008], ["Head", "Head"], [1, 2, 3], axis=(0.0, 1.0, 0.0),
        )
    surface.blade(
        "Head lantern crest", [head + np.array([0.0, 0.39, -0.02]), head + np.array([0.0, 0.58, -0.07])],
        [0.11, 0.020], [0.032, 0.007], ["Head", "joint22"], [4, 5, 7], axis=(1.0, 0.0, 0.0),
    )
    surface.ellipsoid("Crest seed", head + np.array([0.0, 0.61, -0.07]),
                      (0.052, 0.052, 0.052), "joint22", [4, 5, 7], sides=8)

    chains = [
        ("R upper reed tusk", ["RUpper_1", "RUpper_2", "joint25"], 1.0, 0.06, 0.00),
        ("L upper reed tusk", ["LUpper_1", "LUpper_2", "joint31"], -1.0, 0.06, 0.00),
        ("R lower reed tusk", ["RLower_1", "RLower_2", "joint28"], 1.0, -0.26, -0.04),
        ("L lower reed tusk", ["LLower_1", "LLower_2", "joint34"], -1.0, -0.26, -0.04),
    ]
    for label, bones, sign, vertical, forward in chains:
        # Tiny separated cheek plates leave the eye/mouth read clear while each
        # native chain bone still has a positive authored influence.
        for index, bone in enumerate(bones):
            position = head + np.array([
                sign * (0.43 + index * 0.045), vertical - index * 0.105, 0.34 + forward,
            ])
            surface.ellipsoid(f"{label} plate {index + 1}", position,
                              (0.055 - index * 0.010, 0.065 - index * 0.012, 0.028),
                              bone, [7, 8, 4, 5], sides=7)

    if not surface.all_palette_bones_weighted():
        raise ValueError("authored Mireglass mesh must positively weight every exact palette bone")
    return surface


def build(reference_path: Path, output_dir: Path) -> dict[str, object]:
    reference_path = reference_path.resolve()
    output_dir = output_dir.resolve()
    names, bindposes = binding_only(reference_path)
    surface = body_surface(names, bindposes)
    output_dir.mkdir(parents=True, exist_ok=True)
    palette = output_dir / "mireglass-palette.png"
    source = output_dir / "mireglass.source.json"
    pieces = output_dir / "mireglass.pieces.json"
    glb = output_dir / "mireglass.glb"
    write_palette(palette)
    source.write_text(json.dumps(surface.data, separators=(",", ":")) + "\n")
    pieces.write_text(json.dumps(surface.pieces, indent=2) + "\n")
    reference = np.load(reference_path, allow_pickle=False)
    try:
        write_glb(glb, surface.data, reference)
        validation = validate(glb, reference)
    finally:
        reference.close()
    if not str(validation.get("status", "")).startswith("PASS"):
        raise ValueError("independent GLB validation failed")
    report = {
        "name": "Mireglass Croaker",
        "status": "PASS_ORIGINAL_BIND_AUTHORED",
        "artScope": "Original marsh-dragon geometry and palette; offline binary/bind validation only.",
        "authoringNativeSurfaceRead": False,
        "validationReadsNativeReference": True,
        "allowedReferenceFields": ["bone_names", "bindposes"],
        "target": {
            "nativeEnemy": "acidBlobB",
            "rendererPath": "enAcidMonster",
            "sourceRendererId": 121345,
            "controller": "AcidBlobController",
            "controllerId": 5931,
            "joints": len(names),
        },
        "reference": {"path": str(reference_path.relative_to(ROOT)), "sha256": sha256(reference_path)},
        "assets": {
            "mireglass": {
                "glbSha256": sha256(glb),
                "vertices": validation["vertices"],
                "triangles": validation["triangles"],
                "paletteBones": validation["bones"],
                "allPaletteBonesWeighted": surface.all_palette_bones_weighted(),
                "maxBindRestError": validation["max_bind_rest_error"],
            },
            "palette": {"sha256": sha256(palette), "bytes": palette.stat().st_size},
        },
        "limits": [
            "Offline bind validation does not establish runtime renderer selection, animation, materials, culling, portraits, or gameplay.",
            "acidBlobA evidence does not establish acidBlobB binding or visual acceptance.",
        ],
    }
    report_path = output_dir / "build-report.json"
    report_path.write_text(json.dumps(report, indent=2) + "\n")
    manifest = {
        "name": "Mireglass Croaker",
        "status": "ORIGINAL_ART_OFFLINE_BUILD_VALIDATED_LIVE_INTEGRATION_PENDING",
        "nativeSurfaceCopied": False,
        "target": report["target"],
        "originalAssets": {path.name: sha256(path) for path in (glb, palette, source, pieces)},
        "buildReportSha256": sha256(report_path),
        "limits": report["limits"],
    }
    (output_dir / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
    return report


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--reference", type=Path, default=DEFAULT_REFERENCE)
    parser.add_argument("--output-dir", type=Path, default=PACKAGE)
    args = parser.parse_args()
    report = build(args.reference, args.output_dir)
    print(json.dumps({"status": report["status"], "asset": report["assets"]["mireglass"]}, indent=2))


if __name__ == "__main__":
    main()
