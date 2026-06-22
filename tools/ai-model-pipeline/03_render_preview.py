import bpy, math, sys
from mathutils import Vector
GLB = "/Users/tbrack/Documents/Projects/FTK/ai-model-gen/mudwretch_triposr.glb"
OUT = "/tmp/mudwretch_render"

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=GLB)
meshes = [o for o in bpy.context.scene.objects if o.type=='MESH']
print("imported meshes:", [o.name for o in meshes])
tv = sum(len(o.data.vertices) for o in meshes); tf = sum(len(o.data.polygons) for o in meshes)
print(f"TOTAL verts={tv} faces={tf}")

# combined bounds
mn = Vector((1e9,1e9,1e9)); mx = Vector((-1e9,-1e9,-1e9))
for o in meshes:
    for c in o.bound_box:
        w = o.matrix_world @ Vector(c)
        mn = Vector(map(min, mn, w)); mx = Vector(map(max, mx, w))
center = (mn+mx)/2; size = (mx-mn); rad = max(size)/2 or 1.0
print("bounds size:", tuple(round(v,3) for v in size))

# empty target for camera track
tgt = bpy.data.objects.new("tgt", None); bpy.context.collection.objects.link(tgt); tgt.location = center

scn = bpy.context.scene
scn.render.engine = 'BLENDER_WORKBENCH'
try:
    scn.display.shading.light = 'STUDIO'
    scn.display.shading.color_type = 'VERTEX'
except Exception as e: print("shading set warn:", e)
scn.render.resolution_x = 720; scn.render.resolution_y = 720
scn.render.film_transparent = False
try: scn.view_settings.view_transform = 'Standard'
except: pass

cam_data = bpy.data.cameras.new("cam"); cam = bpy.data.objects.new("cam", cam_data)
bpy.context.collection.objects.link(cam); scn.camera = cam
con = cam.constraints.new('TRACK_TO'); con.target = tgt
con.track_axis='TRACK_NEGATIVE_Z'; con.up_axis='UP_Y'

dist = rad*3.2
# Blender glTF imports Y-up as Z-up; "front" is -Y in Blender world. Render 3 azimuths.
angles = {"front":(0,-1,0.25), "threequarter":(-0.8,-0.8,0.35), "side":(-1,0,0.25)}
for name,(dx,dy,dz) in angles.items():
    d = Vector((dx,dy,dz)); d.normalize()
    cam.location = center + d*dist
    scn.render.filepath = f"{OUT}_{name}.png"
    bpy.ops.render.render(write_still=True)
    print("rendered", name)
print("RENDER DONE")
