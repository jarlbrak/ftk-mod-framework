"""Original art study. Run with Blender --background --python build.py.

The exported GLB is a standard review asset, not the FTK runtime format.
"""
import bpy
import math
import json
from pathlib import Path
from mathutils import Vector

OUT = Path(__file__).resolve().parent
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

def material(name, color, emission=0):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = (*color, 1)
    p.inputs['Roughness'].default_value = .9
    if emission:
        p.inputs['Emission Color'].default_value = (*color, 1)
        p.inputs['Emission Strength'].default_value = emission
    return m

stone = material('01 | warm weathered limestone', (.30,.335,.27))
light = material('02 | exposed cut stone', (.43,.46,.36))
dark = material('03 | peat and deep joints', (.075,.091,.067))
moss = material('04 | moss shawl', (.16,.25,.068))
moss_light = material('05 | moss upper planes', (.25,.34,.10))
amber = material('06 | amber eyes', (.94,.54,.095), .5)
assets=[]

def block(name, loc, size, mat=stone, bevel=.10, rot=(0,0,0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    ob=bpy.context.object
    ob.name=name
    ob.dimensions=size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod=ob.modifiers.new('Broad carved edges','BEVEL')
        mod.width=bevel
        mod.segments=1
        bpy.ops.object.modifier_apply(modifier=mod.name)
    ob.rotation_euler=tuple(math.radians(a) for a in rot)
    ob.data.materials.append(mat)
    assets.append(ob)
    return ob

def slab(name, rings, mat=stone):
    # Each ring: z, half-width, half-depth, x offset, y offset.
    verts=[]
    for z,w,d,x,y in rings:
        for px,py in [(-.73,-1),(.73,-1),(1,-.65),(1,.65),(.73,1),(-.73,1),(-1,.65),(-1,-.65)]:
            verts.append((x+px*w,y+py*d,z))
    faces=[tuple(reversed(range(8)))]
    for j in range(len(rings)-1):
        for i in range(8): faces.append((j*8+i,j*8+(i+1)%8,(j+1)*8+(i+1)%8,(j+1)*8+i))
    faces.append(tuple(range((len(rings)-1)*8,len(rings)*8)))
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(verts,[],faces)
    mesh.materials.append(mat)
    ob=bpy.data.objects.new(name,mesh)
    bpy.context.collection.objects.link(ob)
    assets.append(ob)
    return ob

def carve(target_name, name, loc, size, rot=(0,0,0)):
    target=bpy.data.objects[target_name]
    cutter=block(name,loc,size,dark,0,rot)
    bpy.context.view_layer.objects.active=target
    modifier=target.modifiers.new(name,'BOOLEAN')
    modifier.operation='DIFFERENCE'
    modifier.object=cutter
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    assets.remove(cutter)
    bpy.data.objects.remove(cutter,do_unlink=True)

# A deliberate anatomical blockout, front faces -Y. Separate stone parts remain editable.
slab('Trunk | tapered foundation',[(1.40,.49,.34,0,.04),(1.72,.58,.40,0,.03),(2.52,.86,.44,0,.09),(2.73,.62,.34,0,.13)],dark)
slab('Breastplate | single old lintel',[(1.96,.48,.16,0,-.32),(2.17,.72,.20,0,-.32),(2.54,.83,.19,0,-.29),(2.66,.66,.16,0,-.27)],light)
block('Belly | lower stone', (0,-.20,1.79),(.93,.55,.36),stone,.10,rot=(0,0,-3))
block('Pelvis', (0,.08,1.40),(.99,.65,.40),stone,.13)
for side in [-1,1]:
    sx=side*.43
    block(('Left' if side<0 else 'Right')+' thigh',(sx,.07,1.16),(.52,.58,.55),stone,.13,rot=(0,side*-7,side*4))
    block('Recessed knee',(sx,-.10,.88),(.40,.48,.34),dark,.10)
    block('Shin carved face',(sx,-.19,.58),(.54,.53,.61),light,.12,rot=(3,side*-4,0))
    block('Broad planted foot',(sx,-.32,.21),(.64,.91,.37),stone,.10,rot=(0,0,side*-7))
    for k in [-1,0,1]:
        block('Toe cut',(sx+k*.17,-.747,.19),(.13,.065,.20),light,.025)
    block('Shoulder foundation',(side*.88,.03,2.52),(.55,.64,.55),dark,.16)
    block('Upper arm',(side*1.04,.02,2.19),(.48,.53,.61),stone,.12,rot=(0,side*-15,side*-3))
    block('Elbow joint',(side*1.13,-.015,1.89),(.36,.40,.32),dark,.09)
    bulk=1.12 if side>0 else 1
    slab('Heavy forearm',[(1.29,.245*bulk,.29,side*1.25,-.12),(1.43,.33*bulk,.34,side*1.25,-.10),(1.85,.28*bulk,.27,side*1.18,-.035),(1.99,.20,.22,side*1.14,-.015)],light if side>0 else stone)
    block('Palm',(side*1.28,-.12,1.13),(.57*bulk,.58,.39),stone,.10,rot=(0,side*-5,0))
    for k in [-1,0,1]:
        block('Broad finger',(side*1.28+k*.17,-.345,1.04),(.145,.27,.30),light,.035)
    block('Thumb',(side*.985,-.16,1.19),(.19,.32,.31),stone,.055,rot=(0,side*-22,0))

block('Right shoulder | split cap',(.87,.05,2.70),(.77,.80,.40),light,.12,rot=(0,12,-4))
block('Left shoulder | taller boundary stone',(-.83,.06,2.77),(.79,.78,.54),stone,.13,rot=(0,-13,3))
# Small head set forward, a single heavy eyebrow over a dark eye recess.
block('Neck',(0,.01,2.77),(.39,.42,.30),dark,.06)
slab('Head | worn standing stone',[(2.77,.25,.24,0,-.10),(2.91,.34,.31,0,-.10),(3.26,.31,.29,-.025,-.10),(3.40,.22,.24,-.055,-.08)],stone)
block('Face recess',(0,-.435,3.085),(.49,.032,.19),dark,.03)
for side in [-1,1]:
    block('Amber eye',(side*.142,-.457,3.08),(.105,.025,.075),amber,.008,rot=(0,side*8,0))
block('Heavy continuous brow',(0,-.437,3.225),(.63,.17,.14),light,.035,rot=(0,-3,0))
block('Square nose', (0,-.476,3.015),(.135,.16,.20),light,.028)
block('Chin', (0,-.38,2.84),(.44,.23,.15),light,.04)
block('Mouth seam', (0,-.508,2.918),(.28,.02,.026),dark,.005,rot=(0,3,0))

# Broad hand-shaped moss mantle. No scattered particle noise.
slab('Moss | shoulder mantle',[(2.89,.45,.42,-.88,.06),(3.01,.36,.35,-.85,.08),(3.055,.20,.23,-.79,.06)],moss_light)
slab('Moss | long hanging fold',[(2.10,.11,.055,-1.055,-.34),(2.45,.15,.09,-1.02,-.36),(2.90,.26,.13,-.88,-.34)],moss)
slab('Moss | short hanging fold',[(2.42,.095,.04,-.65,-.47),(2.75,.14,.08,-.66,-.43),(2.95,.18,.13,-.68,-.31)],moss)
block('Moss | back drape',(-.78,.44,2.68),(.64,.14,.47),moss,.09,rot=(0,-9,0))
block('Moss | shin patch',(.41,-.44,.80),(.38,.055,.16),moss,.03,rot=(0,6,0))

# An inset old waymarker motif: three readable cuts on the breastplate.
carve('Breastplate | single old lintel','Waymark | stem',(0,-.49,2.28),(.052,.13,.30))
for side in [-1,1]:
    carve('Breastplate | single old lintel','Waymark | branch',(side*.09,-.49,2.32),(.045,.13,.22),rot=(0,side*43,0))
# A few long seams, intentionally sparse and large enough to read.
carve('Right shoulder | split cap','Shoulder fracture',(.92,-.32,2.72),(.04,.17,.40),rot=(0,19,0))
carve('Head | worn standing stone','Chipped crown',(.16,-.10,3.39),(.115,.7,.18),rot=(0,-28,0))

# Collection makes the art easy to select without its presentation stage.
collection=bpy.data.collections.new('ASSET | Mirewarden')
bpy.context.scene.collection.children.link(collection)
for ob in assets:
    for c in list(ob.users_collection): c.objects.unlink(ob)
    collection.objects.link(ob)

floor=material('Stage | warm gray',(.15,.175,.17))
bpy.ops.mesh.primitive_plane_add(size=200)
bpy.context.object.name='STAGE | ground'
bpy.context.object.data.materials.append(floor)

def aim(ob,point): ob.rotation_euler=(Vector(point)-ob.location).to_track_quat('-Z','Y').to_euler()
for name,loc,power,size,color in [('Key',(-3,-4,7),750,5,(1,.88,.72)),('Fill',(4,-2,5),500,4,(.76,.88,1)),('Rim',(-1,4,6),850,3,(1,.94,.79))]:
    bpy.ops.object.light_add(type='AREA',location=loc)
    ob=bpy.context.object
    ob.name=name
    ob.data.energy=power
    ob.data.shape='DISK'
    ob.data.size=size
    ob.data.color=color
    aim(ob,(0,0,1.6))
bpy.ops.object.camera_add(location=(5,-8,4.5))
camera=bpy.context.object
camera.name='CAMERA | review'
camera.data.type='ORTHO'
camera.data.ortho_scale=4.5
aim(camera,(0,0,1.66))
scene=bpy.context.scene
scene.camera=camera
scene.render.engine='CYCLES'
scene.cycles.samples=32
scene.cycles.use_denoising=True
scene.world.color=(.22,.22,.22)
scene.render.resolution_x=1000
scene.render.resolution_y=1000
scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'

bpy.ops.object.select_all(action='DESELECT')
for ob in assets: ob.select_set(True)
bpy.context.view_layer.objects.active=assets[0]
bpy.ops.export_scene.gltf(filepath=str(OUT/'mirewarden-review.glb'),use_selection=True,export_format='GLB')
triangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in assets)
(OUT/'metrics.json').write_text(json.dumps({'mesh_objects':len(assets),'triangles':triangles,'vertices':sum(len(o.data.vertices) for o in assets),'rigged':False,'ftk_runtime_compatible':False},indent=2)+'\n')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'mirewarden.blend'))
for name,loc in [('hero',(5,-8,4.5)),('front',(0,-9,3.6)),('back',(-5,8,4.4))]:
    camera.location=loc
    aim(camera,(0,0,1.66))
    scene.render.filepath=str(OUT/(name+'.png'))
    bpy.ops.render.render(write_still=True)
print('MIREWARDEN_COMPLETE',triangles)
