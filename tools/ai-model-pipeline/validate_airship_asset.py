#!/usr/bin/env python3
"""Audit original SkySpike GLB geometry and an authored .ship.json, without the game.

Requires NumPy. This is the spike's ordinary glTF route, not the public FTK skin
export format. A finite sample grid cannot prove continuous floor or live fit.
"""
import argparse
import base64
import hashlib
import json
import math
import re
import struct
from pathlib import Path

import numpy as np

FLOAT_MAX = float(np.finfo(np.float32).max)

def require(condition, message):
    if not condition:
        raise ValueError(message)


def number(value, label):
    require(type(value) in (float, int) and math.isfinite(value), f"Nonfinite/nonnumeric {label}")
    require(abs(value) <= FLOAT_MAX, f"{label} exceeds runtime float range")
    return float(value)


def rotor_number(value, label):
    # The runtime parses rotor fields as float before axis length and speed checks.
    return float(np.float32(number(value, label)))


def matches_node(part, name):
    # Equality is deliberately excluded: a literal source name can itself look like
    # another source's primitive label. Only emitted ASCII primitive/chunk suffixes match.
    return re.fullmatch(re.escape(name) + r"/p[0-9]+(?:#[0-9]+)?", part) is not None


def load_contract(path):
    c = json.loads(Path(path).read_text())
    require(number(c.get("schemaVersion"), "schemaVersion") == 1, "Unsupported schemaVersion")
    require(c.get("longAxis") in ("x", "z"), "longAxis must be x or z")
    for key in ("deck", "hull"):
        require(isinstance(c.get(key), dict), f"Missing {key} rectangle")
        r = c[key]
        for field in ("minX", "maxX", "minZ", "maxZ"):
            number(r.get(field), f"{key}.{field}")
        for axis in ("X", "Z"):
            extent = r["max" + axis] - r["min" + axis]
            require(0.01 <= extent <= FLOAT_MAX, f"Invalid {key} extent")
    number(c["deck"].get("y"), "deck.y")
    d, h = c["deck"], c["hull"]
    require(all(h["min" + a] - .001 <= d["min" + a] <= d["max" + a] <= h["max" + a] + .001
                for a in ("X", "Z")), "Deck must lie inside hull")
    upper = c.get("upperworks", [])
    require(isinstance(upper, list) and all(isinstance(x, str) and x for x in upper), "Invalid upperworks")
    require(len(set(upper)) == len(upper), "Duplicate upperworks declaration")
    c["upperworks"] = upper
    model = c.get("combatModel")
    require(model is None or isinstance(model, str) and re.fullmatch(r"[a-z0-9_-]{1,80}", model),
            "combatModel must be a local lowercase model key")
    rotors = c.get("rotors", [])
    require(isinstance(rotors, list) and len(rotors) <= 16, "Invalid rotors array or more than 16 rotors")
    names = set()
    for rotor in rotors:
        require(isinstance(rotor, dict), "Rotor must be an object")
        name = rotor.get("node")
        require(isinstance(name, str) and bool(name) and name not in names and name not in upper,
                "Rotor node must be unique and separate from upperworks")
        names.add(name)
        for field in ("pivot", "axis"):
            vector = rotor.get(field)
            require(isinstance(vector, list) and len(vector) == 3, "Rotor vector requires three numbers")
            rotor[field] = [rotor_number(x, "rotor." + field) for x in vector]
        length = math.sqrt(sum(x*x for x in rotor["axis"]))
        require(length >= .000001, "Rotor axis must be nonzero")
        rotor["axis"] = [float(np.float32(x / length)) for x in rotor["axis"]]
        rotor["degreesPerSecond"] = rotor_number(rotor.get("degreesPerSecond"), "rotor.degreesPerSecond")
        require(abs(rotor["degreesPerSecond"]) <= 720,
                "Rotor speed exceeds 720 degrees per second")
    c["rotors"] = rotors
    return c


