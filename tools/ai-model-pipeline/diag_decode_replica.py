#!/usr/bin/env python3
"""
diag_decode_replica.py

INVESTIGATION ONLY. Replicates the EXACT decode logic in
FTKModFramework/Core/RuntimeGltfMeshLoader.cs (byte-for-byte, NO glTF library
for the replica) to determine whether the in-game SHATTER is a C# geometry/weight
decode bug or a downstream Unity-runtime bug.

Mirrors:
  - SplitGlb (12-byte header, JSON chunk 0x4E4F534A, BIN chunk 0x004E4942)
  - JSON parse (we use the stdlib json here; it produces the same JObj/JArr tree)
  - AccessorView: byteOffset = bufferView.byteOffset + accessor.byteOffset
  - ReadVec3 (POSITION, 12 B/vtx LE f32)
  - ReadVec2 (UV)
  - ReadVec4U16 (JOINTS_0, 4 u16/vtx)
  - ReadVec4F32 (WEIGHTS_0, 4 f32/vtx)
  - ReadScalarIndices (u32 comp 5125 or u16 comp 5123)
  - BuildBoneWeights remap (slot->bone, drop w<=0, renormalize)

Then loads the SAME glb with trimesh as ground truth, compares, and renders both
to PNGs via PIL (matplotlib is not installed).
"""

import json
import math
import struct
import sys

GLB_PATH = "/Users/tbrack/Documents/Projects/FTK/ai-model-gen/mudwretch_rigged.glb"
RENDER_REPLICA = "/tmp/ftk_decode_replica.png"
RENDER_GT = "/tmp/ftk_decode_groundtruth.png"

# glTF componentType values (mirror the C# consts)
COMP_F32 = 5126
COMP_U32 = 5125
COMP_U16 = 5123

# GLB container magic numbers (LE uint32)
GLB_MAGIC = 0x46546C67  # "glTF"
CHUNK_JSON = 0x4E4F534A  # "JSON"
CHUNK_BIN = 0x004E4942   # "BIN\0"


# ----------------------------------------------------------------------------
# SplitGlb: mirror of RuntimeGltfMeshLoader.SplitGlb
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
    _total, p = read_u32(b, p)  # totalLength, not trusted
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
# GltfDoc: mirror of the C# GltfDoc reader (over the stdlib-parsed JSON tree).
# The stdlib json tree is equivalent to the hand-rolled JObj/JArr the C# builds.
# ----------------------------------------------------------------------------
class GltfDoc:
    def __init__(self, root, bin_payload):
        self.root = root
        self.bin = bin_payload
        self.accessors = root.get("accessors")
        self.bufferViews = root.get("bufferViews")
        self.nodes = root.get("nodes")
        self.skins = root.get("skins")
        self.meshes = root.get("meshes")

    def read_primitive(self):
        # returns (pos, norm, uv, joints, weights, indices, ok)
        pos = norm = uv = joints = weights = indices = -1
        if not self.meshes:
            return pos, norm, uv, joints, weights, indices, False
        mesh0 = self.meshes[0]
        prims = mesh0.get("primitives")
        if not prims:
            return pos, norm, uv, joints, weights, indices, False
        prim0 = prims[0]
        attrs = prim0.get("attributes")
        if attrs is None:
            return pos, norm, uv, joints, weights, indices, False
        pos = attrs.get("POSITION", -1)
        norm = attrs.get("NORMAL", -1)
        uv = attrs.get("TEXCOORD_0", -1)
        joints = attrs.get("JOINTS_0", -1)
        weights = attrs.get("WEIGHTS_0", -1)
        indices = prim0.get("indices", -1)
        ok = pos >= 0 and joints >= 0 and weights >= 0 and indices >= 0
        return pos, norm, uv, joints, weights, indices, ok

    def read_skin_joint_names(self):
        if not self.skins or self.nodes is None:
            return None
        skin0 = self.skins[0]
        joints = skin0.get("joints")
        if not joints:
            return None
        names = []
        for nodeIdx in joints:
            if 0 <= nodeIdx < len(self.nodes):
                names.append(self.nodes[nodeIdx].get("name"))
            else:
                names.append(None)
        return names

    def accessor_view(self, acc_index):
        # returns (byteOffset, count, componentType) or None
        if self.accessors is None or acc_index < 0 or acc_index >= len(self.accessors):
            return None
        acc = self.accessors[acc_index]
        count = acc.get("count", 0)
        comp = acc.get("componentType", 0)
        bv_index = acc.get("bufferView", -1)
        acc_byte_offset = acc.get("byteOffset", 0)
        if self.bufferViews is None or bv_index < 0 or bv_index >= len(self.bufferViews):
            return None
        bv = self.bufferViews[bv_index]
        bv_offset = bv.get("byteOffset", 0)
        byte_offset = bv_offset + acc_byte_offset
        if count > 0 and byte_offset >= 0:
            return byte_offset, count, comp
        return None

    def read_f32(self, p):
        return struct.unpack_from("<f", self.bin, p)[0]

    def read_vec3(self, acc_index):
        av = self.accessor_view(acc_index)
        if av is None:
            return None
        off, count, comp = av
        if comp != COMP_F32:
            return None
        if off + count * 12 > len(self.bin):
            return None
        r = []
        p = off
        for _ in range(count):
            r.append((self.read_f32(p), self.read_f32(p + 4), self.read_f32(p + 8)))
            p += 12
        return r

    def read_vec2(self, acc_index):
        av = self.accessor_view(acc_index)
        if av is None:
            return None
        off, count, comp = av
        if comp != COMP_F32:
            return None
        if off + count * 8 > len(self.bin):
            return None
        r = []
        p = off
        for _ in range(count):
            r.append((self.read_f32(p), self.read_f32(p + 4)))
            p += 8
        return r

    def read_vec4_u16(self, acc_index):
        av = self.accessor_view(acc_index)
        if av is None:
            return None
        off, count, comp = av
        if comp != COMP_U16:
            return None
        if off + count * 8 > len(self.bin):
            return None
        r = [0] * (count * 4)
        p = off
        for i in range(count * 4):
            r[i] = self.bin[p] | (self.bin[p + 1] << 8)
            p += 2
        return r

    def read_vec4_f32(self, acc_index):
        av = self.accessor_view(acc_index)
        if av is None:
            return None
        off, count, comp = av
        if comp != COMP_F32:
            return None
        if off + count * 16 > len(self.bin):
            return None
        r = [0.0] * (count * 4)
        p = off
        for i in range(count * 4):
            r[i] = self.read_f32(p)
            p += 4
        return r

    def read_scalar_indices(self, acc_index):
        av = self.accessor_view(acc_index)
        if av is None:
            return None
        off, count, comp = av
        r = [0] * count
        p = off
        if comp == COMP_U32:
            if off + count * 4 > len(self.bin):
                return None
            for i in range(count):
                r[i] = (self.bin[p] | (self.bin[p + 1] << 8) |
                        (self.bin[p + 2] << 16) | (self.bin[p + 3] << 24)) & 0xFFFFFFFF
                p += 4
            return r
        if comp == COMP_U16:
            if off + count * 2 > len(self.bin):
                return None
            for i in range(count):
                r[i] = self.bin[p] | (self.bin[p + 1] << 8)
                p += 2
            return r
        return None


