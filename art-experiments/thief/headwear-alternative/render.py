import importlib.util,json
from pathlib import Path
import bpy
OUT=Path(__file__).resolve().parent;ROOT=OUT.parents[2]
spec=importlib.util.spec_from_file_location('icons',ROOT/'art-experiments/paladin-equipment/render_icons.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
colors=json.loads((OUT.parent/'ranged-apparel/manifest.json').read_text())['palette']
mats=m.reset(colors)
for mat in mats:mat.node_tree.nodes.get('Principled BSDF').inputs['Metallic'].default_value=0
points=m.add_source(OUT/'nightblade-cap-cowl-display.source.json',mats)
bpy.context.scene.view_settings.view_transform='Standard'
m.render(OUT/'nightblade-cap-cowl-icon.png',points)
bpy.context.scene.render.resolution_x=512;bpy.context.scene.render.resolution_y=512
bpy.context.scene.render.filepath=str(OUT/'nightblade-cap-cowl-preview.png');bpy.ops.render.render(write_still=True)