def load_glb(path):
    raw = Path(path).read_bytes()
    require(20 <= len(raw) <= 256 * 1024 * 1024, "GLB outside runtime file budget")
    require(struct.unpack_from("<III", raw) == (0x46546C67, 2, len(raw)), "Invalid GLB header")
    chunks, offset = {}, 12
    while offset < len(raw):
        require(offset + 8 <= len(raw), "Truncated GLB chunk header")
        size, kind = struct.unpack_from("<II", raw, offset)
        offset += 8
        require(size % 4 == 0 and offset + size <= len(raw), "Invalid GLB chunk length")
        require(kind not in chunks, "Duplicate GLB chunk")
        chunks[kind] = raw[offset:offset + size]
        offset += size
    require(0x4E4F534A in chunks and 0x004E4942 in chunks, "GLB requires JSON and BIN")
    doc, binary = json.loads(chunks[0x4E4F534A]), chunks[0x004E4942]
    require(doc.get("asset", {}).get("version") == "2.0", "Expected glTF 2.0")
    require(not doc.get("extensionsRequired"), "Runtime rejects required glTF extensions")
    unsupported = {"KHR_draco_mesh_compression", "EXT_meshopt_compression", "KHR_texture_basisu", "EXT_texture_webp"}

    def check_extensions(value):
        if isinstance(value, dict):
            require(not unsupported.intersection(value.get("extensions", {})), "Unsupported compression/image extension")
            for child in value.values():
                check_extensions(child)
        elif isinstance(value, list):
            for child in value:
                check_extensions(child)

    check_extensions(doc)
    buffers = doc.get("buffers", [])
    require(len(buffers) == 1 and "uri" not in buffers[0], "Expected one self-contained binary buffer")
    require(0 <= buffers[0]["byteLength"] <= len(binary), "Binary buffer exceeds BIN chunk")
    require(not doc.get("skins") and not doc.get("animations"), "Audit expects static unskinned ships")

    def view(index):
        require(type(index) is int and 0 <= index < len(doc.get("bufferViews", [])), "Invalid bufferView index")
        v = doc["bufferViews"][index]
        start, length = v.get("byteOffset", 0), v["byteLength"]
        require(v.get("buffer", 0) == 0 and type(start) is int and type(length) is int and
                start >= 0 and length >= 0 and start + length <= buffers[0]["byteLength"], "Invalid bufferView range")
        return v, start, length

    def read(index, kind, components):
        require(type(index) is int and 0 <= index < len(doc.get("accessors", [])), "Invalid accessor index")
        a = doc["accessors"][index]
        require("sparse" not in a and a.get("type") == kind and a.get("componentType") in components,
                "Unsupported accessor type or sparse storage")
        v, start, size = view(a["bufferView"])
        width = {"SCALAR": 1, "VEC2": 2, "VEC3": 3}[kind]
        dtype = np.dtype({5121: "u1", 5123: "<u2", 5125: "<u4", 5126: "<f4"}[a["componentType"]])
        count, local = a["count"], a.get("byteOffset", 0)
        stride = v.get("byteStride", dtype.itemsize * width)
        require(type(count) is int and 0 < count <= 3000000 and type(local) is int and local >= 0 and
                type(stride) is int and stride >= dtype.itemsize * width and stride % dtype.itemsize == 0,
                "Invalid accessor count/offset/stride")
        require(local + (count - 1) * stride + dtype.itemsize * width <= size, "Accessor exceeds bufferView")
        result = np.ndarray((count, width), dtype=dtype, buffer=binary,
                            offset=start + local, strides=(stride, dtype.itemsize)).copy()
        require(np.isfinite(result).all(), "Nonfinite accessor values")
        if a.get("normalized"):
            require(kind == "VEC2" and a["componentType"] in (5121, 5123), "Unsupported normalized accessor")
            result = result.astype(float) / np.iinfo(dtype).max
        return result

    for im in doc.get("images", []):
        require(im.get("mimeType") in ("image/png", "image/jpeg"), "Only embedded PNG/JPEG images supported by audit")
        if "bufferView" in im:
            _, start, length = view(im["bufferView"])
            encoded = binary[start:start + length]
        else:
            require(isinstance(im.get("uri"), str) and im["uri"].startswith(("data:image/png;base64,", "data:image/jpeg;base64,")),
                    "Audit requires embedded images")
            encoded = base64.b64decode(im["uri"].split(",", 1)[1], validate=True)
        require(encoded.startswith(b"\x89PNG\r\n\x1a\n") if im["mimeType"] == "image/png" else encoded.startswith(b"\xff\xd8\xff"),
                "Embedded image signature does not match PNG/JPEG MIME type")
    for texture in doc.get("textures", []):
        source = texture.get("source")
        require(type(source) is int and 0 <= source < len(doc.get("images", [])), "Texture has invalid image source")
    for material in doc.get("materials", []):
        pbr = material.get("pbrMetallicRoughness", {})
        for owner, key, width in ((pbr, "baseColorFactor", 4), (material, "emissiveFactor", 3)):
            if key in owner:
                require(isinstance(owner[key], list) and len(owner[key]) == width, "Invalid material factor vector")
                for value in owner[key]:
                    number(value, key)
        for owner, key in ((pbr, "metallicFactor"), (pbr, "roughnessFactor"), (material, "alphaCutoff")):
            if key in owner:
                number(owner[key], key)
        for owner, key in ((pbr, "baseColorTexture"), (material, "emissiveTexture")):
            if key in owner:
                index = owner[key].get("index")
                require(type(index) is int and 0 <= index < len(doc.get("textures", [])), "Invalid material texture index")
    return doc, read


