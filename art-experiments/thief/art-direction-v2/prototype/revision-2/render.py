"""Render the actual exported geometry. No native body or simulated animation."""
import hashlib
import json
import math
import sys
from pathlib import Path
import bpy
import numpy as np
from mathutils import Vector

OUT=Path(__file__).resolve().parent
sys.path.insert(0,str(OUT))
from validate import decode


def aim(obj,target):
    obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()


def render(label,files,camera_direction,span,target):
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    palette=json.loads((OUT/'manifest.json').read_text())['palette'];materials=[]
    def linear(v):return v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4
    for i,color in enumerate(palette):
        mat=bpy.data.materials.new(color);mat.use_nodes=True
        node=mat.node_tree.nodes.get('Principled BSDF')
        node.inputs['Base Color'].default_value=tuple(linear(int(color[j:j+2],16)/255) for j in [0,2,4])+(1,)
        node.inputs['Metallic'].default_value=0
        node.inputs['Roughness'].default_value=.85
        materials.append(mat)
    for filename,matrix in files:
        doc,read=decode(OUT/filename);prim=doc['meshes'][0]['primitives'][0];a=prim['attributes']
        vertices=read(a['POSITION']);vertices=vertices@matrix[:3,:3].T+matrix[:3,3]
        points=[(x,-z,y) for x,y,z in vertices]
        mesh=bpy.data.meshes.new(filename);mesh.from_pydata(points,[],read(prim['indices']).reshape(-1,3).tolist());mesh.update()
        obj=bpy.data.objects.new(filename,mesh);bpy.context.collection.objects.link(obj)
        for material in materials:mesh.materials.append(material)
        uv=read(a['TEXCOORD_0'])
        for face in mesh.polygons:face.material_index=min(len(palette)-1,int(uv[face.vertices[0]][0]*len(palette)))
    bpy.ops.object.camera_add(location=Vector(target)+Vector(camera_direction)*span*3)
    cam=bpy.context.object;cam.data.type='ORTHO';cam.data.ortho_scale=span;aim(cam,target)
    scene=bpy.context.scene;scene.camera=cam;scene.render.engine='CYCLES';scene.cycles.samples=16
    scene.render.film_transparent=True;scene.world.color=(.20,.20,.20)
    scene.view_settings.view_transform='Standard'
    for delta,energy in [((-2,-4,5),420),((3,-2,2),200),((2,3,4),300)]:
        bpy.ops.object.light_add(type='AREA',location=Vector(target)+Vector(delta)*span)
        lamp=bpy.context.object;lamp.data.energy=energy*span*span;lamp.data.size=span*3;aim(lamp,target)
    scene.render.resolution_x=512;scene.render.resolution_y=512;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA'
    path=OUT/('review-'+label+'.png');scene.render.filepath=str(path);bpy.ops.render.render(write_still=True)
    return {'file':path.name,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),
            'inputs':[{ 'file':f,'sha256':hashlib.sha256((OUT/f).read_bytes()).hexdigest()} for f,_ in files],
            'camera':{'direction':camera_direction,'target':target,'orthographicSpan':span}}


records=[]
apparel=[('thief-coat-street-male.glb',np.eye(4)),('thief-boots-street.glb',np.eye(4))]
for label,direction in [('front',(0,-1,.05)),('three-quarter',(.65,-1,.10)),('side',(1,0,.05)),('back',(0,1,.05))]:
    records.append(render('apparel-'+label,apparel,direction,2.25,(0,0,.85)))
records.append(render('headwear',[('thief-hood-street-display.glb',np.eye(4))],(.55,-1,.24),.90,(0,.05,.20)))
# Undo only the documented native mount rotation for a separate knife studio view.
c,s=math.cos(math.radians(55)),math.sin(math.radians(55))
rz=np.eye(4);rz[:3,:3]=[[c,-s,0],[s,c,0],[0,0,1]]
ry=np.eye(4);ry[:3,:3]=[[0,0,1],[0,1,0],[-1,0,0]]
records.append(render('knife',[('thief-street-twins.glb',np.linalg.inv(rz@ry))],(.35,-1,.08),.80,(0,0,.19)))
(OUT/'preview.json').write_text(json.dumps({'scope':'Actual exported GLBs in neutral bind pose. Native body, face, hands, hair and backpack omitted. This is an apparel geometry review, not native fit or animation evidence. Headwear and knife are separate studio views.',
    'generator':'render.py','generatorSha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'views':records},indent=2)+'\n')
