#!/usr/bin/env python3
"""Author Duneshade Asp from the Desert Snake A binding palette only.

The generator reads only bone_names and inverse bind matrices from the local
reference.  Every surface below is original procedural geometry; native mesh
positions, topology, normals, UVs, skin weights, textures, and animation curves
are never used as authoring input.
"""
from __future__ import annotations

import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image


OUT = Path(__file__).resolve().parent
ROOT = OUT.parent.parent
sys.path.insert(0, str(ROOT / "tools/ai-model-pipeline"))
from export_ftk_glb import write_glb
from validate_glb import validate


PALETTE = [
    "130E22",  # moon-violet shadow
    "2C1B3D",  # deep amethyst scale
    "553653",  # weathered plum
    "906057",  # rose sandstone
    "C68C56",  # sun-baked ochre
    "E5BE72",  # mica gold
    "F0E0A6",  # pale limestone
    "2C5870",  # lapis inset
    "17424E",  # deep oasis teal
    "4D918C",  # turquoise patina
    "B94E43",  # ember red
    "F08A3E",  # flare orange
    "FFF0BB",  # eye ivory
    "16131D",  # obsidian eye socket
    "67E5D6",  # glassy venom eye
    "A34C8D",  # desert orchid tongue
]


def write_palette() -> None:
    image = Image.new("RGB", (32 * len(PALETTE), 32))
    for index, value in enumerate(PALETTE):
        image.paste(tuple(int(value[offset:offset + 2], 16) for offset in (0, 2, 4)), (index * 32, 0, (index + 1) * 32, 32))
    image.save(OUT / "duneshade-desert-asp_basecolor.png")


