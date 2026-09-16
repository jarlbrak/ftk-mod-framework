#!/usr/bin/env python3
"""Author Bramblecoil Viper from the Jungle Snake C binding palette only.

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
    "071616",  # deep jungle shadow
    "123A35",  # midnight leaf
    "1D5D45",  # forest scale
    "338465",  # sunlit fern
    "66B88A",  # jade edge
    "B2E0A1",  # pale leaf / eye rim
    "14384D",  # peacock blue
    "287A88",  # rainwater teal
    "E1A84E",  # amber thorns
    "D56142",  # coral warning stripe
    "F3D57C",  # venom gold
    "E9F0D4",  # ivory fang
    "10151A",  # eye socket
    "8FF4D0",  # glow eye
    "6A315B",  # orchid underside
    "263038",  # charcoal outline
]


def write_palette() -> None:
    image = Image.new("RGB", (32 * len(PALETTE), 32))
    for index, value in enumerate(PALETTE):
        image.paste(tuple(int(value[offset:offset + 2], 16) for offset in (0, 2, 4)), (index * 32, 0, (index + 1) * 32, 32))
    image.save(OUT / "bramblecoil-jungle-snake_basecolor.png")


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
surface = Surface(121424)
B = surface.B

# Tail-to-head order is composed from the actual two native spine chains.  Each
# ring is bound to its corresponding chain bone, so S-curves move the authored
# scales rather than carrying a static decorative tube around by the root.
body_names = ["BackRib_End"] + [f"BackRib_{index:02d}" for index in range(20, 0, -1)] + ["Root_M"] + [f"FrontRib_{index:02d}" for index in range(1, 12)]
assert all(name in B for name in body_names) and len(body_names) == 33
body_points = [B[name] for name in body_names]
widths, depths = [], []
for index, name in enumerate(body_names):
    progress = index / (len(body_names) - 1)
    if progress < .18:
        radius = .11 + progress / .18 * .29
    elif progress < .72:
        radius = .40 + .13 * math.sin((progress - .18) / .54 * math.pi)
    else:
        radius = .47 - (progress - .72) / .28 * .06
    widths.append(radius)
    depths.append(radius * .88)
surface.tube("Articulated bramblecoil body", body_points, widths, depths, body_names,
             [0, 1, 2, 3, 2, 1, 0, 1, 2, 6, 7, 6, 2, 3, 4, 3], sides=10)

# A raised line of individual original thorns makes the long silhouette read at
# combat distance while staying tied to every few actual spine bones.
thorn_names = ["BackRib_18", "BackRib_15", "BackRib_12", "BackRib_09", "BackRib_06", "BackRib_03", "FrontRib_03", "FrontRib_06", "FrontRib_09", "FrontRib_11"]
for index, name in enumerate(thorn_names, 1):
    chain_index = body_names.index(name)
    surface.pyramid(
        f"Bramble thorn {index}", B[name] + np.array([0, widths[chain_index] * .52, 0]),
        widths[chain_index] * .42, .18, .35 + .18 * math.sin(index / len(thorn_names) * math.pi), name,
        [8, 9, 10, 9, 15],
    )

# Closed paired hood leaves use the front spine and head, yielding a clear cobra
# silhouette without borrowing any native surface, material, or mesh fragment.
for side, sign in (("R", 1), ("L", -1)):
    surface.kite(
        f"{side} rainleaf hood",
        [B["FrontRib_08"] + np.array([sign * .12, .14, -.18]), B["FrontRib_10"] + np.array([sign * 1.18, .36, .05]), B["Head"] + np.array([sign * .95, .18, -.20]), B["Head"] + np.array([sign * .18, .10, -.48])],
        ["FrontRib_08", "FrontRib_10", "Head", "Head"], [6, 7, 4, 3, 2, 1, 15], thickness=.045,
    )

# The head, eyes, lower jaw, tongue, and cheek thorns use the native head/jaw/
# tongue chains.  These are original low-poly volumes with no source-surface
# transfer, so jaw and tongue animation test independently meaningful anatomy.
surface.ellipsoid("Faceted jade viper head", B["Head"] + np.array([0, .13, .12]), (.77, .63, .84), "Head", [1, 2, 3, 4, 3, 2, 1, 6], sides=11)
surface.tube("Animated ivory lower jaw", [B["Jaw"], B["Jaw_End"]], [.34, .12], [.29, .10], ["Jaw", "Jaw_End"], [11, 5, 15, 5, 11], sides=8)
surface.tube("Seven-bone orchid tongue", [B[f"Tongue_{index:02d}"] for index in range(1, 8)],
             [.065, .055, .045, .035, .026, .018, .009], [.022, .019, .016, .013, .010, .007, .004],
             [f"Tongue_{index:02d}" for index in range(1, 8)], [14, 9, 14, 9], axis=(0, 1, 0), sides=6)
for side, sign in (("R", 1), ("L", -1)):
    surface.ellipsoid(f"{side} luminous jungle eye", B["Head"] + np.array([sign * .43, .33, .65]), (.14, .12, .055), "Head", [12, 13, 5, 13, 12], sides=8)
    surface.tube(
        f"{side} amber brow blade", [B["Head"] + np.array([sign * .12, .48, .45]), B["Head"] + np.array([sign * .62, .37, .48])],
        [.105, .035], [.075, .022], ["Head", "Head"], [8, 9, 10, 9, 8], axis=(0, 1, 0), sides=6,
    )
    surface.pyramid(f"{side} cheek thorn", B["Head"] + np.array([sign * .64, .02, .15]), .12, .14, .36, "Head", [8, 9, 10, 15])
surface.pyramid("Central crown thorn", B["joint13"] + np.array([0, -.12, -.12]), .25, .20, .62, "joint13", [8, 9, 10, 9, 15])

# Broad original stripe plates are separate shallow closed volumes tied to the
# body chain.  They break the silhouette and palette without using native UVs.
for index, name in enumerate(("BackRib_14", "BackRib_10", "BackRib_06", "BackRib_02", "FrontRib_04", "FrontRib_08"), 1):
    chain_index = body_names.index(name)
    width = widths[chain_index]
    surface.ellipsoid(
        f"Gold scale band {index}", B[name] + np.array([0, width * .72, 0]),
        (width * .78, .075, .24), name, [8, 9, 10, 9, 8, 15], sides=8,
    )

validation = surface.write("bramblecoil-jungle-snake")
report = {
    "name": "Bramblecoil Viper",
    "nativeChassis": "snakeJungleC",
    "referenceRenderer": 121424,
    "rendererPath": "enJungleSnakeC",
    "boneCount": len(surface.names),
    "nativeSurfaceCopied": False,
    "authoringInputs": "bone_names and inverse bind matrices only",
    "directValidation": validation,
    "artStatus": "Original Jungle Snake C candidate generated and binary-validated; Blender/studio and live validation pending.",
    "scope": "The generated 44-bone body follows the actual back/front spine, head, jaw, tongue, and joint13 chains. It does not establish native combat, portrait, material, culling, ragdoll, lifecycle, or art acceptance.",
}
(OUT / "build-report.json").write_text(json.dumps(report, indent=2) + "\n")
print("Bramblecoil Viper geometry and FTK contract export complete")
