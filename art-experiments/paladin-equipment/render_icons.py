#!/usr/bin/env python3
"""Render transparent UI icons from this project's original mesh sources."""
import hashlib
import json
import sys
from pathlib import Path
import bpy
from mathutils import Matrix,Vector

OUT=Path(__file__).resolve().parent
ROOT=OUT.parents[1]
CHAR=ROOT/"art-experiments/paladin-characters"
EQUIPMENT_COLORS=["233344","66829B","C7D7DD","936735","E2B458","294C85","702C37","261F2A","83C9CA","EBE1B8"]
CHARACTER_COLORS=["172633","51687B","B8C9CE","97662D","E3B658","254C86","762535","201D28","81CDD4","EEE1B7","B68D70","3D2C25"]


def aim(obj,target):obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()


def reset(colors):
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    materials=[]
    for color in colors:
        mat=bpy.data.materials.new("Original icon "+color);mat.use_nodes=True
        node=mat.node_tree.nodes.get("Principled BSDF")
        def linear(v):return v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4
        node.inputs['Base Color'].default_value=tuple(linear(int(color[i:i+2],16)/255) for i in [0,2,4])+(1,)
        node.inputs['Metallic'].default_value=.35;node.inputs['Roughness'].default_value=.42
        materials.append(mat)
    return materials


def add_source(path,materials,shift=Vector((0,0,0))):
    data=json.loads(path.read_text())
    author_transform=Matrix(data['authorToRuntime']).inverted() if 'authorToRuntime' in data else Matrix.Identity(4)
    points=[]
    for v in data['positions']:
        p=(author_transform@Vector(v))+shift;points.append((p.x,-p.z,p.y))
    mesh=bpy.data.meshes.new(path.stem);mesh.from_pydata(points,[],data['triangles']);mesh.update()
    obj=bpy.data.objects.new(path.stem,mesh);bpy.context.collection.objects.link(obj)
    for material in materials:mesh.materials.append(material)
    for face in mesh.polygons:face.material_index=min(len(materials)-1,int(data['uvs'][face.vertices[0]][0]*len(materials)))
    return [Vector(p) for p in points]


def render(path,points,portrait=False):
    low=Vector(tuple(min(p[i] for p in points) for i in range(3)))
    high=Vector(tuple(max(p[i] for p in points) for i in range(3)))
    target=(low+high)/2
    span=max(high.x-low.x,high.z-low.z)*1.22
    if portrait:
        span=1.45
        target.z=high.z-.60
    bpy.ops.object.camera_add(location=target+Vector((span*.25,-span*4,span*.28)))
    camera=bpy.context.object;camera.data.type='ORTHO';camera.data.ortho_scale=span;aim(camera,target)
    scene=bpy.context.scene;scene.camera=camera;scene.render.engine='CYCLES';scene.cycles.samples=24
    scene.render.film_transparent=True
    scene.world.color=(.18,.18,.18)
    for delta,energy in [((-2,-4,5),420),((3,-2,2),230),((2,3,4),400)]:
        bpy.ops.object.light_add(type='AREA',location=target+Vector(delta)*span)
        light=bpy.context.object;light.data.energy=energy*span*span;light.data.size=span*3;aim(light,target)
    scene.render.resolution_x=256;scene.render.resolution_y=256;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA'
    scene.render.filepath=str(path);bpy.ops.render.render(write_still=True)


def main():
    equipment=json.loads((OUT/'manifest.json').read_text());equipment_icons=[];character_icons=[]
    characters_only='--characters-only' in sys.argv
    if not characters_only:
        for item in equipment['assets']:
            mats=reset(EQUIPMENT_COLORS);source=OUT/(item['key']+'.source.json')
            points=add_source(source,mats);path=OUT/(item['key']+'-icon.png');render(path,points)
            equipment_icons.append({'owner':item['key'],'file':path.name,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest()})
        mats=reset(EQUIPMENT_COLORS);source=OUT/'paladin-shield-highward.source.json'
        points=add_source(source,mats);path=OUT/'paladin-guard-icon.png';render(path,points)
        equipment_icons.append({'owner':'paladin-guard','file':path.name,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest()})
    characters=json.loads((CHAR/'manifest.json').read_text())
    novice_only='--novice-only' in sys.argv
    previous_icons=json.loads((CHAR/'icons-manifest.json').read_text())['icons'] if novice_only else []
    for equipment_set in characters['equipmentSets']:
        if novice_only and equipment_set['set']!='novice':
            character_icons.extend(r for r in previous_icons if r['owner'].startswith('paladin-'+equipment_set['set']+'-'))
            continue
        for part in ['armor','boots','helmet']:
            glb=equipment_set['armor']['male'] if part=='armor' else equipment_set[part]
            source=CHAR/glb.replace('.glb','.source.json')
            mats=reset(CHARACTER_COLORS);points=add_source(source,mats)
            owner='paladin-'+equipment_set['set']+'-'+part
            path=CHAR/(owner+'-icon.png');render(path,points)
            character_icons.append({'owner':owner,'file':path.name,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest()})
    for directory,records in ([(CHAR,character_icons)] if characters_only else [(OUT,equipment_icons),(CHAR,character_icons)]):
        report={'schema':'ftkmf.paladin-original-icons.v1','generator':'art-experiments/paladin-equipment/render_icons.py','generatorSha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'scope':'Original source meshes rendered at 256x256 RGBA. Studio icons, not native portrait capture or UI acceptance.','icons':records,'files':{r['file']:r['sha256'] for r in records}}
        (directory/'icons-manifest.json').write_text(json.dumps(report,indent=2)+'\n')


if __name__=='__main__':main()