class Surface:
    """A small original low-poly surface writer with explicit skinning."""

    def __init__(self, renderer_id: int):
        self.reference = np.load(ROOT / f"scratch/skeleton-audit/{renderer_id}/reference.npz", allow_pickle=False)
        self.names = self.reference["bone_names"].tolist()
        self.centers = np.linalg.inv(self.reference["bindposes"])[:, :3, 3]
        self.B = dict(zip(self.names, self.centers))
        self.data = {key: [] for key in ("positions", "normals", "uvs", "triangles", "joints", "weights")}
        self.data["bone_names"] = self.names
        self.pieces: list[dict[str, object]] = []

    def skin(self, source: str | dict[str, float]) -> dict[str, float]:
        return {source: 1.0} if isinstance(source, str) else source

    def triangle(self, points, skins, color: int) -> None:
        points = np.asarray(points, dtype=float)
        normal = np.cross(points[1] - points[0], points[2] - points[0])
        length = float(np.linalg.norm(normal))
        if length < 1e-10:
            raise ValueError("Degenerate authored triangle")
        normal /= length
        start = len(self.data["positions"])
        self.data["triangles"].append([start, start + 1, start + 2])
        for point, source in zip(points, skins):
            weights = self.skin(source)
            indices = [self.names.index(name) for name in weights]
            values = list(weights.values())
            total = sum(values)
            if not (0 < len(indices) <= 4 and total > 0):
                raise ValueError("Invalid authored skin")
            self.data["positions"].append(point.tolist())
            self.data["normals"].append(normal.tolist())
            self.data["uvs"].append([(color + .5) / len(PALETTE), .5])
            self.data["joints"].append(indices + [0] * (4 - len(indices)))
            self.data["weights"].append([value / total for value in values] + [0.0] * (4 - len(indices)))

    def tube(self, label, points, widths, depths, skins, color, axis=(1, 0, 0), sides=8) -> None:
        """Write a capped, consistently wound original tapered volume."""
        start = len(self.data["positions"])
        points = np.asarray(points, dtype=float)
        if not (len(points) == len(widths) == len(depths) == len(skins) and len(points) >= 2):
            raise ValueError("Tube inputs disagree")
        rings = []
        for index, point in enumerate(points):
            tangent = points[min(index + 1, len(points) - 1)] - points[max(index - 1, 0)]
            length = float(np.linalg.norm(tangent))
            if length < 1e-10:
                raise ValueError("Coincident bind landmarks")
            tangent /= length
            u = np.asarray(axis, dtype=float)
            u -= tangent * np.dot(u, tangent)
            if np.linalg.norm(u) < 0.01:
                u = np.cross(tangent, [0, 0, 1])
            u /= np.linalg.norm(u)
            v = np.cross(tangent, u)
            rings.append([
                point + u * widths[index] * math.cos(slot * 2 * math.pi / sides)
                + v * depths[index] * math.sin(slot * 2 * math.pi / sides)
                for slot in range(sides)
            ])
        for index in range(len(points) - 1):
            for slot in range(sides):
                next_slot = (slot + 1) % sides
                shade = color[slot % len(color)] if isinstance(color, list) else color
                self.triangle([rings[index][slot], rings[index][next_slot], rings[index + 1][next_slot]],
                              [skins[index], skins[index], skins[index + 1]], shade)
                self.triangle([rings[index][slot], rings[index + 1][next_slot], rings[index + 1][slot]],
                              [skins[index], skins[index + 1], skins[index + 1]], shade)
        cap_color = color[0] if isinstance(color, list) else color
        for slot in range(sides):
            next_slot = (slot + 1) % sides
            self.triangle([points[0], rings[0][next_slot], rings[0][slot]], [skins[0]] * 3, cap_color)
            self.triangle([points[-1], rings[-1][slot], rings[-1][next_slot]], [skins[-1]] * 3, cap_color)
        self.pieces.append({"name": label, "vertex_start": start, "vertex_count": len(self.data["positions"]) - start})

    def ellipsoid(self, label, center, size, bone, color, sides=10) -> None:
        center = np.asarray(center, dtype=float)
        points = [center + np.array([0, factor * size[1], 0]) for factor in (-0.98, -0.70, 0, 0.70, 0.98)]
        radii = (0.20, 0.71, 1.0, 0.71, 0.20)
        self.tube(label, points, [radius * size[0] for radius in radii], [radius * size[2] for radius in radii], [bone] * 5, color, sides=sides)

    def pyramid(self, label, center, width, depth, height, bone, color) -> None:
        """A closed four-sided dorsal thorn, deliberately a real volume."""
        start = len(self.data["positions"])
        center = np.asarray(center, dtype=float)
        base = [
            center + np.array([-width, 0, -depth]), center + np.array([width, 0, -depth]),
            center + np.array([width, 0, depth]), center + np.array([-width, 0, depth]),
        ]
        apex = center + np.array([0, height, 0])
        for index in range(4):
            next_index = (index + 1) % 4
            self.triangle([base[index], base[next_index], apex], [bone] * 3, color[index % len(color)])
        self.triangle([base[0], base[2], base[1]], [bone] * 3, color[-1])
        self.triangle([base[0], base[3], base[2]], [bone] * 3, color[-1])
        self.pieces.append({"name": label, "vertex_start": start, "vertex_count": len(self.data["positions"]) - start})

    def kite(self, label, points, skins, color, thickness=0.04) -> None:
        """A closed four-corner leaf plate for the cobra-style throat hood."""
        start = len(self.data["positions"])
        points = np.asarray(points, dtype=float)
        normal = np.cross(points[1] - points[0], points[2] - points[0])
        length = float(np.linalg.norm(normal))
        if points.shape != (4, 3) or len(skins) != 4 or length < 1e-10:
            raise ValueError("Invalid authored kite")
        normal /= length
        front, back = points + normal * thickness, points - normal * thickness
        face_color = color[0] if isinstance(color, list) else color
        for indices in ((0, 1, 2), (0, 2, 3)):
            self.triangle([front[index] for index in indices], [skins[index] for index in indices], face_color)
            self.triangle([back[index] for index in indices[::-1]], [skins[index] for index in indices[::-1]], face_color)
        edge_color = color[-1] if isinstance(color, list) else color
        for index in range(4):
            next_index = (index + 1) % 4
            self.triangle([front[index], back[index], back[next_index]], [skins[index], skins[index], skins[next_index]], edge_color)
            self.triangle([front[index], back[next_index], front[next_index]], [skins[index], skins[next_index], skins[next_index]], edge_color)
        self.pieces.append({"name": label, "vertex_start": start, "vertex_count": len(self.data["positions"]) - start})

    def write(self, name: str) -> dict:
        (OUT / f"{name}.source.json").write_text(json.dumps(self.data, separators=(",", ":")) + "\n")
        write_glb(OUT / f"{name}.glb", self.data, self.reference)
        validation = validate(OUT / f"{name}.glb", self.reference)
        (OUT / f"{name}.validation.json").write_text(json.dumps(validation, indent=2) + "\n")
        (OUT / f"{name}.pieces.json").write_text(json.dumps(self.pieces, indent=2) + "\n")
        return validation


