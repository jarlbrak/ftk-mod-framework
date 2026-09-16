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


def rgb(index: int) -> np.ndarray:
    value = PALETTE[index % len(PALETTE)]
    return np.array([int(value[offset:offset + 2], 16) for offset in (0, 2, 4)], dtype=float)


def write_texture(filename: str, theme: str, shift: int = 0) -> None:
    """Write a deterministic painted original texture with a small swatch strip.

    Wrapped tube faces use the painted field. Small ornamental pieces can keep
    using the lower swatch strip through ``triangle``'s fallback UVs. Nothing
    here is sampled from the game; palette values and paint marks are authored
    in this file.
    """
    width = height = 512
    yy, xx = np.mgrid[0:height, 0:width]
    u, v = xx / (width - 1), yy / (height - 1)
    base = (0.58 + 0.22 * np.sin((u * 7.0 + v * 2.0) * math.pi)
            + 0.11 * np.sin((u * 19.0 - v * 11.0) * math.pi))
    if theme == "head":
        dark, mid, glow = rgb(1 + shift), rgb(3 + shift), rgb(5 + shift)
        paint = dark[None, None, :] * (1.0 - base[..., None]) + mid[None, None, :] * base[..., None]
        # Four aurora ribbons and small pearl droplets give the orb a readable
        # surface even when an encounter camera sees it from an unexpected side.
        for center, color, spread in ((.16, glow, .030), (.42, rgb(7 + shift), .022), (.68, rgb(4 + shift), .028), (.86, rgb(8 + shift), .018)):
            curve = center + .055 * np.sin(v * math.pi * 5.0 + center * 17.0)
            mask = np.exp(-((u - curve) ** 2) / spread)
            paint = paint * (1.0 - .72 * mask[..., None]) + color[None, None, :] * (.72 * mask[..., None])
        dots = ((np.sin(u * math.pi * 34.0) + np.sin(v * math.pi * 27.0)) > 1.62)
        paint[dots] = paint[dots] * .38 + glow * .62
    elif theme == "tentacle":
        dark, mid, light = rgb(1 + shift), rgb(3 + shift), rgb(4 + shift)
        paint = dark[None, None, :] * (1.0 - base[..., None]) + mid[None, None, :] * base[..., None]
        # Long, wavy kelp grain and occasional narrow gold growth rings avoid
        # the flat candy-stripe look of a palette-only tube.
        for phase in (.08, .31, .59, .83):
            curve = phase + .045 * np.sin(v * math.pi * (4.0 + phase * 3.0) + phase * 11.0)
            mask = np.exp(-((u - curve) ** 2) / .0018)
            paint = paint * (1.0 - .55 * mask[..., None]) + light[None, None, :] * (.55 * mask[..., None])
        for center in (.15, .31, .48, .65, .81):
            mask = np.exp(-((v - center) ** 2) / .00009)
            gold = rgb(7 + shift) * .72 + rgb(8 + shift) * .28
            paint = paint * (1.0 - .58 * mask[..., None]) + gold[None, None, :] * (.58 * mask[..., None])
        dots = ((np.sin((u + v) * math.pi * 45.0) + np.sin((u - v * .7) * math.pi * 39.0)) > 1.78)
        paint[dots] = paint[dots] * .45 + rgb(5 + shift) * .55
    else:
        raise ValueError(f"Unknown original paint theme: {theme}")
    image = Image.fromarray(np.clip(paint, 0, 255).astype(np.uint8), "RGB")
    # Preserve a deliberately authored color strip at both edges so tiny
    # ornaments remain readable when they use a constant fallback UV.
    for edge in (0, height - 24):
        for index in range(len(PALETTE)):
            left = index * width // len(PALETTE)
            right = (index + 1) * width // len(PALETTE)
            image.paste(tuple(rgb(index + shift).astype(int)), (left, edge, right, edge + 24))
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

    def triangle(self, points, skins, color: int = 0, uvs=None) -> None:
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
        if uvs is not None and (len(uvs) != 3 or any(len(uv) != 2 for uv in uvs)):
            raise ValueError("Triangle UVs must contain three authored coordinates")
        for index, (point, skin) in enumerate(zip(points, skins)):
            joints, weights = self._weights(skin)
            self.data["positions"].append(point.tolist())
            self.data["normals"].append(normal.tolist())
            self.data["uvs"].append(list(uvs[index]) if uvs is not None else [((int(color) % len(PALETTE)) + 0.5) / len(PALETTE), 0.02])
            self.data["joints"].append(joints)
            self.data["weights"].append(weights)

    def tube(self, label, points, widths, depths, skins, color, axis=(1, 0, 0), sides=10, uv_wrap=False, uv_offset=(0.0, 0.0), uv_scale=(1.0, 1.0)) -> None:
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
                ring_uv = lambda ring, side: [uv_offset[0] + uv_scale[0] * side / sides, uv_offset[1] + uv_scale[1] * ring / (len(points) - 1)]
                uv_a = [ring_uv(index, slot), ring_uv(index, nxt), ring_uv(index + 1, nxt)] if uv_wrap else None
                uv_b = [ring_uv(index, slot), ring_uv(index + 1, nxt), ring_uv(index + 1, slot)] if uv_wrap else None
                self.triangle([rings[index][slot], rings[index][nxt], rings[index + 1][nxt]],
                              [skins[index], skins[index], skins[index + 1]], shade, uv_a)
                self.triangle([rings[index][slot], rings[index + 1][nxt], rings[index + 1][slot]],
                              [skins[index], skins[index + 1], skins[index + 1]], shade, uv_b)
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
    write_texture("abyssal-crown-kraken-head-v2.png", "head")
    surface = Surface(121035)
    B = surface.B
    required = ["Root_M", "base", "body", "neck", "head", "topHead", "eye"]
    if surface.names != required:
        raise ValueError("Modern Kraken head palette changed")

    # The V1 live pass proved that a broad bell reads as a dome in FTK's combat
    # camera. This V2 design instead uses a narrow articulated spine and a
    # compact many-eyed crown, so it stays legible from the three-quarter game
    # view without borrowing the native mesh's silhouette.
    lower_chain = ["Root_M", "base", "body", "neck", "head"]
    spine_points = [B[name] + offset for name, offset in zip(lower_chain, (
        np.array([0, .12, .10]), np.array([0, .14, .12]), np.array([0, .16, .08]),
        np.array([0, .10, .04]), np.array([0, .08, .04]),
    ))]
    surface.tube(
        "Abyssal Crown articulated lantern spine",
        spine_points,
        [.52, .67, .80, .93, 1.06], [.48, .58, .70, .82, .94],
        ["Root_M", blend("Root_M", "base", .62), blend("base", "body", .58), blend("body", "neck", .58), blend("neck", "head", .58)],
        [1, 2, 3, 4, 3, 2], axis=(1, 0, 0), sides=10, uv_wrap=True, uv_scale=(1.0, .82),
    )
    core = B["head"] + np.array([0, .10, .04])
    surface.ellipsoid("Faceted crown core", core, (1.62, 1.34, 1.56), "head", [1, 2, 3, 4, 3, 2], sides=10)
    surface.ellipsoid("Antique-gold crown collar", core + np.array([0, .02, 0]), (1.83, .18, 1.75), "head", [7, 8, 12, 7, 8], sides=10)

    # The long topHead landmark gets a narrow articulated comet crest rather
    # than a giant cap. The whole connector is weighted across the actual head
    # and topHead pair.
    crest_points = [
        B["head"] + np.array([0, .52, -.22]),
        (B["head"] + B["topHead"]) * .5 + np.array([0, .24, -.20]),
        B["topHead"] + np.array([0, -.04, .10]),
    ]
    surface.tube("Top-head comet crest", crest_points, [.60, .34, .16], [.50, .27, .13],
                 ["head", blend("head", "topHead", .68), "topHead"], [7, 8, 9, 7, 8], axis=(1, 0, 0), sides=8, uv_wrap=True, uv_offset=(.16, .08), uv_scale=(.70, .58))
    surface.ellipsoid("Top-head pearl", B["topHead"] + np.array([0, -.04, .10]), (.28, .34, .28), "topHead", [12, 7, 6, 7], sides=8)

    # Four symmetrical eyes make the original character readable from either
    # side of FTK's encounter camera. Each is a small closed volume, not a
    # billboard, and all remain local to the actual head bone.
    for label, direction in (("east", np.array([1.0, 0.0, 0.0])), ("west", np.array([-1.0, 0.0, 0.0])),
                             ("north", np.array([0.0, 0.0, 1.0])), ("south", np.array([0.0, 0.0, -1.0]))):
        eye_center = core + direction * 1.66 + np.array([0, .08, 0])
        surface.tube(f"{label} eye stalk", [core + direction * 1.10, eye_center], [.17, .12], [.17, .12],
                     ["head", "head"], [7, 8, 12], axis=(0, 1, 0), sides=7)
        surface.ellipsoid(f"{label} sea-glass eye", eye_center, (.36, .41, .36), "head", [13, 5, 6, 5, 13], sides=8)
        surface.ellipsoid(f"{label} pupil", eye_center + direction * .28, (.12, .18, .12), "head", 13, sides=7)

    # Small closed fin plates break the core outline while remaining compact.
    for side, sign in (("right", 1), ("left", -1)):
        surface.kite(
            f"{side} copper crown fin",
            [core + np.array([sign * .30, 1.12, -.32]), core + np.array([sign * 1.22, 1.76, -.06]),
             core + np.array([sign * .90, .84, .68]), core + np.array([sign * .22, .54, .54])],
            ["head"] * 4, [7, 8, 9, 10, 11], thickness=.06,
        )

    # The eye bone lives behind the crown in the production hierarchy. A small
    # independent lantern gives it a real surface share without turning the
    # body into the old V1 parasol.
    surface.ellipsoid("Eye-bone anchor lantern", B["eye"] + np.array([0, .04, .06]), (.34, .38, .34), "eye", [4, 5, 15, 6], sides=8)
    surface.ellipsoid("Root pearl anchor", B["Root_M"] + np.array([0, -.18, .12]), (.62, .50, .58), "Root_M", [1, 2, 14, 15], sides=9)

    validation = surface.write("abyssal-crown-kraken-head-v2")
    return {
        "asset": "abyssal-crown-kraken-head-v2",
        "referenceRenderer": 121035,
        "nativeChassis": "krakenHead",
        "rendererPath": "kraken2",
        "boneCount": len(surface.names),
        "directValidation": validation,
        "artScope": "Original seven-bone modern Kraken head V2, generated only from palette names and inverse bind matrices after the V1 live silhouette rejection.",
    }

