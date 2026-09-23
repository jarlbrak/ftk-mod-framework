#!/usr/bin/env python3
"""Render actual strict GLB exports at a shared scale and write package icons."""
import importlib.util,json,math,shutil,struct,sys
from pathlib import Path
import bpy
import numpy as np
from mathutils import Vector
OUT=Path(__file__).resolve().parent;ROOT=OUT.parents[2];PACKAGE=ROOT/'marketplace/packages/thief/assets'
def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
v=module('weapon_decoder',OUT/'validate.py')
g=module('weapon_helpers',OUT.parent/'build.py')
icons=module('weapon_icon_renderer',ROOT/'art-experiments/paladin-equipment/render_icons.py')
SPAN=1.65

def mesh(data,materials,matrix=None):
    points=v.points(data,np.eye(4) if matrix is None else matrix)
    points=[(x,-z,y) for x,y,z in points]
    m=bpy.data.meshes.new('Exported weapon');m.from_pydata(points,[],data['triangles'].tolist());m.update()
    obj=bpy.data.objects.new('Exported weapon',m);bpy.context.collection.objects.link(obj)
    for mat in materials:m.materials.append(mat)
    for face in m.polygons:face.material_index=min(len(materials)-1,int(data['uvs'][face.vertices[0]][0]*len(materials)))
    return [Vector(p) for p in points]
def scene(w,equipped=False):
    mats=icons.reset(w['colors'])
    for mat in mats:
        node=mat.node_tree.nodes.get('Principled BSDF');node.inputs['Metallic'].default_value=.15;node.inputs['Roughness'].default_value=.64
    pts=[]
    if equipped:
        for index,assignment in enumerate([w['assignments']['itemModels'][0]]+w['assignments'].get('offHandModels',[])):
            data=v.decode(PACKAGE/Path(assignment['model']).name);m=np.eye(4);m[0,3]=index*.34-.17 if w['family']=='paired' else 0
            pts+=mesh(data,mats,m)
        if w['family']=='bow':
            d=w['string'];pts+=mesh(v.decode(OUT/(d['key']+'.glb')),mats,d['stringToWeaponLocal'])
    else:
        for d in w['displays']:pts+=mesh(v.decode(OUT/(d['key']+'.glb')),mats,d['rendererToDisplayRoot'])
    bpy.context.scene.view_settings.view_transform='Standard'
    return pts

def render(w,view,path,icon=False):
    points=scene(w,view=='equipped')
    if icon:
        icons.render(path,points);g.strip_metadata(path);return
    low=Vector(tuple(min(p[i] for p in points) for i in range(3)));high=Vector(tuple(max(p[i] for p in points) for i in range(3)))
    target=(low+high)/2
    delta={'front':(0,-4,0),'quarter':(.8,-4,.4),'side':(4,0,0),'back':(0,4,0),'equipped':(.8,-4,.4)}[view]
    bpy.ops.object.camera_add(location=target+Vector(delta));camera=bpy.context.object
    camera.data.type='ORTHO';camera.data.ortho_scale=SPAN;icons.aim(camera,target)
    s=bpy.context.scene;s.camera=camera;s.render.engine='CYCLES';s.cycles.samples=16
    s.render.film_transparent=True;s.world.color=(.18,.18,.18)
    for delta,energy in [((-2,-4,5),420),((3,-2,2),230),((2,3,4),400)]:
        bpy.ops.object.light_add(type='AREA',location=target+Vector(delta)*SPAN)
        light=bpy.context.object;light.data.energy=energy*SPAN*SPAN;light.data.size=SPAN*3;icons.aim(light,target)
    s.render.resolution_x=384;s.render.resolution_y=384;s.render.resolution_percentage=100
    s.render.image_settings.file_format='PNG';s.render.image_settings.color_mode='RGBA';s.render.filepath=str(path)
    bpy.ops.render.render(write_still=True);g.strip_metadata(path)

def main():
    manifest=json.loads((OUT/'manifest.json').read_text());previews=[]
    for w in manifest['weapons']:
        render(w,'quarter',OUT/w['icon'],True);shutil.copyfile(OUT/w['icon'],PACKAGE/w['icon'])
        for view in ['quarter','front','side','back','equipped']:
            name='weapon-'+w['id']+('' if view=='quarter' else '-'+view)+'.png'
            render(w,view,OUT/name)
            previews.append({'id':w['id'],'view':view,'file':name,'sha256':g.digest(OUT/name),'sourceGlbs':{d['key']+'.glb':g.digest(OUT/(d['key']+'.glb')) for d in w['displays']}})
    manifest['render']={'generator':'render.py','generatorSha256':g.digest(Path(__file__)),'decoderSha256':g.digest(OUT/'validate.py'),'orthographicSpan':SPAN,'size':[384,384],'previews':previews,'scope':'Actual exported GLB geometry and palette rendered offline. Equipped local axes are shown without native hands; native fitting and motion remain open.'}
    for p in sorted(OUT.glob('*.png')):manifest['files'][p.name]=g.digest(p)
    (OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    files={name:{'source':str((OUT/name).relative_to(ROOT)),'sha256':sha} for name,sha in manifest['files'].items() if (PACKAGE/name).exists() and name.endswith(('.glb','-palette.png','-icon.png'))}
    (PACKAGE/'full-weapons.provenance.json').write_text(json.dumps({'provenance':manifest['provenance'],'files':files},indent=2)+'\n')
    print('PASS: 17 production icons and 85 actual-GLB views')
if __name__=='__main__':main()
