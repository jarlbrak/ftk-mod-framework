#!/usr/bin/env python3
"""Build the original Sablevine Serpent for FTK's exact enbaseysnake route.

The art generator reads only the ordered bone palette and inverse bind matrices
from the ignored local reference.  All resulting sculpture geometry, UVs,
normals, weights, palette pixels and named pieces are original work.
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
PACKAGE = ROOT / "art-experiments" / "sablevine-basey-snake"
DEFAULT_REFERENCE = ROOT / "scratch" / "basey-snake-reference-v1" / "reference.npz"
sys.path.insert(0, str(ROOT / "tools" / "ai-model-pipeline"))
from export_ftk_glb import write_glb
from validate_glb import validate

PALETTE = [
    "070D11", "12232B", "1A4140", "246152", "397A5B", "65A467", "A3CB76",
    "D2E39B", "9E5737", "D98543", "F0C66D", "73263A", "DB5571", "171018",
]
EXPECTED_BONES = [
    "Root_M", *[f"BackRib_{i:02d}" for i in range(1, 21)], "BackRib_End",
    *[f"FrontRib_{i:02d}" for i in range(1, 12)], "Head", "joint13", "Jaw",
    *[f"Tongue_{i:02d}" for i in range(1, 9)],
]


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def binding_only(reference_path: Path) -> tuple[list[str], np.ndarray]:
    with np.load(reference_path, allow_pickle=False) as reference:
        names = reference["bone_names"].tolist()
        bindposes = np.asarray(reference["bindposes"], dtype=float)
    if names != EXPECTED_BONES:
        raise ValueError("enbaseysnake ordered palette changed; re-extract and inspect the exact route")
    if bindposes.shape != (len(names), 4, 4) or not np.isfinite(bindposes).all():
        raise ValueError("enbaseysnake inverse bind matrix contract changed")
    return names, bindposes


def normalized(influences: str | dict[str, float]) -> dict[str, float]:
    values = {influences: 1.0} if isinstance(influences, str) else dict(influences)
    if not 1 <= len(values) <= 4 or any(not math.isfinite(weight) or weight <= 0 for weight in values.values()):
        raise ValueError(f"invalid original influence set: {values}")
    total = sum(values.values())
    return {bone: weight / total for bone, weight in values.items()}


class Surface:
    """Low-poly original volumes with exact, explicit, positive skin weights."""

    def __init__(self, names: list[str], bindposes: np.ndarray) -> None:
        self.names = names
        self.B = dict(zip(names, np.linalg.inv(bindposes)[:, :3, 3]))
        self.data: dict[str, list] = {"positions": [], "normals": [], "uvs": [], "triangles": [],
                                      "joints": [], "weights": [], "bone_names": names}
        self.pieces: list[dict[str, object]] = []

    @staticmethod
    def palette_value(value: int | list[int], index: int = 0) -> int:
        return value[index % len(value)] if isinstance(value, list) else value

    def triangle(self, points: list[np.ndarray], skins: list[str | dict[str, float]], color: int) -> None:
        vertices = [np.asarray(point, dtype=float) for point in points]
        normal = np.cross(vertices[1] - vertices[0], vertices[2] - vertices[0])
        size = float(np.linalg.norm(normal))
        if size <= 1e-9:
            raise ValueError("degenerate original triangle")
        start = len(self.data["positions"])
        self.data["triangles"].append([start, start + 1, start + 2])
        uv = [(color + .5) / len(PALETTE), .5]
        for point, skin in zip(vertices, skins):
            chosen = normalized(skin)
            if any(bone not in self.B for bone in chosen):
                raise ValueError(f"unknown authored rig bone: {chosen}")
            pairs = [(self.names.index(bone), weight) for bone, weight in chosen.items()]
            pairs += [(0, 0.0)] * (4 - len(pairs))
            self.data["positions"].append(point.tolist())
            self.data["normals"].append((normal / size).tolist())
            self.data["uvs"].append(uv)
            self.data["joints"].append([joint for joint, _ in pairs])
            self.data["weights"].append([weight for _, weight in pairs])

    def tube(self, label: str, points: list[np.ndarray], radii_x: list[float], radii_z: list[float],
             skins: list[str | dict[str, float]], colors: int | list[int], *, sides: int = 8,
             axis: tuple[float, float, float] = (1.0, 0.0, 0.0)) -> None:
        if not (len(points) >= 2 and len(points) == len(radii_x) == len(radii_z) == len(skins)):
            raise ValueError(f"malformed tube: {label}")
        start = len(self.data["positions"])
        rows = [np.asarray(point, dtype=float) for point in points]
        rings: list[list[np.ndarray]] = []
        for index, center in enumerate(rows):
            tangent = rows[min(index + 1, len(rows) - 1)] - rows[max(index - 1, 0)]
            length = float(np.linalg.norm(tangent))
            if length <= 1e-8:
                raise ValueError(f"coincident tube controls: {label}")
            tangent /= length
            u = np.asarray(axis, dtype=float)
            u -= tangent * np.dot(u, tangent)
            if np.linalg.norm(u) <= 1e-6:
                u = np.cross(tangent, np.array([0.0, 0.0, 1.0]))
            u /= np.linalg.norm(u)
            v = np.cross(tangent, u)
            rings.append([center + u * radii_x[index] * math.cos(step * math.tau / sides)
                          + v * radii_z[index] * math.sin(step * math.tau / sides)
                          for step in range(sides)])
        for row in range(len(rows) - 1):
            for side in range(sides):
                following = (side + 1) % sides
                color = self.palette_value(colors, row + side)
                self.triangle([rings[row][side], rings[row][following], rings[row + 1][following]],
                              [skins[row], skins[row], skins[row + 1]], color)
                self.triangle([rings[row][side], rings[row + 1][following], rings[row + 1][side]],
                              [skins[row], skins[row + 1], skins[row + 1]], color)
        for side in range(sides):
            following = (side + 1) % sides
            self.triangle([rows[0], rings[0][following], rings[0][side]], [skins[0]] * 3,
                          self.palette_value(colors, side))
            self.triangle([rows[-1], rings[-1][side], rings[-1][following]], [skins[-1]] * 3,
                          self.palette_value(colors, side + 1))
        self.pieces.append({"name": label, "vertex_start": start,
                            "vertex_count": len(self.data["positions"]) - start})

    def ellipsoid(self, label: str, center: np.ndarray, radii: tuple[float, float, float],
                  skin: str | dict[str, float], colors: int | list[int], *, sides: int = 10) -> None:
        center = np.asarray(center, dtype=float)
        fractions, heights = [0.08, 0.54, 1.0, 0.54, 0.08], [-.95, -.60, 0.0, .60, .95]
        self.tube(label, [center + np.array([0.0, h * radii[1], 0.0]) for h in heights],
                  [f * radii[0] for f in fractions], [f * radii[2] for f in fractions],
                  [skin] * len(fractions), colors, sides=sides)

    def blade(self, label: str, points: list[np.ndarray], widths: list[float], thicknesses: list[float],
              skins: list[str | dict[str, float]], colors: int | list[int], *, axis: tuple[float, float, float]) -> None:
        self.tube(label, points, widths, thicknesses, skins, colors, sides=4, axis=axis)

    def all_palette_bones_weighted(self) -> bool:
        used = {self.names[joint] for row, weights in zip(self.data["joints"], self.data["weights"])
                for joint, weight in zip(row, weights) if weight > 0}
        return used == set(self.names)


def write_palette(path: Path) -> None:
    width, height = 72, 102
    image = Image.new("RGB", (width * len(PALETTE), height), "black")
    for index, encoded in enumerate(PALETTE):
        color = tuple(int(encoded[offset:offset + 2], 16) for offset in (0, 2, 4))
        tile = Image.new("RGB", (width, height), color)
        draw = ImageDraw.Draw(tile)
        light = tuple(min(255, int(channel * 1.17 + 22)) for channel in color)
        shade = tuple(max(0, int(channel * .38)) for channel in color)
        for y in range(-8, height + 25, 18):
            draw.arc((-14, y, width + 14, y + 28), 188, 352, fill=shade, width=2)
        for x in range(-20, width + 28, 23):
            draw.line((x, 0, x + 38, height), fill=light, width=2)
        for y in range(18, height, 25):
            draw.ellipse((12 + (y % 19), y, 16 + (y % 19), y + 4), fill=light)
        draw.line((0, height - 6, width, height - 6), fill=shade, width=5)
        image.paste(tile, (index * width, 0))
    image.save(path)


def taper(count: int, start: float, end: float, power: float = 1.0) -> list[float]:
    return [start + (end - start) * (i / (count - 1)) ** power for i in range(count)]


def serpent_surface(names: list[str], bindposes: np.ndarray) -> Surface:
    surface = Surface(names, bindposes)
    B = surface.B
    back = ["Root_M", *[f"BackRib_{i:02d}" for i in range(1, 21)], "BackRib_End"]
    front = ["Root_M", *[f"FrontRib_{i:02d}" for i in range(1, 12)], "Head"]
    # A continuous original body on both articulated native chains. The small
    # offset pattern is authored texture/silhouette relief, not sampled from a
    # native mesh; controls themselves originate only in the allowed bind data.
    back_points = [B[bone] + np.array([0.0, 0.035 + .012 * math.sin(i * .7), 0.0])
                   for i, bone in enumerate(back)]
    front_points = [B[bone] + np.array([0.0, 0.035 + .014 * math.cos(i * .6), 0.0])
                    for i, bone in enumerate(front)]
    surface.tube("Sablevine tail chain", back_points, taper(len(back), .48, .055, 1.13),
                 taper(len(back), .39, .042, 1.10), back, [0, 1, 2, 3, 4, 7, 8], sides=12)
    surface.tube("Sablevine body chain", front_points, taper(len(front), .49, .40, .65),
                 taper(len(front), .39, .32, .70), front, [0, 1, 2, 3, 4, 5, 7, 8], sides=12)
    for index, bone in enumerate(back[3:-1:3], start=1):
        center = B[bone]
        surface.ellipsoid(f"Tail bark scale {index}", center + np.array([0.0, .24, 0.0]),
                          (.31 - index * .025, .08, .28 - index * .020), bone, [2, 3, 4, 5], sides=8)
        surface.blade(f"Tail fern vane {index}", [center + np.array([0.0, .28, -.08]),
                      center + np.array([0.0, .58 - index * .035, -.05])], [.16 - index * .011, .014],
                      [.04, .007], [bone, bone], [4, 5, 6, 7], axis=(0.0, 1.0, 0.0))
    for index, bone in enumerate(front[2:-1:3], start=1):
        center = B[bone]
        surface.ellipsoid(f"Body lichen plate {index}", center + np.array([0.0, .23, 0.0]),
                          (.33, .075, .23), bone, [2, 3, 4, 5, 6], sides=8)
        for sign in (-1.0, 1.0):
            surface.blade(f"Body leaf {index} {sign:+.0f}",
                          [center + np.array([sign * .18, .12, 0.0]), center + np.array([sign * .44, .24, -.02])],
                          [.085, .012], [.028, .006], [bone, bone], [3, 4, 5, 6], axis=(0.0, 1.0, 0.0))

    head, jaw, horn = B["Head"], B["Jaw"], B["joint13"]
    surface.ellipsoid("Crown of the sablevine head", head + np.array([0.0, .13, .14]),
                      (.54, .39, .61), {"FrontRib_11": .24, "Head": .76}, [1, 2, 3, 4, 5, 7], sides=13)
    surface.ellipsoid("Copper lower jaw", jaw + np.array([0.0, -.08, .18]),
                      (.37, .13, .42), {"Head": .30, "Jaw": .70}, [8, 9, 10, 8], sides=11)
    surface.blade("Crested amber horn", [head + np.array([0.0, .30, .16]), horn + np.array([0.0, .08, .02])],
                  [.16, .018], [.052, .008], [{"Head": .44, "joint13": .56}, "joint13"], [5, 6, 10, 11], axis=(1.0, 0.0, 0.0))
    for sign in (-1.0, 1.0):
        surface.ellipsoid(f"Lantern eye {sign:+.0f}", head + np.array([sign * .36, .12, .32]),
                          (.115, .11, .035), "Head", [10, 11, 10], sides=8)
        surface.ellipsoid(f"Ink pupil {sign:+.0f}", head + np.array([sign * .36, .12, .358]),
                          (.030, .072, .012), "Head", 13, sides=7)
        surface.blade(f"Cheek leaf {sign:+.0f}", [head + np.array([sign * .30, .01, .19]),
                      head + np.array([sign * .58, -.03, .08])], [.105, .014], [.032, .006], ["Head", "Head"],
                      [4, 5, 6], axis=(0.0, 1.0, 0.0))
        surface.blade(f"Copper fang {sign:+.0f}", [jaw + np.array([sign * .20, -.15, .30]),
                      jaw + np.array([sign * .20, -.29, .42])], [.035, .007], [.014, .004], ["Jaw", "Jaw"],
                      [10, 11, 7], axis=(sign, 0.0, 0.0))

    tongue = [f"Tongue_{i:02d}" for i in range(1, 9)]
    tongue_points = [B[bone] + np.array([.0, .01 * math.sin(i), .0]) for i, bone in enumerate(tongue)]
    surface.tube("Eight-joint rose tongue", tongue_points, taper(len(tongue), .075, .025),
                 taper(len(tongue), .045, .014), tongue, [11, 12, 12, 13], sides=7)
    last = B["Tongue_08"]
    for sign in (-1.0, 1.0):
        surface.blade(f"Tongue fork {sign:+.0f}", [last, last + np.array([sign * .12, .005, .20])], [.030, .004],
                      [.015, .004], ["Tongue_08", "Tongue_08"], [11, 12, 13], axis=(0.0, 1.0, 0.0))

    if not surface.all_palette_bones_weighted():
        raise ValueError("authored Sablevine mesh must positively weight every exact palette bone")
    return surface


def build(reference_path: Path, output_dir: Path) -> dict[str, object]:
    reference_path, output_dir = reference_path.resolve(), output_dir.resolve()
    names, bindposes = binding_only(reference_path)
    surface = serpent_surface(names, bindposes)
    output_dir.mkdir(parents=True, exist_ok=True)
    palette, source, pieces, glb = (output_dir / "sablevine-palette.png", output_dir / "sablevine.source.json",
                                     output_dir / "sablevine.pieces.json", output_dir / "sablevine.glb")
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
        "name": "Sablevine Serpent", "status": "PASS_ORIGINAL_BIND_AUTHORED",
        "artScope": "Original moss-and-ember serpent geometry and palette; offline binary/bind validation only.",
        "authoringNativeSurfaceRead": False, "validationReadsNativeReference": True,
        "allowedReferenceFields": ["bone_names", "bindposes"],
        "target": {"resourcePrefab": "enbaseysnake", "nativeEnemy": "snakeJungleA", "rendererPath": "enSnake_Basey",
                   "sourceRendererId": 121488, "controller": "snakeController", "controllerId": 6002, "joints": len(names)},
        "reference": {"path": str(reference_path.relative_to(ROOT)), "sha256": sha256(reference_path)},
        "assets": {"sablevine": {"glbSha256": sha256(glb), "vertices": validation["vertices"],
                   "triangles": validation["triangles"], "paletteBones": validation["bones"],
                   "allPaletteBonesWeighted": surface.all_palette_bones_weighted(), "maxBindRestError": validation["max_bind_rest_error"]},
                   "palette": {"sha256": sha256(palette), "bytes": palette.stat().st_size}},
        "limits": ["Offline bind validation does not establish resource selection, runtime binding, animation, materials, culling, portraits or gameplay.",
                   "Direct snakeJungleA/C and other resource-prefab evidence cannot establish this distinct enbaseysnake source pair."],
    }
    report_path = output_dir / "build-report.json"
    report_path.write_text(json.dumps(report, indent=2) + "\n")
    manifest = {"name": "Sablevine Serpent", "status": "ORIGINAL_ART_OFFLINE_BUILD_VALIDATED_LIVE_INTEGRATION_PENDING",
                "nativeSurfaceCopied": False, "target": report["target"],
                "originalAssets": {path.name: sha256(path) for path in (glb, palette, source, pieces)},
                "buildReportSha256": sha256(report_path), "limits": report["limits"]}
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
