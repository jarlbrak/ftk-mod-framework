#!/usr/bin/env python3
"""Render exported apparel at a shared scale, without fabricated native avatars."""
import hashlib,json,struct,sys
from pathlib import Path
import bpy
import numpy as np
from mathutils import Vector

BASE=Path(__file__).resolve().parent
OUT=BASE/'apparel-review';OUT.mkdir(exist_ok=True)
BANDS=['street','burglar','guild','masterwork','locksmith','nightblade','wayfarer']
manifest=json.loads((BASE/'manifest.json').read_text())
SPAN=2.40
TARGET=(0,-.04,.92)
DIRECTIONS={'front':(0,-1,.03),'three-quarter':(.60,-1,.08),'side':(1,0,.03),'back':(0,1,.03)}

def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def decode(path):
    raw=path.read_bytes();size=struct.unpack_from('<I',raw,12)[0]
    doc=json.loads(raw[20:20+size]);blob=raw[28+size:]
    def read(index):
        a=doc['accessors'][index];v=doc['bufferViews'][a['bufferView']]
        width={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']]
        dtype={5126:'<f4',5123:'<u2',5125:'<u4'}[a['componentType']]
        return np.frombuffer(blob,dtype=dtype,count=a['count']*width,offset=v.get('byteOffset',0)+a.get('byteOffset',0)).reshape(-1,width)
    return doc,read

def aim(obj,target):obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
def make_scene(files):
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    materials=[]
    for color in manifest['apparelPalette']:
        mat=bpy.data.materials.new(color);mat.use_nodes=True
        node=mat.node_tree.nodes.get('Principled BSDF')
        def linear(v):return v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4
        node.inputs['Base Color'].default_value=tuple(linear(int(color[i:i+2],16)/255) for i in [0,2,4])+(1,)
        node.inputs['Metallic'].default_value=0;node.inputs['Roughness'].default_value=.82
        materials.append(mat)
    for filename,shift in files:
        doc,read=decode(BASE/filename);prim=doc['meshes'][0]['primitives'][0];attr=prim['attributes']
        pts=read(attr['POSITION'])+np.array(shift)
        mesh=bpy.data.meshes.new(filename);mesh.from_pydata([(x,-z,y) for x,y,z in pts],[],read(prim['indices']).reshape(-1,3).tolist());mesh.update()
        obj=bpy.data.objects.new(filename,mesh);bpy.context.collection.objects.link(obj)
        for mat in materials:mesh.materials.append(mat)
        uv=read(attr['TEXCOORD_0'])
        for face in mesh.polygons:face.material_index=min(len(materials)-1,int(uv[face.vertices[0],0]*len(materials)))
    bpy.ops.object.camera_add(location=(0,-7,.92));camera=bpy.context.object;camera.data.type='ORTHO';camera.data.ortho_scale=SPAN
    scene=bpy.context.scene;scene.camera=camera;scene.render.engine='CYCLES';scene.cycles.samples=16
    scene.render.film_transparent=True;scene.world.color=(.20,.20,.20);scene.view_settings.view_transform='Standard'
    for delta,energy in [((-2,-4,5),420),((3,-2,2),230),((2,3,4),340)]:
        bpy.ops.object.light_add(type='AREA',location=Vector(TARGET)+Vector(delta)*SPAN)
        light=bpy.context.object;light.data.energy=energy*SPAN*SPAN;light.data.size=SPAN*3;aim(light,TARGET)
    scene.render.resolution_x=640;scene.render.resolution_y=640;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA'
    return scene,camera

records=[]
for band in BANDS:
    for sex in ['male','female']:
        # The existing hood display is face-centered. Its authored review origin
        # comes from the pinned male Head_M rest landmark. This is an explicit
        # assembly convention, not proof of the native crown mount or hair fit.
        files=[('thief-coat-'+band+'-'+sex+'.glb',(0,0,0)),('thief-boots-'+band+'.glb',(0,0,0)),
               ('thief-hood-'+band+'-display.glb',(0,1.76766508,.08568515))]
        scene,camera=make_scene(files)
        for view,direction in DIRECTIONS.items():
            suffix='' if view=='three-quarter' else '-'+view
            name='armor-'+band+'-'+sex+suffix+'.png'
            camera.location=Vector(TARGET)+Vector(direction)*SPAN*3;aim(camera,TARGET)
            scene.render.filepath=str(OUT/name);bpy.ops.render.render(write_still=True)
            records.append({'file':name,'sha256':digest(OUT/name),'band':band,'sex':sex,'view':view,
                'inputs':[{'file':f,'sha256':digest(BASE/f),'reviewTranslation':list(shift)} for f,shift in files],
                'camera':{'direction':direction,'target':TARGET,'orthographicSpan':SPAN},'pose':'exported neutral bind pose'})
report={'schema':'ftkmf.thief-apparel-common-scale-review.v1','generator':'render_sets.py','generatorSha256':digest(Path(__file__)),
    'scope':'Actual exported apparel GLBs. Original coat, boots and low neckerchief only. Native body, face, hair, hands and backpack omitted. No fabricated avatar, animation or fit claim. All tiers and sexes share camera, light and framing.',
    'headwearAssembly':'Display GLB at authored Head_M rest landmark (0,1.76766508,0.08568515). Native rigid mount, native hair visibility and neck motion require separate live checks.',
    'palette':'thief-apparel-palette.png','paletteSha256':digest(BASE/'thief-apparel-palette.png'),
    'renderSettings':{'resolution':[640,640],'engine':'CYCLES','samples':16,'background':'transparent','viewTransform':'Standard'},'views':records}
(OUT/'manifest.json').write_text(json.dumps(report,indent=2)+'\n')
print('Rendered',len(records),'common-scale exported apparel views')