# ----------------------------------------------------------------------------
# BuildBoneWeights: mirror of RuntimeGltfMeshLoader.BuildBoneWeights.
# slotToRuntime here is identity (slot s -> bone s) because we are testing the
# DECODE, not the live-rig name remap; the question for weightsTrulyRigid is
# whether the decoded JOINTS_0/WEIGHTS_0 are pure rigid (slot 0, weight 1).
# ----------------------------------------------------------------------------
def build_bone_weights(v_count, joints, weights, slot_to_runtime):
    result = []
    slot_count = len(slot_to_runtime)
    for v in range(v_count):
        base_i = v * 4
        kept_bone = []
        kept_weight = []
        s = 0.0
        for k in range(4):
            slot = joints[base_i + k]
            w = weights[base_i + k]
            if w <= 0.0:
                continue
            if slot < 0 or slot >= slot_count:
                continue
            bone = slot_to_runtime[slot]
            if bone < 0:
                continue
            kept_bone.append(bone)
            kept_weight.append(w)
            s += w
        if len(kept_bone) == 0 or s <= 0.0:
            result.append([(0, 1.0)])
            continue
        inv = 1.0 / s
        kept_weight = [w * inv for w in kept_weight]
        infl = sorted(zip(kept_bone, kept_weight), key=lambda t: -t[1])
        result.append(infl)
    return result


# ----------------------------------------------------------------------------
# PIL ortho render (XY plane, Y up). Filled triangles, white background.
# ----------------------------------------------------------------------------
def render_ortho(positions, triangles, out_path, w=800, h=1000, fill=(70, 110, 70), title=""):
    from PIL import Image, ImageDraw

    xs = [p[0] for p in positions]
    ys = [p[1] for p in positions]
    minx, maxx = min(xs), max(xs)
    miny, maxy = min(ys), max(ys)
    spanx = maxx - minx or 1e-6
    spany = maxy - miny or 1e-6
    pad = 40
    sx = (w - 2 * pad) / spanx
    sy = (h - 2 * pad) / spany
    sc = min(sx, sy)
    # center
    ox = (w - spanx * sc) / 2.0
    oy = (h - spany * sc) / 2.0

    def proj(p):
        px = ox + (p[0] - minx) * sc
        # Y up: flip image Y
        py = h - (oy + (p[1] - miny) * sc)
        return (px, py)

    img = Image.new("RGB", (w, h), (255, 255, 255))
    draw = ImageDraw.Draw(img)

    n_tri = len(triangles) // 3
    for t in range(n_tri):
        i0 = triangles[t * 3]
        i1 = triangles[t * 3 + 1]
        i2 = triangles[t * 3 + 2]
        if i0 >= len(positions) or i1 >= len(positions) or i2 >= len(positions):
            continue
        a = proj(positions[i0])
        b = proj(positions[i1])
        c = proj(positions[i2])
        draw.polygon([a, b, c], fill=fill, outline=(30, 50, 30))

    if title:
        draw.text((10, 10), title, fill=(0, 0, 0))
    img.save(out_path)


