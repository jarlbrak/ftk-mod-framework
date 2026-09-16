#!/usr/bin/env python3
"""Create editable Blender scenes and studio views for each Abyssal Kraken asset."""
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

CONFIG = {
    "head": {
        "asset": "abyssal-crown-kraken-head-v2",
        "texture": "abyssal-crown-kraken-head-v2.png",
        "renderer": 121035,
        "label": "Abyssal Crown V2, original modern Kraken head",
        "world": (0.004, 0.014, 0.026, 1),
        "key": (0.22, 0.92, 0.85),
        "fill": (1.0, 0.48, 0.20),
        "rim": (0.50, 0.22, 0.95),
        "front": True,
    },
    "tentacle": {
        "asset": "sargassum-kraken-tentacle-v2",
        "texture": "sargassum-kraken-tentacle-v2.png",
        "renderer": 121595,
        "label": "Sargassum Lash V2, original Kraken tentacle",
        "world": (0.003, 0.021, 0.023, 1),
        "key": (0.20, 0.88, 0.72),
        "fill": (0.16, 0.46, 1.0),
        "rim": (1.0, 0.72, 0.28),
        "front": False,
    },
    "seaking": {
        "asset": "royal-sargassum-seaking-tentacle-v3",
        "texture": "royal-sargassum-seaking-tentacle-v3.png",
        "renderer": 121315,
        "label": "Royal Sargassum V3, original Sea King tentacle",
        "world": (0.012, 0.006, 0.026, 1),
        "key": (0.34, 0.26, 0.95),
        "fill": (0.10, 0.78, 0.74),
        "rim": (1.0, 0.66, 0.20),
        "front": False,
    },
}
PYTHON = ROOT / "scratch/model-venv/bin/python"


def get_target() -> str:
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if len(args) != 1 or args[0] not in CONFIG:
        raise ValueError("Usage: build_blender.py -- head|tentacle|seaking")
    return args[0]


def import_original(asset: str, texture: str, armature):
    data = json.loads((OUT / f"{asset}.source.json").read_text())
    mesh = bpy.data.meshes.new(asset)
    # FTK mesh local (x, y, z) maps to Blender (x, -z, y).
    mesh.from_pydata([(x, -z, y) for x, y, z in data["positions"]], [], data["triangles"])
    mesh.update()
    obj = bpy.data.objects.new(asset, mesh)
    bpy.context.scene.collection.objects.link(obj)
    uv = mesh.uv_layers.new(name="Abyssal Kraken palette")
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
    image = bpy.data.images.load(str(OUT / texture), check_existing=True)
    image.pack()
    material = bpy.data.materials.new(asset + " original palette")
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Roughness"].default_value = 0.70
    texture_node = material.node_tree.nodes.new("ShaderNodeTexImage")
    texture_node.image = image
    texture_node.interpolation = "Closest"
    material.node_tree.links.new(texture_node.outputs["Color"], shader.inputs["Base Color"])
    mesh.materials.append(material)
    return obj


def bounds_for(obj):
    low = Vector((float("inf"), float("inf"), float("inf")))
    high = Vector((float("-inf"), float("-inf"), float("-inf")))
    for vertex in obj.data.vertices:
        point = obj.matrix_world @ vertex.co
        low.x, low.y, low.z = min(low.x, point.x), min(low.y, point.y), min(low.z, point.z)
        high.x, high.y, high.z = max(high.x, point.x), max(high.y, point.y), max(high.z, point.z)
    return low, high


