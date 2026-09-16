#!/usr/bin/env python3
"""Author the original rigid companion for the modern Kraken head eye renderer.

The selected target is a MeshRenderer under the animated eye transform. Its
surface is authored entirely in the MeshFilter local space and intentionally
uses no game mesh vertices, texture pixels, skinning data, or animation data.
"""
from __future__ import annotations

import hashlib
import json
import math
import struct
import sys
from pathlib import Path

import numpy as np
from PIL import Image

OUT = Path(__file__).resolve().parent
ROOT = OUT.parent.parent
sys.path.insert(0, str(ROOT / "tools/ai-model-pipeline"))
from export_ftk_glb import write_static_glb

ASSET = "abyssal-crown-kraken-eye-v3"
TEXTURE = ASSET + ".png"
GLB = OUT / (ASSET + ".glb")
SOURCE = OUT / (ASSET + ".source.json")
PIECES = OUT / (ASSET + ".pieces.json")
VALIDATION = OUT / (ASSET + ".validation.json")


PALETTE = {
    "abyss": np.array([5, 17, 28], dtype=float),
    "slate": np.array([10, 46, 62], dtype=float),
    "teal": np.array([17, 91, 104], dtype=float),
    "sea_glass": np.array([60, 158, 153], dtype=float),
    "pearl": np.array([183, 230, 211], dtype=float),
    "antique_gold": np.array([184, 139, 72], dtype=float),
    "iris": np.array([28, 112, 124], dtype=float),
    "pupil": np.array([8, 12, 21], dtype=float),
}


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def paint_texture(path: Path) -> None:
    """Paint a deterministic original deep-sea carapace texture."""
    width = height = 512
    yy, xx = np.mgrid[0:height, 0:width]
    u = xx / (width - 1)
    v = yy / (height - 1)
    grain = .34 + .18 * np.sin((u * 8.0 + v * 3.0) * math.pi) + .08 * np.sin((u * 23.0 - v * 17.0) * math.pi)
    paint = PALETTE["abyss"][None, None, :] * (1.0 - grain[..., None]) + PALETTE["teal"][None, None, :] * grain[..., None]
    # Eight deliberate faceted meridian ribbons give the spherical shell an
    # authored orientation while retaining readable detail under flat lighting.
    for phase in np.linspace(.04, .94, 8):
        curve = phase + .028 * np.sin(v * math.pi * 5.0 + phase * 15.0)
        mask = np.exp(-((u - curve) ** 2) / .0007)
        color = PALETTE["sea_glass"] if int(phase * 100) % 2 else PALETTE["slate"]
        paint = paint * (1.0 - .42 * mask[..., None]) + color[None, None, :] * (.42 * mask[..., None])
    # Small pearl flecks are sparse, painted marks rather than a sampled noise map.
    flecks = ((np.sin((u + v * .35) * math.pi * 47.0) + np.sin((u * .61 - v) * math.pi * 39.0)) > 1.84)
    paint[flecks] = paint[flecks] * .55 + PALETTE["pearl"] * .45
    # Narrow authored gold seams remain secondary to the blue-green shell.
    for center in (.11, .37, .63, .89):
        seam = np.exp(-((v - center) ** 2) / .00005)
        paint = paint * (1.0 - .18 * seam[..., None]) + PALETTE["antique_gold"][None, None, :] * (.18 * seam[..., None])
    image = Image.fromarray(np.clip(paint, 0, 255).astype(np.uint8), "RGB")
    image.save(path)