def render_ortho_trimesh(mesh, out_path, w=800, h=1000, fill=(110, 80, 70), title=""):
    verts = [(float(v[0]), float(v[1]), float(v[2])) for v in mesh.vertices]
    tris = []
    for f in mesh.faces:
        tris.extend([int(f[0]), int(f[1]), int(f[2])])
    render_ortho(verts, tris, out_path, w, h, fill, title)


# ----------------------------------------------------------------------------
# Main
# ----------------------------------------------------------------------------
def main():
    with open(GLB_PATH, "rb") as fh:
        raw = fh.read()

    json_text, bin_payload = split_glb(raw)
    root = json.loads(json_text)
    doc = GltfDoc(root, bin_payload)

    pos_a, norm_a, uv_a, joints_a, weights_a, idx_a, ok = doc.read_primitive()
    report = {}
    report["primitive_ok"] = ok
    report["accessors"] = dict(pos=pos_a, norm=norm_a, uv=uv_a, joints=joints_a,
                               weights=weights_a, indices=idx_a)
    if not ok:
        print("PRIMITIVE READ FAILED", report)
        return

    positions = doc.read_vec3(pos_a)
    joints = doc.read_vec4_u16(joints_a)
    weights = doc.read_vec4_f32(weights_a)
    triangles = doc.read_scalar_indices(idx_a)
    joint_names = doc.read_skin_joint_names()

    v_count = len(positions)

    # 1) POSITION bbox / diagonal
    xs = [p[0] for p in positions]
    ys = [p[1] for p in positions]
    zs = [p[2] for p in positions]
    bbox_min = (min(xs), min(ys), min(zs))
    bbox_max = (max(xs), max(ys), max(zs))
    diag = math.sqrt(sum((bbox_max[d] - bbox_min[d]) ** 2 for d in range(3)))

    # 2) INDICES
    idx_count = len(triangles)
    idx_min = min(triangles)
    idx_max = max(triangles)
    oor = sum(1 for v in triangles if v < 0 or v >= v_count)

    # 3) TRIANGLE COHERENCE
    n_tri = idx_count // 3
    max_edge = 0.0
    sum_edge = 0.0
    edge_count = 0
    over_thresh = 0
    thresh = 0.10 * diag
    for t in range(n_tri):
        a = positions[triangles[t * 3]]
        b = positions[triangles[t * 3 + 1]]
        c = positions[triangles[t * 3 + 2]]
        e_ab = math.dist(a, b)
        e_bc = math.dist(b, c)
        e_ca = math.dist(c, a)
        longest = max(e_ab, e_bc, e_ca)
        for e in (e_ab, e_bc, e_ca):
            sum_edge += e
            edge_count += 1
            if e > max_edge:
                max_edge = e
        if longest > thresh:
            over_thresh += 1
    mean_edge = sum_edge / edge_count if edge_count else 0.0
    frac_over = over_thresh / n_tri if n_tri else 0.0
    geometry_coherent = (frac_over < 0.02) and (max_edge < 0.5 * diag)

    # 4) WEIGHTS as decoded by C# logic.
    # Identity slot->bone map (we test the DECODE; rigidity question is whether
    # decoded joints are all slot 0 and weights (1,0,0,0)).
    n_slots = len(joint_names) if joint_names else (max(joints) + 1)
    slot_to_runtime = list(range(n_slots))
    bw = build_bone_weights(v_count, joints, weights, slot_to_runtime)

    unique_joint_vals = sorted(set(joints))
    # distinct WEIGHTS_0 tuples (rounded so float noise collapses)
    distinct_w = {}
    for v in range(v_count):
        bi = v * 4
        tup = (round(weights[bi], 4), round(weights[bi + 1], 4),
               round(weights[bi + 2], 4), round(weights[bi + 3], 4))
        distinct_w[tup] = distinct_w.get(tup, 0) + 1
    distinct_w_sorted = sorted(distinct_w.items(), key=lambda kv: -kv[1])

    # weightsTrulyRigid: every vertex resolves to exactly one bone w~1, joints
    # all slot 0, weights (1,0,0,0).
    truly_rigid = True
    for v in range(v_count):
        bi = v * 4
        j = joints[bi:bi + 4]
        w = weights[bi:bi + 4]
        # joint slot 0 and weights (1,0,0,0): slot 0 carries full weight, others 0
        if abs(w[0] - 1.0) > 1e-4:
            truly_rigid = False
            break
        if abs(w[1]) > 1e-4 or abs(w[2]) > 1e-4 or abs(w[3]) > 1e-4:
            truly_rigid = False
            break
        if j[0] != 0:
            truly_rigid = False
            break
    # also confirm the remapped BuildBoneWeights result: one bone, weight 1
    bw_rigid = all(len(infl) == 1 and infl[0][0] == 0 and abs(infl[0][1] - 1.0) < 1e-4
                   for infl in bw)

    # sample tuples string
    sample_tuples = "; ".join(
        "j0={} w=({:.3f},{:.3f},{:.3f},{:.3f}) x{}".format(
            joints[0], t[0], t[1], t[2], t[3], cnt)
        for (t, cnt) in distinct_w_sorted[:6]
    )

    # 5) GROUND TRUTH via trimesh
    import trimesh
    loaded = trimesh.load(GLB_PATH, force="mesh", process=False)
    if isinstance(loaded, trimesh.Scene):
        loaded = loaded.dump(concatenate=True)
    gt_vcount = len(loaded.vertices)
    gt_tris = len(loaded.faces)
    gt_max_edge = float(loaded.edges_unique_length.max())
    gt_xs = loaded.vertices[:, 0]
    gt_ys = loaded.vertices[:, 1]
    gt_zs = loaded.vertices[:, 2]
    gt_min = (float(gt_xs.min()), float(gt_ys.min()), float(gt_zs.min()))
    gt_max = (float(gt_xs.max()), float(gt_ys.max()), float(gt_zs.max()))
    gt_diag = math.sqrt(sum((gt_max[d] - gt_min[d]) ** 2 for d in range(3)))
    gt_geometry_coherent = gt_max_edge < 0.5 * gt_diag

    # position match: compare replica vs trimesh vertex arrays
    pos_match = "none"
    if gt_vcount == v_count:
        maxd = 0.0
        # trimesh may reorder verts under process=True; we used process=False
        for i in range(v_count):
            d = abs(positions[i][0] - float(loaded.vertices[i][0])) + \
                abs(positions[i][1] - float(loaded.vertices[i][1])) + \
                abs(positions[i][2] - float(loaded.vertices[i][2]))
            if d > maxd:
                maxd = d
        pos_match = "max abs vertex coord delta = {:.6g}".format(maxd)
    else:
        pos_match = "vertexCount differs replica={} trimesh={}".format(v_count, gt_vcount)

    # 6) RENDER
    render_ortho(positions, triangles, RENDER_REPLICA,
                 title="C# replica decode  v={} tris={}".format(v_count, n_tri))
    render_ortho_trimesh(loaded, RENDER_GT,
                         title="trimesh ground truth  v={} tris={}".format(gt_vcount, gt_tris))

    # -------- emit JSON-ish report --------
    out = {
        "vertexCount": v_count,
        "bboxMin": list(bbox_min),
        "bboxMax": list(bbox_max),
        "bboxDiagonal": diag,
        "indexCount": idx_count,
        "indexMin": idx_min,
        "indexMax": idx_max,
        "indexOutOfRangeCount": oor,
        "triCount": n_tri,
        "maxEdgeLen": max_edge,
        "meanEdgeLen": mean_edge,
        "fracTrisOver10pctDiag": frac_over,
        "thresh10pct": thresh,
        "geometryCoherent": geometry_coherent,
        "jointSlotCount": n_slots,
        "jointNamesSample": (joint_names[:5] if joint_names else None),
        "uniqueJointValues": unique_joint_vals,
        "distinctWeightTuples_top": [list(t) + [c] for (t, c) in distinct_w_sorted[:8]],
        "distinctWeightTupleCount": len(distinct_w),
        "weightsTrulyRigid_rawDecode": truly_rigid,
        "weightsTrulyRigid_afterRemap": bw_rigid,
        "weightsSample": sample_tuples,
        "GT_vertexCount": gt_vcount,
        "GT_triCount": gt_tris,
        "GT_maxEdgeLen": gt_max_edge,
        "GT_bboxDiagonal": gt_diag,
        "GT_geometryCoherent": gt_geometry_coherent,
        "GT_vs_replica": pos_match,
        "renderReplica": RENDER_REPLICA,
        "renderGroundTruth": RENDER_GT,
    }
    print(json.dumps(out, indent=2))


if __name__ == "__main__":
    main()