def node_matrix(node):
    if "matrix" in node:
        m = np.asarray(node["matrix"], dtype=float)
        require(m.shape == (16,), "Invalid node matrix")
        m = m.reshape(4, 4).T
        require(np.allclose(m[3], [0, 0, 0, 1]), "Node matrix must be affine")
    else:
        t = np.asarray(node.get("translation", [0, 0, 0]), dtype=float)
        s = np.asarray(node.get("scale", [1, 1, 1]), dtype=float)
        q = np.asarray(node.get("rotation", [0, 0, 0, 1]), dtype=float)
        require(t.shape == s.shape == (3,) and q.shape == (4,), "Invalid TRS dimensions")
        require(np.isfinite(q).all(), "Nonfinite rotation")
        norm = np.linalg.norm(q)
        q = q / norm if norm > 1e-6 else np.array([0., 0., 0., 1.])
        x, y, z, w = q
        r = np.array([[1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w)],
                      [2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w)],
                      [2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y)]])
        m = np.eye(4)
        m[:3, :3], m[:3, 3] = r @ np.diag(s), t
    require(np.isfinite(m).all() and abs(np.linalg.det(m[:3, :3])) > 1e-15, "Nonfinite or singular transform")
    return m


def geometry(doc, read, upperworks, max_triangles):
    parts, points, node_names = [], [], []
    vertices, triangles = 0, 0

    def emit(mesh_index, world, name):
        nonlocal vertices, triangles
        require(type(mesh_index) is int and 0 <= mesh_index < len(doc.get("meshes", [])), "Invalid mesh index")
        node_names.append(name)
        for i, p in enumerate(doc["meshes"][mesh_index]["primitives"]):
            require(p.get("mode", 4) == 4 and not p.get("targets"), "Expected triangle primitive without morphs")
            a = p["attributes"]
            if "material" in p:
                require(type(p["material"]) is int and 0 <= p["material"] < len(doc.get("materials", [])), "Invalid primitive material index")
            require(not any(k.startswith(("JOINTS_", "WEIGHTS_")) for k in a), "Skin attributes outside static audit")
            pos = read(a["POSITION"], "VEC3", (5126,)).astype(float)
            for key, kind, types in (("NORMAL", "VEC3", (5126,)), ("TEXCOORD_0", "VEC2", (5126, 5121, 5123))):
                if key in a:
                    require(len(read(a[key], kind, types)) == len(pos), f"{key} count mismatch")
            ix = read(p["indices"], "SCALAR", (5121, 5123, 5125)).ravel() if "indices" in p else np.arange(len(pos))
            require(len(ix) % 3 == 0 and len(ix) > 0 and ix.max() < len(pos), "Invalid triangle indices")
            vertices += len(pos)
            triangles += len(ix) // 3
            require(vertices <= 250000, "Runtime vertex budget exceeded")
            require(triangles <= max_triangles, "Selected triangle budget exceeded")
            pos = (np.column_stack([pos, np.ones(len(pos))]) @ world.T)[:, :3]
            pos[:, 0] *= -1
            require(np.isfinite(pos).all() and np.abs(pos).max() <= FLOAT_MAX,
                    "Transformed geometry outside runtime finite range")
            ix = ix.reshape(-1, 3).copy()
            if np.linalg.det(world) >= 0:
                ix[:, [1, 2]] = ix[:, [2, 1]]
            part_name = f"{name}/p{i}"
            parts.append({"name": part_name, "node": name,
                          "excluded": any(matches_node(part_name, upper) for upper in upperworks), "triangles": pos[ix]})
            points.append(pos)

    nodes = doc.get("nodes", [])

    def walk(index, parent, ancestors):
        require(type(index) is int and 0 <= index < len(nodes) and index not in ancestors and len(ancestors) <= 64,
                "Invalid/cyclic/deep node graph")
        n = nodes[index]
        require("skin" not in n, "Skinned node outside static audit")
        world = parent @ node_matrix(n)
        if "mesh" in n:
            emit(n["mesh"], world, n.get("name", f"node{index}"))
        for child in n.get("children", []):
            walk(child, world, ancestors + [index])

    scenes = doc.get("scenes", [])
    if scenes:
        scene = doc.get("scene", 0)
        require(type(scene) is int and 0 <= scene < len(scenes), "Invalid default scene")
        roots = scenes[scene].get("nodes", [])
    else:
        roots = []
    if roots:
        for root in roots:
            walk(root, np.eye(4), [])
    else:
        for i in range(len(doc.get("meshes", []))):
            emit(i, np.eye(4), f"mesh{i}")
    require(parts, "No triangle geometry")
    for name in upperworks:
        require(node_names.count(name) == 1, f"Upperwork node missing or ambiguous: {name}")
    retained = [p["triangles"] for p in parts if not p["excluded"]]
    require(retained, "Cutaway removes all geometry")
    all_points = np.concatenate(points)
    return np.concatenate(retained), all_points.min(0), all_points.max(0), {
        "vertices": vertices, "triangles": triangles, "source_nodes": node_names,
        "retained_triangles": sum(len(p["triangles"]) for p in parts if not p["excluded"]),
        "parts": [{"name": p["name"], "triangles": len(p["triangles"]), "excluded_upperwork": p["excluded"]} for p in parts]}