class StaticSurface:
    """Original closed triangle geometry for an unskinned FTK MeshFilter GLB."""

    def __init__(self) -> None:
        self.positions: list[list[float]] = []
        self.normals: list[list[float]] = []
        self.uvs: list[list[float]] = []
        self.triangles: list[list[int]] = []
        self.pieces: list[dict[str, object]] = []

    def triangle(self, points, uvs, expected) -> None:
        pts = [np.asarray(point, dtype=float) for point in points]
        uv_values = [list(map(float, uv)) for uv in uvs]
        normal = np.cross(pts[1] - pts[0], pts[2] - pts[0])
        length = float(np.linalg.norm(normal))
        if length < 1e-9:
            raise ValueError("Degenerate authored static triangle")
        normal /= length
        expectation = np.asarray(expected, dtype=float)
        if float(np.dot(normal, expectation)) < 0:
            pts[1], pts[2] = pts[2], pts[1]
            uv_values[1], uv_values[2] = uv_values[2], uv_values[1]
            normal = -normal
        start = len(self.positions)
        self.positions.extend(point.tolist() for point in pts)
        self.normals.extend(normal.tolist() for _ in pts)
        self.uvs.extend(uv_values)
        self.triangles.append([start, start + 1, start + 2])

    def ellipsoid(self, label: str, center, radii, rings: int = 8, segments: int = 12) -> None:
        """Write a closed, outward-wound low-poly ellipsoid in local space."""
        start = len(self.positions)
        center = np.asarray(center, dtype=float)
        radii = np.asarray(radii, dtype=float)
        if np.any(radii <= 0) or rings < 3 or segments < 3:
            raise ValueError("Invalid original ellipsoid")

        def point(lat: int, lon: int) -> np.ndarray:
            theta = math.pi * lat / rings
            phi = 2.0 * math.pi * lon / segments
            return center + np.array([
                radii[0] * math.sin(theta) * math.cos(phi),
                radii[1] * math.cos(theta),
                radii[2] * math.sin(theta) * math.sin(phi),
            ])

        def expected(point_value: np.ndarray) -> np.ndarray:
            return (point_value - center) / (radii * radii)

        top = point(0, 0)
        bottom = point(rings, 0)
        for lon in range(segments):
            nxt = (lon + 1) % segments
            a, b = point(1, lon), point(1, nxt)
            self.triangle([top, a, b], [[(lon + .5) / segments, 0], [lon / segments, 1 / rings], [nxt / segments, 1 / rings]],
                          expected((top + a + b) / 3.0))
        for lat in range(1, rings - 1):
            for lon in range(segments):
                nxt = (lon + 1) % segments
                a, b, c, d = point(lat, lon), point(lat, nxt), point(lat + 1, nxt), point(lat + 1, lon)
                self.triangle([a, d, c], [[lon / segments, lat / rings], [lon / segments, (lat + 1) / rings], [nxt / segments, (lat + 1) / rings]],
                              expected((a + d + c) / 3.0))
                self.triangle([a, c, b], [[lon / segments, lat / rings], [nxt / segments, (lat + 1) / rings], [nxt / segments, lat / rings]],
                              expected((a + c + b) / 3.0))
        for lon in range(segments):
            nxt = (lon + 1) % segments
            a, b = point(rings - 1, lon), point(rings - 1, nxt)
            self.triangle([bottom, b, a], [[(lon + .5) / segments, 1], [nxt / segments, (rings - 1) / rings], [lon / segments, (rings - 1) / rings]],
                          expected((bottom + a + b) / 3.0))
        self.pieces.append({"name": label, "vertex_start": start, "vertex_count": len(self.positions) - start})

    def torus(self, label: str, center, major: float, minor: float, direction: float, segments: int = 16, tube: int = 6) -> None:
        """Write an outward-wound ring around the local Z axis."""
        start = len(self.positions)
        center = np.asarray(center, dtype=float)
        if major <= 0 or minor <= 0:
            raise ValueError("Invalid original torus")

        def point(around: int, cross: int) -> np.ndarray:
            phi = 2.0 * math.pi * around / segments
            theta = 2.0 * math.pi * cross / tube
            radius = major + minor * math.cos(theta)
            return center + np.array([radius * math.cos(phi), radius * math.sin(phi), direction * minor * math.sin(theta)])

        def expected(points) -> np.ndarray:
            average = sum(points) / len(points)
            radial = average - center
            radial[2] = 0
            radial_length = float(np.linalg.norm(radial))
            if radial_length < 1e-9:
                radial = np.array([1.0, 0.0, 0.0])
            else:
                radial /= radial_length
            return radial + np.array([0.0, 0.0, direction * (average[2] - center[2]) / minor])

        for around in range(segments):
            nxt_around = (around + 1) % segments
            for cross in range(tube):
                nxt_cross = (cross + 1) % tube
                a, b, c, d = point(around, cross), point(nxt_around, cross), point(nxt_around, nxt_cross), point(around, nxt_cross)
                uv_a = [around / segments, cross / tube]
                uv_b = [nxt_around / segments, cross / tube]
                uv_c = [nxt_around / segments, nxt_cross / tube]
                uv_d = [around / segments, nxt_cross / tube]
                self.triangle([a, b, c], [uv_a, uv_b, uv_c], expected([a, b, c]))
                self.triangle([a, c, d], [uv_a, uv_c, uv_d], expected([a, c, d]))
        self.pieces.append({"name": label, "vertex_start": start, "vertex_count": len(self.positions) - start})

    def pyramid(self, label: str, center, size: float, height: float) -> None:
        """Write a compact four-sided crown shard that remains part of the rigid eye assembly."""
        start = len(self.positions)
        center = np.asarray(center, dtype=float)
        base = [
            center + np.array([-size, -size, 0]), center + np.array([size, -size, 0]),
            center + np.array([size, size, 0]), center + np.array([-size, size, 0]),
        ]
        apex = center + np.array([0, 0, height])
        for index in range(4):
            nxt = (index + 1) % 4
            points = [base[index], base[nxt], apex]
            self.triangle(points, [[0.05, .05], [.95, .05], [.5, .95]], np.mean(points, axis=0) - center)
        self.triangle([base[0], base[2], base[1]], [[0, 0], [1, 1], [1, 0]], np.array([0, 0, -1]))
        self.triangle([base[0], base[3], base[2]], [[0, 0], [0, 1], [1, 1]], np.array([0, 0, -1]))
        self.pieces.append({"name": label, "vertex_start": start, "vertex_count": len(self.positions) - start})

    def data(self) -> dict:
        return {
            "positions": self.positions,
            "normals": self.normals,
            "uvs": self.uvs,
            "triangles": self.triangles,
            "authoringSpace": "MeshFilter local space under Root_M/base/body/neck/eye/kraken2_eye",
            "authoringBoundary": "Original procedural rigid geometry. No native mesh surface, texture pixels, UVs, skin weights, or animation data are read.",
        }


