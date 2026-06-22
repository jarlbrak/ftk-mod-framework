"""Editor-free rig: align the decimated AI mesh into the troll's Unity mesh-local
space, transfer skin weights from the vanilla troll by nearest surface, and write a
skinned GLB (Unity coordinates) keyed by the 37 troll bone NAMES.

No Blender coordinate round-trips: everything here is plain numpy in Unity mesh
space, so vertices and the reused troll bindposes share one frame by construction.

Run:
  .venv-3dgen/bin/python tools/ai-model-pipeline/05b_rig_numpy.py [--flip-front] [--target-head Y] [--knn K]
Outputs:
  ai-model-gen/mudwretch_rigged.glb     (ship this)
  ai-model-gen/mudwretch_rigged.npz     (intermediate for the verify step)
"""
import sys, json, struct, numpy as np, trimesh
from scipy.spatial import cKDTree

REPO = "/Users/tbrack/Documents/Projects/FTK"
AI_GLB = f"{REPO}/ai-model-gen/mudwretch_tpose_dec.glb"
TROLL_NPZ = f"{REPO}/ai-model-gen/troll_skinned.npz"
OUT_GLB = f"{REPO}/ai-model-gen/mudwretch_rigged.glb"
OUT_NPZ = f"{REPO}/ai-model-gen/mudwretch_rigged.npz"

flip_front = "--flip-front" in sys.argv
def argf(flag, default):
    return float(sys.argv[sys.argv.index(flag)+1]) if flag in sys.argv else default
target_head = argf("--target-head", 2.65)   # AI top maps to this troll Y (head bone ~2.65)
KNN = int(argf("--knn", 5))

# ---- load troll (Unity mesh-local coords) ----
t = np.load(TROLL_NPZ, allow_pickle=True)
Vt = t['verts'].astype(np.float64)
t_bidx = t['bone_idx']; t_bw = t['bone_w']; bind = t['bind']; names = list(t['bone_names'])
tmin, tmax = Vt.min(0), Vt.max(0)
tcx, tcz = 0.0, (tmin[2]+tmax[2])/2.0       # troll is symmetric in x; match z center

# ---- load AI mesh (raw glTF accessor data) ----
m = trimesh.load(AI_GLB, force='mesh', process=False)
Va = np.asarray(m.vertices, dtype=np.float64)
Fa = np.asarray(m.faces, dtype=np.int64)
try:
    UVa = np.asarray(m.visual.uv, dtype=np.float32)
    if UVa.shape[0] != Va.shape[0]: UVa = None
except Exception:
    UVa = None
if UVa is None:
    UVa = np.zeros((len(Va), 2), np.float32)

# ---- weld coincident verts (same pos+uv) so smooth normals are possible ----
# Blender's glTF export fully splits verts for flat shading (~3 per face); welding by
# position+uv re-shares them (keeps UV seams), enabling smooth shading and ~6x fewer verts.
_key = np.round(np.concatenate([Va, UVa], axis=1), 5)
_uniq, _inv = np.unique(_key, axis=0, return_inverse=True)
_inv = _inv.reshape(-1)
Vw = np.zeros((len(_uniq), 3)); UVw = np.zeros((len(_uniq), 2)); _cnt = np.zeros(len(_uniq))
np.add.at(Vw, _inv, Va); np.add.at(UVw, _inv, UVa); np.add.at(_cnt, _inv, 1)
Va = Vw / _cnt[:, None]; UVa = (UVw / _cnt[:, None]).astype(np.float32); Fa = _inv[Fa]
print(f"welded verts: -> {len(Va)} (from split mesh)")

# ---- glTF -> Unity: rotate -90 about X  (x, y, z) -> (x, z, -y); head up ----
# (TRELLIS source: feet at min glTF_Z, head at max glTF_Z, arms along glTF_X; verified
#  via bottom-slice leg-bimodality detector. det +1, so winding/normals stay correct.)
def rxm90(P):  # (...,3)
    out = np.empty_like(P); out[...,0]=P[...,0]; out[...,1]=P[...,2]; out[...,2]=-P[...,1]; return out
Va = rxm90(Va)
if flip_front:  # 180 about Y: (x,y,z)->(-x,y,-z); swaps front/back and L/R, keeps det +1
    Va[:,0] *= -1; Va[:,2] *= -1

# ---- uniform scale by height; feet to troll feet; center x/z ----
amin, amax = Va.min(0), Va.max(0)
ai_h = amax[1]-amin[1]
scale = target_head / ai_h
Va *= scale
amin, amax = Va.min(0), Va.max(0)
acx, acz = (amin[0]+amax[0])/2.0, (amin[2]+amax[2])/2.0
Va[:,0] += tcx - acx
Va[:,2] += tcz - acz
Va[:,1] += tmin[1] - amin[1]            # feet on troll feet

