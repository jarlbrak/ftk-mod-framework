#!/usr/bin/env python3
"""
06_clean_slivers.py

Mesh-geometry cleanup of ai-model-gen/mudwretch_rigged.glb (the Mudwretch Foreman
boss body) for FTK spec #72.

Problem: ~145 DEGENERATE SLIVER triangles (1.1% of 13213 tris) are near-zero-area
needles. Two of their three verts are nearly coincident near the body center
(x~0, y~1.5) while the third is far out at an arm tip (x~+/-1.2). In-game these
render as a radiating shard "cloud" around the shoulders/arms. The body surface
itself is coherent; only these extra-webbing slivers are the problem.

Fix: drop the flagged triangles from the INDEX buffer ONLY. Everything else
(POSITION, NORMAL, TEXCOORD_0, JOINTS_0, WEIGHTS_0, inverseBindMatrices, nodes,
skins, the rig) is left byte-for-byte unchanged. Vert count stays 17007. The mesh
stays fully RIGID (every vert weighted 100% to bone slot 0 = Root_M).

This does NOT scale or move any vertex (scale is handled separately in Core) and
touches no C# / no Content.* API.

GLB parsing mirrors tools/ai-model-pipeline/diag_decode_replica.py (12-byte header,
JSON chunk 0x4E4F534A, BIN chunk 0x004E4942, accessor decode of POSITION / indices
/ JOINTS_0 / WEIGHTS_0). GLB writing reuses the writer idiom from
tools/ai-model-pipeline/05b_rig_numpy.py (JSON chunk space-padded, BIN chunk
null-padded, little-endian u32 header) so the output is byte-compatible with the
C# RuntimeGltfMeshLoader.

Run:
  .venv-3dgen/bin/python tools/ai-model-pipeline/06_clean_slivers.py
"""

import json
import math
import shutil
import struct
import sys

REPO = "/Users/tbrack/Documents/Projects/FTK"
GLB_PATH = f"{REPO}/ai-model-gen/mudwretch_rigged.glb"
BACKUP_PATH = f"{REPO}/ai-model-gen/mudwretch_rigged.preslivers.glb"

# glTF componentType values (mirror diag_decode_replica.py)
COMP_F32 = 5126
COMP_U32 = 5125
COMP_U16 = 5123

# GLB container magic numbers (LE uint32)
GLB_MAGIC = 0x46546C67  # "glTF"
CHUNK_JSON = 0x4E4F534A  # "JSON"
CHUNK_BIN = 0x004E4942   # "BIN\0"

# --- sliver criterion (spec #72) ---
# A triangle is a degenerate stray needle if its LONGEST edge exceeds 10% of the
# mesh bbox diagonal. This is exactly the in-game render-problem signal that
# diag_decode_replica.py reports as fracTrisOver10pctDiag, and the histogram
# below shows it cleanly isolates the ~145 center-to-arm-tip needles (all
# maxEdge 0.40-1.18) while leaving real body faces untouched.
#
# NOTE on the brief's aspect-ratio idea (maxEdge/minEdge > 8 AND maxEdge > 0.15):
# the data shows that criterion is NOT a clean separator here. It flags 246 tris,
# sweeping in ~100 legitimate ELONGATED body faces that live in the 0.15-0.40
# maxEdge band and carry real surface area (mean area ~4.4e-3, comparable to
# legit faces). Conversely some true strays have minEdge large enough that their
# edge-ratio is only ~1.7, so aspect alone misses them. The maxEdge-vs-diagonal
# cut isolates exactly the 145 strays the brief describes and drives
# fracTrisOver10pctDiag to ~0, which the brief sets as the success target. Per
# the brief: "Tune the threshold if the histogram shows a gap elsewhere ... aim
# to remove ~145, not hundreds of legit faces." The histogram gap is at the
# 0.40 (=10% diagonal) line: the 0.30-0.40 band is sparse (54 tris) and the
# real-face mass sits below 0.15.
DIAG_FRAC = 0.10        # longest edge > 10% of bbox diagonal => stray needle
# aspect threshold kept ONLY as a logged secondary signal, not as the gate
ASPECT_THRESH = 8.0
MIN_EDGE_FLOOR = 1e-6