def parse_glb(path: Path) -> tuple[dict, bytes]:
    raw = path.read_bytes()
    if len(raw) < 20:
        raise ValueError("GLB is too short")
    magic, version, length = struct.unpack_from("<III", raw, 0)
    if magic != 0x46546C67 or version != 2 or length != len(raw):
        raise ValueError("Invalid GLB header")
    json_length, json_type = struct.unpack_from("<II", raw, 12)
    if json_type != 0x4E4F534A:
        raise ValueError("GLB JSON chunk missing")
    json_start = 20
    json_end = json_start + json_length
    bin_length, bin_type = struct.unpack_from("<II", raw, json_end)
    if bin_type != 0x004E4942 or json_end + 8 + bin_length != len(raw):
        raise ValueError("GLB BIN chunk missing")
    return json.loads(raw[json_start:json_end].decode()), raw[json_end + 8:]


def accessor(root: dict, bin_data: bytes, index: int, shape, dtype) -> np.ndarray:
    value = root["accessors"][index]
    view = root["bufferViews"][value["bufferView"]]
    assert view["buffer"] == 0 and "byteStride" not in view
    assert value.get("byteOffset", 0) == 0
    count = value["count"]
    raw = memoryview(bin_data)[view.get("byteOffset", 0):view.get("byteOffset", 0) + view["byteLength"]]
    array = np.frombuffer(raw, dtype=dtype).reshape(shape(count))
    return array.copy()


