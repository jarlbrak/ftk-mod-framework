#!/usr/bin/env python3
"""Create editable Blender source and studio views for the rigid Kraken eye companion."""
from __future__ import annotations

import json
from pathlib import Path

import bpy
from mathutils import Vector

OUT = Path(__file__).resolve().parent
ASSET = "abyssal-crown-kraken-eye-v3"
SOURCE = OUT / f"{ASSET}.source.json"
BLEND = OUT / f"{ASSET}.blend"
STUDIO = OUT / f"{ASSET}-studio.blend"
VALIDATION = OUT / f"{ASSET}-blend-validation.json"


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


def add_area(label, location, energy, color, size, target):
    data = bpy.data.lights.new(label, "AREA")
    data.energy = energy
    data.color = color
    data.shape = "DISK"
    data.size = size
    light = bpy.data.objects.new(label, data)
    bpy.context.scene.collection.objects.link(light)
    light.location = location
    aim(light, target)


def make_source():
    data = json.loads(SOURCE.read_text())
    mesh = bpy.data.meshes.new(ASSET)
    # The shared project convention maps FTK (x, y, z) to Blender (x, -z, y).
    mesh.from_pydata([(x, -z, y) for x, y, z in data["positions"]], [], data["triangles"])
    mesh.update()
    obj = bpy.data.objects.new(ASSET, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj["ftk_export"] = True
    obj["ftk_renderer_kind"] = "MeshRenderer"
    obj["ftk_local_space"] = "Root_M/base/body/neck/eye/kraken2_eye"
    obj["ftk_art_status"] = "Original static companion. Export through build_static_eye.py."
    uv = mesh.uv_layers.new(name="Abyssal eye original UV")
    for loop in mesh.loops:
        u, v = data["uvs"][loop.vertex_index]
        uv.data[loop.index].uv = (u, 1.0 - v)
    image = bpy.data.images.load(str(OUT / f"{ASSET}.png"), check_existing=True)
    image.pack()
    material = bpy.data.materials.new("Abyssal eye original palette")
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Roughness"].default_value = .72
    texture = material.node_tree.nodes.new("ShaderNodeTexImage")
    texture.image = image
    texture.interpolation = "Closest"
    material.node_tree.links.new(texture.outputs["Color"], shader.inputs["Base Color"])
    mesh.materials.append(material)
    return obj, data


def render_studio(obj):
    scene = bpy.context.scene
    scene["ftk_art_status"] = "Studio presentation only. The source object remains MeshFilter local space."
    low, high = bounds_for(obj)
    center = (low + high) * .5
    span = max(high.x - low.x, high.y - low.y, high.z - low.z, 1.0)
    world = bpy.data.worlds.new("Abyssal eye studio")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.003, 0.012, 0.025, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = .62
    scene.world = world
    bpy.ops.mesh.primitive_plane_add(size=span * 8, location=(center.x, center.y, low.z - span * .16))
    floor = bpy.context.object
    floor.name = "Studio floor, not export"
    floor["ftk_export"] = False
    floor_material = bpy.data.materials.new("Abyssal eye studio floor")
    floor_material.use_nodes = True
    floor_material.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.005, .022, .035, 1)
    floor_material.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = .94
    floor.data.materials.append(floor_material)
    add_area("Sea-glass key", center + Vector((span * 1.25, -span * 1.6, span * 1.4)), 8500, (.18, .93, .78), span * .7, center)
    add_area("Antique-gold fill", center + Vector((-span * 1.45, -span * .7, span * .75)), 5600, (1.0, .52, .17), span * .58, center)
    add_area("Violet rim", center + Vector((0, span * 1.65, span * 1.15)), 7000, (.48, .20, .95), span * .7, center)
    camera_data = bpy.data.cameras.new("Studio camera")
    camera = bpy.data.objects.new("Studio camera", camera_data)
    bpy.context.scene.collection.objects.link(camera)
    scene.camera = camera
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = span * 1.38
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1000
    scene.render.resolution_y = 1000
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.look = "AgX - Medium High Contrast"
    camera.location = center + Vector((span * .4, -span * 1.8, span * .5))
    aim(camera, center)
    scene.render.filepath = str(OUT / f"{ASSET}-hero.png")
    bpy.ops.render.render(write_still=True)
    camera.location = center + Vector((-span * 1.55, span * 1.2, span * .62))
    aim(camera, center)
    scene.render.filepath = str(OUT / f"{ASSET}-side.png")
    bpy.ops.render.render(write_still=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(STUDIO))


def main():
    bpy.context.preferences.filepaths.save_version = 0
    obj, data = make_source()
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND))
    bpy.ops.wm.open_mainfile(filepath=str(BLEND))
    reopened = bpy.data.objects.get(ASSET)
    assert reopened is not None and reopened.get("ftk_export")
    assert len(reopened.data.vertices) == len(data["positions"])
    assert len(reopened.data.polygons) == len(data["triangles"])
    VALIDATION.write_text(json.dumps({
        "status": "PASS_ABYSSAL_KRAKEN_STATIC_EYE_BLEND_REOPEN",
        "asset": ASSET,
        "vertices": len(reopened.data.vertices),
        "triangles": len(reopened.data.polygons),
        "rendererKind": reopened["ftk_renderer_kind"],
        "localSpace": reopened["ftk_local_space"],
        "scope": "Editable source save/reopen proof only. The supported runtime GLB is written by build_static_eye.py and needs a fresh isolated-game trial.",
    }, indent=2) + "\n")
    render_studio(reopened)
    print(json.dumps({"status": "PASS_ABYSSAL_KRAKEN_STATIC_EYE_BLEND_REOPEN", "asset": ASSET}, indent=2))


if __name__ == "__main__":
    main()
