"""Render original, already-reviewed FTK equipped hammer geometry as studio stills.
Blender -b -t 2 --python render.py -- PACKAGE OUTPUT ITEM_ID [ITEM_ID...]
This does not alter models and supplies no native fit or motion evidence.
"""
import bpy,sys,json,struct,hashlib
from pathlib import Path
from mathutils import Vector
args=sys.argv[sys.argv.index('--')+1:];package=Path(args[0]);output=Path(args[1]);output.mkdir(parents=True,exist_ok=True)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
entries={e['id']:e for e in json.loads((package/'content.json').read_text())['entries']}
receipt={'scope':'Studio stills of exact original exported equipped hammer geometry, albedo and metallic/smoothness maps. No native game assets or fit/motion claims.','rendererSha256':sha(Path(__file__)),'items':{}}
for item in args[2:]:
 entry=entries[item];assert item.startswith('paladin_hammer_');models=[m for m in entry['itemModels'] if m['path']=='.'];assert len(models)==1,'One intact equipped root renderer is required; break fragments are inactive'
 spec=models[0];path=package/spec['model'];data=path.read_bytes();length=struct.unpack_from('<I',data,12)[0];doc=json.loads(data[20:20+length]);binary=data[28+length:]
 def read(index):
  a=doc['accessors'][index];v=doc['bufferViews'][a['bufferView']];n={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']];kind={5126:'f',5123:'H',5125:'I'}[a['componentType']];stride=v.get('byteStride',struct.calcsize(kind)*n);start=v.get('byteOffset',0)+a.get('byteOffset',0)
  return [struct.unpack_from('<'+kind*n,binary,start+i*stride) for i in range(a['count'])]
 bpy.ops.wm.read_factory_settings(use_empty=True)
 objects=[]
 for m in doc['meshes']:
  for primitive in m['primitives']:
   positions=read(primitive['attributes']['POSITION']);uvs=read(primitive['attributes']['TEXCOORD_0']);indices=[i[0] for i in read(primitive['indices'])]
   mesh=bpy.data.meshes.new(item);mesh.from_pydata([(x,-z,y) for x,y,z in positions],[],[indices[i:i+3] for i in range(0,len(indices),3)]);mesh.update()
   obj=bpy.data.objects.new(item,mesh);bpy.context.collection.objects.link(obj);objects.append(obj)
   uv=mesh.uv_layers.new()
   for loop in mesh.loops:
    u,v=uvs[loop.vertex_index];uv.data[loop.index].uv=(u,1-v)
   if 'NORMAL' in primitive['attributes']:
    for poly in mesh.polygons:poly.use_smooth=True
    mesh.normals_split_custom_set_from_vertices([(x,-z,y) for x,y,z in read(primitive['attributes']['NORMAL'])])
   mat=bpy.data.materials.new('Authored runtime materials');mat.use_nodes=True;nodes=mat.node_tree.nodes;links=mat.node_tree.links;bsdf=nodes.get('Principled BSDF')
   tex=nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str((package/spec['texture']).resolve()));links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
   mask=nodes.new('ShaderNodeTexImage');mask.image=bpy.data.images.load(str((package/spec['metallicGlossTexture']).resolve()));mask.image.colorspace_settings.name='Non-Color';separate=nodes.new('ShaderNodeSeparateColor');links.new(mask.outputs['Color'],separate.inputs[0]);links.new(separate.outputs[0],bsdf.inputs['Metallic']);inverse=nodes.new('ShaderNodeMath');inverse.operation='SUBTRACT';inverse.inputs[0].default_value=1;links.new(mask.outputs['Alpha'],inverse.inputs[1]);links.new(inverse.outputs[0],bsdf.inputs['Roughness']);mesh.materials.append(mat)
 points=[v.co for o in objects for v in o.data.vertices];lo=Vector([min(v[i] for v in points) for i in range(3)]);hi=Vector([max(v[i] for v in points) for i in range(3)]);center=(lo+hi)/2;span=max(hi-lo)*1.24
 scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True;scene.render.resolution_x=512;scene.render.resolution_y=512;scene.render.resolution_percentage=100;scene.render.film_transparent=True;scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA';scene.view_settings.view_transform='Standard';scene.world=bpy.data.worlds.new('Neutral studio');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.45,.45,.45,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.65
 bpy.ops.object.camera_add();camera=bpy.context.object;camera.data.type='ORTHO';camera.data.ortho_scale=span;scene.camera=camera;camera.location=center+Vector((1,-4,.45))*span;camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
 for direction,power in [((2,-3,4),450),((-3,-1,1),250),((1,3,3),500)]:
  bpy.ops.object.light_add(type='AREA',location=center+Vector(direction)*span);light=bpy.context.object;light.data.energy=power*span*span;light.data.shape='DISK';light.data.size=span*3;light.rotation_euler=(center-light.location).to_track_quat('-Z','Y').to_euler()
 filename=item+'.png';scene.render.filepath=str((output/filename).resolve());bpy.ops.render.render(write_still=True)
 receipt['items'][item]={'output':filename,'outputSha256':sha(output/filename),'sources':{key:{'path':spec[key],'sha256':sha(package/spec[key])} for key in ['model','texture','metallicGlossTexture']},'cameraDirection':[1,-4,.45],'orthographicMargin':1.24,'route':'itemModels intact root, break fragments inactive','sourceContentSha256':sha(package/'content.json'),'UVConversion':'FTK Y up to Blender Z up; flip texture V once'}
 (output/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
