"""Render published original equipment only; never imports native game assets.
Run: Blender -b -t 4 --python website/scripts/render-turntable.py -- PACKAGE_DIR OUTPUT_DIR ITEM_ID
Then encode frames with the commands in website/README.md.
"""
import bpy, sys, json, math, pathlib
from mathutils import Vector
package,out,item_id=sys.argv[sys.argv.index('--')+1:]
package=pathlib.Path(package); out=pathlib.Path(out); out.mkdir(parents=True,exist_ok=True)
entry=next(e for e in json.loads((package/'content.json').read_text())['entries'] if e['id']==item_id)
model=entry['itemModels'][0]
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(package/model['model']))
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
mat=bpy.data.materials.new('Published atlas'); mat.use_nodes=True
bsdf=mat.node_tree.nodes.get('Principled BSDF'); bsdf.inputs['Roughness'].default_value=.65
tex=mat.node_tree.nodes.new('ShaderNodeTexImage'); tex.image=bpy.data.images.load(str(package/model['texture']))
mat.node_tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
for obj in meshes: obj.data.materials.clear(); obj.data.materials.append(mat)
points=[obj.matrix_world@Vector(c) for obj in meshes for c in obj.bound_box]
center=sum(points,Vector())/len(points); extent=max(max(p[i] for p in points)-min(p[i] for p in points) for i in range(3))
pivot=bpy.data.objects.new('Turntable',None); bpy.context.collection.objects.link(pivot)
for obj in meshes:
 obj.parent=pivot; obj.location-=center
pivot.scale=(3/extent,)*3
scene=bpy.context.scene
scene.render.engine='CYCLES'; scene.cycles.samples=12; scene.cycles.use_denoising=True
scene.world.color=(.18,.18,.18)
scene.render.resolution_x=800; scene.render.resolution_y=800; scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'; scene.render.fps=24
scene.view_settings.view_transform='AgX'
bpy.ops.object.camera_add(location=(4,-6,2.4)); camera=bpy.context.object; camera.rotation_euler=(-camera.location).to_track_quat('-Z','Y').to_euler(); camera.data.type='ORTHO'; camera.data.ortho_scale=4.3; scene.camera=camera
for loc,power,size in [((3,-4,5),650,4),((-4,-2,2),450,3),((1,4,3),850,3)]:
 bpy.ops.object.light_add(type='AREA',location=loc); light=bpy.context.object;light.data.energy=power;light.data.shape='DISK';light.data.size=size;light.rotation_euler=(-light.location).to_track_quat('-Z','Y').to_euler()
scene.render.film_transparent=False
# Exact camera and render settings live here; a studio rotation is not a native animation.
for frame in range(96):
 pivot.rotation_euler[2]=2*math.pi*frame/96
 scene.render.filepath=str(out/f'{frame:04}.png'); bpy.ops.render.render(write_still=True)