# ----------------------------------------------------------------------------
# GLB split (mirror of diag_decode_replica.split_glb)
# ----------------------------------------------------------------------------
def read_u32(b, p):
    v = b[p] | (b[p + 1] << 8) | (b[p + 2] << 16) | (b[p + 3] << 24)
    return v & 0xFFFFFFFF, p + 4


def split_glb(b):
    if b is None or len(b) < 12:
        raise ValueError("bad glb: < 12 bytes")
    p = 0
    magic, p = read_u32(b, p)
    version, p = read_u32(b, p)
    _total, p = read_u32(b, p)
    if magic != GLB_MAGIC or version != 2:
        raise ValueError("bad magic/version")
    if p + 8 > len(b):
        raise ValueError("no json chunk header")
    json_len, p = read_u32(b, p)
    json_type, p = read_u32(b, p)
    if json_type != CHUNK_JSON:
        raise ValueError("chunk 0 not JSON")
    if p + json_len > len(b):
        raise ValueError("json chunk out of bounds")
    json_text = b[p:p + json_len].decode("utf-8")
    p += json_len
    bin_payload = b""
    if p + 8 <= len(b):
        bin_len, p = read_u32(b, p)
        bin_type, p = read_u32(b, p)
        if bin_type == CHUNK_BIN and p + bin_len <= len(b):
            bin_payload = b[p:p + bin_len]
    return json_text, bin_payload


# ----------------------------------------------------------------------------
# Minimal accessor decode helpers (subset of diag_decode_replica.GltfDoc),
# enough to read POSITION (vec3 f32), indices (scalar u16/u32), JOINTS_0
# (vec4 u16), WEIGHTS_0 (vec4 f32).
# ----------------------------------------------------------------------------
def accessor_view(root, bin_payload, acc_index):
    accessors = root.get("accessors")
    bufferViews = root.get("bufferViews")
    if accessors is None or acc_index < 0 or acc_index >= len(accessors):
        return None
    acc = accessors[acc_index]
    count = acc.get("count", 0)
    comp = acc.get("componentType", 0)
    bv_index = acc.get("bufferView", -1)
    acc_byte_offset = acc.get("byteOffset", 0)
    if bufferViews is None or bv_index < 0 or bv_index >= len(bufferViews):
        return None
    bv = bufferViews[bv_index]
    bv_offset = bv.get("byteOffset", 0)
    byte_offset = bv_offset + acc_byte_offset
    if count > 0 and byte_offset >= 0:
        return byte_offset, count, comp
    return None


def read_f32(bin_payload, p):
    return struct.unpack_from("<f", bin_payload, p)[0]


def read_vec3(root, bin_payload, acc_index):
    av = accessor_view(root, bin_payload, acc_index)
    if av is None:
        return None
    off, count, comp = av
    if comp != COMP_F32 or off + count * 12 > len(bin_payload):
        return None
    r = []
    p = off
    for _ in range(count):
        r.append((read_f32(bin_payload, p), read_f32(bin_payload, p + 4),
                  read_f32(bin_payload, p + 8)))
        p += 12
    return r


def read_vec4_u16(root, bin_payload, acc_index):
    av = accessor_view(root, bin_payload, acc_index)
    if av is None:
        return None
    off, count, comp = av
    if comp != COMP_U16 or off + count * 8 > len(bin_payload):
        return None
    r = [0] * (count * 4)
    p = off
    for i in range(count * 4):
        r[i] = bin_payload[p] | (bin_payload[p + 1] << 8)
        p += 2
    return r


def read_vec4_f32(root, bin_payload, acc_index):
    av = accessor_view(root, bin_payload, acc_index)
    if av is None:
        return None
    off, count, comp = av
    if comp != COMP_F32 or off + count * 16 > len(bin_payload):
        return None
    r = [0.0] * (count * 4)
    p = off
    for i in range(count * 4):
        r[i] = read_f32(bin_payload, p)
        p += 4
    return r


