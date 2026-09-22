#!/usr/bin/env python3
"""Author the three approved original Paladin relics and their rigid delivery assets."""
import copy
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import shutil
import struct
import sys
import bpy
import numpy as np
from mathutils import Vector

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[1]
PACKAGE = ROOT / 'marketplace/packages/paladin/assets'
COLORS = ['EEE4CC','ABB8C8','DDB359','9D7134','172D4C','102037','1985B6','47BCDD','30343D','515665','682F38','A34849','F3AB39','151B26']
IVORY,SILVER,GOLD,BRONZE,NAVY,DEEP,BLUE,CYAN,IRON,STEEL,RED,REDLIGHT,AMBER,BLACK = range(14)
KEYS = ['paladin-hammer-1h-last-vigil','paladin-hammer-2h-kingsfall','paladin-shield-last-bastion']

def load(name,path):
    spec=importlib.util.spec_from_file_location(name,path); module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module

B=load('equipment_builder',ROOT/'art-experiments/paladin-equipment/build_blender.py')
I=load('equipment_icons',ROOT/'art-experiments/paladin-equipment/render_icons.py')
D=load('display_fit',ROOT/'art-experiments/paladin-equipment/loot-display/build.py')
B.OUT=OUT;B.COLORS=COLORS
box,cylinder,plate=B.box,B.cylinder,B.plate

def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def save(path,data):path.write_text(json.dumps(data,indent=2)+'\n')

def crystal(name,z,y,width,height,color=BLUE):
    outline=[(0,z-height*.5),(width*.48,z-height*.17),(width*.5,z+height*.21),(0,z+height*.5),(-width*.5,z+height*.21),(-width*.48,z-height*.17)]
    vertices=[(x,y,vz) for x,vz in outline]+[(0,y-width*.34,z+height*.08),(0,y+.015,z)]
    faces=[(i,(i+1)%6,6) for i in range(6)]+[(i,7,(i+1)%6) for i in range(6)]
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);B.finish(obj,name,color)
    return obj

def bar(name,a,b,width,depth,y,color=GOLD):
    delta=Vector((b[0]-a[0],0,b[1]-a[1]));obj=box(name,((a[0]+b[0])/2,y,(a[1]+b[1])/2),(width,depth,delta.length+width*.18),color,min(.009,width*.22))
    obj.rotation_euler=delta.to_track_quat('Z','Y').to_euler();return obj

def frame(name,outline,y,width=.024,depth=.025,color=GOLD,skip=()):
    for i,a in enumerate(outline):
        if i not in skip:bar(name+' %02d'%i,a,outline[(i+1)%len(outline)],width,depth,y,color)

def haft(two):
    length=1.5 if two else .88
    cylinder('Octagonal dark structural haft',(0,0,length*.43),.040 if two else .036,length*.94,IRON if two else GOLD,8)
    count=8 if two else 7;step=.086 if two else .082
    for i in range(count):
        cylinder('Broad leather wrap %02d'%i,(0,0,.055+i*step),.051 if two else .048,step*.91,RED if two else NAVY,8)
    for z in [.012,.055+(count-.3)*step]:cylinder('Grip gold collar',(0,0,z),.059,.040,GOLD,8)
    if two:
        obj=B.gem('Faceted black pommel',(0,0,-.060),.091,IRON)
        cylinder('Gold pommel belt',(0,0,-.054),.077,.026,GOLD,6)
    else:
        points=[(0,-.16),(.072,-.073),(.060,.006),(-.060,.006),(-.072,-.073)]
        plate('Shield shaped gold pommel',points,.077,0,GOLD)
        plate('Ivory pommel inset',[(x*.69,z*.71-.022) for x,z in points],.018,-.045,IVORY)
    cylinder('Structural head collar',(0,0,length-.205),.066,.105,GOLD,8)

