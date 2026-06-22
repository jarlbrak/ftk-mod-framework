"""Extract the vanilla enTroll01 SKINNED mesh + 37-bone skeleton + bindposes
into durable repo paths, for the editor-free runtime-glb rig pipeline.

Outputs (all in Unity local-mesh coordinates, the troll's own bind space):
  ai-model-gen/troll_skinned.npz   verts/normals/uv0/tris/bone_idx/bone_w/bind/bone_names
  ai-model-gen/troll_skel.json     37-bone local transforms (SMR m_Bones order, durable)

Bone order is taken from the SkinnedMeshRenderer m_Bones list, which is the same
order that m_BoneIndices reference and that m_BindPose is keyed by. Do not assume.
"""
import UnityPy, os, json
import numpy as np
from UnityPy.helpers.MeshHelper import MeshHandler

REPO = "/Users/tbrack/Documents/Projects/FTK"
DATA = "/Users/tbrack/Library/Application Support/Steam/steamapps/common/For The King/FTK.app/Contents/Resources/Data"
TROLL_MESH_PID = 3190

env = UnityPy.load(os.path.join(DATA, "resources.assets"))
byid = {o.path_id: o for o in env.objects}

mesh = byid[TROLL_MESH_PID].read()
assert mesh.m_Name == "enTroll01", f"path_id {TROLL_MESH_PID} is {mesh.m_Name}, not enTroll01"

# --- decode skinned vertex data ---
h = MeshHandler(mesh); h.process()
verts = np.array(h.m_Vertices, dtype=np.float32)
normals = np.array(h.m_Normals, dtype=np.float32) if h.m_Normals else None
uv0 = np.array(h.m_UV0, dtype=np.float32) if h.m_UV0 else None
bidx = np.array(h.m_BoneIndices, dtype=np.int32)
bw = np.array(h.m_BoneWeights, dtype=np.float32)
# Clean a decode artifact: ~18% of verts have a corrupted 4th weight (decodes as a
# large negative int) while the first 3 weights and all bone indices are valid.
# Clamp negatives to 0 and renormalize per vertex; the dominant influences (which
# bone each region follows) are preserved, which is all nearest-surface transfer needs.
bw = np.clip(bw, 0.0, None)
_s = bw.sum(1, keepdims=True); _s[_s == 0] = 1.0
bw = (bw / _s).astype(np.float32)
sub_tris = h.get_triangles()
tris = np.array([t for sm in sub_tris for t in sm], dtype=np.int32)

# --- bindposes (37 x 4x4), row-major e[r][c] ---
def mat(m):
    return np.array([[m.e00, m.e01, m.e02, m.e03],
                     [m.e10, m.e11, m.e12, m.e13],
                     [m.e20, m.e21, m.e22, m.e23],
                     [m.e30, m.e31, m.e32, m.e33]], dtype=np.float32)
bind = np.stack([mat(b) for b in mesh.m_BindPose], axis=0)

# --- SMR + bone names in m_Bones order ---
def pid(p): return getattr(p, 'm_PathID', getattr(p, 'path_id', None))
smr = None
for o in env.objects:
    if o.type.name == 'SkinnedMeshRenderer':
        d = o.read(); mp = getattr(d, 'm_Mesh', None)
        if mp is not None and pid(mp) == TROLL_MESH_PID:
            smr = d; break
assert smr is not None, "no SkinnedMeshRenderer for enTroll01"

def tr_of(p):
    t = byid.get(pid(p)); return t.read() if t else None
def goname(trp):
    tr = tr_of(trp)
    if tr is None: return "?"
    gp = getattr(tr, 'm_GameObject', None); go = byid.get(pid(gp)) if gp else None
    go = go.read() if go else None
    return getattr(go, 'm_Name', '?') if go else "?"

bone_names = [goname(b) for b in smr.m_Bones]
assert len(bone_names) == 37, f"expected 37 bones, got {len(bone_names)}"

# --- durable skeleton json (local transforms, SMR m_Bones order) ---
def v3(v): return [getattr(v,'x',0.0), getattr(v,'y',0.0), getattr(v,'z',0.0)]
def v4(v): return [getattr(v,'x',0.0), getattr(v,'y',0.0), getattr(v,'z',0.0), getattr(v,'w',1.0)]
skel = []
for b in smr.m_Bones:
    tr = tr_of(b)
    fp = getattr(tr, 'm_Father', None)
    skel.append({
        "pid": pid(b),
        "name": goname(b),
        "lp": v3(getattr(tr, 'm_LocalPosition', None)),
        "lr": v4(getattr(tr, 'm_LocalRotation', None)),
        "ls": v3(getattr(tr, 'm_LocalScale', None)),
        "father": pid(fp) if fp else None,
    })
json.dump({"bones": skel}, open(os.path.join(REPO, "ai-model-gen/troll_skel.json"), "w"))

# --- save npz ---
out = os.path.join(REPO, "ai-model-gen/troll_skinned.npz")
np.savez_compressed(out,
    verts=verts, normals=(normals if normals is not None else np.zeros((0,3),np.float32)),
    uv0=(uv0 if uv0 is not None else np.zeros((0,2),np.float32)),
    tris=tris, bone_idx=bidx, bone_w=bw, bind=bind,
    bone_names=np.array(bone_names))

# --- compact summary + sanity ---
print("verts", verts.shape, "tris", tris.shape, "normals", None if normals is None else normals.shape,
      "uv0", None if uv0 is None else uv0.shape)
print("bone_idx", bidx.shape, "bone_w", bw.shape, "bind", bind.shape)
print("bbox min", verts.min(0).round(3).tolist(), "max", verts.max(0).round(3).tolist())
print("index15 name:", bone_names[15], "(expect Head_M)")
print("bone_names:", bone_names)
# weight sums sane
wsum = bw.sum(1); print("weight-sum min/max", round(float(wsum.min()),4), round(float(wsum.max()),4))
print("SAVED", out)