# ---- smooth vertex normals in Unity space ----
fn = np.cross(Va[Fa[:,1]]-Va[Fa[:,0]], Va[Fa[:,2]]-Va[Fa[:,0]])
N = np.zeros_like(Va)
for k in range(3): np.add.at(N, Fa[:,k], fn)
ln = np.linalg.norm(N,axis=1,keepdims=True); ln[ln==0]=1; N/=ln

# ---- nearest-surface weight transfer (k-NN smoothed) ----
tree = cKDTree(Vt)
dist, nn = tree.query(Va, k=KNN)                       # (M,K)
if KNN == 1: nn = nn[:,None]; dist = dist[:,None]
w = 1.0/(dist+1e-6); w /= w.sum(1, keepdims=True)      # inverse-distance blend
# accumulate per-bone weight from the K neighbours, then keep top-4 bones
M = len(Va)
acc = {}
for j in range(nn.shape[1]):
    bi = t_bidx[nn[:,j]]; bwt = t_bw[nn[:,j]] * w[:,j:j+1]
    for c in range(4):
        for vi in range(M):
            b = int(bi[vi,c]); val = float(bwt[vi,c])
            if val <= 0: continue
            acc.setdefault(vi, {}); acc[vi][b] = acc[vi].get(b,0.0)+val
JOINTS = np.zeros((M,4), np.uint16); WEIGHTS = np.zeros((M,4), np.float32)
for vi in range(M):
    items = sorted(acc.get(vi,{0:1.0}).items(), key=lambda kv:-kv[1])[:4]
    s = sum(v for _,v in items) or 1.0
    for c,(b,v) in enumerate(items):
        JOINTS[vi,c]=b; WEIGHTS[vi,c]=v/s
# --posearms <deg>: rotate the T-pose arms DOWN around each shoulder so the golem stands
# with arms at its sides (natural hulking pose) instead of a stiff scarecrow T. Bakes into the
# mesh, so combine with --rigid (the frozen posed shape needs no skeleton articulation).
if "--posearms" in sys.argv:
    pdeg = argf("--posearms", 72.0)
    dom = JOINTS[np.arange(M), WEIGHTS.argmax(1)]
    posb = np.linalg.inv(bind)[:, :3, 3]
    def rotz(d):
        a = np.radians(d); c, s = np.cos(a), np.sin(a)
        return np.array([[c, -s, 0.0], [s, c, 0.0], [0.0, 0.0, 1.0]])
    for side, ang in [("_L", pdeg), ("_R", -pdeg)]:
        armb = [names.index(b + side) for b in ["Shoulder", "Elbow", "Wrist",
                "MiddleFinger1", "MiddleFinger2", "MiddleFinger3",
                "ThumbFinger1", "ThumbFinger2", "ThumbFinger3"]]
        piv = posb[names.index("Shoulder" + side)]
        mask = np.isin(dom, armb)
        Va[mask] = piv + (Va[mask] - piv) @ rotz(ang).T
    # recompute smooth normals after posing
    fn = np.cross(Va[Fa[:,1]]-Va[Fa[:,0]], Va[Fa[:,2]]-Va[Fa[:,0]])
    N = np.zeros_like(Va)
    for k in range(3): np.add.at(N, Fa[:,k], fn)
    ln = np.linalg.norm(N,axis=1,keepdims=True); ln[ln==0]=1; N/=ln
    print(f"posed arms down by {pdeg} deg ({int(mask.sum())} right-arm verts last side)")

# --rigid <bone>: weight ALL verts 100% to one bone (rigid prop, cannot shatter from skinning)
if "--rigid" in sys.argv:
    rb = sys.argv[sys.argv.index("--rigid")+1]
    ri = names.index(rb)
    JOINTS[:] = 0; JOINTS[:,0] = ri
    WEIGHTS[:] = 0; WEIGHTS[:,0] = 1.0
    print(f"RIGID mode: all verts -> bone '{rb}' (idx {ri})")

# ---- write skinned GLB (Unity coords, joints named by troll bones) ----
def pad4(b): return b + b'\x00'*((4-len(b)%4)%4)
buf = b''; views=[]; accs=[]
def add(arr, comp, atype, tgt=None, mn=None, mx=None):
    global buf
    off=len(buf); data=arr.tobytes(); buf=pad4(buf+data)
    views.append({"buffer":0,"byteOffset":off,"byteLength":len(data),**({"target":tgt} if tgt else {})})
    a={"bufferView":len(views)-1,"componentType":comp,"count":int(arr.shape[0]),"type":atype}
    if mn is not None: a["min"]=mn; a["max"]=mx
    accs.append(a); return len(accs)-1
