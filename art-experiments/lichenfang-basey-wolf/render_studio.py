#!/usr/bin/env python3
"""Render the authored Lichenfang bind-pose sculpture for studio review only.

This reads the package's authored JSON and authored palette. Native FTK loading,
material response, culling and motion still need an isolated live trial.
"""
from __future__ import annotations

import json
from pathlib import Path

import bpy
from mathutils import Vector

PACKAGE = Path(__file__).resolve().parent


def unity_to_blender(point: list[float]) -> tuple[float, float, float]:
    return point[0], -point[2], point[1]


def look_at(camera: bpy.types.Object, target: tuple[float, float, float]) -> None:
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()


def make_creature() -> bpy.types.Object:
    source = json.loads((PACKAGE / "lichenfang.source.json").read_text())
    mesh = bpy.data.meshes.new("Lichenfang Prowler Original Bind Mesh")
    mesh.from_pydata([unity_to_blender(point) for point in source["positions"]], [], source["triangles"])
    mesh.update()
    uv = mesh.uv_layers.new(name="UVMap")
    for loop in mesh.loops:
        value = source["uvs"][loop.vertex_index]
        uv.data[loop.index].uv = (value[0], 1.0 - value[1])
    creature = bpy.data.objects.new("Lichenfang Prowler", mesh)
    bpy.context.scene.collection.objects.link(creature)
    material = bpy.data.materials.new("Lichenfang Palette")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    bsdf.inputs["Roughness"].default_value = 0.69
    bsdf.inputs["Specular IOR Level"].default_value = 0.22
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = bpy.data.images.load(str(PACKAGE / "lichenfang-palette.png"))
    texture.interpolation = "Closest"
    links.new(texture.outputs["Color"], bsdf.inputs["Base Color"])
    creature.data.materials.append(material)
    return creature


def add_area(name: str, location: tuple[float, float, float], energy: float, color: tuple[float, float, float],
             size: float, target: tuple[float, float, float]) -> None:
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.color = color
    data.shape = "DISK"
    data.size = size
    light = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(light)
    light.location = location
    light.rotation_euler = (Vector(target) - light.location).to_track_quat("-Z", "Y").to_euler()


def render(name: str, location: tuple[float, float, float], target: tuple[float, float, float]) -> None:
    data = bpy.data.cameras.new(name)
    camera = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(camera)
    camera.location = location
    data.lens = 62
    look_at(camera, target)
    bpy.context.scene.camera = camera
    bpy.context.scene.render.filepath = str(PACKAGE / f"{name.lower().replace(' ', '-')}.png")
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(camera, do_unlink=True)


def main() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 800
    scene.render.resolution_y = 800
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.view_settings.look = "AgX - Medium High Contrast"
    world = bpy.data.worlds.new("Lichenfang Studio World")
    scene.world = world
    world.color = (0.006, 0.014, 0.012)
    creature = make_creature()
    target = (0.0, -0.10, 0.78)
    bpy.ops.mesh.primitive_plane_add(size=30, location=(0.0, 0.0, -0.045))
    ground = bpy.context.object
    ground.name = "Lichenfang Studio Ground"
    ground_material = bpy.data.materials.new("Lichenfang Studio Ground")
    ground_material.diffuse_color = (0.010, 0.026, 0.020, 1.0)
    ground.data.materials.append(ground_material)
    add_area("Amber key", (4.6, -5.8, 6.7), 1220, (1.0, 0.61, 0.30), 4.7, target)
    add_area("Moss rim", (-5.3, 2.9, 5.2), 1040, (0.26, 0.90, 0.60), 4.2, target)
    add_area("Rain fill", (0.2, 4.6, 7.8), 620, (0.45, 0.72, 0.78), 3.4, target)
    render("lichenfang-hero", (4.85, -6.95, 3.55), target)
    render("lichenfang-side", (7.55, 0.25, 2.85), target)
    print(f"Rendered {creature.name} studio views")


if __name__ == "__main__":
    main()
