#!/usr/bin/env python3
"""Render Hearthveil's authored source in a neutral local Blender studio.

This presentation reads only original source JSON and the authored palette.  It
never imports a native FTK mesh, texture, UV map, weight table, or animation.
"""
from __future__ import annotations

import json
from pathlib import Path

import bpy
from mathutils import Vector


PACKAGE = Path(__file__).resolve().parent
PALETTE = PACKAGE / "hearthveil-palette.png"
ASSETS = {
    "body": ("Hearthveil body", PACKAGE / "hearthveil-body.source.json", (0.0, 0.0, 0.0)),
    "hair_top": ("Hearthveil hair top", PACKAGE / "hearthveil-hair-top.source.json", (0.0, 0.0, 0.0)),
    "hair_bottom": ("Hearthveil hair bottom", PACKAGE / "hearthveil-hair-bottom.source.json", (0.0, 0.0, 0.0)),
    "default_armor": ("Hearthveil default armor", PACKAGE / "hearthveil-default-armor.source.json", (0.0, 0.0, 0.0)),
    "boots": ("Hearthveil boots", PACKAGE / "hearthveil-boots.source.json", (0.0, 0.0, 0.0)),
    # The equipped apparel renderer has a -3 mesh-local X bind offset. The
    # studio normalizes only that authored source coordinate so it can be
    # reviewed beside the body. It does not import a game mesh or transform.
    "gambeson": ("Hearthveil gambeson", PACKAGE / "hearthveil-gambeson.source.json", (3.0, 0.0, 0.0)),
}
STUDIO = PACKAGE.parents[2] / "scratch" / "hearthveil-blacksmith-studio.blend"


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


def authored_material(image: bpy.types.Image) -> bpy.types.Material:
    material = bpy.data.materials.new("Hearthveil authored palette")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    shader = nodes.get("Principled BSDF")
    shader.inputs["Roughness"].default_value = 0.79
    shader.inputs["Metallic"].default_value = 0.24
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = image
    texture.interpolation = "Closest"
    texture.projection = "FLAT"
    links.new(texture.outputs["Color"], shader.inputs["Base Color"])
    return material


def build_mesh(
    scene: bpy.types.Scene,
    name: str,
    source: Path,
    material: bpy.types.Material,
    display_offset: tuple[float, float, float],
) -> bpy.types.Object:
    data = json.loads(source.read_text())
    mesh = bpy.data.meshes.new(name + " original geometry")
    mesh.from_pydata([unity_to_blender(point) for point in data["positions"]], [], data["triangles"])
    mesh.update()
    object_ = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(object_)
    object_.location = display_offset
    object_["ftk_art_status"] = "Original art preview only. Native game surfaces are absent."

    uv_layer = mesh.uv_layers.new(name="Authored palette UV")
    for loop in mesh.loops:
        u, v = data["uvs"][loop.vertex_index]
        # The authored palette is horizontally striped, so its colors are
        # deliberately invariant to the Unity exporter's required V flip.
        uv_layer.data[loop.index].uv = (u, 1.0 - v)
    mesh.materials.append(material)
    return object_


def add_floor(scene: bpy.types.Scene) -> None:
    bpy.ops.mesh.primitive_cylinder_add(vertices=96, radius=2.15, depth=0.18, location=(0.0, 0.0, -0.09))
    floor = bpy.context.object
    floor.name = "Neutral studio plinth"
    material = bpy.data.materials.new("Studio forge slate")
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (0.019, 0.032, 0.042, 1.0)
    shader.inputs["Roughness"].default_value = 0.88
    floor.data.materials.append(material)


def render(
    scene: bpy.types.Scene,
    camera: bpy.types.Object,
    filename: str,
    location: tuple[float, float, float],
    target: tuple[float, float, float],
) -> None:
    camera.location = location
    aim(camera, target)
    scene.render.filepath = str(PACKAGE / filename)
    bpy.ops.render.render(write_still=True)


def show_only(objects: dict[str, bpy.types.Object], visible: set[str]) -> None:
    for key, object_ in objects.items():
        object_.hide_render = key not in visible


def main() -> None:
    missing = [str(source) for _, source, _ in ASSETS.values() if not source.is_file()]
    if missing or not PALETTE.is_file():
        raise FileNotFoundError("Build Hearthveil source and palette before rendering the studio: " + ", ".join(missing))

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene["ftk_art_status"] = "Original Hearthveil source, neutral studio presentation"
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1080
    scene.render.resolution_y = 1080
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.film_transparent = False
    scene.render.use_file_extension = True
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.world = bpy.data.worlds.new("Hearthveil studio atmosphere")
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.010, 0.018, 0.027, 1.0)
    background.inputs["Strength"].default_value = 0.19

    image = bpy.data.images.load(str(PALETTE), check_existing=True)
    image.pack()
    material = authored_material(image)
    objects = {
        key: build_mesh(scene, name, source, material, display_offset)
        for key, (name, source, display_offset) in ASSETS.items()
    }
    add_floor(scene)

    target = (0.0, 0.0, 1.24)
    area_light(scene, "Amber forge key", (4.8, -5.8, 5.8), 1280, (1.0, 0.48, 0.18), 3.5, target)
    area_light(scene, "Patina fill", (-4.8, -3.5, 4.3), 920, (0.22, 0.68, 0.73), 4.5, target)
    area_light(scene, "Furnace rim", (0.1, 4.6, 5.4), 1060, (1.0, 0.26, 0.09), 2.8, target)

    camera_data = bpy.data.cameras.new("Hearthveil studio camera")
    camera_data.lens = 58
    camera_data.sensor_width = 36
    camera = bpy.data.objects.new("Hearthveil studio camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    default_outfit = {"body", "hair_top", "hair_bottom", "default_armor", "boots"}
    equipped_outfit = {"body", "hair_top", "hair_bottom", "gambeson", "boots"}
    show_only(objects, default_outfit)
    render(scene, camera, "hearthveil-hero.png", (3.7, -6.8, 2.9), target)
    render(scene, camera, "hearthveil-side.png", (-5.8, -4.0, 2.6), target)
    show_only(objects, equipped_outfit)
    render(scene, camera, "hearthveil-gambeson-hero.png", (3.7, -6.8, 2.9), target)
    render(scene, camera, "hearthveil-gambeson-side.png", (-5.8, -4.0, 2.6), target)

    STUDIO.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(STUDIO))
    print(f"Rendered {PACKAGE / 'hearthveil-hero.png'}")
    print(f"Rendered {PACKAGE / 'hearthveil-side.png'}")
    print(f"Rendered {PACKAGE / 'hearthveil-gambeson-hero.png'}")
    print(f"Rendered {PACKAGE / 'hearthveil-gambeson-side.png'}")
    print(f"Saved local studio {STUDIO}")


if __name__ == "__main__":
    main()