def vigil():
    haft(False);z=.88
    box('Ivory reliquary core',(0,0,z),(.57,.235,.270),IVORY,.026)
    for side in [-1,1]:
        box('Silver blunt striking face',(side*.319,0,z),(.096,.300,.329),SILVER,.026)
        box('Gold protective upright',(side*.204,0,z),(.056,.269,.315),GOLD,.015)
        box('Gold upright foot',(side*.204,-.006,z-.124),(.077,.285,.060),GOLD,.009)
    window=[(0,z-.125),(.123,z-.055),(.123,z+.086),(0,z+.139),(-.123,z+.086),(-.123,z-.055)]
    plate('Recessed blue ward field',window,.025,-.135,DEEP)
    frame('Reliquary gold window frame',window,-.157,.027,.026)
    crystal('Sapphire guardian stone',z+.009,-.160,.118,.209)
    box('Low stepped reliquary roof',(0,0,z+.150),(.236,.215,.030),GOLD,.008)
    plate('Central low roof crest',[(-.088,z+.160),(-.055,z+.202),(.051,z+.202),(.085,z+.160)],.119,0,GOLD)
    # Rear identity is deliberately simpler, while still finished when swung.
    plate('Rear gold shield seal',[(0,z-.081),(.059,z),(.047,z+.063),(-.047,z+.063),(-.059,z)],.017,.127,GOLD)

def kingsfall():
    haft(True);z=1.5
    box('Heavy charcoal anvil core',(0,0,z),(.758,.290,.338),IRON,.033)
    for side in [-1,1]:
        box('Broad iron striking block',(side*.400,0,z),(.122,.365,.405),STEEL,.033)
        box('Inset dark strike end',(side*.464,0,z),(.017,.282,.311),IRON,.007)
        box('Gold structural head band',(side*.264,0,z),(.070,.321,.379),GOLD,.016)
        plate('Broad crown shoulder',[(side*.252,z+.173),(side*.249,z+.274),(side*.183,z+.285),(side*.180,z+.183)],.176,0,GOLD)
    plate('Fractured crown center',[(-.048,z+.175),(-.048,z+.292),(.009,z+.309),(.044,z+.266),(.018,z+.241),(.063,z+.259),(.063,z+.175)],.180,0,GOLD)
    ring=[(-.112,z-.048),(-.118,z+.070),(-.051,z+.130),(.053,z+.133),(.115,z+.066),(.109,z-.056),(.039,z-.111),(-.050,z-.110)]
    frame('Fractured royal seal',ring,-.160,.032,.030,GOLD,skip=(3,7))
    # A single continuous narrow material seam stays legible at combat distance.
    seam=[(-.089,z-.168),(-.045,z-.075),(-.021,z-.045),(.022,z-.012),(.016,z+.030),(.055,z+.069),(.128,z+.150)]
    for i in range(len(seam)-1):bar('Amber fractured seal seam',seam[i],seam[i+1],.011,.010,-.155,AMBER)
    box('Rear gold seal field',(0,.153,z),(.137,.018,.173),BRONZE,.015)

def bastion():
    outline=[(0,-.70),(.285,-.360),(.386,.095),(.386,.528),(.260,.548),(.254,.469),(.088,.469),(.088,.603),(-.088,.603),(-.088,.469),(-.254,.469),(-.260,.548),(-.386,.528),(-.386,.095),(-.285,-.360)]
    plate('Continuous silver battlement perimeter',outline,.097,0,SILVER)
    inner=[(x*.938,z*.950) for x,z in outline]
    plate('Aged gold inner perimeter',inner,.024,-.059,GOLD)
    field=[(x*.875,z*.894) for x,z in outline]
    plate('Deep navy shield body',field,.023,-.079,NAVY)
    for side in [-1,1]:
        panel=[(side*.121,-.459),(side*.251,-.287),(side*.329,.094),(side*.329,.451),(side*.271,.471),(side*.262,.398),(side*.128,.394)]
        plate('Broad ivory side plane',panel,.022,-.099,IVORY)
    arch=[(0,-.080),(.118,.026),(.118,.305),(0,.421),(-.118,.305),(-.118,.026)]
    plate('Deep inset sanctuary well',arch,.015,-.102,DEEP)
    frame('Protective gold sanctuary arch',arch,-.131,.035,.034)
    crystal('Central sapphire shelter stone',.172,-.135,.123,.346)
    plate('Broad gold central keel',[(-.027,-.073),(.027,-.073),(.040,-.550),(0,-.614),(-.040,-.550)],.034,-.111,GOLD)
    # Real rear furniture prevents a paper-thin prop appearance during Guard.
    for z in [-.208,.200]:
        box('Rear metal anchor',(0,.067,z),(.218,.038,.054),BRONZE,.011)
        for x in [-.085,.085]:cylinder('Rear anchor stud',(x,.091,z),.018,.014,GOLD,6).rotation_euler[0]=math.pi/2
    box('Rear leather hand grip',(0,.120,0),(.058,.061,.411),NAVY,.018)
    for side in [-1,1]:bar('Rear diagonal brace',(side*.240,-.340),(side*.190,.354),.026,.020,.059,IRON)