def sample_deck(tris, deck, spacing, tolerance):
    width, length = deck["maxX"] - deck["minX"], deck["maxZ"] - deck["minZ"]
    nx, nz = max(1, math.ceil(width / spacing)), max(1, math.ceil(length / spacing))
    require(nx * nz <= 20000, "Sampling budget exceeded; increase --spacing (maximum 20,000 samples)")
    xs = deck["minX"] + (np.arange(nx) + .5) * width / nx
    zs = deck["minZ"] + (np.arange(nz) + .5) * length / nz
    a, u, v = tris[:, 0], tris[:, 1] - tris[:, 0], tris[:, 2] - tris[:, 0]
    cross = np.cross(u, v)
    norm = np.linalg.norm(cross, axis=1)
    det = u[:, 0] * v[:, 2] - u[:, 2] * v[:, 0]
    valid = np.abs(det) > 1e-12
    a, u, v, cross, norm, det = [x[valid] for x in (a, u, v, cross, norm, det)]
    upward = cross[:, 1] / norm >= .65
    lo, hi = tris[valid].min(1), tris[valid].max(1)
    floors, top_clear, blocked, missing, examples = [], 0, 0, 0, []
    for z in zs:
        for x in xs:
            candidates = (lo[:, 0] <= x + 1e-9) & (hi[:, 0] >= x - 1e-9) & (lo[:, 2] <= z + 1e-9) & (hi[:, 2] >= z - 1e-9)
            ids = np.flatnonzero(candidates)
            dx, dz = x - a[ids, 0], z - a[ids, 2]
            b = (dx * v[ids, 2] - dz * v[ids, 0]) / det[ids]
            c = (u[ids, 0] * dz - u[ids, 2] * dx) / det[ids]
            inside = (b >= -1e-8) & (c >= -1e-8) & (b + c <= 1 + 1e-8)
            ids, b, c = ids[inside], b[inside], c[inside]
            heights = a[ids, 1] + b * u[ids, 1] + c * v[ids, 1]
            near = heights[(np.abs(heights - deck["y"]) <= tolerance) & upward[ids]]
            floor = float(near[np.argmin(np.abs(near - deck["y"]))]) if len(near) else None
            top = float(heights.max()) if len(heights) else None
            obstruction = top is not None and top > deck["y"] + tolerance
            if floor is None:
                missing += 1
            else:
                floors.append(floor)
            blocked += int(obstruction)
            top_clear += int(floor is not None and not obstruction)
            if (floor is None or obstruction) and len(examples) < 32:
                examples.append({"x": float(x), "z": float(z), "floor_y": floor, "topmost_y": top,
                                 "missing_floor": floor is None, "obstructed": obstruction})
    count = nx * nz
    return {"method": "cell-center vertical triangle intersections; upward floor normal Y >= 0.65",
            "grid": [nx, nz], "samples": count, "spacing_x": width / nx, "spacing_z": length / nz,
            "height_tolerance": tolerance, "floor_coverage": len(floors) / count,
            "unobstructed_floor_coverage": top_clear / count, "obstructed_fraction": blocked / count,
            "missing_floor_samples": missing, "obstructed_samples": blocked,
            "floor_y_min": min(floors) if floors else None, "floor_y_max": max(floors) if floors else None,
            "floor_y_variation": max(floors) - min(floors) if floors else None,
            "problem_samples_first_32": examples}


