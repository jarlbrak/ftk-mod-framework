"""Keep editable original-only baseline and crown-corrected meshes for inspection."""
import bpy,sys
from pathlib import Path
here=Path(__file__).resolve().parent;sys.path.insert(0,str(here));from mesh_io import decode
bpy.ops.wm.read_factory_settings(use_empty=True)
for name,folder in [('Baseline','inputs'),('Crown correction','outputs')]:
 d=decode(here/folder/'paladin-censure-helmet.glb');mesh=bpy.data.meshes.new(name);mesh.from_pydata([(x,-z,y) for x,y,z in d['POSITION']],[],d['triangles'].tolist());mesh.update();ob=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(ob);layer=mesh.uv_layers.new()
 for loop in mesh.loops:
  u,v=d['TEXCOORD_0'][loop.vertex_index];layer.data[loop.index].uv=(float(u),float(1-v))
 for face in mesh.polygons:face.use_smooth=True
 mesh.normals_split_custom_set_from_vertices([(x,-z,y) for x,y,z in d['NORMAL']]);ob.hide_render=name=='Baseline';ob.hide_set(name=='Baseline')
 if name!='Baseline':ob.select_set(True);bpy.context.view_layer.objects.active=ob
bpy.ops.wm.save_as_mainfile(filepath=str(here/'censure-crown.blend'))