def validate_static_glb() -> dict:
    root, bin_data = parse_glb(GLB)
    assert len(root["meshes"]) == 1
    assert "skins" not in root or len(root["skins"]) == 0
    primitive = root["meshes"][0]["primitives"]
    assert len(primitive) == 1
    primitive = primitive[0]
    attrs = primitive["attributes"]
    assert primitive.get("mode", 4) == 4
    assert "JOINTS_0" not in attrs and "WEIGHTS_0" not in attrs
    assert set(attrs) == {"POSITION", "NORMAL", "TEXCOORD_0"}
    positions = accessor(root, bin_data, attrs["POSITION"], lambda n: (n, 3), "<f4")
    normals = accessor(root, bin_data, attrs["NORMAL"], lambda n: (n, 3), "<f4")
    uvs = accessor(root, bin_data, attrs["TEXCOORD_0"], lambda n: (n, 2), "<f4")
    indices = accessor(root, bin_data, primitive["indices"], lambda n: (n,), "<u2")
    assert positions.shape == normals.shape and len(uvs) == len(positions)
    assert len(indices) % 3 == 0 and len(indices) > 0 and indices.max() < len(positions)
    assert np.isfinite(positions).all() and np.isfinite(normals).all() and np.isfinite(uvs).all()
    faces = positions[indices.reshape(-1, 3)]
    expected = np.cross(faces[:, 1] - faces[:, 0], faces[:, 2] - faces[:, 0])
    expected /= np.linalg.norm(expected, axis=1)[:, None]
    actual = normals[indices.reshape(-1, 3)].mean(axis=1)
    actual /= np.linalg.norm(actual, axis=1)[:, None]
    assert float(np.min(np.einsum("ij,ij->i", expected, actual))) > .999
    source = json.loads(SOURCE.read_text())
    assert np.allclose(positions, np.asarray(source["positions"], dtype=np.float32))
    assert np.allclose(normals, np.asarray(source["normals"], dtype=np.float32))
    assert np.allclose(uvs, np.asarray(source["uvs"], dtype=np.float32))
    assert np.array_equal(indices.reshape(-1, 3), np.asarray(source["triangles"], dtype=np.uint16))
    return {
        "status": "PASS_ABYSSAL_KRAKEN_STATIC_EYE_CONTRACT",
        "asset": ASSET,
        "contract": "one unskinned triangle primitive in the selected MeshFilter local space",
        "vertices": int(len(positions)),
        "triangles": int(len(indices) // 3),
        "bounds": {"min": positions.min(axis=0).tolist(), "max": positions.max(axis=0).tolist()},
        "minimumTriangleNormalAgreement": float(np.min(np.einsum("ij,ij->i", expected, actual))),
        "glbSha256": sha(GLB),
        "sourceSha256": sha(SOURCE),
        "textureSha256": sha(OUT / TEXTURE),
        "noSkinAttributes": True,
        "authoringBoundary": source["authoringBoundary"],
        "scope": "Static GLB contract and authored source equivalence only. Live exact-target binding, material, animation parent motion, culling, gameplay, and art review require a fresh isolated-game trial.",
    }


def main() -> None:
    paint_texture(OUT / TEXTURE)
    surface = StaticSurface()
    # The replacement is a complete crown-eye carapace centered on the static
    # renderer's own local origin. Its size and silhouette are independently
    # authored; no native eye mesh dimensions or surface data are consulted.
    surface.ellipsoid("Faceted abyssal eye carapace", [0, 0, 0], [1.82, 1.48, 1.82], rings=9, segments=14)
    for direction, label in ((1.0, "front"), (-1.0, "back")):
        surface.ellipsoid(f"{label} sea-glass lens", [0, .03, direction * 1.58], [.76, .64, .24], rings=6, segments=12)
        surface.torus(f"{label} antique-gold iris ring", [0, .03, direction * 1.68], .91, .11, direction, segments=16, tube=6)
        surface.ellipsoid(f"{label} dark pupil", [0, .03, direction * 1.84], [.28, .26, .065], rings=5, segments=10)
    for index in range(8):
        angle = 2.0 * math.pi * index / 8.0
        center = np.array([1.52 * math.cos(angle), 1.21 * math.sin(angle), .04])
        surface.ellipsoid(f"Equatorial pearl node {index + 1}", center, [.24, .22, .20], rings=5, segments=8)
    for index in range(4):
        angle = math.pi / 4.0 + index * math.pi / 2.0
        center = np.array([1.36 * math.cos(angle), 1.10 * math.sin(angle), 1.12])
        surface.pyramid(f"Forward crown shard {index + 1}", center, .19, .33)
    data = surface.data()
    SOURCE.write_text(json.dumps(data, separators=(",", ":")) + "\n")
    PIECES.write_text(json.dumps(surface.pieces, indent=2) + "\n")
    write_static_glb(GLB, data)
    validation = validate_static_glb()
    validation["piecesSha256"] = sha(PIECES)
    validation["generatorSha256"] = sha(Path(__file__))
    VALIDATION.write_text(json.dumps(validation, indent=2) + "\n")
    print(json.dumps(validation, indent=2))


if __name__ == "__main__":
    main()