def read_scalar_indices(root, bin_payload, acc_index):
    av = accessor_view(root, bin_payload, acc_index)
    if av is None:
        return None
    off, count, comp = av
    r = [0] * count
    p = off
    if comp == COMP_U32:
        if off + count * 4 > len(bin_payload):
            return None
        for i in range(count):
            r[i] = (bin_payload[p] | (bin_payload[p + 1] << 8) |
                    (bin_payload[p + 2] << 16) | (bin_payload[p + 3] << 24)) & 0xFFFFFFFF
            p += 4
        return r
    if comp == COMP_U16:
        if off + count * 2 > len(bin_payload):
            return None
        for i in range(count):
            r[i] = bin_payload[p] | (bin_payload[p + 1] << 8)
            p += 2
        return r
    return None


def read_primitive_accessors(root):
    """Returns (pos, joints, weights, indices) accessor indices for mesh[0].prim[0]."""
    mesh0 = root["meshes"][0]
    prim0 = mesh0["primitives"][0]
    attrs = prim0["attributes"]
    return (attrs.get("POSITION", -1), attrs.get("JOINTS_0", -1),
            attrs.get("WEIGHTS_0", -1), prim0.get("indices", -1))


# ----------------------------------------------------------------------------
# Sliver detection
# ----------------------------------------------------------------------------
def tri_edges(positions, i0, i1, i2):
    a = positions[i0]
    b = positions[i1]
    c = positions[i2]
    e_ab = math.dist(a, b)
    e_bc = math.dist(b, c)
    e_ca = math.dist(c, a)
    return e_ab, e_bc, e_ca


def histogram_maxedge(max_edges, edge_thresh):
    """Print a histogram of per-triangle maxEdge so the gap at the threshold is
    visible. Bins at/above edge_thresh are the flagged stray needles."""
    edges = [
        (0.00, 0.02), (0.02, 0.05), (0.05, 0.10), (0.10, 0.15),
        (0.15, 0.20), (0.20, 0.30), (0.30, 0.40), (0.40, 0.60),
        (0.60, 0.80), (0.80, 1.00), (1.00, 1.20), (1.20, 99.0),
    ]
    print("maxEdge histogram (per triangle):")
    for lo, hi in edges:
        c = sum(1 for e in max_edges if lo <= e < hi)
        bar = "#" * min(60, c)
        if lo >= edge_thresh:
            flag = "   <- DROP (stray needle)"
        elif hi <= edge_thresh:
            flag = "   <- keep (real face)"
        else:
            flag = "   <- keep/drop boundary (threshold = %.3f)" % edge_thresh
        print(f"  [{lo:5.2f}, {hi:5.2f})  {c:6d} {bar}{flag}")


# ----------------------------------------------------------------------------
# GLB writer: re-pack the BIN and patch all bufferView offsets/byteLengths plus
# buffers[0].byteLength, then re-emit the GLB. Mirrors 05b_rig_numpy.py:
# JSON chunk space-padded, BIN chunk null-padded, LE u32 header.
#
# Strategy that handles "index view is not last" correctly: rebuild the BIN by
# concatenating each bufferView's bytes in *ascending byteOffset order*, 4-byte
# aligned, recording the new offset for each view. The index view supplies the
# rebuilt (shortened) index bytes; every other view copies its original slice
# verbatim. This guarantees all downstream offsets and byteLengths are corrected.
# ----------------------------------------------------------------------------
def pad4(b):
    return b + b"\x00" * ((4 - len(b) % 4) % 4)