def aim(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def add_area(scene, label, location, energy, color, size, target):
    data = bpy.data.lights.new(label, "AREA")
    data.energy = energy
    data.color = color
    data.shape = "DISK"
    data.size = size
    light = bpy.data.objects.new(label, data)
    scene.collection.objects.link(light)
    light.location = location
    aim(light, target)


def render_studio(asset: str, config, model) -> None:
    for obj in bpy.context.scene.objects:
        if obj.type == "ARMATURE":
            obj.hide_render = True
            obj.hide_set(True)
    scene = bpy.context.scene
    scene["ftk_art_status"] = "Studio presentation only. Export from the source bind scene."
    low, high = bounds_for(model)
    center = (low + high) * .5
    span = max(high.x - low.x, high.y - low.y, high.z - low.z, 1.0)
    world = bpy.data.worlds.new("Abyssal studio")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = config["world"]
    world.node_tree.nodes["Background"].inputs[1].default_value = .72
    scene.world = world
    bpy.ops.mesh.primitive_plane_add(size=span * 8, location=(center.x, center.y, low.z - span * .07))
    floor = bpy.context.object
    floor.name = "Studio floor, not export"
    floor["ftk_export"] = False
    material = bpy.data.materials.new("Abyssal studio floor")
    material.use_nodes = True
    material.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = config["world"]
    material.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = .92
    floor.data.materials.append(material)
    add_area(scene, "Sea glass key", center + Vector((span * 1.25, -span * 1.70, span * 1.45)), 9000, config["key"], span * .72, center)
    add_area(scene, "Copper fill", center + Vector((-span * 1.55, -span * .90, span * .75)), 6200, config["fill"], span * .62, center)
    add_area(scene, "Violet rim", center + Vector((0, span * 1.75, span * 1.20)), 8500, config["rim"], span * .78, center)
    camera_data = bpy.data.cameras.new("Studio camera")
    camera = bpy.data.objects.new("Studio camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = span * 1.30
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1000
    scene.render.resolution_y = 1000
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.look = "AgX - Medium High Contrast"
    camera.location = center + (Vector((span * .34, span * 1.75, span * .50)) if config["front"] else Vector((span * 1.15, -span * 1.75, span * .75)))
    aim(camera, center)
    scene.render.filepath = str(OUT / f"{asset}-hero.png")
    bpy.ops.render.render(write_still=True)
    camera.location = center + (Vector((-span * 1.55, span * 1.25, span * .55)) if config["front"] else Vector((-span * 1.55, -span * 1.25, span * .55)))
    aim(camera, center)
    scene.render.filepath = str(OUT / f"{asset}-side.png")
    bpy.ops.render.render(write_still=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT / f"{asset}-studio.blend"))


def main() -> None:
    target = get_target()
    config = CONFIG[target]
    asset = config["asset"]
    reference = ROOT / f"scratch/skeleton-audit/{config['renderer']}"
    scratch = ROOT / f"scratch/abyssal-kraken-{target}-roundtrip"
    scratch.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    create(reference / "reference.npz", reference / "skeleton.json", scratch / f"{asset}-template.blend", PYTHON, False)
    armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    for obj in list(bpy.context.scene.objects):
        if obj.type == "MESH":
            bpy.data.objects.remove(obj, do_unlink=True)
    model = import_original(asset, config["texture"], armature)
    bpy.context.scene["ftk_art_status"] = config["label"] + ". Live validation pending."
    bpy.context.view_layer.objects.active = model
    model.select_set(True)
    armature.select_set(False)
    source_blend = OUT / f"{asset}.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(source_blend))
    bpy.ops.wm.open_mainfile(filepath=str(source_blend))
    export(OUT / f"{asset}-reopened.glb")
    result = subprocess.run(
        [str(PYTHON), str(ROOT / "tools/ai-model-pipeline/validate_glb.py"), str(OUT / f"{asset}-reopened.glb"), "--reference", str(reference / "reference.npz")],
        check=True, capture_output=True, text=True,
    )
    (OUT / f"{asset}-reopened-validation.json").write_text(result.stdout)
    bpy.ops.wm.open_mainfile(filepath=str(source_blend))
    model = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH" and obj.get("ftk_export"))
    render_studio(asset, config, model)
    print(json.dumps({"status": "PASS_ABYSSAL_KRAKEN_BLEND_REOPEN_EXPORT", "target": target, "asset": asset}, indent=2))


if __name__ == "__main__":
    main()
