#!/usr/bin/env python3
"""Render the authored Rivenquill bind-pose sculpture for studio review only."""
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
    source = json.loads((PACKAGE / "rivenquill.source.json").read_text())
    mesh = bpy.data.meshes.new("Rivenquill Cockatrice Original Bind Mesh")
    mesh.from_pydata([unity_to_blender(point) for point in source["positions"]], [], source["triangles"])
    mesh.update()
    uv = mesh.uv_layers.new(name="UVMap")
    for loop in mesh.loops:
        value = source["uvs"][loop.vertex_index]
        uv.data[loop.index].uv = (value[0], 1.0 - value[1])
    obj = bpy.data.objects.new("Rivenquill Cockatrice", mesh)
    bpy.context.scene.collection.objects.link(obj)
    material = bpy.data.materials.new("Rivenquill Palette")
    material.use_nodes = True
    nodes, links = material.node_tree.nodes, material.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    bsdf.inputs["Roughness"].default_value = .68
    bsdf.inputs["Specular IOR Level"].default_value = .24
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = bpy.data.images.load(str(PACKAGE / "rivenquill-palette.png"))
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


def render(name: str, location: tuple[float, float, float], target: tuple[float, float, float], lens: float = 52) -> None:
    data = bpy.data.cameras.new(name)
    camera = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(camera)
    camera.location, data.lens = location, lens
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
    world = bpy.data.worlds.new("Rivenquill Studio World")
    scene.world = world
    world.color = (.006, .009, .014)
    obj = creature()
    target = (0.0, .7, 2.05)
    bpy.ops.mesh.primitive_plane_add(size=36, location=(0.0, .7, -.04))
    ground = bpy.context.object
    ground.name = "Rivenquill Studio Ground"
    material = bpy.data.materials.new("Rivenquill Studio Ground")
    material.diffuse_color = (.010, .021, .026, 1.0)
    ground.data.materials.append(material)
    area("Copper key", (7.8, -8.5, 9.5), 1840, (1.0, .57, .28), 5.8, target)
    area("Teal rim", (-7.2, 4.0, 7.0), 1590, (.20, .82, .72), 5.5, target)
    area("High fill", (0.0, 4.8, 10.5), 970, (.42, .58, 1.0), 4.5, target)
    render("rivenquill-hero", (8.3, -10.5, 6.2), target, 54)
    # The tail spans nearly the full articulated rig; the wider lens keeps the
    # complete bind-pose silhouette in the review frame.
    render("rivenquill-side", (10.5, 1.0, 4.7), target, 30)
    print(f"Rendered {obj.name} studio views")


if __name__ == "__main__":
    main()