def audit(glb, sidecar=None, spacing=None, height_tolerance=None, min_coverage=.98,
          max_obstruction=.02, max_triangles=60000):
    glb = Path(glb)
    sidecar = Path(sidecar) if sidecar else glb.with_suffix(".ship.json")
    c = load_contract(sidecar)
    doc, read = load_glb(glb)
    tris, mn, mx, stats = geometry(doc, read, c["upperworks"], max_triangles)
    for rotor in c["rotors"]:
        require(stats["source_nodes"].count(rotor["node"]) == 1,
                "Rotor node missing or ambiguous: " + rotor["node"])
        require(any(matches_node(part["name"], rotor["node"]) for part in stats["parts"]),
                "Declared rotor has no rendered parts: " + rotor["node"])
        require(all(mn[i] - .02 <= rotor["pivot"][i] <= mx[i] + .02 for i in range(3)),
                "Rotor pivot exceeds the loaded model bounds")
    for axis, i in (("X", 0), ("Z", 2)):
        require(c["hull"]["min" + axis] >= mn[i] - .02 and c["hull"]["max" + axis] <= mx[i] + .02,
                "Authored hull exceeds loaded model bounds")
    require(mn[1] - .02 <= c["deck"]["y"] <= mx[1] + .02, "Deck height exceeds model bounds")
    beam_axis = "X" if c["longAxis"] == "z" else "Z"
    beam = c["deck"]["max" + beam_axis] - c["deck"]["min" + beam_axis]
    tolerance_source = "relative_default" if height_tolerance is None else "explicit"
    spacing = beam / 40 if spacing is None else number(spacing, "spacing")
    height_tolerance = beam * .02 if height_tolerance is None else number(height_tolerance, "height tolerance")
    require(spacing > 0 and height_tolerance >= 0, "Spacing must be positive; tolerance must be nonnegative")
    require(0 <= min_coverage <= 1 and 0 <= max_obstruction <= 1, "Coverage thresholds must be fractions")
    sampled = sample_deck(tris, c["deck"], spacing, height_tolerance)
    passed = sampled["floor_coverage"] >= min_coverage and sampled["obstructed_fraction"] <= max_obstruction
    warnings = ["Finite samples do not prove continuous floor, native slot fit, camera clearance, gameplay, or art acceptance."]
    if c["rotors"]:
        warnings.append("Rotor metadata checks do not prove correct blade selection, axle fit, full-turn clearance, motion, or pause behavior.")
    if c.get("combatModel") and c["combatModel"] != glb.stem:
        warnings.append("Named combatModel needs its own audit; this report measures only the current GLB cutaway.")
        for suffix in (".glb", ".ship.json"):
            require((glb.parent / (c["combatModel"] + suffix)).is_file(), "Named combat variant missing " + suffix)
    materials = doc.get("materials", [])
    if any("normalTexture" in m or "metallicRoughnessTexture" in m.get("pbrMetallicRoughness", {}) for m in materials):
        warnings.append("Normal/packed metallic-roughness textures are ignored by the runtime; inspect native material appearance.")
    if any(m.get("alphaMode") == "BLEND" or m.get("doubleSided") for m in materials):
        warnings.append("Runtime renders BLEND materials opaque and culls backfaces despite doubleSided settings.")
    if any(m.get("alphaMode") == "MASK" for m in materials):
        warnings.append("Triangle intersections do not evaluate texture alpha cutouts; confirm visible floor separately.")
    return {"schema_version": 1, "route": "skyspike-static-glb-authored-cutaway", "status": "PASS" if passed else "FAIL",
            "scope": "offline current-model geometry and sampled deck only", "file": glb.name, "sidecar": sidecar.name,
            "sha256": hashlib.sha256(glb.read_bytes()).hexdigest(),
            "sidecar_sha256": hashlib.sha256(sidecar.read_bytes()).hexdigest(),
            "runtime_bounds_min": mn.tolist(), "runtime_bounds_max": mx.tolist(), "geometry": stats,
            "thresholds": {"min_floor_coverage": min_coverage, "max_obstructed_fraction": max_obstruction,
                           "max_triangles": max_triangles, "runtime_max_vertices": 250000,
                           "triangle_budget_kind": "selected authoring threshold, not a loader or measured performance limit",
                           "deck_beam_asset_units": beam, "height_tolerance_fraction_of_beam": height_tolerance / beam,
                           "height_tolerance_source": tolerance_source},
            "rotors": c["rotors"], "deck": sampled, "warnings": warnings}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("glb", type=Path)
    parser.add_argument("--sidecar", type=Path)
    parser.add_argument("--spacing", type=float, help="maximum grid spacing in runtime asset units; default beam/40")
    parser.add_argument("--height-tolerance", type=float, help="floor contact tolerance in asset units; default 0.02*deck beam")
    parser.add_argument("--min-coverage", type=float, default=.98)
    parser.add_argument("--max-obstruction", type=float, default=.02)
    parser.add_argument("--max-triangles", type=int, default=60000,
                        help="selected authoring budget (default 60000); not a loader limit or performance claim")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    try:
        report = audit(args.glb, args.sidecar, args.spacing, args.height_tolerance,
                       args.min_coverage, args.max_obstruction, args.max_triangles)
    except (ValueError, KeyError, TypeError, IndexError, OverflowError, OSError, struct.error) as exc:
        report = {"schema_version": 1, "route": "skyspike-static-glb-authored-cutaway", "status": "FAIL",
                  "file": args.glb.name, "error": str(exc), "scope": "offline asset validation only"}
    rendered = json.dumps(report, indent=2, allow_nan=False) + "\n"
    if args.output:
        args.output.write_text(rendered)
    print(rendered, end="")
    return 0 if report["status"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