def blend(a: str, b: str, amount: float) -> dict[str, float]:
    return {a: 1 - amount, b: amount}


write_palette()
surface = Surface(121552)
B = surface.B

# This exact 46-joint chassis grows from its native tail through Root_M into the
# front ribs and head. Every authored body ring is tied to its matching joint;
# the longer Desert tongue additionally uses Tongue_08 and Tongue_End.
body_names = ["BackRib_End"] + [f"BackRib_{index:02d}" for index in range(20, 0, -1)] + ["Root_M"] + [f"FrontRib_{index:02d}" for index in range(1, 12)]
assert all(name in B for name in body_names) and len(body_names) == 33
body_points = [B[name] for name in body_names]
widths, depths = [], []
for index, name in enumerate(body_names):
    progress = index / (len(body_names) - 1)
    if progress < .16:
        radius = .12 + progress / .16 * .27
    elif progress < .70:
        radius = .39 + .15 * math.sin((progress - .16) / .54 * math.pi)
    else:
        radius = .48 - (progress - .70) / .30 * .055
    widths.append(radius)
    depths.append(radius * .84)
surface.tube("Articulated duneshell body", body_points, widths, depths, body_names,
             [0, 1, 2, 3, 4, 3, 2, 1, 7, 8, 9, 8, 2, 3, 4, 5], sides=10)

# Three little original tail bells make the low back end legible while remaining
# attached to the real terminal tail joint rather than a root-bound accessory.
tail = B["BackRib_End"]
for index, offset in enumerate(((-.08, .04, -.18), (.08, .04, -.36), (0, .06, -.54)), 1):
    surface.ellipsoid(
        f"Sunstone tail bell {index}", tail + np.array(offset), (.16, .13, .16),
        "BackRib_End", [4, 5, 11, 5, 4, 13], sides=8,
    )

# A spaced ridge of asymmetric sunspines changes the silhouette at combat range
# without borrowing a native spine surface or topology.
spine_names = ["BackRib_18", "BackRib_15", "BackRib_12", "BackRib_09", "BackRib_06", "BackRib_03", "FrontRib_03", "FrontRib_06", "FrontRib_09", "FrontRib_11"]
for index, name in enumerate(spine_names, 1):
    chain_index = body_names.index(name)
    surface.pyramid(
        f"Dunespire {index}", B[name] + np.array([0, widths[chain_index] * .55, 0]),
        widths[chain_index] * .38, .18, .31 + .20 * math.sin(index / len(spine_names) * math.pi), name,
        [4, 5, 11, 10, 13],
    )

# Closed paired sunshield panels create a different, broad desert-asp outline.
for side, sign in (("R", 1), ("L", -1)):
    surface.kite(
        f"{side} lapis sunshield",
        [B["FrontRib_08"] + np.array([sign * .12, .14, -.20]), B["FrontRib_10"] + np.array([sign * 1.28, .40, .08]), B["Head"] + np.array([sign * 1.02, .16, -.18]), B["Head"] + np.array([sign * .17, .10, -.47])],
        ["FrontRib_08", "FrontRib_10", "Head", "Head"], [7, 8, 9, 5, 4, 2, 13], thickness=.048,
    )
    surface.kite(
        f"{side} amber sunshield inlay",
        [B["FrontRib_09"] + np.array([sign * .14, .18, -.16]), B["FrontRib_10"] + np.array([sign * .83, .31, .02]), B["Head"] + np.array([sign * .71, .18, -.14]), B["Head"] + np.array([sign * .16, .15, -.35])],
        ["FrontRib_09", "FrontRib_10", "Head", "Head"], [4, 5, 6, 5, 4, 10], thickness=.032,
    )

