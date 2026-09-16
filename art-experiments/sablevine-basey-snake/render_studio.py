#!/usr/bin/env python3
"""Render the authored Sablevine bind-pose sculpture for studio review only."""
from __future__ import annotations

import json
from pathlib import Path

import bpy
from mathutils import Vector

PACKAGE = Path(__file__).resolve().parent


def unity_to_blender(point: list[float]) -> tuple[float, float, float]:
    return point[0], -point[2], point[1]


def orient(obj: bpy.types.Object, target: tuple[float, float, float]) -> None:
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def creature() -> bpy.types.Object:
    source = json.loads((PACKAGE / "sablevine.source.json").read_text())
    mesh = bpy.data.meshes.new("Sablevine Serpent Original Bind Mesh")
    mesh.from_pydata([unity_to_blender(point) for point in source["positions"]], [], source["triangles"])
    mesh.update()
    uv = mesh.uv_layers.new(name="UVMap")
    for loop in mesh.loops:
        value = source["uvs"][loop.vertex_index]
        uv.data[loop.index].uv = (value[0], 1.0 - value[1])
    obj = bpy.data.objects.new("Sablevine Serpent", mesh)
    bpy.context.scene.collection.objects.link(obj)
    material = bpy.data.materials.new("Sablevine Palette")
    material.use_nodes = True
    nodes, links = material.node_tree.nodes, material.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    bsdf.inputs["Roughness"].default_value = .70
    bsdf.inputs["Specular IOR Level"].default_value = .21
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = bpy.data.images.load(str(PACKAGE / "sablevine-palette.png"))
    texture.interpolation = "Closest"
    links.new(texture.outputs["Color"], bsdf.inputs["Base Color"])
    obj.data.materials.append(material)
    return obj


def area(name: str, location: tuple[float, float, float], energy: float, color: tuple[float, float, float],
         size: float, target: tuple[float, float, float]) -> None:
    data = bpy.data.lights.new(name, "AREA")
    data.energy, data.color, data.shape, data.size = energy, color, "DISK", size
    light = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(light)
    light.location = location
    orient(light, target)


def render(name: str, location: tuple[float, float, float], target: tuple[float, float, float], lens: float = 48, size: tuple[int, int] = (900, 900)) -> None:
    data = bpy.data.cameras.new(name)
    camera = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(camera)
    camera.location, data.lens = location, lens
    bpy.context.scene.render.resolution_x, bpy.context.scene.render.resolution_y = size
    orient(camera, target)
    bpy.context.scene.camera = camera
    bpy.context.scene.render.filepath = str(PACKAGE / f"{name.lower().replace(' ', '-')}.png")
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(camera, do_unlink=True)


def main() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.look = "AgX - Medium High Contrast"
    world = bpy.data.worlds.new("Sablevine Studio World")
    scene.world = world
    world.color = (.006, .009, .011)
    obj = creature()
    target = (0.0, 3.10, .30)
    bpy.ops.mesh.primitive_plane_add(size=36, location=(0.0, 3.1, -.04))
    ground = bpy.context.object
    ground.name = "Sablevine Studio Ground"
    ground_material = bpy.data.materials.new("Sablevine Studio Ground")
    ground_material.diffuse_color = (.011, .021, .019, 1.0)
    ground.data.materials.append(ground_material)
    area("Amber head key", (5.0, -7.3, 7.4), 1540, (1.0, .58, .28), 5.4, target)
    area("Moss tail rim", (-5.6, 11.8, 6.1), 1370, (.22, .82, .55), 6.0, target)
    area("Cool canopy", (0.0, 2.5, 9.5), 940, (.42, .70, .83), 5.0, target)
    render("sablevine-hero", (4.8, -7.4, 3.1), (0.0, -2.55, .34), 54)
    render("sablevine-side", (23.0, 3.1, 3.0), target, 48, (1200, 360))
    print(f"Rendered {obj.name} studio views")


if __name__ == "__main__":
    main()
