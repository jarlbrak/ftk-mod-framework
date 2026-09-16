"""Render the original Cairnfire Troll source in a neutral local Blender studio.

This presentation imports only the authored source JSON and authored palette PNG.
It does not load a native mesh, texture, UV map, weights, or animation.
"""
from __future__ import annotations

import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


PACKAGE = Path(__file__).resolve().parent
SOURCE = PACKAGE / "cairnfire-trollb.source.json"
PALETTE = PACKAGE / "cairnfire-trollb.png"
STUDIO = PACKAGE.parents[2] / "scratch" / "cairnfire-trollb-studio.blend"


def unity_to_blender(point: list[float]) -> tuple[float, float, float]:
    """Convert authored Unity-local (x, y, z) coordinates to Blender space."""
    x, y, z = point
    return (x, -z, y)


def aim(object_: bpy.types.Object, target: tuple[float, float, float]) -> None:
    object_.rotation_euler = (Vector(target) - object_.location).to_track_quat("-Z", "Y").to_euler()


def area_light(
    scene: bpy.types.Scene,
    name: str,
    location: tuple[float, float, float],
    energy: float,
    color: tuple[float, float, float],
    size: float,
    target: tuple[float, float, float],
) -> None:
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.color = color
    data.shape = "DISK"
    data.size = size
    object_ = bpy.data.objects.new(name, data)
    scene.collection.objects.link(object_)
    object_.location = location
    aim(object_, target)


def build_mesh(scene: bpy.types.Scene) -> bpy.types.Object:
    data = json.loads(SOURCE.read_text())
    mesh = bpy.data.meshes.new("Cairnfire Troll B original geometry")
    mesh.from_pydata([unity_to_blender(point) for point in data["positions"]], [], data["triangles"])
    mesh.update()
    object_ = bpy.data.objects.new("Cairnfire Troll B", mesh)
    scene.collection.objects.link(object_)
    object_["ftk_art_status"] = "Original art preview only. Native game surfaces are absent."

    uv_layer = mesh.uv_layers.new(name="Palette UV")
    for loop in mesh.loops:
        u, v = data["uvs"][loop.vertex_index]
        # Mirror the runtime's required V flip so the studio preview uses the
        # same palette orientation that the Unity renderer receives.
        uv_layer.data[loop.index].uv = (u, 1.0 - v)

    image = bpy.data.images.load(str(PALETTE), check_existing=True)
    image.pack()
    material = bpy.data.materials.new("Cairnfire authored palette")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    principled = nodes.get("Principled BSDF")
    principled.inputs["Roughness"].default_value = 0.82
    principled.inputs["Metallic"].default_value = 0.18
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = image
    texture.interpolation = "Closest"
    texture.projection = "FLAT"
    links.new(texture.outputs["Color"], principled.inputs["Base Color"])
    mesh.materials.append(material)
    return object_


def add_floor(scene: bpy.types.Scene) -> None:
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0.0, 0.0, -0.145))
    floor = bpy.context.object
    floor.name = "Neutral studio floor"
    floor["ftk_export"] = False
    material = bpy.data.materials.new("Studio slate floor")
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (0.025, 0.038, 0.047, 1.0)
    shader.inputs["Roughness"].default_value = 0.91
    floor.data.materials.append(material)


def render(scene: bpy.types.Scene, camera: bpy.types.Object, name: str, location: tuple[float, float, float]) -> None:
    camera.location = location
    aim(camera, (0.0, -0.02, 1.78))
    scene.render.filepath = str(PACKAGE / name)
    bpy.ops.render.render(write_still=True)


def main() -> None:
    if not SOURCE.is_file() or not PALETTE.is_file():
        raise FileNotFoundError("Build Cairnfire Troll source and palette before rendering the studio.")

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene["ftk_art_status"] = "Original Cairnfire Troll source, neutral studio presentation"
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1080
    scene.render.resolution_y = 1080
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.image_settings.color_depth = "8"
    scene.render.resolution_percentage = 100
    scene.render.use_file_extension = True
    scene.render.fps = 24
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.world = bpy.data.worlds.new("Cairnfire studio atmosphere")
    scene.world.use_nodes = True
    world_background = scene.world.node_tree.nodes.get("Background")
    world_background.inputs["Color"].default_value = (0.016, 0.025, 0.036, 1.0)
    world_background.inputs["Strength"].default_value = 0.22

    build_mesh(scene)
    add_floor(scene)
    target = (0.0, -0.02, 1.78)
    area_light(scene, "Warm basalt key", (4.7, -5.7, 7.1), 1100, (1.0, 0.60, 0.31), 4.0, target)
    area_light(scene, "Cool slate fill", (-5.2, -3.3, 4.8), 860, (0.32, 0.62, 0.88), 4.5, target)
    area_light(scene, "Copper rim", (0.0, 4.8, 6.0), 1250, (1.0, 0.39, 0.16), 3.0, target)

    camera_data = bpy.data.cameras.new("Cairnfire studio camera")
    camera_data.lens = 58
    camera_data.sensor_width = 36
    camera = bpy.data.objects.new("Cairnfire studio camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    render(scene, camera, "cairnfire-trollb-hero.png", (7.1, -10.2, 5.2))
    render(scene, camera, "cairnfire-trollb-side.png", (-8.9, -5.6, 4.5))

    STUDIO.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(STUDIO))
    print(f"Rendered {PACKAGE / 'cairnfire-trollb-hero.png'}")
    print(f"Rendered {PACKAGE / 'cairnfire-trollb-side.png'}")
    print(f"Saved local studio {STUDIO}")


if __name__ == "__main__":
    main()
