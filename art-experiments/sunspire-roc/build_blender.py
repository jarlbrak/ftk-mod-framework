"""Create editable Sunspire Roc source scenes, re-export, and render its studio views."""
import json
import subprocess
import sys
from pathlib import Path

import bpy
from mathutils import Vector

OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
sys.path.insert(0, str(ROOT / 'tools/ai-model-pipeline'))
from create_blender_template import create
from export_blender_model import export

bpy.context.preferences.filepaths.save_version = 0
PYTHON = ROOT / 'scratch/model-venv/bin/python'
SCRATCH = ROOT / 'scratch/sunspire-roc-roundtrip'
SCRATCH.mkdir(parents=True, exist_ok=True)


def import_original(name, armature):
    data = json.loads((OUT / f'{name}.source.json').read_text())
    mesh = bpy.data.meshes.new(name)
    # FTK mesh-local (x,y,z) maps to Blender (x,-z,y).
    mesh.from_pydata([(x, -z, y) for x, y, z in data['positions']], [], data['triangles'])
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    uv = mesh.uv_layers.new(name='Sunspire Palette')
    for loop in mesh.loops:
        u, v = data['uvs'][loop.vertex_index]
        uv.data[loop.index].uv = (u, 1 - v)
    for bone in data['bone_names']:
        obj.vertex_groups.new(name=bone)
    for vertex, (joints, weights) in enumerate(zip(data['joints'], data['weights'])):
        for joint, weight in zip(joints, weights):
            if weight > 0:
                obj.vertex_groups[joint].add([vertex], weight, 'REPLACE')
    modifier = obj.modifiers.new('Native bind armature', 'ARMATURE')
    modifier.object = armature
    obj['ftk_export'] = True
    image = bpy.data.images.load(str(OUT / 'sunspire-roc_basecolor.png'), check_existing=True)
    image.pack()
    material = bpy.data.materials.new('Sunspire Roc original palette')
    material.use_nodes = True
    shader = material.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Roughness'].default_value = .78
    texture = material.node_tree.nodes.new('ShaderNodeTexImage')
    texture.image = image
    texture.interpolation = 'Closest'
    material.node_tree.links.new(texture.outputs['Color'], shader.inputs['Base Color'])
    mesh.materials.append(material)
    return obj


reference = ROOT / 'scratch/skeleton-audit/121238'
create(reference / 'reference.npz', reference / 'skeleton.json', SCRATCH / 'sunspire-roc-template.blend', PYTHON, False)
armature = next(obj for obj in bpy.context.scene.objects if obj.type == 'ARMATURE')
for obj in list(bpy.context.scene.objects):
    if obj.type == 'MESH':
        bpy.data.objects.remove(obj, do_unlink=True)
roc = import_original('sunspire-roc', armature)
bpy.context.scene['ftk_art_status'] = 'Original Sunspire Roc surface; live validation pending.'
bpy.context.view_layer.objects.active = roc
roc.select_set(True)
armature.select_set(False)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'sunspire-roc.blend'))
bpy.ops.wm.open_mainfile(filepath=str(OUT / 'sunspire-roc.blend'))
export(OUT / 'sunspire-roc-reopened.glb')
result = subprocess.run(
    [str(PYTHON), str(ROOT / 'tools/ai-model-pipeline/validate_glb.py'), str(OUT / 'sunspire-roc-reopened.glb'), '--reference', str(reference / 'reference.npz')],
    capture_output=True,
    text=True,
    check=True,
)
(OUT / 'reopened-validation.json').write_text(result.stdout)

# A separate studio presentation makes the original source inspectable without
# accidentally exporting scenery or lights with the mesh.
bpy.ops.wm.open_mainfile(filepath=str(OUT / 'sunspire-roc.blend'))
for obj in bpy.context.scene.objects:
    if obj.type == 'ARMATURE':
        obj.hide_render = True
        obj.hide_set(True)
scene = bpy.context.scene
scene['ftk_art_status'] = 'Studio presentation only; export from sunspire-roc.blend.'
world = bpy.data.worlds.new('Deep blue studio')
world.use_nodes = True
world.node_tree.nodes['Background'].inputs[0].default_value = (.024, .037, .054, 1)
world.node_tree.nodes['Background'].inputs[1].default_value = .35
scene.world = world
bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -.07))
floor = bpy.context.object
floor.name = 'Studio floor - not export'
floor['ftk_export'] = False
floor_material = bpy.data.materials.new('Studio slate')
floor_material.use_nodes = True
floor_material.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (.026, .042, .061, 1)
floor_material.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value = .91
floor.data.materials.append(floor_material)


def aim(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat('-Z', 'Y').to_euler()


def light(name, location, power, color, size):
    data = bpy.data.lights.new(name, 'AREA')
    data.energy = power
    data.color = color
    data.shape = 'DISK'
    data.size = size
    obj = bpy.data.objects.new(name, data)
    scene.collection.objects.link(obj)
    obj.location = location
    aim(obj, (0, 0, 1.10))


light('Sun-gold key', (7, -9, 10), 1500, (1.0, .69, .34), 5)
light('Cool feather fill', (-7, -5, 6), 900, (.42, .67, 1.0), 4)
light('High rim', (0, 7, 11), 1800, (.94, .80, .55), 4)
camera_data = bpy.data.cameras.new('Studio camera')
camera = bpy.data.objects.new('Studio camera', camera_data)
scene.collection.objects.link(camera)
scene.camera = camera
camera_data.type = 'ORTHO'
camera_data.ortho_scale = 8.2
scene.render.engine = 'BLENDER_EEVEE'
scene.render.resolution_x = 1000
scene.render.resolution_y = 1000
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.view_settings.look = 'AgX - Medium High Contrast'
camera.location = (5.8, -10.5, 5.7)
aim(camera, (0, 0, 1.15))
scene.render.filepath = str(OUT / 'hero.png')
bpy.ops.render.render(write_still=True)
camera.location = (-9.8, -2.5, 3.4)
aim(camera, (0, 0, 1.12))
scene.render.filepath = str(OUT / 'side.png')
bpy.ops.render.render(write_still=True)
camera.location = (5.8, -10.5, 5.7)
aim(camera, (0, 0, 1.15))
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'sunspire-roc-studio.blend'))