def build_tentacle(renderer_id: int, name: str, texture: str, native_chassis: str, renderer_path: str, style_shift: int) -> dict:
    write_texture(texture, "tentacle", shift=style_shift)
    surface = Surface(renderer_id)
    B = surface.B
    chain = [f"Tentacle_{index}" for index in range(1, 19)] + ["Tentacle_End"]
    expected = ["Root_M"] + chain
    if surface.names != expected:
        raise ValueError(f"Tentacle palette changed for renderer {renderer_id}")
    progress = [index / (len(chain) - 1) for index in range(len(chain))]
    width = [.88 * ((1.0 - value) ** .68) + .075 for value in progress]
    depth = [radius * .72 for radius in width]
    skins = [chain[0]] + [blend(chain[index - 1], chain[index], .62) for index in range(1, len(chain))]
    palette = [1 + style_shift, 2 + style_shift, 3 + style_shift, 4 + style_shift, 15 + style_shift, 4 + style_shift, 3 + style_shift]
    surface.tube("Sargassum articulated central tendril", [B[bone] for bone in chain], width, depth, skins, palette, axis=(1, 0, 0), sides=12, uv_wrap=True, uv_scale=(1.0, .94))
    surface.ellipsoid("Root kelp knot", B["Root_M"] + np.array([0, -.25, 0]), (.78, .52, .68), "Root_M", [1 + style_shift, 2 + style_shift, 7 + style_shift], sides=10)

    # Three raised helices traverse the complete actual chain. Their rotating
    # offsets make the otherwise straight bind pose read as living kelp, while
    # every vertex still follows the local native bone blend.
    for strand, phase in enumerate((0.0, 2.0 * math.pi / 3.0, 4.0 * math.pi / 3.0), 1):
        braid_points = []
        for index, bone in enumerate(chain):
            theta = phase + progress[index] * math.pi * 4.25
            braid_points.append(B[bone] + np.array([
                width[index] * .64 * math.cos(theta), 0, depth[index] * .64 * math.sin(theta),
            ]))
        surface.tube(f"Bioluminescent spiral strand {strand}", braid_points,
                     [max(.040, radius * .13) for radius in width], [max(.030, radius * .10) for radius in width],
                     skins, [4 + style_shift + strand, 5 + style_shift + strand, 7 + style_shift], axis=(1, 0, 0), sides=7, uv_wrap=True, uv_offset=(strand * .17, .06), uv_scale=(.78, .92))

    for index in (2, 5, 8, 11, 14, 17):
        bone = chain[index]
        radius = width[index]
        surface.ellipsoid(f"Gold cincture {index}", B[bone] + np.array([0, 0, 0]), (radius * 1.08, .095, depth[index] * 1.08), bone, [7 + style_shift, 8 + style_shift, 12 + style_shift], sides=10)
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
                [B[bone] + np.array([0, -.48, depth[index] * .26]), B[bone] + np.array([sign * radius * 1.55, .02, depth[index] * .10]),
                 B[bone] + np.array([sign * radius * .72, .62, -depth[index] * .26]), B[bone] + np.array([0, .58, depth[index] * .42])],
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
        build_tentacle(121595, "sargassum-kraken-tentacle-v2", "sargassum-kraken-tentacle-v2.png", "krakenTentacle", "krakenTentacle", 0),
        build_tentacle(121315, "sargassum-seaking-tentacle-v2", "sargassum-seaking-tentacle-v2.png", "seaKingTentacleA/B", "KrakenGodTentacle", 4),
    ]
    (OUT / "build-report.json").write_text(json.dumps({
        "name": "Abyssal Kraken original profile family",
        "nativeSurfaceCopied": False,
        "authoringInputs": "bone_names and inverse bind matrices only",
        "assets": reports,
        "limits": "Offline binary validation only. Live binding, appearance, controller motion, gameplay, portrait, culling, material lifetime, death and native progression require separate evidence.",
    }, indent=2) + "\n")
    print(json.dumps({"status": "PASS_ORIGINAL_KRAKEN_GEOMETRY_EXPORT", "assets": [report["asset"] for report in reports], "revision": "V2 after V1 live silhouette review"}, indent=2))


if __name__ == "__main__":
    main()