def subset(source,pieces,selected):
    data={k:[] for k in ['positions','normals','uvs','triangles']};mapping=[]
    for piece in selected:
        start=len(data['triangles'])
        for tri in source['triangles'][piece['firstTriangle']:piece['firstTriangle']+piece['triangleCount']]:
            indices=[]
            for v in tri:
                indices.append(len(data['positions']))
                for k in ['positions','normals','uvs']:data[k].append(source[k][v])
            data['triangles'].append(indices)
        p=copy.deepcopy(piece);p['firstTriangle']=start;mapping.append(p)
    return data,mapping

def write_derived(key,data,pieces):
    (OUT/(key+'.source.json')).write_text(json.dumps(data,separators=(',',':'))+'\n');save(OUT/(key+'.pieces.json'),pieces);B.write_static_glb(OUT/(key+'.glb'),data)

def derivatives(key,kind):
    source=json.loads((OUT/(key+'.source.json')).read_text());pieces=json.loads((OUT/(key+'.pieces.json')).read_text());records=[]
    if kind!='shield':
        count=2 if kind=='1h' else 3;groups=[[] for _ in range(count)]
        p=np.array(source['positions']);t=np.array(source['triangles'])
        for piece in pieces:
            center=p[t[piece['firstTriangle']:piece['firstTriangle']+piece['triangleCount']].reshape(-1)].mean(axis=0)
            group=2 if count==3 and center[1]<1.2 else (0 if center[0]<-1e-5 else 1)
            groups[group].append(piece)
        paths=['Break','Break/Break'] if count==2 else ['break','break/break2','break/break1']
        for i,group in enumerate(groups):
            data,mapping=subset(source,pieces,group);name=key+'-fragment-'+str(i+1);write_derived(name,data,mapping)
            records.append({'key':name,'rendererPath':paths[i],'triangles':len(data['triangles']),'sourcePieceNames':[p['name'] for p in group]})
        assert sum(r['triangles'] for r in records)==len(source['triangles'])
    bindings=json.loads(D.BINDING.read_text())['rigidEquipmentRenderers']
    path={'1h':'smithhammer','2h':'hammer','shield':'shieldBlacksmith01'}[kind]
    transform=D.matrix(next(v for v in bindings if v['path']==path))
    envelope_path=ROOT/('art-experiments/paladin-equipment/'+('shield-display' if kind=='shield' else 'loot-display')+'/reference-envelope.json')
    envelope=json.loads(envelope_path.read_text());assert envelope['bindingMetadataSha256']==digest(D.BINDING)
    low=np.array(envelope['referenceRootMin']);high=np.array(envelope['referenceRootMax']);points=np.array(source['positions']);posed=D.transform(points,transform)
    scale=float(min((high-low)[:2]/np.ptp(posed,axis=0)[:2])*.95)
    fit=np.eye(4);fit[:3,:3]*=scale;fit[:3,3]=(low+high)/2-scale*(posed.min(0)+posed.max(0))/2
    local=np.linalg.inv(transform)@fit@transform;data=copy.deepcopy(source);data['positions']=D.transform(points,local).tolist()
    normals=np.array(source['normals'])@np.linalg.inv(local[:3,:3]);normals/=np.linalg.norm(normals,axis=1)[:,None];data['normals']=normals.tolist()
    name=key+'-display';write_derived(name,data,pieces)
    return {'fragments':records,'display':{'key':name,'rendererPath':path,'scale':scale,'authorToDisplayLocal':local.tolist(),'referenceEnvelope':str(envelope_path.relative_to(ROOT)),'referenceEnvelopeSha256':digest(envelope_path),'bindingMetadataSha256':digest(D.BINDING)}}

def strip_render_metadata(path):
    # Blender writes wall-clock Date and timing text chunks even without a visible stamp.
    # Remove only ancillary text metadata; preserve encoded pixels and color information.
    raw=path.read_bytes();chunks=[raw[:8]];offset=8
    while offset<len(raw):
        length=struct.unpack_from('>I',raw,offset)[0];kind=raw[offset+4:offset+8]
        if kind not in (b'tEXt',b'iTXt',b'zTXt'):chunks.append(raw[offset:offset+length+12])
        offset+=length+12
    path.write_bytes(b''.join(chunks))

