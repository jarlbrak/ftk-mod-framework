#!/usr/bin/env python3
"""Build original Rivenquill Cockatrice art for the Basey boss/small resource pair.

Authoring reads only ordered bone names and inverse bind matrices from the two
ignored local references. Both exact resource renderers are verified to have the
same allowed palette and inverse binds before this one original asset is shared
between their separately validated runtime profiles.
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
PACKAGE = ROOT / "art-experiments" / "rivenquill-basey-cockatrice"
DEFAULT_REFERENCE = ROOT / "scratch" / "basey-cockatrice-boss-reference-v1" / "reference.npz"
COMPATIBLE_REFERENCE = ROOT / "scratch" / "basey-cockatrice-small-reference-v1" / "reference.npz"
sys.path.insert(0, str(ROOT / "tools" / "ai-model-pipeline"))
from export_ftk_glb import write_glb
from validate_glb import validate

PALETTE = [
    "09141B", "14303A", "1E5260", "2E7680", "4B9F94", "83C7A3", "C9E7B8",
    "1C294B", "435A92", "8063A8", "A45B43", "D68549", "F4C969", "FAE8A5", "171018",
]
EXPECTED_BONES = [
    "Root_M", "BackA_M", "BackB_M", "Chest_M",
    "Scapula_R", "Shoulder_R", "Elbow_R", "Wrist_R", "IndexFinger1_R", "IndexFinger2_R", "R_Wing", "R_Wing_End",
    "Neck_M", "Head_M", "Jaw_M", "JawEnd_M", "R_Gobble_01", "R_Gobble_02", "R_Gobble_End", "L_Gobble_01", "L_Gobble_02", "L_Gobble_End",
    "Scapula_L", "Shoulder_L", "Elbow_L", "Wrist_L", "IndexFinger1_L", "IndexFinger2_L", "L_Wing", "L_WingEnd",
    "Hip_R", "Knee_R", "Ankle_R", "MiddleToe1_R", "MiddleToe2_R",
    "Tail1_M", "Tail2_M", "Tail3_M", "Tail4_M", "Tail5_M", "Tail5_M 1", "Tail6_M", "Tail7_M", "Tail8_M", "Tail8_M_End",
    "Hip_L", "Knee_L", "Ankle_L", "MiddleToe1_L", "MiddleToe2_L",
]


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def binding_only(path: Path) -> tuple[list[str], np.ndarray]:
    with np.load(path, allow_pickle=False) as ref:
        names = ref["bone_names"].tolist()
        bindposes = np.asarray(ref["bindposes"], dtype=float)
    if names != EXPECTED_BONES:
        raise ValueError("Basey Cockatrice ordered palette changed; re-extract exact references")
    if bindposes.shape != (len(names), 4, 4) or not np.isfinite(bindposes).all():
        raise ValueError("Basey Cockatrice inverse bind contract changed")
    return names, bindposes


def normalized(influences: str | dict[str, float]) -> dict[str, float]:
    values = {influences: 1.0} if isinstance(influences, str) else dict(influences)
    if not 1 <= len(values) <= 4 or any(not math.isfinite(value) or value <= 0 for value in values.values()):
        raise ValueError(f"invalid original influences: {values}")
    total = sum(values.values())
    return {bone: value / total for bone, value in values.items()}


class Surface:
    def __init__(self, names: list[str], bindposes: np.ndarray) -> None:
        self.names = names
        self.B = dict(zip(names, np.linalg.inv(bindposes)[:, :3, 3]))
        self.data: dict[str, list] = {"positions": [], "normals": [], "uvs": [], "triangles": [],
                                      "joints": [], "weights": [], "bone_names": names}
        self.pieces: list[dict[str, object]] = []

    @staticmethod
    def color(value: int | list[int], index: int = 0) -> int:
        return value[index % len(value)] if isinstance(value, list) else value

    def triangle(self, points: list[np.ndarray], skins: list[str | dict[str, float]], palette: int) -> None:
        vertices = [np.asarray(point, dtype=float) for point in points]
        normal = np.cross(vertices[1] - vertices[0], vertices[2] - vertices[0])
        magnitude = float(np.linalg.norm(normal))
        if magnitude <= 1e-9:
            raise ValueError("degenerate original triangle")
        start = len(self.data["positions"])
        self.data["triangles"].append([start, start + 1, start + 2])
        uv = [(palette + .5) / len(PALETTE), .5]
        for point, skin in zip(vertices, skins):
            selected = normalized(skin)
            if any(bone not in self.B for bone in selected):
                raise ValueError(f"unknown authored bone: {selected}")
            pairs = [(self.names.index(bone), weight) for bone, weight in selected.items()]
            pairs += [(0, 0.0)] * (4 - len(pairs))
            self.data["positions"].append(point.tolist())
            self.data["normals"].append((normal / magnitude).tolist())
            self.data["uvs"].append(uv)
            self.data["joints"].append([joint for joint, _ in pairs])
            self.data["weights"].append([weight for _, weight in pairs])

    def tube(self, label: str, points: list[np.ndarray], rx: list[float], rz: list[float],
             skins: list[str | dict[str, float]], colors: int | list[int], *, sides: int = 8,
             axis: tuple[float, float, float] = (1., 0., 0.)) -> None:
        if not (len(points) >= 2 and len(points) == len(rx) == len(rz) == len(skins)):
            raise ValueError(f"malformed tube: {label}")
        begin = len(self.data["positions"])
        rows = [np.asarray(point, dtype=float) for point in points]
        rings: list[list[np.ndarray]] = []
        for index, center in enumerate(rows):
            tangent = rows[min(index + 1, len(rows) - 1)] - rows[max(index - 1, 0)]
            length = float(np.linalg.norm(tangent))
            if length <= 1e-8:
                raise ValueError(f"coincident controls: {label}")
            tangent /= length
            u = np.asarray(axis, dtype=float)
            u -= tangent * np.dot(u, tangent)
            if np.linalg.norm(u) <= 1e-6:
                u = np.cross(tangent, np.array([0., 0., 1.]))
            u /= np.linalg.norm(u)
            v = np.cross(tangent, u)
            rings.append([center + u * rx[index] * math.cos(step * math.tau / sides)
                          + v * rz[index] * math.sin(step * math.tau / sides)
                          for step in range(sides)])
        for row in range(len(rows) - 1):
            for side in range(sides):
                following = (side + 1) % sides
                palette = self.color(colors, row + side)
                self.triangle([rings[row][side], rings[row][following], rings[row + 1][following]],
                              [skins[row], skins[row], skins[row + 1]], palette)
                self.triangle([rings[row][side], rings[row + 1][following], rings[row + 1][side]],
                              [skins[row], skins[row + 1], skins[row + 1]], palette)
        for side in range(sides):
            following = (side + 1) % sides
            self.triangle([rows[0], rings[0][following], rings[0][side]], [skins[0]] * 3, self.color(colors, side))
            self.triangle([rows[-1], rings[-1][side], rings[-1][following]], [skins[-1]] * 3, self.color(colors, side + 1))
        self.pieces.append({"name": label, "vertex_start": begin, "vertex_count": len(self.data["positions"]) - begin})

    def ellipsoid(self, label: str, center: np.ndarray, radii: tuple[float, float, float],
                  skin: str | dict[str, float], colors: int | list[int], *, sides: int = 10) -> None:
        center = np.asarray(center, dtype=float)
        fractions, heights = [.08, .54, 1., .54, .08], [-.95, -.60, 0., .60, .95]
        self.tube(label, [center + np.array([0., h * radii[1], 0.]) for h in heights],
                  [fraction * radii[0] for fraction in fractions], [fraction * radii[2] for fraction in fractions],
                  [skin] * len(fractions), colors, sides=sides)

    def blade(self, label: str, points: list[np.ndarray], widths: list[float], thickness: list[float],
              skins: list[str | dict[str, float]], colors: int | list[int], *, axis: tuple[float, float, float]) -> None:
        """A closed articulated feather/leaf volume, never a decorative plane."""
        self.tube(label, points, widths, thickness, skins, colors, sides=4, axis=axis)

    def complete_palette(self) -> bool:
        present = {self.names[joint] for joints, weights in zip(self.data["joints"], self.data["weights"])
                   for joint, weight in zip(joints, weights) if weight > 0}
        return present == set(self.names)


def write_palette(path: Path) -> None:
    width, height = 70, 100
    image = Image.new("RGB", (width * len(PALETTE), height), "black")
    for i, encoded in enumerate(PALETTE):
        color = tuple(int(encoded[offset:offset + 2], 16) for offset in (0, 2, 4))
        tile = Image.new("RGB", (width, height), color)
        draw = ImageDraw.Draw(tile)
        light = tuple(min(255, int(component * 1.15 + 25)) for component in color)
        shadow = tuple(max(0, int(component * .43)) for component in color)
        for y in range(-16, height + 30, 19):
            draw.arc((-18, y, width + 18, y + 31), 185, 355, fill=shadow, width=2)
        for x in range(7, width, 18):
            draw.line((x, 8, x + 15, height - 8), fill=light, width=2)
        for y in range(12, height - 8, 22):
            draw.ellipse((width // 2 - 2, y, width // 2 + 2, y + 4), fill=light)
        draw.line((0, height - 6, width, height - 6), fill=shadow, width=5)
        image.paste(tile, (i * width, 0))
    image.save(path)


def cockatrice(names: list[str], bindposes: np.ndarray) -> Surface:
    s = Surface(names, bindposes)
    B = s.B
    # Original compact game-camera silhouette: a slate pear-shaped body, tall
    # crown, long feather tail, broad wings and deliberately clear bird feet.
    s.tube("Rivenquill core", [B["Root_M"] + np.array([0., .04, .04]), B["BackA_M"] + np.array([0., .10, .02]),
           B["BackB_M"] + np.array([0., .12, .02]), B["Chest_M"] + np.array([0., .14, -.03])],
           [.60, .74, .70, .59], [.56, .62, .60, .50],
           ["Root_M", {"Root_M": .35, "BackA_M": .65}, {"BackA_M": .40, "BackB_M": .60}, {"BackB_M": .35, "Chest_M": .65}],
           [0, 1, 2, 3, 4, 7], sides=14)
    s.ellipsoid("Copper belly", B["BackA_M"] + np.array([0., -.27, .02]), (.57, .31, .56),
                {"Root_M": .30, "BackA_M": .70}, [0, 1, 2, 10, 11], sides=12)
    s.ellipsoid("Jade shoulder mantle", B["Chest_M"] + np.array([0., .23, -.03]), (.67, .34, .48),
                {"BackB_M": .30, "Chest_M": .70}, [2, 3, 4, 5, 6], sides=12)
    s.tube("Feathered neck", [B["Chest_M"] + np.array([0., .22, .18]), B["Neck_M"] + np.array([0., .12, .03]),
           B["Head_M"] + np.array([0., -.08, -.04])], [.44, .37, .31], [.38, .32, .30],
           [{"Chest_M": .56, "Neck_M": .44}, "Neck_M", {"Neck_M": .34, "Head_M": .66}], [2, 3, 4, 5, 8], sides=11)
    s.ellipsoid("Rivenquill crown skull", B["Head_M"] + np.array([0., .12, .03]), (.43, .42, .45),
                {"Neck_M": .20, "Head_M": .80}, [1, 2, 3, 4, 5], sides=13)
    s.tube("Golden hooked beak", [B["Head_M"] + np.array([0., -.04, .22]), B["Jaw_M"] + np.array([0., .01, .08]),
           B["JawEnd_M"] + np.array([0., -.01, .04])], [.31, .26, .07], [.25, .19, .05],
           [{"Head_M": .52, "Jaw_M": .48}, "Jaw_M", "JawEnd_M"], [10, 11, 12, 13], sides=10)
    s.ellipsoid("Copper lower beak", B["Jaw_M"] + np.array([0., -.15, .12]), (.31, .08, .28),
                {"Head_M": .22, "Jaw_M": .78}, [10, 11, 12], sides=10)
    for sign in (-1., 1.):
        s.ellipsoid(f"Sun eye {sign:+.0f}", B["Head_M"] + np.array([sign * .29, .14, .28]), (.12, .12, .035), "Head_M", [12, 13, 12], sides=8)
        s.ellipsoid(f"Ink pupil {sign:+.0f}", B["Head_M"] + np.array([sign * .29, .14, .318]), (.03, .075, .012), "Head_M", 14, sides=7)
        for index, bone in enumerate((f"{'R' if sign > 0 else 'L'}_Gobble_01", f"{'R' if sign > 0 else 'L'}_Gobble_02", f"{'R' if sign > 0 else 'L'}_Gobble_End")):
            s.ellipsoid(f"Gobble ember {sign:+.0f} {index + 1}", B[bone] + np.array([sign * .02, .0, .04]),
                        (.065 - index * .010, .10 - index * .014, .055), bone, [10, 11, 12], sides=7)
    s.blade("Crowned neck sail", [B["Neck_M"] + np.array([0., .33, -.05]), B["Head_M"] + np.array([0., .58, -.04])],
            [.16, .02], [.05, .008], [{"Neck_M": .35, "Head_M": .65}, "Head_M"], [5, 6, 12, 13], axis=(1., 0., 0.))

    # Articulated wing chains and secondary wing bones are each enclosed volumes.
    for side, sign, secondary_end in (("R", 1., "R_Wing_End"), ("L", -1., "L_WingEnd")):
        arm = [f"Scapula_{side}", f"Shoulder_{side}", f"Elbow_{side}", f"Wrist_{side}", f"IndexFinger1_{side}", f"IndexFinger2_{side}"]
        s.tube(f"{side} wing armature", [B[bone] for bone in arm], [.25, .29, .22, .17, .11, .045],
               [.20, .24, .18, .14, .09, .035], arm, [2, 3, 4, 7, 8], sides=8)
        s.blade(f"{side} primary feather sail", [B[bone] + np.array([0., .0, -.10]) for bone in arm],
                [.23, .42, .45, .38, .25, .03], [.055, .075, .075, .060, .040, .010], arm,
                [3, 4, 5, 6, 8], axis=(0., 0., 1.))
        s.blade(f"{side} secondary feather sail", [B[f"{side}_Wing"] + np.array([sign * .02, .0, .04]),
                B[secondary_end] + np.array([sign * .02, .0, -.04])], [.33, .045], [.07, .015],
                [f"{side}_Wing", secondary_end], [4, 5, 6, 7], axis=(sign, 0., 0.))
        for index, bone in enumerate((f"Shoulder_{side}", f"Elbow_{side}", f"Wrist_{side}")):
            s.blade(f"{side} wing gold barb {index + 1}", [B[bone], B[bone] + np.array([0., .0, .42 - index * .06])],
                    [.07, .012], [.025, .005], [bone, bone], [10, 11, 12], axis=(0., 1., 0.))

    for side, sign in (("R", 1.), ("L", -1.)):
        hip, knee, ankle, toe1, toe2 = (B[f"Hip_{side}"], B[f"Knee_{side}"], B[f"Ankle_{side}"],
                                         B[f"MiddleToe1_{side}"], B[f"MiddleToe2_{side}"])
        s.ellipsoid(f"{side} hip plate", hip + np.array([sign * .03, .04, .0]), (.28, .26, .28), f"Hip_{side}", [2, 3, 4, 5], sides=9)
        s.tube(f"{side} raptor leg", [hip, knee, ankle, toe1], [.25, .20, .14, .10], [.23, .18, .13, .10],
               [f"Hip_{side}", f"Knee_{side}", f"Ankle_{side}", f"MiddleToe1_{side}"], [1, 2, 3, 4], sides=8)
        s.ellipsoid(f"{side} front talon", toe1 + np.array([0., -.015, .16]), (.17, .075, .29), f"MiddleToe1_{side}", [8, 9, 10, 11], sides=8)
        s.blade(f"{side} rear talon", [toe1 + np.array([0., .01, -.07]), toe2 + np.array([0., .0, .02])], [.11, .017], [.035, .007],
                [f"MiddleToe1_{side}", f"MiddleToe2_{side}"], [10, 11, 12], axis=(sign, 0., 0.))
        s.ellipsoid(f"{side} toe gem", toe2 + np.array([0., .01, .02]), (.08, .065, .09), f"MiddleToe2_{side}", [12, 13, 12], sides=7)

    tail = ["Root_M", "Tail1_M", "Tail2_M", "Tail3_M", "Tail4_M", "Tail5_M", "Tail5_M 1", "Tail6_M", "Tail7_M", "Tail8_M", "Tail8_M_End"]
    s.tube("Eleven-segment plume tail", [B[bone] + np.array([0., .06, 0.]) for bone in tail],
           [.43, .42, .37, .31, .27, .23, .19, .15, .11, .075, .028],
           [.36, .35, .31, .27, .23, .19, .16, .13, .09, .060, .020], tail,
           [0, 1, 2, 3, 4, 7, 8, 9], sides=10)
    for index, bone in enumerate(tail[2:-1:2], start=1):
        center = B[bone]
        s.blade(f"Tail riven feather {index}", [center + np.array([0., .12, 0.]), center + np.array([0., .46 - index * .04, -.05])],
                [.16 - index * .018, .018], [.045, .008], [bone, bone], [4, 5, 6, 8, 9], axis=(1., 0., 0.))
    s.ellipsoid("Tail ember lantern", B["Tail8_M_End"] + np.array([0., .03, -.01]), (.06, .06, .06), "Tail8_M_End", [10, 11, 12], sides=7)

    if not s.complete_palette():
        raise ValueError("authored Rivenquill mesh must positively weight every exact palette bone")
    return s


def build(reference_path: Path, compatible_reference: Path, output_dir: Path) -> dict[str, object]:
    reference_path, compatible_reference, output_dir = reference_path.resolve(), compatible_reference.resolve(), output_dir.resolve()
    names, bindposes = binding_only(reference_path)
    compat_names, compat_bindposes = binding_only(compatible_reference)
    if names != compat_names or not np.array_equal(bindposes, compat_bindposes):
        raise ValueError("boss/small resource references no longer share exact allowed bind data")
    surface = cockatrice(names, bindposes)
    output_dir.mkdir(parents=True, exist_ok=True)
    palette, source, pieces, glb = (output_dir / "rivenquill-palette.png", output_dir / "rivenquill.source.json",
                                     output_dir / "rivenquill.pieces.json", output_dir / "rivenquill.glb")
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
    routes = [
        {"resourcePrefab": "enbaseycockatriceboss", "nativeEnemy": "bossCockatrice", "rendererPath": "enBaseyCockatrice", "sourceRendererId": 121693, "controller": "cockatriceController", "controllerId": 5949},
        {"resourcePrefab": "enbaseycockatricesmall", "nativeEnemy": "cockatriceC", "rendererPath": "enBaseyCockatrice", "sourceRendererId": 121694, "controller": "cockatriceController", "controllerId": 5949},
    ]
    report = {"name": "Rivenquill Cockatrice", "status": "PASS_ORIGINAL_BIND_AUTHORED",
              "artScope": "Original cobalt-and-copper cockatrice geometry and palette; offline binary/bind validation only.",
              "authoringNativeSurfaceRead": False, "validationReadsNativeReference": True,
              "allowedReferenceFields": ["bone_names", "bindposes"], "routes": routes,
              "references": [{"path": str(reference_path.relative_to(ROOT)), "sha256": sha256(reference_path)},
                             {"path": str(compatible_reference.relative_to(ROOT)), "sha256": sha256(compatible_reference)}],
              "sharedBindReference": {"sameOrderedBoneNames": True, "sameInverseBindMatrices": True},
              "assets": {"rivenquill": {"glbSha256": sha256(glb), "vertices": validation["vertices"], "triangles": validation["triangles"],
                         "paletteBones": validation["bones"], "allPaletteBonesWeighted": surface.complete_palette(), "maxBindRestError": validation["max_bind_rest_error"]},
                         "palette": {"sha256": sha256(palette), "bytes": palette.stat().st_size}},
              "limits": ["Offline bind validation does not establish resource selection, runtime binding, animation, material response, culling, portraits or gameplay.",
                         "The two resource profiles share only checked bind data and asset bytes; each needs its own native controller/source-pair trial."]}
    report_path = output_dir / "build-report.json"
    report_path.write_text(json.dumps(report, indent=2) + "\n")
    manifest = {"name": "Rivenquill Cockatrice", "status": "ORIGINAL_ART_OFFLINE_BUILD_VALIDATED_LIVE_INTEGRATION_PENDING",
                "nativeSurfaceCopied": False, "routes": routes, "originalAssets": {path.name: sha256(path) for path in (glb, palette, source, pieces)},
                "buildReportSha256": sha256(report_path), "limits": report["limits"]}
    (output_dir / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
    return report


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--reference", type=Path, default=DEFAULT_REFERENCE)
    parser.add_argument("--compatible-reference", type=Path, default=COMPATIBLE_REFERENCE)
    parser.add_argument("--output-dir", type=Path, default=PACKAGE)
    args = parser.parse_args()
    print(json.dumps(build(args.reference, args.compatible_reference, args.output_dir), indent=2))


if __name__ == "__main__":
    main()
