#!/usr/bin/env python3
"""Render original Thief asset icons and offline review views in Blender."""
import importlib.util,json,sys
from pathlib import Path
import bpy

OUT=Path(__file__).resolve().parent;ROOT=OUT.parents[2]
spec=importlib.util.spec_from_file_location('icons',ROOT/'art-experiments/paladin-equipment/render_icons.py')
icons=importlib.util.module_from_spec(spec);spec.loader.exec_module(icons)
manifest=json.loads((OUT/'manifest.json').read_text())
selected=next((a.split('=',1)[1] for a in sys.argv if a.startswith('--only=')),None)
for item in manifest['items']:
    if '--apparel-only' in sys.argv and item['family']=='bow':continue
    if selected and selected not in item['id']:continue
    mats=icons.reset(manifest['palette'] if item['family']=='bow' else manifest['apparelPalette'])
    for i,mat in enumerate(mats):
        node=mat.node_tree.nodes.get('Principled BSDF')
        node.inputs['Metallic'].default_value=.5 if i in [5,9,10] else 0
        node.inputs['Roughness'].default_value=.45 if i in [5,9,10] else .8
    points=icons.add_source(OUT/item['display'].replace('.glb','.source.json'),mats)
    bpy.context.scene.view_settings.view_transform='Standard'
    icons.render(OUT/item['icon'],points)
    if item['family']=='hood' or item['band'] in ['street','locksmith','nightblade','wayfarer']:
        bpy.context.scene.render.resolution_x=512;bpy.context.scene.render.resolution_y=512
        bpy.context.scene.render.filepath=str(OUT/item['icon'].replace('-icon','-preview'))
        bpy.ops.render.render(write_still=True)
print('Rendered original mesh icons')