def render_board():
    mats=I.reset(COLORS)
    for key,x in zip(KEYS,[-1.17,0,1.13]):
        data=json.loads((OUT/(key+'.source.json')).read_text());minimum=min(p[1] for p in data['positions']);I.add_source(OUT/(key+'.source.json'),mats,Vector((x,-minimum,0)))
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=40
    scene.render.film_transparent=False;scene.world.use_nodes=True
    background=scene.world.node_tree.nodes.get('Background');background.inputs['Color'].default_value=(.07,.065,.06,1);background.inputs['Strength'].default_value=.35
    bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.025));plane=bpy.context.object
    mat=bpy.data.materials.new('Warm charcoal studio');mat.use_nodes=True
    shader=mat.node_tree.nodes.get('Principled BSDF');shader.inputs['Base Color'].default_value=(.028,.023,.020,1);shader.inputs['Roughness'].default_value=.85;plane.data.materials.append(mat)
    target=Vector((0,0,.89));bpy.ops.object.camera_add(location=(3,-10,3.8));camera=bpy.context.object;B.aim(camera,target);camera.data.type='ORTHO';camera.data.ortho_scale=4.1;scene.camera=camera
    for loc,power,size in [((-3,-4,6),700,5),((4,-1,3),450,4),((0,3,5),900,4)]:
        bpy.ops.object.light_add(type='AREA',location=loc);lamp=bpy.context.object;lamp.data.energy=power;lamp.data.size=size;B.aim(lamp,target)
    scene.view_settings.view_transform='AgX';scene.render.resolution_x=1800;scene.render.resolution_y=1200;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG';scene.render.filepath=str(OUT/'actual-mesh-board.png')
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'paladin-legendaries.blend'));bpy.ops.render.render(write_still=True)

def main():
    records=[]
    for key,kind,builder in zip(KEYS,['1h','2h','shield'],[vigil,kingsfall,bastion]):
        B.MATS=I.reset(COLORS);B.pieces.clear();builder();bpy.context.view_layer.update();record=B.export_asset(key);record['kind']=kind;record.update(derivatives(key,kind));records.append(record)
    image=bpy.data.images.new('Original legendary palette',width=len(COLORS),height=1)
    image.pixels=[v for color in COLORS for v in [*(int(color[i:i+2],16)/255 for i in [0,2,4]),1]];image.filepath_raw=str(OUT/'paladin-legendary-palette.png');image.file_format='PNG';image.save()
    for key in KEYS:
        mats=I.reset(COLORS);points=I.add_source(OUT/(key+'.source.json'),mats);I.render(OUT/(key+'-icon.png'),points);strip_render_metadata(OUT/(key+'-icon.png'))
    render_board()
    files={p.name:digest(p) for p in sorted(OUT.iterdir()) if p.suffix=='.glb' or p.name.endswith(('.source.json','.pieces.json','-icon.png','-palette.png'))}
    manifest={'schema':'ftkmf.paladin-original-legendaries.v1','generator':'build.py','generatorSha256':digest(Path(__file__)),'dependencies':{str(p.relative_to(ROOT)):digest(p) for p in [ROOT/'art-experiments/paladin-equipment/build_blender.py',ROOT/'art-experiments/paladin-equipment/render_icons.py',ROOT/'art-experiments/paladin-equipment/loot-display/build.py',ROOT/'tools/ai-model-pipeline/export_ftk_glb.py']},'provenance':'All surfaces and colors are original authored geometry. No native surface data read. Existing transform-only display metadata and frozen original-art envelopes used for card fit. Approved generated concepts interpreted as visual references only.','space':'Unity Y up, Z front via orientation-preserving Blender (x,z,-y). Existing authored grip stations retained.','status':'Offline original assets; exact equipped fit, Guard, attacks, break detach and cards require fresh live evidence.','assets':records,'files':files}
    save(OUT/'manifest.json',manifest)
    provenance_path=ROOT/'marketplace/packages/paladin-assets.provenance.json';provenance=json.loads(provenance_path.read_text())
    for name,sha in files.items():
        if name.endswith('.glb') or name.endswith(('-icon.png','-palette.png')):
            shutil.copyfile(OUT/name,PACKAGE/name);provenance['files']['assets/'+name]={'source':str((OUT/name).relative_to(ROOT)),'sha256':sha}
    save(provenance_path,provenance)
    print('PASS: authored three relics, equipped/display/break meshes, three icons, palette, render board and additive package provenance')

if __name__=='__main__':main()