# The articulated face uses the actual head, jaw, tongue, and joint13 palette.
surface.ellipsoid("Faceted sunstone asp head", B["Head"] + np.array([0, .13, .12]), (.79, .62, .86), "Head", [1, 2, 3, 4, 5, 4, 3, 7], sides=11)
surface.tube("Animated limestone lower jaw", [B["Jaw"], B["Jaw_End"]], [.35, .12], [.30, .10], ["Jaw", "Jaw_End"], [6, 5, 13, 5, 6], sides=8)
tongue_names = [f"Tongue_{index:02d}" for index in range(1, 9)] + ["Tongue_End"]
surface.tube("Nine-joint orchid tongue", [B[name] for name in tongue_names],
             [.066, .059, .052, .045, .037, .029, .021, .014, .007],
             [.023, .021, .019, .016, .013, .010, .008, .005, .003], tongue_names,
             [15, 10, 15, 11], axis=(0, 1, 0), sides=6)
for side, sign in (("R", 1), ("L", -1)):
    surface.tube(
        f"{side} tongue fork", [B["Tongue_End"], B["Tongue_End"] + np.array([sign * .11, .012, .24])],
        [.016, .003], [.006, .002], ["Tongue_End", "Tongue_End"], [15, 10, 15], axis=(0, 1, 0), sides=5,
    )
    surface.ellipsoid(f"{side} turquoise viper eye", B["Head"] + np.array([sign * .44, .32, .66]), (.145, .12, .057), "Head", [13, 14, 12, 14, 13], sides=8)
    surface.tube(
        f"{side} sunstone brow blade", [B["Head"] + np.array([sign * .10, .49, .43]), B["Head"] + np.array([sign * .64, .37, .49])],
        [.11, .035], [.078, .022], ["Head", "Head"], [4, 5, 11, 5, 4], axis=(0, 1, 0), sides=6,
    )
    surface.pyramid(f"{side} cheek obelisk", B["Head"] + np.array([sign * .66, .02, .14]), .12, .14, .38, "Head", [4, 5, 11, 13])
surface.pyramid("Lapis crown obelisk", B["joint13"] + np.array([0, -.12, -.12]), .25, .20, .65, "joint13", [7, 8, 9, 5, 13])

# Six raised scarab plates make the long side-read visibly distinct from the
# jungle asset while following real moving joints along the same exact skeleton.
for index, name in enumerate(("BackRib_14", "BackRib_10", "BackRib_06", "BackRib_02", "FrontRib_04", "FrontRib_08"), 1):
    chain_index = body_names.index(name)
    width = widths[chain_index]
    surface.ellipsoid(
        f"Lapis scarab plate {index}", B[name] + np.array([0, width * .73, 0]),
        (width * .80, .078, .25), name, [7, 8, 9, 5, 4, 13], sides=8,
    )

validation = surface.write("duneshade-desert-asp")
report = {
    "name": "Duneshade Asp",
    "nativeChassis": "snakeDesertA",
    "referenceRenderer": 121552,
    "rendererPath": "enDesertSnakeA",
    "boneCount": len(surface.names),
    "nativeSurfaceCopied": False,
    "authoringInputs": "bone_names and inverse bind matrices only",
    "directValidation": validation,
    "artStatus": "Original Desert Snake A candidate generated and binary-validated; Blender/studio and live validation pending.",
    "scope": "The generated 46-bone body follows the actual back/front spine, head, jaw, full nine-joint tongue, and joint13 chains. It does not establish native combat, portrait, material, culling, ragdoll, lifecycle, or art acceptance.",
}
(OUT / "build-report.json").write_text(json.dumps(report, indent=2) + "\n")
print("Duneshade Asp geometry and FTK contract export complete")
