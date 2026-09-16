"""Create editable Duneshade scenes, re-export the source, and render studio views."""
from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path

import bpy
from mathutils import Vector


OUT = Path(__file__).resolve().parent
ROOT = OUT.parent.parent
sys.path.insert(0, str(ROOT / "tools/ai-model-pipeline"))
from create_blender_template import create
from export_blender_model import export


bpy.context.preferences.filepaths.save_version = 0
PYTHON = ROOT / "scratch/model-venv/bin/python"
REFERENCE = ROOT / "scratch/skeleton-audit/121552"
SCRATCH = ROOT / "scratch/duneshade-desert-asp-roundtrip"
SCRATCH.mkdir(parents=True, exist_ok=True)


def import_original(name: str, armature):
    data = json.loads((OUT / f"{name}.source.json").read_text())
    mesh = bpy.data.meshes.new(name)
    # FTK mesh-local (x, y, z) becomes Blender (x, -z, y).
    mesh.from_pydata([(x, -z, y) for x, y, z in data["positions"]], [], data["triangles"])
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    uv = mesh.uv_layers.new(name="Duneshade palette")
    for loop in mesh.loops:
        u, v = data["uvs"][loop.vertex_index]
        uv.data[loop.index].uv = (u, 1 - v)
    for bone in data["bone_names"]:
        obj.vertex_groups.new(name=bone)
    for vertex, (joints, weights) in enumerate(zip(data["joints"], data["weights"])):
        for joint, weight in zip(joints, weights):
            if weight > 0:
                obj.vertex_groups[data["bone_names"][joint]].add([vertex], weight, "REPLACE")
    modifier = obj.modifiers.new("Native bind armature", "ARMATURE")
    modifier.object = armature
    obj["ftk_export"] = True
    image = bpy.data.images.load(str(OUT / "duneshade-desert-asp_basecolor.png"), check_existing=True)
    image.pack()
    material = bpy.data.materials.new("Duneshade original palette")
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Roughness"].default_value = 0.72
    texture = material.node_tree.nodes.new("ShaderNodeTexImage")
    texture.image = image
    texture.interpolation = "Closest"
    material.node_tree.links.new(texture.outputs["Color"], shader.inputs["Base Color"])
    mesh.materials.append(material)
    return obj


create(REFERENCE / "reference.npz", REFERENCE / "skeleton.json", SCRATCH / "duneshade-template.blend", PYTHON, False)
armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
for obj in list(bpy.context.scene.objects):
    if obj.type == "MESH":
        bpy.data.objects.remove(obj, do_unlink=True)
model = import_original("duneshade-desert-asp", armature)
bpy.context.scene["ftk_art_status"] = "Original Duneshade Asp source; live validation pending."
bpy.context.view_layer.objects.active = model
model.select_set(True)
armature.select_set(False)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / "duneshade-desert-asp.blend"))
bpy.ops.wm.open_mainfile(filepath=str(OUT / "duneshade-desert-asp.blend"))
export(OUT / "duneshade-desert-asp-reopened.glb")
result = subprocess.run(
    [
        str(PYTHON), str(ROOT / "tools/ai-model-pipeline/validate_glb.py"),
        str(OUT / "duneshade-desert-asp-reopened.glb"),
        "--reference", str(REFERENCE / "reference.npz"),
    ],
    check=True, capture_output=True, text=True,
)
(OUT / "reopened-validation.json").write_text(result.stdout)

# Studio presentation is deliberately separate from the exportable model scene.
bpy.ops.wm.open_mainfile(filepath=str(OUT / "duneshade-desert-asp.blend"))
for obj in bpy.context.scene.objects:
    if obj.type == "ARMATURE":
        obj.hide_render = True
        obj.hide_set(True)
scene = bpy.context.scene
scene["ftk_art_status"] = "Studio presentation only; export from duneshade-desert-asp.blend."
world = bpy.data.worlds.new("Twilight dunes studio")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.020, 0.008, 0.040, 1)
world.node_tree.nodes["Background"].inputs[1].default_value = 0.34
scene.world = world
bpy.ops.mesh.primitive_plane_add(size=80, location=(0, 3.0, -0.33))
floor = bpy.context.object
floor.name = "Studio floor - not export"
floor["ftk_export"] = False
floor_material = bpy.data.materials.new("Twilight dune floor")
floor_material.use_nodes = True
floor_material.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.070, 0.027, 0.055, 1)
floor_material.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.92
floor.data.materials.append(floor_material)


def aim(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def light(name, location, power, color, size, target=(0, 3.0, 0.30)):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = power
    data.color = color
    data.shape = "DISK"
    data.size = size
    obj = bpy.data.objects.new(name, data)
    scene.collection.objects.link(obj)
    obj.location = location
    aim(obj, target)


light("Moonlit dune key", (8, -9, 11), 2800, (0.46, 0.62, 1.0), 6)
light("Sunset sandstone fill", (-10, -8, 6), 1700, (1.0, 0.34, 0.15), 5)
light("Turquoise horizon rim", (0, 13, 10), 2600, (0.18, 0.92, 0.86), 6)
camera_data = bpy.data.cameras.new("Studio camera")
camera = bpy.data.objects.new("Studio camera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera
camera_data.type = "ORTHO"
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1000
scene.render.resolution_y = 1000
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.view_settings.look = "AgX - Medium High Contrast"

camera_data.ortho_scale = 16.6
camera.location = (10.5, -17.5, 9.5)
aim(camera, (0, 3.0, 0.30))
scene.render.filepath = str(OUT / "hero.png")
bpy.ops.render.render(write_still=True)

camera_data.ortho_scale = 15.8
camera.location = (-13.0, -15.5, 7.2)
aim(camera, (0, 3.1, 0.24))
scene.render.filepath = str(OUT / "side.png")
bpy.ops.render.render(write_still=True)

camera_data.ortho_scale = 3.9
camera.location = (5.3, -9.0, 4.7)
aim(camera, (0, -3.45, 0.50))
scene.render.filepath = str(OUT / "portrait.png")
bpy.ops.render.render(write_still=True)

bpy.ops.wm.save_as_mainfile(filepath=str(OUT / "duneshade-desert-asp-studio.blend"))
