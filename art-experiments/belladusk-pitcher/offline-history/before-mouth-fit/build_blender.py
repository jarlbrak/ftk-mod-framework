"""Create editable armature scenes, independently re-export, and render original Belladusk Pitcher."""
import bpy,json,sys,math,subprocess
from pathlib import Path
from mathutils import Vector
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from create_blender_template import create
from export_blender_model import export
bpy.context.preferences.filepaths.save_version=0
PYTHON=ROOT/'scratch/model-venv/bin/python';SCRATCH=ROOT/'scratch/belladusk-roundtrip';SCRATCH.mkdir(parents=True,exist_ok=True)
def import_original(name,arm):
 d=json.loads((OUT/f'{name}.source.json').read_text());mesh=bpy.data.meshes.new(name)
 mesh.from_pydata([(x,-z,y) for x,y,z in d['positions']],[],d['triangles']);mesh.update();obj=bpy.data.objects.new(name,mesh);bpy.context.scene.collection.objects.link(obj)
 uv=mesh.uv_layers.new(name='Palette')
 for loop in mesh.loops:u,v=d['uvs'][loop.vertex_index];uv.data[loop.index].uv=(u,1-v)
 for bone in d['bone_names']:obj.vertex_groups.new(name=bone)
 for i,(joints,weights) in enumerate(zip(d['joints'],d['weights'])):
  for j,w in zip(joints,weights):
   if w>0:obj.vertex_groups[j].add([i],w,'REPLACE')
 mod=obj.modifiers.new('Native bind armature','ARMATURE');mod.object=arm;obj['ftk_export']=True
 image=bpy.data.images.load(str(OUT/'belladusk_basecolor.png'),check_existing=True);image.pack()
 material=bpy.data.materials.new('Belladusk Pitcher original palette');material.use_nodes=True;nodes=material.node_tree.nodes;shader=nodes.get('Principled BSDF');shader.inputs['Roughness'].default_value=.78
 texture=nodes.new('ShaderNodeTexImage');texture.image=image;texture.interpolation='Closest';material.node_tree.links.new(texture.outputs['Color'],shader.inputs['Base Color']);mesh.materials.append(material)
 return obj
for rid,name in [(121530,'belladusk')]:
 ref=ROOT/'scratch/plant-e-native-topology-analysis/reference-121530';create(ref/'reference.npz',ref/'skeleton.json',SCRATCH/f'{name}-template.blend',PYTHON,False)
 arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
 for obj in list(bpy.data.objects):
  if obj.type=='MESH':bpy.data.objects.remove(obj,do_unlink=True)
 obj=import_original(name,arm);bpy.context.scene['ftk_art_status']='Original Belladusk Pitcher surface; live validation pending'
 bpy.context.view_layer.objects.active=obj;obj.select_set(True);arm.select_set(False)
 bpy.context.preferences.filepaths.save_version=0
 bpy.ops.wm.save_as_mainfile(filepath=str(OUT/f'{name}.blend'))
 bpy.ops.wm.open_mainfile(filepath=str(OUT/f'{name}.blend'));export(OUT/'belladusk-reopened.glb')
 validation=subprocess.run([str(PYTHON),str(ROOT/'tools/ai-model-pipeline/validate_glb.py'),str(OUT/'belladusk-reopened.glb'),'--reference',str(ref/'reference.npz')],capture_output=True,text=True,check=True)
 (OUT/'reopened-validation.json').write_text(validation.stdout)
# Present the single original rigged source in a separate studio scene.
bpy.ops.wm.open_mainfile(filepath=str(OUT/'belladusk.blend'))
for obj in bpy.context.scene.objects:
 if obj.type=='ARMATURE':obj.hide_render=True;obj.hide_set(True)
scene=bpy.context.scene;scene.render.fps=12;scene['ftk_art_status']='Studio presentation: export original from belladusk.blend'
# Neutral studio lighting; no game environment or native mesh is included.
world=bpy.data.worlds.new('Slate atmosphere');scene.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.045,.061,.07,1);world.node_tree.nodes['Background'].inputs[1].default_value=.35
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.07));floor=bpy.context.object;floor.name='Studio floor - not export';floor['ftk_export']=False
mat=bpy.data.materials.new('Slate floor');mat.diffuse_color=(.047,.063,.070,1);mat.use_nodes=True;mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(.047,.063,.070,1);mat.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.9;floor.data.materials.append(mat)
def aim(obj,target):obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
def light(name,loc,power,color,size):
 data=bpy.data.lights.new(name,'AREA');data.energy=power;data.color=color;data.shape='DISK';data.size=size;obj=bpy.data.objects.new(name,data);scene.collection.objects.link(obj);obj.location=loc;aim(obj,(0,0,.8))
light('Warm key',(4,-5,7),650,(.82,.90,1),4);light('Cool fill',(-4,-2,4),400,(.59,.78,1),3);light('Copper rim',(0,4,6),900,(.70,.62,1),2)
camdata=bpy.data.cameras.new('Studio camera');cam=bpy.data.objects.new('Studio camera',camdata);scene.collection.objects.link(cam);scene.camera=cam;camdata.type='ORTHO';camdata.ortho_scale=2.8
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True;scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG';scene.render.film_transparent=False
cam.location=(3,-7,3);aim(cam,(0,0,.78));scene.render.filepath=str(OUT/'hero.png');bpy.ops.render.render(write_still=True)
cam.location=(-7,-1,3.2);aim(cam,(0,0,.78));scene.render.filepath=str(OUT/'side.png');bpy.ops.render.render(write_still=True)
cam.location=(3,-7,3);aim(cam,(0,0,.78));bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'belladusk-studio.blend'))
