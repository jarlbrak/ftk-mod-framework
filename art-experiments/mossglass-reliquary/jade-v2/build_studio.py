"""Texture-only editable variant and native2x-scale studio comparison; does not rewrite V1."""
import bpy
from pathlib import Path
from mathutils import Vector
O=Path(__file__).resolve().parent;B=O.parent
bpy.context.preferences.filepaths.save_version=0
for scene_file,out_file in [('mossglass.blend','mossglass-jade-v2.blend'),('mossglass-studio.blend','mossglass-jade-v2-studio.blend')]:
 bpy.ops.wm.open_mainfile(filepath=str(B/scene_file))
 for mat in bpy.data.materials:
  if not mat.use_nodes:continue
  for node in list(mat.node_tree.nodes):
   if node.type=='TEX_IMAGE' and node.image and 'slot1' in node.image.name:
    node.image=bpy.data.images.load(str(O/'mossglass_jade_v2.slot1.png'));node.image.pack()
    if 'studio' in scene_file:
     coords=mat.node_tree.nodes.new('ShaderNodeTexCoord');mul=mat.node_tree.nodes.new('ShaderNodeVectorMath');mul.operation='MULTIPLY';mul.inputs[1].default_value=(2,2,1);mat.node_tree.links.new(coords.outputs['UV'],mul.inputs[0]);mat.node_tree.links.new(mul.outputs['Vector'],node.inputs['Vector'])
 bpy.ops.wm.save_as_mainfile(filepath=str(O/out_file))
 if 'studio' in scene_file:
  scene=bpy.context.scene;scene.render.filepath=str(O/'hero-native-scale2.png');bpy.ops.render.render(write_still=True)
  scene.camera.location=(-7,-1,3.2);scene.camera.rotation_euler=(Vector((0,0,1.02))-scene.camera.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(O/'side-native-scale2.png');bpy.ops.render.render(write_still=True)
