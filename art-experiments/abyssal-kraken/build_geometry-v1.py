#!/usr/bin/env python3
"""Author original modern-Kraken examples from local palette metadata only.

The generator reads only each selected reference's bone names and inverse bind
matrices. It never reads native vertices, triangles, normals, UVs, weights,
textures, or animation curves. Every exported surface is original procedural
geometry tied to the real production bind palette.
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
    "07141D",  # trench blue black
    "0A2934",  # deep teal
    "10515B",  # blue green plate
    "16806F",  # kelp jade
    "31B595",  # bright sea glass
    "8BE4B7",  # bioluminescent mint
    "D7F5C8",  # pearl light
    "E3C167",  # antique gold
    "E98543",  # copper orange
    "B94D63",  # coral rose
    "5A2D6B",  # violet shadow
    "201529",  # purple black
    "F3F0D4",  # warm ivory
    "101319",  # pupil black
    "2A7893",  # marine blue
    "5DC6D0",  # cyan rim
]


def write_palette(filename: str, shift: int = 0) -> None:
    """Write an original UV palette, not an extracted game texture."""
    image = Image.new("RGB", (32 * len(PALETTE), 64))
    for index in range(len(PALETTE)):
        value = PALETTE[(index + shift) % len(PALETTE)]
        color = tuple(int(value[offset:offset + 2], 16) for offset in (0, 2, 4))
        image.paste(color, (index * 32, 0, (index + 1) * 32, 64))
    image.save(OUT / filename)


class Surface:
    """Small original low-poly surface writer with explicit FTK skinning."""

    def __init__(self, renderer_id: int):
        self.renderer_id = renderer_id
        self.reference = np.load(ROOT / f"scratch/skeleton-audit/{renderer_id}/reference.npz", allow_pickle=False)
        self.names = self.reference["bone_names"].tolist()
        self.centers = np.linalg.inv(self.reference["bindposes"])[:, :3, 3]
        self.B = dict(zip(self.names, self.centers))
        self.data = {key: [] for key in ("positions", "normals", "uvs", "triangles", "joints", "weights")}
        self.data["bone_names"] = self.names
        self.pieces: list[dict[str, object]] = []

    def _weights(self, source: str | dict[str, float]) -> tuple[list[int], list[float]]:
        values = {source: 1.0} if isinstance(source, str) else dict(source)
        if not (1 <= len(values) <= 4):
            raise ValueError("Original vertex must use one to four palette bones")
        if any(name not in self.names or weight <= 0 for name, weight in values.items()):
            raise ValueError("Unknown or nonpositive authored bone weight")
        total = float(sum(values.values()))
        joints = [self.names.index(name) for name in values]
        weights = [float(weight) / total for weight in values.values()]
        return joints + [0] * (4 - len(joints)), weights + [0.0] * (4 - len(weights))

    def triangle(self, points, skins, color: int) -> None:
        points = np.asarray(points, dtype=float)
        if points.shape != (3, 3) or len(skins) != 3:
            raise ValueError("Invalid authored triangle")
        normal = np.cross(points[1] - points[0], points[2] - points[0])
        length = float(np.linalg.norm(normal))
        if length < 1e-10:
            raise ValueError("Degenerate authored triangle")
        normal /= length
        start = len(self.data["positions"])
        self.data["triangles"].append([start, start + 1, start + 2])
        for point, skin in zip(points, skins):
            joints, weights = self._weights(skin)
            self.data["positions"].append(point.tolist())
            self.data["normals"].append(normal.tolist())
            self.data["uvs"].append([((int(color) % len(PALETTE)) + 0.5) / len(PALETTE), 0.5])
            self.data["joints"].append(joints)
            self.data["weights"].append(weights)

    def tube(self, label, points, widths, depths, skins, color, axis=(1, 0, 0), sides=10) -> None:
        """Write a capped, consistently wound original tube around real bones."""
        start = len(self.data["positions"])
        points = np.asarray(points, dtype=float)
        if not (len(points) >= 2 and len(points) == len(widths) == len(depths) == len(skins)):
            raise ValueError("Tube authoring inputs disagree")
        rings = []
        for index, point in enumerate(points):
            tangent = points[min(index + 1, len(points) - 1)] - points[max(index - 1, 0)]
            tangent_length = float(np.linalg.norm(tangent))
            if tangent_length < 1e-10:
                raise ValueError("Coincident authored bind landmarks")
            tangent /= tangent_length
            u = np.asarray(axis, dtype=float)
            u -= tangent * float(np.dot(u, tangent))
            if float(np.linalg.norm(u)) < 0.01:
                u = np.cross(tangent, [0, 0, 1])
            if float(np.linalg.norm(u)) < 0.01:
                u = np.cross(tangent, [0, 1, 0])
            u /= float(np.linalg.norm(u))
            v = np.cross(tangent, u)
            rings.append([
                point + u * float(widths[index]) * math.cos(slot * 2 * math.pi / sides)
                + v * float(depths[index]) * math.sin(slot * 2 * math.pi / sides)
                for slot in range(sides)
            ])
        palette = color if isinstance(color, list) else [color]
        for index in range(len(points) - 1):
            for slot in range(sides):
                nxt = (slot + 1) % sides
                shade = palette[slot % len(palette)]
                self.triangle([rings[index][slot], rings[index][nxt], rings[index + 1][nxt]],
                              [skins[index], skins[index], skins[index + 1]], shade)
                self.triangle([rings[index][slot], rings[index + 1][nxt], rings[index + 1][slot]],
                              [skins[index], skins[index + 1], skins[index + 1]], shade)
        cap_color = palette[0]
        for slot in range(sides):
            nxt = (slot + 1) % sides
            self.triangle([points[0], rings[0][nxt], rings[0][slot]], [skins[0]] * 3, cap_color)
            self.triangle([points[-1], rings[-1][slot], rings[-1][nxt]], [skins[-1]] * 3, cap_color)
        self.pieces.append({"name": label, "vertex_start": start, "vertex_count": len(self.data["positions"]) - start})

    def ellipsoid(self, label, center, size, bone, color, sides=10) -> None:
        center = np.asarray(center, dtype=float)
        points = [center + np.array([0, factor * size[1], 0]) for factor in (-0.98, -0.70, 0, 0.70, 0.98)]
        radii = (0.20, 0.71, 1.0, 0.71, 0.20)
        self.tube(label, points, [radius * size[0] for radius in radii], [radius * size[2] for radius in radii],
                  [bone] * len(points), color, sides=sides)

    def kite(self, label, points, skins, color, thickness=0.045) -> None:
        """Write a shallow closed plate with an intentional outside and edge band."""
        start = len(self.data["positions"])
        points = np.asarray(points, dtype=float)
        if points.shape != (4, 3) or len(skins) != 4:
            raise ValueError("Invalid authored kite")
        normal = np.cross(points[1] - points[0], points[2] - points[0])
        length = float(np.linalg.norm(normal))
        if length < 1e-10:
            raise ValueError("Degenerate authored kite")
        normal /= length
        front, back = points + normal * thickness, points - normal * thickness
        face_color = color[0] if isinstance(color, list) else color
        edge_color = color[-1] if isinstance(color, list) else color
        for indices in ((0, 1, 2), (0, 2, 3)):
            self.triangle([front[i] for i in indices], [skins[i] for i in indices], face_color)
            self.triangle([back[i] for i in indices[::-1]], [skins[i] for i in indices[::-1]], face_color)
        for index in range(4):
            nxt = (index + 1) % 4
            self.triangle([front[index], back[index], back[nxt]], [skins[index], skins[index], skins[nxt]], edge_color)
            self.triangle([front[index], back[nxt], front[nxt]], [skins[index], skins[nxt], skins[nxt]], edge_color)
        self.pieces.append({"name": label, "vertex_start": start, "vertex_count": len(self.data["positions"]) - start})

    def pyramid(self, label, center, width, depth, height, bone, color) -> None:
        """Write a closed low-poly dorsal spike rather than a root-rigid plane."""
        start = len(self.data["positions"])
        center = np.asarray(center, dtype=float)
        base = [
            center + np.array([-width, 0, -depth]), center + np.array([width, 0, -depth]),
            center + np.array([width, 0, depth]), center + np.array([-width, 0, depth]),
        ]
        apex = center + np.array([0, height, 0])
        palette = color if isinstance(color, list) else [color]
        for index in range(4):
            nxt = (index + 1) % 4
            self.triangle([base[index], base[nxt], apex], [bone, bone, bone], palette[index % len(palette)])
        self.triangle([base[0], base[2], base[1]], [bone] * 3, palette[-1])
        self.triangle([base[0], base[3], base[2]], [bone] * 3, palette[-1])
        self.pieces.append({"name": label, "vertex_start": start, "vertex_count": len(self.data["positions"]) - start})

    def write(self, name: str) -> dict:
        source = OUT / f"{name}.source.json"
        source.write_text(json.dumps(self.data, separators=(",", ":")) + "\n")
        glb = OUT / f"{name}.glb"
        write_glb(glb, self.data, self.reference)
        validation = validate(glb, self.reference)
        (OUT / f"{name}.validation.json").write_text(json.dumps(validation, indent=2) + "\n")
        (OUT / f"{name}.pieces.json").write_text(json.dumps(self.pieces, indent=2) + "\n")
        return validation


def blend(first: str, second: str, second_weight: float = 0.50) -> dict[str, float]:
    if not 0 < second_weight < 1:
        raise ValueError("Blend must have positive weights")
    return {first: 1.0 - second_weight, second: second_weight}


def build_head() -> dict:
    write_palette("abyssal-crown-kraken-head.png")
    surface = Surface(121035)
    B = surface.B
    required = ["Root_M", "base", "body", "neck", "head", "topHead", "eye"]
    if surface.names != required:
        raise ValueError("Modern Kraken head palette changed")

    # A compact bell follows the real lower chain while the front-facing hood
    # creates a readable mask. This deliberately avoids reproducing the native
    # model's jagged silhouette or its multi-piece face construction.
    lower_chain = ["Root_M", "base", "body", "neck"]
    surface.tube(
        "Abyssal Crown articulated bell",
        [B[name] + offset for name, offset in zip(lower_chain, (
            np.array([0, -.28, .30]), np.array([0, .08, .15]), np.array([0, .22, -.12]), np.array([0, .35, -.52]),
        ))],
        [1.20, 2.00, 3.05, 3.55], [1.05, 1.70, 2.30, 2.72],
        ["Root_M", blend("Root_M", "base", .70), blend("base", "body", .58), blend("body", "neck", .58)],
        [0, 1, 2, 3, 14, 15, 3, 2], axis=(1, 0, 0), sides=12,
    )
    hood_center = B["neck"] + np.array([0, .66, -1.72])
    surface.ellipsoid("Faceted abyssal hood", hood_center, (4.35, 2.78, 3.32), "neck", [1, 2, 3, 4, 15, 14], sides=12)
    surface.ellipsoid("Central obsidian mask", B["head"] + np.array([0, -.16, -3.86]), (2.58, 1.65, .64), "head", [11, 10, 1, 2, 11], sides=10)

    # The top-head bone drives an original tapered beak from the mask. A small
    # end volume makes this readable in profile without leaving floating crowns.
    beak_points = [B["head"] + np.array([0, -.44, -4.05]), B["topHead"] + np.array([0, -.45, -2.62]), B["topHead"] + np.array([0, -.15, -3.22])]
    surface.tube("Copper lantern beak", beak_points, [1.12, .66, .09], [.62, .40, .07],
                 ["head", blend("head", "topHead", .72), "topHead"], [7, 8, 9, 7, 8], axis=(1, 0, 0), sides=8)
    surface.ellipsoid("Top-head pearl", B["topHead"] + np.array([0, -.15, -3.18]), (.24, .32, .18), "topHead", [12, 7, 6], sides=8)

    # Closed lateral frills use head/neck blends. Their roots lie inside the hood
    # volume and their tips are outside it, so they remain attached from all views.
    for side, sign in (("right", 1), ("left", -1)):
        surface.kite(
            f"{side} jade mantle frill",
            [B["neck"] + np.array([sign * 1.15, .95, -1.68]), B["neck"] + np.array([sign * 4.88, .12, -1.54]),
             B["head"] + np.array([sign * 3.85, -.62, -3.45]), B["head"] + np.array([sign * 1.12, .32, -3.70])],
            ["neck", "neck", "head", "head"], [3, 4, 5, 15, 3], thickness=.08,
        )
        surface.kite(
            f"{side} copper cheek vane",
            [B["head"] + np.array([sign * .74, -.70, -3.96]), B["head"] + np.array([sign * 2.98, -.88, -4.24]),
             B["head"] + np.array([sign * 2.34, -1.62, -4.82]), B["head"] + np.array([sign * .48, -1.10, -4.70])],
            ["head"] * 4, [7, 8, 9, 10, 11], thickness=.06,
        )

    # Bright eye plates sit on the front mask. The local eye bone also owns a
    # small rear lantern, preserving an independently visible seven-bone marker.
    for side, sign in (("right", 1), ("left", -1)):
        eye_center = B["head"] + np.array([sign * 1.24, .32, -4.54])
        surface.ellipsoid(f"{side} mint eye plate", eye_center, (.56, .48, .13), "head", [13, 5, 6, 5, 13], sides=9)
        surface.ellipsoid(f"{side} ink eye slit", eye_center + np.array([0, 0, -.15]), (.13, .31, .035), "head", 13, sides=8)
        surface.tube(f"{side} gold brow", [B["head"] + np.array([sign * .36, 1.08, -4.24]), B["head"] + np.array([sign * 1.88, .86, -4.48])],
                     [.16, .025], [.10, .018], ["head", "head"], [7, 8, 12, 8], axis=(0, 1, 0), sides=6)
        surface.ellipsoid(f"{side} eye-bone back lantern", B["eye"] + np.array([sign * .58, 0, -.30]), (.20, .24, .20), "eye", [4, 5, 15], sides=8)

    surface.ellipsoid("Root pearl anchor", B["Root_M"] + np.array([0, -.86, .32]), (1.35, .90, 1.16), "Root_M", [1, 2, 14, 15], sides=10)

    validation = surface.write("abyssal-crown-kraken-head")
    return {
        "asset": "abyssal-crown-kraken-head",
        "referenceRenderer": 121035,
        "nativeChassis": "krakenHead",
        "rendererPath": "kraken2",
        "boneCount": len(surface.names),
        "directValidation": validation,
        "artScope": "Original seven-bone modern Kraken head, generated only from palette names and inverse bind matrices.",
    }


def build_tentacle(renderer_id: int, name: str, texture: str, native_chassis: str, renderer_path: str, style_shift: int) -> dict:
    write_palette(texture, shift=style_shift)
    surface = Surface(renderer_id)
    B = surface.B
    chain = [f"Tentacle_{index}" for index in range(1, 19)] + ["Tentacle_End"]
    expected = ["Root_M"] + chain
    if surface.names != expected:
        raise ValueError(f"Tentacle palette changed for renderer {renderer_id}")
    progress = [index / (len(chain) - 1) for index in range(len(chain))]
    width = [1.28 * ((1.0 - value) ** .68) + .105 for value in progress]
    depth = [radius * .88 for radius in width]
    skins = [chain[0]] + [blend(chain[index - 1], chain[index], .62) for index in range(1, len(chain))]
    palette = [1 + style_shift, 2 + style_shift, 3 + style_shift, 4 + style_shift, 15 + style_shift, 4 + style_shift, 3 + style_shift]
    surface.tube("Sargassum articulated central tendril", [B[bone] for bone in chain], width, depth, skins, palette, axis=(1, 0, 0), sides=12)
    surface.ellipsoid("Root kelp knot", B["Root_M"] + np.array([0, -.35, 0]), (1.20, .72, 1.00), "Root_M", [1 + style_shift, 2 + style_shift, 7 + style_shift], sides=10)

    # Three raised helices traverse the complete actual chain. Their rotating
    # offsets make the otherwise straight bind pose read as living kelp, while
    # every vertex still follows the local native bone blend.
    for strand, phase in enumerate((0.0, 2.0 * math.pi / 3.0, 4.0 * math.pi / 3.0), 1):
        braid_points = []
        for index, bone in enumerate(chain):
            theta = phase + progress[index] * math.pi * 4.25
            braid_points.append(B[bone] + np.array([
                width[index] * .72 * math.cos(theta), 0, depth[index] * .72 * math.sin(theta),
            ]))
        surface.tube(f"Bioluminescent spiral strand {strand}", braid_points,
                     [max(.045, radius * .16) for radius in width], [max(.035, radius * .12) for radius in width],
                     skins, [4 + style_shift + strand, 5 + style_shift + strand, 7 + style_shift], axis=(1, 0, 0), sides=7)

    for index in (2, 5, 8, 11, 14, 17):
        bone = chain[index]
        radius = width[index]
        surface.ellipsoid(f"Gold cincture {index}", B[bone] + np.array([0, 0, 0]), (radius * 1.06, .13, depth[index] * 1.06), bone, [7 + style_shift, 8 + style_shift, 12 + style_shift], sides=10)
    for index in (3, 6, 9, 12, 15, 18):
        bone = chain[index]
        radius = width[index]
        surface.ellipsoid(f"Pearl suction cup {index}", B[bone] + np.array([0, 0, -depth[index] * 1.03]),
                          (max(.10, radius * .26), .17, max(.06, radius * .12)), bone, [12 + style_shift, 6 + style_shift, 7 + style_shift], sides=8)
    for index in (4, 9, 14):
        bone = chain[index]
        radius = width[index]
        for side, sign in (("right", 1), ("left", -1)):
            surface.kite(
                f"{side} seaweed fin {index}",
                [B[bone] + np.array([0, -.48, depth[index] * .26]), B[bone] + np.array([sign * radius * 1.95, .02, depth[index] * .10]),
                 B[bone] + np.array([sign * radius * .86, .82, -depth[index] * .26]), B[bone] + np.array([0, .76, depth[index] * .48])],
                [bone] * 4, [3 + style_shift, 4 + style_shift, 5 + style_shift, 15 + style_shift], thickness=.04,
            )
    for side, sign in (("right", 1), ("left", -1)):
        surface.kite(
            f"{side} taper crown leaf",
            [B["Tentacle_End"] + np.array([0, -.14, depth[-1] * .22]), B["Tentacle_End"] + np.array([sign * .48, .14, .08]),
             B["Tentacle_End"] + np.array([sign * .20, .72, -.05]), B["Tentacle_End"] + np.array([0, .44, depth[-1] * .32])],
            ["Tentacle_End"] * 4, [4 + style_shift, 5 + style_shift, 7 + style_shift, 12 + style_shift], thickness=.035,
        )
    surface.ellipsoid("Luminous taper tip", B["Tentacle_End"] + np.array([0, .18, 0]), (.30, .40, .24), "Tentacle_End", [5 + style_shift, 6 + style_shift, 12 + style_shift], sides=9)

    validation = surface.write(name)
    return {
        "asset": name,
        "referenceRenderer": renderer_id,
        "nativeChassis": native_chassis,
        "rendererPath": renderer_path,
        "boneCount": len(surface.names),
        "directValidation": validation,
        "artScope": "Original articulated 20-bone tendril, generated only from palette names and inverse bind matrices.",
    }


def main() -> None:
    reports = [
        build_head(),
        build_tentacle(121595, "sargassum-kraken-tentacle", "sargassum-kraken-tentacle.png", "krakenTentacle", "krakenTentacle", 0),
        build_tentacle(121315, "sargassum-seaking-tentacle", "sargassum-seaking-tentacle.png", "seaKingTentacleA/B", "KrakenGodTentacle", 4),
    ]
    (OUT / "build-report.json").write_text(json.dumps({
        "name": "Abyssal Kraken original profile family",
        "nativeSurfaceCopied": False,
        "authoringInputs": "bone_names and inverse bind matrices only",
        "assets": reports,
        "limits": "Offline binary validation only. Live binding, appearance, controller motion, gameplay, portrait, culling, material lifetime, death and native progression require separate evidence.",
    }, indent=2) + "\n")
    print(json.dumps({"status": "PASS_ORIGINAL_KRAKEN_GEOMETRY_EXPORT", "assets": [report["asset"] for report in reports]}, indent=2))


if __name__ == "__main__":
    main()