def repack_glb(root, old_bin, idx_bv_index, new_index_bytes):
    bufferViews = root["bufferViews"]
    # process views in ascending original byteOffset so layout order is preserved
    order = sorted(range(len(bufferViews)),
                   key=lambda i: bufferViews[i].get("byteOffset", 0))
    new_bin = bytearray()
    for bv_i in order:
        bv = bufferViews[bv_i]
        old_off = bv.get("byteOffset", 0)
        old_len = bv.get("byteLength", 0)
        if bv_i == idx_bv_index:
            data = new_index_bytes
        else:
            data = old_bin[old_off:old_off + old_len]
        # 4-byte align the start of each view
        while len(new_bin) % 4 != 0:
            new_bin.append(0)
        bv["byteOffset"] = len(new_bin)
        bv["byteLength"] = len(data)
        new_bin += data

    new_bin = bytes(pad4(new_bin))
    root["buffers"][0]["byteLength"] = len(new_bin)

    jraw = json.dumps(root, separators=(",", ":")).encode("utf-8")
    jchunk = jraw + b" " * ((4 - len(jraw) % 4) % 4)  # JSON pads with SPACES
    bchunk = new_bin                                  # already null-padded to 4
    total = 12 + 8 + len(jchunk) + 8 + len(bchunk)
    glb = struct.pack("<III", GLB_MAGIC, 2, total)
    glb += struct.pack("<II", len(jchunk), CHUNK_JSON) + jchunk
    glb += struct.pack("<II", len(bchunk), CHUNK_BIN) + bchunk
    return glb


# ----------------------------------------------------------------------------
# Verification: decode a glb blob and return a dict of checks.
# ----------------------------------------------------------------------------
def verify_glb_bytes(raw):
    json_text, bin_payload = split_glb(raw)
    root = json.loads(json_text)
    pos_a, joints_a, weights_a, idx_a = read_primitive_accessors(root)
    positions = read_vec3(root, bin_payload, pos_a)
    joints = read_vec4_u16(root, bin_payload, joints_a)
    weights = read_vec4_f32(root, bin_payload, weights_a)
    triangles = read_scalar_indices(root, bin_payload, idx_a)

    v_count = len(positions)
    idx_count = len(triangles)
    n_tri = idx_count // 3

    xs = [p[0] for p in positions]
    ys = [p[1] for p in positions]
    zs = [p[2] for p in positions]
    diag = math.sqrt((max(xs) - min(xs)) ** 2 + (max(ys) - min(ys)) ** 2 +
                     (max(zs) - min(zs)) ** 2)
    thresh = 0.10 * diag

    over = 0
    max_edge = 0.0
    idx_in_range = True
    for t in range(n_tri):
        i0, i1, i2 = triangles[t * 3], triangles[t * 3 + 1], triangles[t * 3 + 2]
        for ii in (i0, i1, i2):
            if ii < 0 or ii >= v_count:
                idx_in_range = False
        e = tri_edges(positions, i0, i1, i2)
        longest = max(e)
        if longest > max_edge:
            max_edge = longest
        if longest > thresh:
            over += 1
    frac_over = over / n_tri if n_tri else 0.0

    # rigid checks: JOINTS_0 unique values == [0], WEIGHTS_0 == (1,0,0,0) for all
    unique_joints = sorted(set(joints))
    rigid_weights = True
    for v in range(v_count):
        bi = v * 4
        if abs(weights[bi] - 1.0) > 1e-4:
            rigid_weights = False
            break
        if abs(weights[bi + 1]) > 1e-4 or abs(weights[bi + 2]) > 1e-4 or \
           abs(weights[bi + 3]) > 1e-4:
            rigid_weights = False
            break

    return {
        "vertexCount": v_count,
        "indexCount": idx_count,
        "triCount": n_tri,
        "bboxDiagonal": diag,
        "maxEdgeLen": max_edge,
        "fracTrisOver10pctDiag": frac_over,
        "indexInRange": idx_in_range,
        "uniqueJointValues": unique_joints,
        "rigidWeights": rigid_weights,
    }


