import bpy, json, math
from mathutils import Vector, Matrix, Quaternion
import numpy as np
skel=json.load(open("/tmp/troll_skel.json"))["bones"]
idx={b["pid"]:i for i,b in enumerate(skel)}
def qmat(q): x,y,z,w=q; return Quaternion((w,x,y,z)).to_matrix().to_4x4()
def lmat(b): return Matrix.Translation(Vector(b["lp"]))@qmat(b["lr"])@Matrix.Diagonal(Vector(b["ls"]+[1]))
world=[None]*len(skel)
def solve(i):
    if world[i] is not None: return world[i]
    f=skel[i]["father"]; pm=solve(idx[f]) if f in idx else Matrix.Identity(4)
    world[i]=pm@lmat(skel[i]); return world[i]
for i in range(len(skel)): solve(i)
def u2b(p): return Vector((p.x,-p.z,p.y))
pos=[u2b(world[i].to_translation()) for i in range(len(skel))]

bpy.ops.wm.read_factory_settings(use_empty=True)
# ---- armature (Z-up) ----
ad=bpy.data.armatures.new("TrollArm"); arm=bpy.data.objects.new("TrollArm",ad)
bpy.context.collection.objects.link(arm); bpy.context.view_layer.objects.active=arm
bpy.ops.object.mode_set(mode='EDIT')
eb={}
for i,b in enumerate(skel):
    e=ad.edit_bones.new(b["name"]); e.head=pos[i]; eb[i]=e
for i,b in enumerate(skel):
    ch=[j for j,bb in enumerate(skel) if bb["father"]==b["pid"]]
    if ch and (pos[ch[0]]-pos[i]).length>1e-4: eb[i].tail=pos[ch[0]]
    else:
        f=b["father"]; d=(pos[i]-pos[idx[f]]) if f in idx else Vector((0,0,1))
        eb[i].tail=pos[i]+(d.normalized()*0.06 if d.length>1e-5 else Vector((0,0,0.06)))
for i,b in enumerate(skel):
    if b["father"] in idx: eb[i].parent=eb[idx[b["father"]]]
bpy.ops.object.mode_set(mode='OBJECT')
amn=Vector((min(p.x for p in pos),min(p.y for p in pos),min(p.z for p in pos)))
amx=Vector((max(p.x for p in pos),max(p.y for p in pos),max(p.z for p in pos)))
print("SKEL ext(x,y,z):", tuple(round(v,2) for v in (amx-amn)))

# ---- mesh + auto-orient by extent rank (largest->X arms, mid->Z height, min->Y depth) ----
bpy.ops.import_scene.gltf(filepath="/Users/tbrack/Documents/Projects/FTK/tools/trellis-mac/mudwretch_tpose.glb")
ms=[o for o in bpy.context.scene.objects if o.type=='MESH']
bpy.context.view_layer.objects.active=ms[0]
for o in ms: o.select_set(True)
if len(ms)>1: bpy.ops.object.join()
mesh=bpy.context.view_layer.objects.active
ext=[mesh.dimensions.x,mesh.dimensions.y,mesh.dimensions.z]
print("MESH raw ext:", tuple(round(v,3) for v in ext))
order=sorted(range(3), key=lambda i:-ext[i])  # [largest, mid, smallest] local-axis indices
# target: largest->X(0), mid->Z(2), smallest->Y(1)
R=np.zeros((3,3)); R[0,order[0]]=1; R[2,order[1]]=1; R[1,order[2]]=1
if np.linalg.det(R)<0: R[0,:]*=-1  # keep proper rotation
Rm=Matrix([[R[i,j] for j in range(3)]+[0] for i in range(3)]+[[0,0,0,1]])
mesh.matrix_world=Rm@mesh.matrix_world
bpy.ops.object.transform_apply(rotation=True)
# center at origin, base at z=0
mn=Vector((1e9,)*3); mx=Vector((-1e9,)*3)
for c in mesh.bound_box:
    w=mesh.matrix_world@Vector(c); mn=Vector(map(min,mn,w)); mx=Vector(map(max,mx,w))
mesh.location-=Vector(((mn.x+mx.x)/2,(mn.y+mx.y)/2,mn.z))
bpy.ops.object.transform_apply(location=True)
print("MESH oriented ext:", tuple(round(v,3) for v in mesh.dimensions))

# ---- fit skeleton to mesh (scale by height Z, center, base align) ----
sc=mesh.dimensions.z/(amx.z-amn.z)
arm.scale=(sc,sc,sc)
# after scale, place skeleton base (min z) at 0 and center xy at 0
arm.location=Vector((-(amn.x+amx.x)/2*sc, -(amn.y+amx.y)/2*sc, -amn.z*sc))
bpy.ops.object.transform_apply(location=True, scale=True)
print("fit scale:", round(sc,3))

# ---- auto weight ----
bpy.ops.object.select_all(action='DESELECT')
mesh.select_set(True); arm.select_set(True); bpy.context.view_layer.objects.active=arm
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
print("vgroups:", len(mesh.vertex_groups))

# ---- export ----
out="/Users/tbrack/Documents/Projects/FTK/ai-model-gen/mudwretch_tpose_rigged.fbx"
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, add_leaf_bones=False, mesh_smooth_type='FACE', apply_scale_options='FBX_SCALE_ALL', path_mode='COPY', embed_textures=True)
print("EXPORTED", out)

# ---- pose test ----
def render(tag):
    scn=bpy.context.scene; scn.render.engine='BLENDER_WORKBENCH'
    scn.display.shading.light='STUDIO'; scn.display.shading.color_type='TEXTURE'
    scn.render.resolution_x=600; scn.render.resolution_y=600
    rad=max(mesh.dimensions)/2 or 1
    if "cam" not in bpy.data.objects:
        cd=bpy.data.cameras.new("cam"); c=bpy.data.objects.new("cam",cd); bpy.context.collection.objects.link(c)
    cam=bpy.data.objects["cam"]; scn.camera=cam
    cam.location=Vector((0,-rad*3.2,rad*0.3)); cam.rotation_euler=(Vector((0,0,rad*0.0))-cam.location).to_track_quat('-Z','Z').to_euler()
    # aim at mesh center
    ctr=Vector((0,0,mesh.dimensions.z/2)); cam.rotation_euler=(ctr-cam.location).to_track_quat('-Z','Z').to_euler()
    scn.render.filepath=f"/tmp/rigT_{tag}.png"; bpy.ops.render.render(write_still=True)
render("rest")
bpy.context.view_layer.objects.active=arm; bpy.ops.object.mode_set(mode='POSE')
for bn,ax,ang in [("Shoulder_L",'X',-55),("Shoulder_R",'X',-55),("Elbow_L",'X',-45),("Elbow_R",'X',-45)]:
    pb=arm.pose.bones.get(bn)
    if pb: pb.rotation_mode='XYZ'; setattr(pb.rotation_euler,ax.lower(),math.radians(ang))
bpy.ops.object.mode_set(mode='OBJECT')
render("posed")
print("DONE")
