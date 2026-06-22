import bpy, math
from mathutils import Vector
GLB="/Users/tbrack/Documents/Projects/FTK/ai-model-gen/mudwretch_triposr.glb"
OUTDIR="/Users/tbrack/Documents/Projects/FTK/ai-model-gen"
TARGET=4500  # game-ready face budget

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=GLB)
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
# join
bpy.context.view_layer.objects.active=meshes[0]
for o in meshes: o.select_set(True)
if len(meshes)>1: bpy.ops.object.join()
obj=bpy.context.view_layer.objects.active
before=len(obj.data.polygons)
# decimate collapse
m=obj.modifiers.new("dec","DECIMATE"); m.decimate_type='COLLAPSE'; m.ratio=min(1.0,TARGET/before)
bpy.ops.object.modifier_apply(modifier=m.name)
after=len(obj.data.polygons)
# faceted flat shading (FTK look)
bpy.ops.object.shade_flat()
print(f"DECIMATE {before} -> {after} faces")

# export FBX (Unity) + GLB
fbx=f"{OUTDIR}/mudwretch_lowpoly.fbx"; glb=f"{OUTDIR}/mudwretch_lowpoly.glb"
ok_fbx=True
try:
    bpy.ops.export_scene.fbx(filepath=fbx, use_selection=False, mesh_smooth_type='FACE',
        add_leaf_bones=False, apply_scale_options='FBX_SCALE_ALL', path_mode='COPY', embed_textures=True)
except Exception as e:
    ok_fbx=False; print("FBX export FAILED:", e)
try:
    bpy.ops.export_scene.gltf(filepath=glb, export_format='GLB')
except Exception as e:
    print("GLB export FAILED:", e)
import os
for f in (fbx,glb):
    if os.path.exists(f): print("saved", f, os.path.getsize(f),"bytes")

# render the lowpoly
mn=Vector((1e9,)*3); mx=Vector((-1e9,)*3)
for c in obj.bound_box:
    w=obj.matrix_world@Vector(c); mn=Vector(map(min,mn,w)); mx=Vector(map(max,mx,w))
center=(mn+mx)/2; rad=max(mx-mn)/2 or 1
tgt=bpy.data.objects.new("t",None); bpy.context.collection.objects.link(tgt); tgt.location=center
scn=bpy.context.scene; scn.render.engine='BLENDER_WORKBENCH'
scn.display.shading.light='STUDIO'; scn.display.shading.color_type='SINGLE'
scn.display.shading.single_color=(0.32,0.42,0.22)
scn.render.resolution_x=720; scn.render.resolution_y=720
cam_d=bpy.data.cameras.new("c"); cam=bpy.data.objects.new("c",cam_d); bpy.context.collection.objects.link(cam); scn.camera=cam
con=cam.constraints.new('TRACK_TO'); con.target=tgt; con.track_axis='TRACK_NEGATIVE_Z'; con.up_axis='UP_Y'
for name,(dx,dy,dz) in {"front":(0,-1,0.2),"tq":(-0.8,-0.8,0.3)}.items():
    d=Vector((dx,dy,dz)); d.normalize(); cam.location=center+d*rad*3.2
    scn.render.filepath=f"/tmp/mudwretch_lowpoly_{name}.png"; bpy.ops.render.render(write_still=True)
print("DONE")
