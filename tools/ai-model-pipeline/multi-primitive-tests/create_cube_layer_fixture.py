"""Build ORIGINAL two-layer slot/scroll calibration geometry, not finished creature art."""
import bpy,sys,json,argparse,subprocess
from pathlib import Path
R=Path(__file__).resolve().parents[3];p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--skeleton',type=Path,required=True);p.add_argument('--python',type=Path,required=True);p.add_argument('--output-dir',type=Path,required=True);a=p.parse_args(sys.argv[sys.argv.index('--')+1:]);O=a.output_dir.resolve();O.mkdir(parents=True,exist_ok=True);sys.path.insert(0,str(R/'tools/ai-model-pipeline'));subprocess.run([str(a.python),str(Path(__file__).with_name('make_fixture_textures.py')),'--output-dir',str(O)],check=True)
from create_blender_template import create
from export_blender_model import export
create(a.reference.resolve(),a.skeleton.resolve(),O/'template.blend',a.python.resolve(),False)
arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
for o in list(bpy.data.objects):
 if o.type=='MESH':bpy.data.objects.remove(o,do_unlink=True)
materials=[]
for i in range(2):
 m=bpy.data.materials.new('Original fixture '+str(i));m.use_nodes=True;im=bpy.data.images.load(str(O/f'original{i}.png'));im.pack();node=m.node_tree.nodes.new('ShaderNodeTexImage');node.image=im;m.node_tree.links.new(node.outputs['Color'],m.node_tree.nodes.get('Principled BSDF').inputs['Base Color']);materials.append(m)
for i in range(2):
 bpy.ops.mesh.primitive_cube_add(size=.35+i*.30,location=(0,0,.45+i*.8));o=bpy.context.object;o.name='Original fixture layer '+str(i);o['ftk_export']=True
 for m in materials:o.data.materials.append(m)
 for p in o.data.polygons:p.material_index=i
 for bone in arm.data.bones:o.vertex_groups.new(name=bone.name)
 o.vertex_groups['jellyCubeMid' if i==0 else 'jellyCubeTop'].add(list(range(len(o.data.vertices))),1,'REPLACE');mod=o.modifiers.new('Native rig','ARMATURE');mod.object=arm
bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(O/'cubea_material_layers_v1.blend'));bpy.ops.wm.open_mainfile(filepath=str(O/'cubea_material_layers_v1.blend'));export(O/'cubea_material_layers_v1.glb',native_material_slots=2)
