#!/usr/bin/env python3
"""Build the original Lichenfang Prowler for FTK's enbaseywolf route.

The authoring phase reads only the exact ordered bone palette and inverse bind
matrices.  Geometry, UVs, normals, weights, palette pixels and named pieces are
created here from authored landmarks; no native mesh surface, texture, UV,
weight, animation, or material data is used to make the model.
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
PACKAGE = ROOT / "art-experiments" / "lichenfang-basey-wolf"
DEFAULT_REFERENCE = ROOT / "scratch" / "basey-wolf-reference-v1" / "reference.npz"
sys.path.insert(0, str(ROOT / "tools" / "ai-model-pipeline"))
from export_ftk_glb import write_glb
from validate_glb import validate


PALETTE = [
    "081412",  # pine-black silhouette
    "12312A",  # deep spruce
    "1E5641",  # lichen shadow
    "347855",  # wet moss
    "5D9B61",  # meadow lichen
    "94C96B",  # sunlit leaf
    "C7E49B",  # pale spores
    "365D57",  # blue slate fur
    "78A99B",  # rain-silver guard hairs
    "A86735",  # weathered copper
    "E2AA57",  # amber eye and claw
    "F4D67A",  # lantern gold
    "15100E",  # ink pupil and mouth
    "D8E7C2",  # bark-bone highlight
]

EXPECTED_BONES = [
    "Root_M", "BackA_M", "BackB_M", "Chest_M", "Neck_M", "Head_M", "Jaw_M", "JawEnd_M", "Hair_M",
    "Scapula_R", "frontHip_R", "frontKnee_R", "frontAnkle_R", "frontBall_R",
    "Scapula_L", "frontHip_L", "frontKnee_L", "frontAnkle_L", "frontBall_L",
    "Tail0_M", "Tail1_M", "Tail2_M", "Tail3_M",
    "Rump_R", "backHip_R", "backKnee_R", "backAnkle_R", "backBall_R",
    "Rump_L", "backHip_L", "backKnee_L", "backAnkle_L", "backBall_L",
]


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def binding_only(reference_path: Path) -> tuple[list[str], np.ndarray]:
    """Read only the local reference fields permitted for art fitting."""
    with np.load(reference_path, allow_pickle=False) as reference:
        names = reference["bone_names"].tolist()
        bindposes = np.asarray(reference["bindposes"], dtype=float)
    if names != EXPECTED_BONES:
        raise ValueError("enbaseywolf ordered palette changed; re-extract and inspect the exact route")
    if bindposes.shape != (len(names), 4, 4) or not np.isfinite(bindposes).all():
        raise ValueError("enbaseywolf inverse bind matrix contract changed")
    return names, bindposes


def normalized(influences: str | dict[str, float]) -> dict[str, float]:
    values = {influences: 1.0} if isinstance(influences, str) else dict(influences)
    if not 1 <= len(values) <= 4 or any(not math.isfinite(weight) or weight <= 0 for weight in values.values()):
        raise ValueError(f"invalid original influence set: {values}")
    total = sum(values.values())
    return {bone: weight / total for bone, weight in values.items()}


class Surface:
    """Faceted original sculpture with explicit authored skin influences."""

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
        magnitude = float(np.linalg.norm(normal))
        if magnitude <= 1e-9:
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
            self.data["normals"].append((normal / magnitude).tolist())
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
        """A closed low-poly leaf, never a root-rigid decorative plane."""
        self.tube(label, points, widths, thicknesses, skins, colors, sides=4, axis=axis)

    def all_palette_bones_weighted(self) -> bool:
        used = {self.names[joint] for row, weights in zip(self.data["joints"], self.data["weights"])
                for joint, weight in zip(row, weights) if weight > 0}
        return used == set(self.names)


def write_palette(path: Path) -> None:
    tile_width, height = 72, 104
    image = Image.new("RGB", (tile_width * len(PALETTE), height), "black")
    for index, encoded in enumerate(PALETTE):
        color = tuple(int(encoded[offset:offset + 2], 16) for offset in (0, 2, 4))
        tile = Image.new("RGB", (tile_width, height), color)
        draw = ImageDraw.Draw(tile)
        light = tuple(min(255, int(channel * 1.17 + 23)) for channel in color)
        dark = tuple(max(0, int(channel * 0.42)) for channel in color)
        for diagonal in range(-70, 140, 22):
            draw.line((diagonal, height, diagonal + 48, -4), fill=dark, width=2)
        for y in range(15, height, 27):
            draw.arc((-12, y - 12, 72, y + 25), 200, 340, fill=light, width=2)
        for x in range(9, tile_width, 19):
            draw.ellipse((x, 22 + (x * 5) % 37, x + 3, 25 + (x * 5) % 37), fill=light)
        draw.line((0, height - 6, tile_width, height - 6), fill=dark, width=5)
        image.paste(tile, (index * tile_width, 0))
    image.save(path)


def wolf_surface(names: list[str], bindposes: np.ndarray) -> Surface:
    surface = Surface(names, bindposes)
    B = surface.B

    # The whole silhouette is an original moss-armored prowler: an intentionally
    # compact blue-green body, high ruff, broad paws and a luminous leaf tail.
    # The centers below come only from inverse binds; offsets and proportions are
    # authored landmarks rather than measurements from a native surface.
    surface.tube(
        "Prowler ribcage",
        [B["Root_M"] + np.array([0.0, 0.02, 0.04]), B["BackA_M"] + np.array([0.0, 0.09, 0.02]),
         B["BackB_M"] + np.array([0.0, 0.12, 0.02]), B["Chest_M"] + np.array([0.0, 0.13, -0.02])],
        [0.36, 0.51, 0.54, 0.47], [0.34, 0.40, 0.42, 0.36],
        ["Root_M", {"Root_M": 0.35, "BackA_M": 0.65},
         {"BackA_M": 0.38, "BackB_M": 0.62}, {"BackB_M": 0.42, "Chest_M": 0.58}],
        [0, 1, 2, 3, 7], sides=13,
    )
    surface.ellipsoid("Low lichen belly", B["BackA_M"] + np.array([0.0, -0.16, 0.03]),
                      (0.46, 0.26, 0.53), {"Root_M": 0.38, "BackA_M": 0.62}, [0, 1, 2, 1], sides=12)
    surface.ellipsoid("Slate shoulder mantle", B["Chest_M"] + np.array([0.0, 0.18, -0.02]),
                      (0.53, 0.29, 0.41), {"BackB_M": 0.35, "Chest_M": 0.65}, [2, 3, 4, 7, 8], sides=12)
    surface.tube(
        "Layered throat ruff",
        [B["Chest_M"] + np.array([0.0, 0.18, 0.15]), B["Neck_M"] + np.array([0.0, 0.19, 0.06]),
         B["Head_M"] + np.array([0.0, 0.13, -0.02])],
        [0.42, 0.36, 0.30], [0.29, 0.29, 0.27],
        [{"Chest_M": 0.57, "Neck_M": 0.43}, "Neck_M", {"Neck_M": 0.35, "Head_M": 0.65}],
        [2, 3, 4, 5, 7, 8], sides=11,
    )
    for sign in (-1.0, 1.0):
        surface.blade(
            f"Lichen ruff leaf {sign:+.0f}",
            [B["Chest_M"] + np.array([sign * 0.25, 0.17, 0.04]),
             B["Neck_M"] + np.array([sign * 0.47, 0.25, 0.01]),
             B["Head_M"] + np.array([sign * 0.36, 0.24, 0.03])],
            [0.12, 0.16, 0.035], [0.035, 0.044, 0.010],
            [{"Chest_M": 0.62, "Neck_M": 0.38}, "Neck_M", {"Neck_M": 0.40, "Head_M": 0.60}],
            [3, 4, 5, 6], axis=(0.0, 1.0, 0.0),
        )

    surface.ellipsoid("Moss-crowned skull", B["Head_M"] + np.array([0.0, 0.10, 0.02]),
                      (0.42, 0.35, 0.38), {"Neck_M": 0.18, "Head_M": 0.82}, [1, 2, 3, 7, 8], sides=13)
    surface.tube(
        "Tapered amber muzzle",
        [B["Head_M"] + np.array([0.0, -0.03, 0.18]), B["Jaw_M"] + np.array([0.0, 0.02, -0.02]),
         B["JawEnd_M"] + np.array([0.0, 0.04, -0.01])],
        [0.31, 0.26, 0.08], [0.25, 0.20, 0.06],
        [{"Head_M": 0.54, "Jaw_M": 0.46}, "Jaw_M", "JawEnd_M"], [2, 3, 4, 5, 9], sides=11,
    )
    surface.ellipsoid("Copper lower jaw", B["Jaw_M"] + np.array([0.0, -0.13, 0.11]),
                      (0.29, 0.09, 0.28), {"Head_M": 0.25, "Jaw_M": 0.75}, [9, 10, 11, 9], sides=10)
    surface.blade(
        "Muzzle leaf nose",
        [B["Jaw_M"] + np.array([0.0, 0.09, 0.10]), B["JawEnd_M"] + np.array([0.0, 0.09, 0.02])],
        [0.105, 0.022], [0.040, 0.010], ["Jaw_M", "JawEnd_M"], [1, 12, 13], axis=(1.0, 0.0, 0.0),
    )
    for sign in (-1.0, 1.0):
        surface.ellipsoid(f"Amber eye {sign:+.0f}", B["Head_M"] + np.array([sign * 0.28, 0.11, 0.25]),
                          (0.105, 0.105, 0.035), "Head_M", [10, 11, 10], sides=8)
        surface.ellipsoid(f"Ink eye slit {sign:+.0f}", B["Head_M"] + np.array([sign * 0.28, 0.11, 0.286]),
                          (0.028, 0.072, 0.012), "Head_M", 12, sides=7)
        surface.blade(
            f"Brow fern {sign:+.0f}",
            [B["Head_M"] + np.array([sign * 0.19, 0.27, 0.11]), B["Head_M"] + np.array([sign * 0.42, 0.27, 0.10])],
            [0.075, 0.015], [0.028, 0.006], ["Head_M", "Head_M"], [4, 5, 6], axis=(0.0, 1.0, 0.0),
        )
    for sign in (-1.0, 1.0):
        surface.blade(
            f"Copper leaf ear {sign:+.0f}",
            [B["Head_M"] + np.array([sign * 0.20, 0.24, -0.03]), B["Hair_M"] + np.array([sign * 0.22, 0.16, -0.02])],
            [0.15, 0.024], [0.050, 0.010], [{"Head_M": 0.48, "Hair_M": 0.52}, "Hair_M"],
            [3, 4, 9, 10], axis=(sign, 0.0, 0.0),
        )
    surface.blade(
        "Central spore crest",
        [B["Head_M"] + np.array([0.0, 0.30, -0.12]), B["Hair_M"] + np.array([0.0, 0.18, -0.05])],
        [0.12, 0.018], [0.038, 0.008], [{"Head_M": 0.30, "Hair_M": 0.70}, "Hair_M"],
        [4, 5, 6, 8], axis=(1.0, 0.0, 0.0),
    )

    # Each leg uses full-volume tubes on the exact native chains.  This keeps
    # knees, ankles and balls visibly articulated under wolfController motion.
    for side, sign in (("R", 1.0), ("L", -1.0)):
        scapula, hip, knee, ankle, ball = (B[f"Scapula_{side}"], B[f"frontHip_{side}"],
                                            B[f"frontKnee_{side}"], B[f"frontAnkle_{side}"], B[f"frontBall_{side}"])
        surface.ellipsoid(f"{side} scapula lichen pad", scapula + np.array([sign * 0.04, 0.01, -0.015]),
                          (0.20, 0.19, 0.18), f"Scapula_{side}", [2, 3, 4, 5], sides=9)
        surface.tube(
            f"{side} front moss limb", [scapula, hip, knee, ankle, ball],
            [0.21, 0.19, 0.145, 0.115, 0.14], [0.19, 0.17, 0.13, 0.105, 0.16],
            [f"Scapula_{side}", {f"Scapula_{side}": 0.33, f"frontHip_{side}": 0.67}, f"frontKnee_{side}",
             {f"frontKnee_{side}": 0.37, f"frontAnkle_{side}": 0.63}, f"frontBall_{side}"],
            [1, 2, 3, 4, 7, 8], sides=8,
        )
        surface.ellipsoid(f"{side} front copper paw", ball + np.array([0.0, -0.015, 0.10]),
                          (0.18, 0.10, 0.25), f"frontBall_{side}", [2, 3, 9, 10], sides=9)
        for toe in (-0.075, 0.0, 0.075):
            surface.blade(
                f"{side} front claw {toe:+.3f}",
                [ball + np.array([toe + sign * 0.015, -0.015, 0.18]), ball + np.array([toe + sign * 0.015, -0.02, 0.31])],
                [0.030, 0.008], [0.018, 0.006], [f"frontBall_{side}", f"frontBall_{side}"], [10, 11, 13],
                axis=(1.0, 0.0, 0.0),
            )

    for side, sign in (("R", 1.0), ("L", -1.0)):
        rump, hip, knee, ankle, ball = (B[f"Rump_{side}"], B[f"backHip_{side}"], B[f"backKnee_{side}"],
                                          B[f"backAnkle_{side}"], B[f"backBall_{side}"])
        surface.ellipsoid(f"{side} rump shield", rump + np.array([sign * 0.04, 0.03, -0.03]),
                          (0.28, 0.24, 0.25), f"Rump_{side}", [1, 2, 3, 4], sides=10)
        surface.ellipsoid(f"{side} folded haunch", hip + np.array([sign * 0.035, 0.01, -0.04]),
                          (0.28, 0.28, 0.30), f"backHip_{side}", [2, 3, 4, 7], sides=10)
        surface.tube(
            f"{side} back fern limb", [rump, hip, knee, ankle, ball],
            [0.20, 0.24, 0.17, 0.115, 0.15], [0.18, 0.23, 0.15, 0.105, 0.17],
            [f"Rump_{side}", {f"Rump_{side}": 0.30, f"backHip_{side}": 0.70}, f"backKnee_{side}",
             {f"backKnee_{side}": 0.35, f"backAnkle_{side}": 0.65}, f"backBall_{side}"],
            [0, 1, 2, 3, 4, 7], sides=8,
        )
        surface.ellipsoid(f"{side} back copper paw", ball + np.array([0.0, -0.01, 0.09]),
                          (0.18, 0.10, 0.23), f"backBall_{side}", [2, 3, 9, 10], sides=9)
        surface.blade(
            f"{side} heel leaf", [ankle + np.array([sign * 0.03, 0.04, -0.03]), ball + np.array([sign * 0.06, 0.07, -0.02])],
            [0.075, 0.014], [0.026, 0.006], [f"backAnkle_{side}", f"backBall_{side}"], [4, 5, 6],
            axis=(sign, 0.0, 0.0),
        )

    surface.tube(
        "Four-segment leaf tail",
        [B["Root_M"] + np.array([0.0, 0.12, -0.18]), B["Tail0_M"] + np.array([0.0, 0.11, 0.00]),
         B["Tail1_M"] + np.array([0.0, 0.10, 0.00]), B["Tail2_M"] + np.array([0.0, 0.08, 0.00]),
         B["Tail3_M"] + np.array([0.0, 0.05, 0.00])],
        [0.29, 0.27, 0.21, 0.14, 0.055], [0.27, 0.24, 0.18, 0.12, 0.045],
        [{"Root_M": 0.48, "Tail0_M": 0.52}, "Tail0_M", {"Tail0_M": 0.38, "Tail1_M": 0.62},
         {"Tail1_M": 0.36, "Tail2_M": 0.64}, {"Tail2_M": 0.34, "Tail3_M": 0.66}],
        [0, 1, 2, 3, 4, 7, 8], sides=10,
    )
    tail_bones = ["Tail0_M", "Tail1_M", "Tail2_M", "Tail3_M"]
    for index, bone in enumerate(tail_bones):
        center = B[bone]
        for sign in (-1.0, 1.0):
            surface.blade(
                f"Tail lichen vane {index + 1} {sign:+.0f}",
                [center + np.array([sign * 0.05, 0.10, -0.02]), center + np.array([sign * (0.22 - index * 0.03), 0.20, -0.04])],
                [0.070 - index * 0.008, 0.012], [0.026, 0.006], [bone, bone], [3, 4, 5, 6],
                axis=(0.0, 1.0, 0.0),
            )
    surface.ellipsoid("Tail lantern seed", B["Tail3_M"] + np.array([0.0, 0.04, -0.02]),
                      (0.075, 0.075, 0.075), "Tail3_M", [5, 6, 10, 11], sides=8)

    if not surface.all_palette_bones_weighted():
        raise ValueError("authored Lichenfang mesh must positively weight every exact palette bone")
    return surface


def build(reference_path: Path, output_dir: Path) -> dict[str, object]:
    reference_path = reference_path.resolve()
    output_dir = output_dir.resolve()
    names, bindposes = binding_only(reference_path)
    surface = wolf_surface(names, bindposes)
    output_dir.mkdir(parents=True, exist_ok=True)
    palette = output_dir / "lichenfang-palette.png"
    source = output_dir / "lichenfang.source.json"
    pieces = output_dir / "lichenfang.pieces.json"
    glb = output_dir / "lichenfang.glb"
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
        "name": "Lichenfang Prowler",
        "status": "PASS_ORIGINAL_BIND_AUTHORED",
        "artScope": "Original lichen-armored wolf geometry and palette; offline binary/bind validation only.",
        "authoringNativeSurfaceRead": False,
        "validationReadsNativeReference": True,
        "allowedReferenceFields": ["bone_names", "bindposes"],
        "target": {
            "resourcePrefab": "enbaseywolf",
            "nativeEnemy": "wolfA",
            "rendererPath": "Wolfie",
            "sourceRendererId": 120975,
            "controller": "wolfController",
            "controllerId": 6007,
            "joints": len(names),
        },
        "reference": {"path": str(reference_path.relative_to(ROOT)), "sha256": sha256(reference_path)},
        "assets": {
            "lichenfang": {
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
            "Offline bind validation does not establish resource prefab selection, runtime renderer binding, animation, materials, culling, portraits, or gameplay.",
            "Direct wolfA evidence and sibling resource-prefab evidence do not establish this enbaseywolf source pair.",
        ],
    }
    report_path = output_dir / "build-report.json"
    report_path.write_text(json.dumps(report, indent=2) + "\n")
    manifest = {
        "name": "Lichenfang Prowler",
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
    print(json.dumps(build(args.reference, args.output_dir), indent=2))


if __name__ == "__main__":
    main()