POS = add(Va.astype(np.float32), 5126, "VEC3", 34962, Va.min(0).astype(np.float32).tolist(), Va.max(0).astype(np.float32).tolist())
NOR = add(N.astype(np.float32), 5126, "VEC3", 34962)
TEX = add(UVa.astype(np.float32), 5126, "VEC2", 34962)
JNT = add(JOINTS, 5123, "VEC4", 34962)
WGT = add(WEIGHTS, 5126, "VEC4", 34962)
IDX = add(Fa.astype(np.uint32).reshape(-1), 5125, "SCALAR", 34963)
IBM = add(np.transpose(bind,(0,2,1)).reshape(len(bind),16).astype(np.float32), 5126, "MAT4")  # column-major
# father map for joint hierarchy
skel = json.load(open(f"{REPO}/ai-model-gen/troll_skel.json"))["bones"]
pid2i = {b["pid"]:i for i,b in enumerate(skel)}
children = {i:[] for i in range(len(skel))}
roots=[]
for i,b in enumerate(skel):
    f=b["father"]
    if f in pid2i: children[pid2i[f]].append(i)
    else: roots.append(i)
nodes=[]
for i,b in enumerate(skel):
    n={"name":names[i]}
    if children[i]: n["children"]=children[i]
    nodes.append(n)
MESHNODE=len(nodes)
nodes.append({"name":"mudwretch","mesh":0,"skin":0})
gltf={
 "asset":{"version":"2.0","generator":"ftk-rig-numpy"},
 "scene":0,"scenes":[{"nodes":roots+[MESHNODE]}],
 "nodes":nodes,
 "meshes":[{"name":"mudwretch","primitives":[{"attributes":{
     "POSITION":POS,"NORMAL":NOR,"TEXCOORD_0":TEX,"JOINTS_0":JNT,"WEIGHTS_0":WGT},
     "indices":IDX,"mode":4}]}],
 "skins":[{"joints":list(range(len(skel))),"inverseBindMatrices":IBM,"skeleton":roots[0]}],
 "buffers":[{"byteLength":len(buf)}],
 "bufferViews":views,"accessors":accs,
}
_jraw = json.dumps(gltf,separators=(',',':')).encode()
jchunk = _jraw + b' '*((4-len(_jraw)%4)%4)   # JSON chunk pads with SPACES, not nulls
bchunk = pad4(buf)                           # BIN chunk pads with nulls
glb = struct.pack("<III",0x46546C67,2,12+8+len(jchunk)+8+len(bchunk))
glb += struct.pack("<II",len(jchunk),0x4E4F534A)+jchunk
glb += struct.pack("<II",len(bchunk),0x004E4942)+bchunk
open(OUT_GLB,"wb").write(glb)
np.savez_compressed(OUT_NPZ, verts=Va.astype(np.float32), normals=N.astype(np.float32),
    uv=UVa, faces=Fa.astype(np.int32), joints=JOINTS, weights=WEIGHTS,
    bind=bind, bone_names=np.array(names))

print(f"AI verts {M} faces {len(Fa)}  scale {scale:.3f}  flip_front={flip_front}")
print("AI bbox  x", [round(Va[:,0].min(),2),round(Va[:,0].max(),2)],
      "y", [round(Va[:,1].min(),2),round(Va[:,1].max(),2)],
      "z", [round(Va[:,2].min(),2),round(Va[:,2].max(),2)])
print("troll bbox x", [round(tmin[0],2),round(tmax[0],2)],
      "y", [round(tmin[1],2),round(tmax[1],2)], "z",[round(tmin[2],2),round(tmax[2],2)])
# weight sanity: which bones dominate which regions
top = JOINTS[np.arange(M), WEIGHTS.argmax(1)]
import collections
head_region = top[Va[:,1] > 2.2]
foot_region = top[Va[:,1] < 0.6]
larm = top[Va[:,0] < -1.5]
print("dominant bone in head region (y>2.2):", collections.Counter(names[b] for b in head_region).most_common(3))
print("dominant bone in foot region (y<0.6):", collections.Counter(names[b] for b in foot_region).most_common(3))
print("dominant bone in left-arm region (x<-1.5):", collections.Counter(names[b] for b in larm).most_common(3))
print("GLB bytes", len(glb), "->", OUT_GLB)
