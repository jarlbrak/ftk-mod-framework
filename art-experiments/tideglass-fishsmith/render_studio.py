#!/usr/bin/env python3
"""Render Tideglass Fishsmith's original source in a neutral Blender studio."""
from __future__ import annotations

import json
from pathlib import Path

import bpy
from mathutils import Vector

PACKAGE = Path(__file__).resolve().parent
PALETTE = PACKAGE / "tideglass-palette.png"
ASSETS = {
    "body": ("Tideglass body", PACKAGE / "tideglass-body.source.json"),
    "hair_top": ("Tideglass crest", PACKAGE / "tideglass-hair-top.source.json"),
    "hair_bottom": ("Tideglass mantle", PACKAGE / "tideglass-hair-bottom.source.json"),
}
STUDIO = PACKAGE.parents[2] / "scratch" / "tideglass-fishsmith-studio.blend"


def unity_to_blender(point: list[float]) -> tuple[float, float, float]:
    x, y, z = point
    return (x, -z, y)


def aim(object_: bpy.types.Object, target: tuple[float, float, float]) -> None:
    object_.rotation_euler = (Vector(target) - object_.location).to_track_quat("-Z", "Y").to_euler()


def material(image: bpy.types.Image) -> bpy.types.Material:
    value = bpy.data.materials.new("Tideglass authored palette")
    value.use_nodes = True
    nodes = value.node_tree.nodes
    links = value.node_tree.links
    shader = nodes.get("Principled BSDF")
    shader.inputs["Roughness"].default_value = 0.63
    shader.inputs["Metallic"].default_value = 0.23
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = image
    texture.interpolation = "Closest"
    links.new(texture.outputs["Color"], shader.inputs["Base Color"])
    return value


def mesh_object(scene: bpy.types.Scene, name: str, source: Path, authored_material: bpy.types.Material) -> bpy.types.Object:
    data = json.loads(source.read_text())
    mesh = bpy.data.meshes.new(name + " original geometry")
    mesh.from_pydata([unity_to_blender(point) for point in data["positions"]], [], data["triangles"])
    mesh.update()
    object_ = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(object_)
    object_["ftk_art_status"] = "Original art studio preview only; no game geometry is present."
    uv = mesh.uv_layers.new(name="Authored palette UV")
    for loop in mesh.loops:
        u, v = data["uvs"][loop.vertex_index]
        uv.data[loop.index].uv = (u, 1.0 - v)
    mesh.materials.append(authored_material)
    return object_


def area(scene: bpy.types.Scene, name: str, location: tuple[float, float, float], energy: float,
         color: tuple[float, float, float], size: float, target: tuple[float, float, float]) -> None:
    light_data = bpy.data.lights.new(name, "AREA")
    light_data.energy = energy
    light_data.color = color
    light_data.shape = "DISK"
    light_data.size = size
    light = bpy.data.objects.new(name, light_data)
    scene.collection.objects.link(light)
    light.location = location
    aim(light, target)


def floor(scene: bpy.types.Scene) -> None:
    bpy.ops.mesh.primitive_cylinder_add(vertices=96, radius=2.2, depth=0.16, location=(0.0, 0.0, 0.60))
    object_ = bpy.context.object
    object_.name = "Neutral tideglass-studio plinth"
    value = bpy.data.materials.new("Tideglass studio stone")
    value.use_nodes = True
    shader = value.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (0.012, 0.025, 0.040, 1.0)
    shader.inputs["Roughness"].default_value = 0.86
    object_.data.materials.append(value)


def render(scene: bpy.types.Scene, camera: bpy.types.Object, name: str, location: tuple[float, float, float],
           target: tuple[float, float, float]) -> None:
    camera.location = location
    aim(camera, target)
    scene.render.filepath = str(PACKAGE / name)
    bpy.ops.render.render(write_still=True)


def main() -> None:
    if not PALETTE.is_file() or any(not source.is_file() for _, source in ASSETS.values()):
        raise FileNotFoundError("Run build_geometry.py before rendering Tideglass.")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene["ftk_art_status"] = "Original Tideglass Fishsmith source in a neutral studio"
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1080
    scene.render.resolution_y = 1080
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.world = bpy.data.worlds.new("Tideglass studio atmosphere")
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.004, 0.012, 0.026, 1.0)
    background.inputs["Strength"].default_value = 0.20
    image = bpy.data.images.load(str(PALETTE), check_existing=True)
    image.pack()
    authored = material(image)
    for name, source in ASSETS.values():
        mesh_object(scene, name, source, authored)
    floor(scene)
    target = (0.0, -0.02, 1.67)
    area(scene, "Brass key", (4.8, -5.2, 5.7), 1180, (1.0, 0.64, 0.29), 3.4, target)
    area(scene, "Seafoam fill", (-4.8, -3.2, 4.5), 1100, (0.18, 0.78, 0.70), 4.8, target)
    area(scene, "Coral rim", (0.3, 4.8, 5.2), 900, (0.95, 0.28, 0.24), 3.2, target)
    camera_data = bpy.data.cameras.new("Tideglass studio camera")
    camera_data.lens = 58
    camera = bpy.data.objects.new("Tideglass studio camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    render(scene, camera, "tideglass-hero.png", (3.8, -6.9, 3.25), target)
    render(scene, camera, "tideglass-side.png", (-5.8, -4.1, 3.0), target)
    STUDIO.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(STUDIO))
    print(f"Rendered {PACKAGE / 'tideglass-hero.png'}")
    print(f"Rendered {PACKAGE / 'tideglass-side.png'}")
    print(f"Saved local studio {STUDIO}")


if __name__ == "__main__":
    main()