# ----------------------------------------------------------------------------
# Main
# ----------------------------------------------------------------------------
def main():
    with open(GLB_PATH, "rb") as fh:
        raw = fh.read()

    json_text, bin_payload = split_glb(raw)
    root = json.loads(json_text)

    pos_a, joints_a, weights_a, idx_a = read_primitive_accessors(root)
    positions = read_vec3(root, bin_payload, pos_a)
    triangles = read_scalar_indices(root, bin_payload, idx_a)

    v_count = len(positions)
    idx_count = len(triangles)
    n_tri = idx_count // 3
    idx_comp = root["accessors"][idx_a]["componentType"]
    idx_bv_index = root["accessors"][idx_a]["bufferView"]

    # bbox diagonal -> the edge threshold (10% of diagonal)
    xs = [p[0] for p in positions]
    ys = [p[1] for p in positions]
    zs = [p[2] for p in positions]
    diag = math.sqrt((max(xs) - min(xs)) ** 2 + (max(ys) - min(ys)) ** 2 +
                     (max(zs) - min(zs)) ** 2)
    edge_thresh = DIAG_FRAC * diag

    print("=" * 70)
    print("BEFORE")
    print(f"  verts={v_count}  indices={idx_count}  tris={n_tri}  "
          f"indexComponentType={idx_comp}  indexBufferView={idx_bv_index}")
    print(f"  bboxDiagonal={diag:.4f}  edgeThreshold(10%)={edge_thresh:.4f}")

    # --- per-triangle edge stats + flagging ---
    max_edges = []
    flagged = []  # triangle indices to drop
    n_aspect_only = 0  # tris flagged by aspect>8 but NOT by the diagonal cut
    for t in range(n_tri):
        i0, i1, i2 = triangles[t * 3], triangles[t * 3 + 1], triangles[t * 3 + 2]
        e_ab, e_bc, e_ca = tri_edges(positions, i0, i1, i2)
        mn = min(e_ab, e_bc, e_ca)
        mx = max(e_ab, e_bc, e_ca)
        max_edges.append(mx)
        aspect = mx / max(mn, MIN_EDGE_FLOOR)
        is_stray = mx > edge_thresh
        if is_stray:
            flagged.append(t)
        elif aspect > ASPECT_THRESH:
            n_aspect_only += 1

    histogram_maxedge(max_edges, edge_thresh)
    flagged_set = set(flagged)
    print(f"\nSliver criterion: longest edge > {DIAG_FRAC:.0%} of bbox diagonal "
          f"(> {edge_thresh:.4f} units)")
    print(f"  (secondary signal aspect>{ASPECT_THRESH:.0f} among KEPT faces: "
          f"{n_aspect_only} elongated-but-legit faces, intentionally retained)")
    print(f"Flagged sliver triangles: {len(flagged)}  "
          f"({100.0 * len(flagged) / n_tri:.3f}% of {n_tri})")
    if flagged:
        fe = [max_edges[t] for t in flagged]
        print(f"  flagged maxEdge range: {min(fe):.4f} .. {max(fe):.4f}")
        kept_max = max((max_edges[t] for t in range(n_tri) if t not in flagged_set),
                       default=0.0)
        print(f"  largest KEPT maxEdge:  {kept_max:.4f}  "
              f"(all kept faces below the {edge_thresh:.4f} cut)")

    if not flagged:
        print("Nothing flagged; leaving glb untouched.")
        return 0

    # sanity guard: refuse to run if we'd remove an implausible number of faces
    if len(flagged) > 0.05 * n_tri:
        print(f"REFUSING: flagged {len(flagged)} (> 5% of tris). "
              f"Threshold likely mistuned; aborting without writing.")
        return 1

    # --- rebuild index buffer with flagged triangles dropped ---
    kept_triangles = []
    for t in range(n_tri):
        if t in flagged_set:
            continue
        kept_triangles.extend(triangles[t * 3:t * 3 + 3])
    new_idx_count = len(kept_triangles)
    new_tri_count = new_idx_count // 3

    # encode indices back with the SAME component type as the source
    if idx_comp == COMP_U32:
        new_index_bytes = struct.pack("<%dI" % new_idx_count, *kept_triangles)
    elif idx_comp == COMP_U16:
        new_index_bytes = struct.pack("<%dH" % new_idx_count, *kept_triangles)
    else:
        print(f"REFUSING: unsupported index componentType {idx_comp}")
        return 1

    # patch the index accessor count (POSITION/NORMAL/UV/JOINTS/WEIGHTS/IBM accessors
    # and all node/skin data are left exactly as-is)
    root["accessors"][idx_a]["count"] = new_idx_count

    # repack BIN + fix all bufferView offsets/byteLengths + buffer byteLength
    new_glb = repack_glb(root, bin_payload, idx_bv_index, new_index_bytes)

    print("\nAFTER (in-memory)")
    print(f"  indices {idx_count} -> {new_idx_count}   "
          f"tris {n_tri} -> {new_tri_count}   removed {n_tri - new_tri_count}")

    # --- verify the cleaned bytes BEFORE touching disk ---
    chk = verify_glb_bytes(new_glb)
    print("\nVERIFY (decoded from cleaned bytes):")
    print(json.dumps(chk, indent=2))

    failures = []
    if chk["vertexCount"] != v_count:
        failures.append(f"vertexCount changed {v_count} -> {chk['vertexCount']}")
    if chk["triCount"] != new_tri_count:
        failures.append(f"triCount mismatch {chk['triCount']} != {new_tri_count}")
    if not chk["indexInRange"]:
        failures.append("some index out of range [0, %d]" % (v_count - 1))
    if chk["uniqueJointValues"] != [0]:
        failures.append(f"JOINTS_0 unique != [0]: {chk['uniqueJointValues']}")
    if not chk["rigidWeights"]:
        failures.append("WEIGHTS_0 no longer (1,0,0,0) for all verts")
    if chk["fracTrisOver10pctDiag"] > 0.001:
        failures.append(
            f"fracTrisOver10pctDiag still high: {chk['fracTrisOver10pctDiag']:.5f}")

    if failures:
        print("\nSELF-CHECK FAILED on cleaned bytes; NOT writing:")
        for f in failures:
            print("  - " + f)
        return 1

    # --- back up original (copy, not move), then overwrite ---
    shutil.copy2(GLB_PATH, BACKUP_PATH)
    print(f"\nbacked up original -> {BACKUP_PATH}")

    with open(GLB_PATH, "wb") as fh:
        fh.write(new_glb)
    print(f"wrote cleaned glb -> {GLB_PATH}  ({len(new_glb)} bytes)")

    # --- re-read from disk and verify again; restore backup if broken ---
    with open(GLB_PATH, "rb") as fh:
        disk_raw = fh.read()
    disk_chk = verify_glb_bytes(disk_raw)
    disk_fail = []
    if disk_chk["vertexCount"] != 17007:
        disk_fail.append(f"vertexCount={disk_chk['vertexCount']} (expected 17007)")
    if disk_chk["triCount"] != new_tri_count:
        disk_fail.append(f"triCount={disk_chk['triCount']} (expected {new_tri_count})")
    if not disk_chk["indexInRange"]:
        disk_fail.append("index out of range")
    if disk_chk["uniqueJointValues"] != [0]:
        disk_fail.append(f"JOINTS unique={disk_chk['uniqueJointValues']}")
    if not disk_chk["rigidWeights"]:
        disk_fail.append("WEIGHTS not rigid")
    if disk_chk["fracTrisOver10pctDiag"] > 0.001:
        disk_fail.append(
            f"fracTrisOver10pctDiag={disk_chk['fracTrisOver10pctDiag']:.5f}")

    if disk_fail:
        print("\nON-DISK VERIFY FAILED; restoring backup:")
        for f in disk_fail:
            print("  - " + f)
        shutil.copy2(BACKUP_PATH, GLB_PATH)
        print("restored original from backup.")
        return 1

    print("\nON-DISK VERIFY OK:")
    print(json.dumps(disk_chk, indent=2))
    print("\nCLEAN-SLIVERS PASS")
    return 0


if __name__ == "__main__":
    sys.exit(main())
