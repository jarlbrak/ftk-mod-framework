"""Original-source-only studio rear and three-quarter backpack views."""
import json
from pathlib import Path
import bpy
from mathutils import Vector
OUT=Path(__file__).resolve().parent
COLORS=['172633','51687B','B8C9CE','97662D','E3B658','254C86','762535','201D28','81CDD4','EEE1B7','B68D70','3D2C25']
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
mats=[]
for color in COLORS:
 m=bpy.data.materials.new(color);m.diffuse_color=tuple(int(color[i:i+2],16)/255 for i in [0,2,4])+(1,);mats.append(m)
records=json.loads((OUT/'manifest.json').read_text())['assets']
for i,record in enumerate(records):
 s=json.loads((OUT/(record['key']+'.source.json')).read_text())
 mesh=bpy.data.meshes.new(record['key']);mesh.from_pydata([(v[0],-v[2],v[1]) for v in s['positions']],[],s['triangles']);mesh.update()
 obj=bpy.data.objects.new(record['key'],mesh);bpy.context.collection.objects.link(obj);obj.location.x=(i%6-2.5)*1.45;obj.location.z=(.5-i//6)*1.5;obj.rotation_euler.z=-.20
 for m in mats:obj.data.materials.append(m)
 for face in mesh.polygons:face.material_index=int(s['uvs'][face.vertices[0]][0]*len(COLORS))
bpy.ops.object.camera_add(location=(1,-9,2));cam=bpy.context.object;cam.rotation_euler=(Vector((0,0,0))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=9.1
scene=bpy.context.scene;scene.camera=cam;scene.render.engine='BLENDER_WORKBENCH';scene.display.shading.light='STUDIO';scene.display.shading.color_type='MATERIAL';scene.display.shading.show_shadows=True;scene.display.shading.show_cavity=True;scene.display.shading.background_type='WORLD';scene.world.color=(.055,.06,.08);scene.render.resolution_x=1800;scene.render.resolution_y=850;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.render.filepath=str(OUT/'paladin-loot-display-studio.png');bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'paladin-loot-display.blend'));bpy.ops.render.render(write_still=True)
