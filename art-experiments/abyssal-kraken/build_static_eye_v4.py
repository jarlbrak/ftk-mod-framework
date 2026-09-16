#!/usr/bin/env python3
"""Author the compact V4 rigid companion for the modern Kraken head eye renderer.

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

ASSET = "abyssal-crown-kraken-eye-v4"
TEXTURE = ASSET + ".png"
GLB = OUT / (ASSET + ".glb")
SOURCE = OUT / (ASSET + ".source.json")
PIECES = OUT / (ASSET + ".pieces.json")
VALIDATION = OUT / (ASSET + ".validation.json")


PALETTE = {
    "abyss": np.array([5, 20, 30], dtype=float),
    "shell": np.array([12, 77, 96], dtype=float),
    "shell_light": np.array([32, 128, 137], dtype=float),
    "glass": np.array([74, 203, 190], dtype=float),
    "glass_light": np.array([178, 242, 224], dtype=float),
    "copper": np.array([184, 123, 67], dtype=float),
    "copper_light": np.array([239, 193, 114], dtype=float),
    "pupil": np.array([3, 10, 18], dtype=float),
    "pupil_light": np.array([19, 71, 84], dtype=float),
}


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def paint_texture(path: Path) -> None:
    """Paint a small original atlas for a readable low-poly crown-eye."""
    width = height = 512
    yy, xx = np.mgrid[0:height, 0:width]
    u = xx / (width - 1)
    v = yy / (height - 1)
    paint = np.zeros((height, width, 3), dtype=float)

    def band(low: float, high: float, dark, light, frequency: float, diagonal: float) -> None:
        mask = (v >= low) & (v < high)
        local = np.clip((v - low) / (high - low), 0.0, 1.0)
        facets = .42 + .30 * np.sin((u * frequency + local * diagonal) * math.pi)
        color = dark[None, None, :] * (1.0 - facets[..., None]) + light[None, None, :] * facets[..., None]
        paint[mask] = color[mask]

    # Each horizontal atlas band belongs to one named original component.
    # Broad facets retain their identity at the game's combat-camera distance.
    band(.00, .27, PALETTE["abyss"], PALETTE["shell_light"], 6.0, 2.0)
    band(.29, .51, PALETTE["shell"], PALETTE["glass_light"], 4.0, -1.0)
    band(.53, .71, PALETTE["copper"], PALETTE["copper_light"], 8.0, 1.0)
    band(.73, .87, PALETTE["pupil"], PALETTE["pupil_light"], 3.0, 0.0)
    band(.89, 1.01, PALETTE["copper"], PALETTE["glass"], 5.0, 2.0)
    # Keep the packing gaps deliberately dark, so bilinear filtering cannot
    # turn a bezel or pupil into the shell color.
    for center in (.28, .52, .72, .88):
        seam = np.exp(-((v - center) ** 2) / .000008)
        paint = paint * (1.0 - seam[..., None]) + PALETTE["abyss"][None, None, :] * seam[..., None]
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

    def ellipsoid(self, label: str, center, radii, rings: int = 8, segments: int = 12, atlas=(0.0, 0.0, 1.0, 1.0)) -> None:
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

        u0, v0, u1, v1 = atlas
        assert 0.0 <= u0 < u1 <= 1.0 and 0.0 <= v0 < v1 <= 1.0

        def uv(longitude: float, latitude: float) -> list[float]:
            return [u0 + (u1 - u0) * longitude, v0 + (v1 - v0) * latitude]

        top = point(0, 0)
        bottom = point(rings, 0)
        for lon in range(segments):
            nxt = (lon + 1) % segments
            a, b = point(1, lon), point(1, nxt)
            self.triangle([top, a, b], [uv((lon + .5) / segments, 0), uv(lon / segments, 1 / rings), uv(nxt / segments, 1 / rings)],
                          expected((top + a + b) / 3.0))
        for lat in range(1, rings - 1):
            for lon in range(segments):
                nxt = (lon + 1) % segments
                a, b, c, d = point(lat, lon), point(lat, nxt), point(lat + 1, nxt), point(lat + 1, lon)
                self.triangle([a, d, c], [uv(lon / segments, lat / rings), uv(lon / segments, (lat + 1) / rings), uv(nxt / segments, (lat + 1) / rings)],
                              expected((a + d + c) / 3.0))
                self.triangle([a, c, b], [uv(lon / segments, lat / rings), uv(nxt / segments, (lat + 1) / rings), uv(nxt / segments, lat / rings)],
                              expected((a + c + b) / 3.0))
        for lon in range(segments):
            nxt = (lon + 1) % segments
            a, b = point(rings - 1, lon), point(rings - 1, nxt)
            self.triangle([bottom, b, a], [uv((lon + .5) / segments, 1), uv(nxt / segments, (rings - 1) / rings), uv(lon / segments, (rings - 1) / rings)],
                          expected((bottom + a + b) / 3.0))
        self.pieces.append({"name": label, "vertex_start": start, "vertex_count": len(self.positions) - start})

    def torus(self, label: str, center, major: float, minor: float, direction: float, segments: int = 16, tube: int = 6, atlas=(0.0, 0.0, 1.0, 1.0)) -> None:
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

        u0, v0, u1, v1 = atlas
        assert 0.0 <= u0 < u1 <= 1.0 and 0.0 <= v0 < v1 <= 1.0

        def uv(around_value: float, cross_value: float) -> list[float]:
            return [u0 + (u1 - u0) * around_value, v0 + (v1 - v0) * cross_value]

        for around in range(segments):
            nxt_around = (around + 1) % segments
            for cross in range(tube):
                nxt_cross = (cross + 1) % tube
                a, b, c, d = point(around, cross), point(nxt_around, cross), point(nxt_around, nxt_cross), point(around, nxt_cross)
                uv_a = uv(around / segments, cross / tube)
                uv_b = uv(nxt_around / segments, cross / tube)
                uv_c = uv(nxt_around / segments, nxt_cross / tube)
                uv_d = uv(around / segments, nxt_cross / tube)
                self.triangle([a, b, c], [uv_a, uv_b, uv_c], expected([a, b, c]))
                self.triangle([a, c, d], [uv_a, uv_c, uv_d], expected([a, c, d]))
        self.pieces.append({"name": label, "vertex_start": start, "vertex_count": len(self.positions) - start})

    def pyramid(self, label: str, center, size: float, height: float, atlas=(0.0, 0.0, 1.0, 1.0)) -> None:
        """Write a compact four-sided crown shard that remains part of the rigid eye assembly."""
        start = len(self.positions)
        center = np.asarray(center, dtype=float)
        base = [
            center + np.array([-size, -size, 0]), center + np.array([size, -size, 0]),
            center + np.array([size, size, 0]), center + np.array([-size, size, 0]),
        ]
        apex = center + np.array([0, 0, height])
        u0, v0, u1, v1 = atlas
        assert 0.0 <= u0 < u1 <= 1.0 and 0.0 <= v0 < v1 <= 1.0

        def uv(u: float, v: float) -> list[float]:
            return [u0 + (u1 - u0) * u, v0 + (v1 - v0) * v]

        for index in range(4):
            nxt = (index + 1) % 4
            points = [base[index], base[nxt], apex]
            self.triangle(points, [uv(.05, .05), uv(.95, .05), uv(.5, .95)], np.mean(points, axis=0) - center)
        self.triangle([base[0], base[2], base[1]], [uv(0, 0), uv(1, 1), uv(1, 0)], np.array([0, 0, -1]))
        self.triangle([base[0], base[3], base[2]], [uv(0, 0), uv(0, 1), uv(1, 1)], np.array([0, 0, -1]))
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
    # V3 proved that a complete globe overwhelms the skinned head in the live
    # combat camera. V4 deliberately keeps an independently authored jewel
    # under 1.5 local units wide, selected from that live silhouette outcome,
    # never from native mesh dimensions or surface data.
    shell_atlas = (0.0, .00, 1.0, .27)
    glass_atlas = (0.0, .29, 1.0, .51)
    copper_atlas = (0.0, .53, 1.0, .71)
    pupil_atlas = (0.0, .73, 1.0, .87)
    crown_atlas = (0.0, .89, 1.0, 1.0)
    surface.ellipsoid(
        "Compressed crown-eye casing", [0, 0, 0], [.72, .48, .25],
        rings=7, segments=12, atlas=shell_atlas,
    )
    # Symmetric faces make the eye legible as the animated parent turns,
    # without needing any game animation data to determine a camera-facing side.
    for direction, label in ((1.0, "front"), (-1.0, "back")):
        surface.ellipsoid(
            f"{label} sea-glass lens", [0, .01, direction * .255], [.43, .30, .052],
            rings=6, segments=12, atlas=glass_atlas,
        )
        surface.torus(
            f"{label} copper bezel", [0, .01, direction * .295], .455, .052, direction,
            segments=14, tube=5, atlas=copper_atlas,
        )
        surface.ellipsoid(
            f"{label} vertical pupil", [0, .01, direction * .355], [.105, .185, .020],
            rings=5, segments=10, atlas=pupil_atlas,
        )
    # Three small cabochons introduce the crown motif without changing the
    # component into another head-sized silhouette.
    for index, x in enumerate((-.30, 0.0, .30), start=1):
        surface.ellipsoid(
            f"Crown cabochon {index}", [x, .455 + (.055 if index == 2 else 0.0), 0],
            [.075, .10, .050], rings=5, segments=8, atlas=crown_atlas,
        )
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
