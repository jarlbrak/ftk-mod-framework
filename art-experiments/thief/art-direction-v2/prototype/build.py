#!/usr/bin/env python3
"""Build one original Street outfit without changing production package assets."""
import argparse
import copy
import hashlib
import importlib.util
import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image

OUT = Path(__file__).resolve().parent
ROOT = next(p for p in OUT.parents if (p / 'FTKModFramework').is_dir())
BASE = ROOT / 'art-experiments/thief/ranged-apparel'
sys.path.insert(0, str(BASE))
spec = importlib.util.spec_from_file_location('street_v2_surface', BASE / 'build.py')
m = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = m
spec.loader.exec_module(m)

# Broad cloth and leather values stay below native skin highlights.
COLORS = ['29323B', '394652', '3E6267', '527478', '624A38', '7D6047',
          '34302D', '9B8153', '444F58', '657783', 'A4AFAC', '65434A']
m.lib.PALETTE = COLORS
p, Surface = m.p, m.Surface


def save(name, value):
    (OUT / name).write_text(json.dumps(value, indent=2) + '\n')


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def solid(s, label, vertices, faces, skin, colors):
    """Closed original volume with explicit flat planes and one skin per vertex."""
    start = len(s.data['positions'])
    verts = [np.asarray(v, dtype=float) for v in vertices]
    skins = [skin] * len(verts) if isinstance(skin, (str, dict)) else skin
    for face, color in zip(faces, colors if isinstance(colors, list) else [colors] * len(faces)):
        for j in range(1, len(face) - 1):
            ids = [face[0], face[j], face[j + 1]]
            s.triangle([verts[k] for k in ids], [skins[k] for k in ids], color)
    s.pieces.append({'name': label, 'vertex_start': start,
                     'vertex_count': len(s.data['positions']) - start})


def ribbon(s, label, rows, color, depth=.013):
    verts, skins = [], []
    for rear in [False, True]:
        for a, b, sk in rows:
            verts += [a - p(z=depth) if rear else a, b - p(z=depth) if rear else b]
            skins += [sk, sk]
    n = 2 * len(rows)
    faces = []
    for j in range(len(rows) - 1):
        a, b, c, d = j * 2, j * 2 + 1, j * 2 + 2, j * 2 + 3
        faces += [(a, b, d, c), (a+n, c+n, d+n, b+n),
                  (a, c, c+n, a+n), (b, b+n, d+n, d)]
    faces += [(0, n, n+1, 1), (n-2, n-1, 2*n-1, 2*n-2)]
    solid(s, label, verts, faces, skins, color)


def crown():
    s = m.rigid()
    # Keep the proven scalp-clear lower cross sections. Shift only the upper
    # cloth toward the left temple, with an angled fold instead of a round band.
    rings = []
    specs = [(0, .263, -.105, .226, .215),
             (-.008, .333, -.10, .266, .260),
             (-.030, .424, -.108, .220, .247),
             (-.047, .480, -.119, .145, .174),
             (-.065, .502, -.135, .055, .070)]
    for level, (cx, y, z, rx, rz) in enumerate(specs):
        ring = []
        for j in range(10):
            t = j * math.tau / 10
            # Broad asymmetric crown plane, independent of scalp-clearance ring.
            tilt = -.045 * math.cos(t) if level >= 2 else -.012 * math.cos(t)
            ring.append(p(cx + rx*math.cos(t), y+tilt, z+rz*math.sin(t)))
        rings += ring
    faces, colors = [], []
    for row in range(4):
        for j in range(10):
            faces.append((row*10+j, row*10+(j+1)%10, (row+1)*10+(j+1)%10, (row+1)*10+j))
            colors.append(1 if j in [0, 1, 4, 5] else 0)
    faces += [tuple(range(9, -1, -1)), tuple(range(40, 50))]
    colors += [0, 1]
    solid(s, 'Slouched five-plane cloth crown', rings, faces, 'Root', colors)
    # One diagonal fold crosses the brow without a uniform encircling band.
    ribbon(s, 'Broad folded brow cloth', [
        (p(-.213,.308,.024), p(-.179,.272,.092), 'Root'),
        (p(-.070,.333,.144), p(-.061,.274,.129), 'Root'),
        (p(.113,.302,.105), p(.157,.278,.059), 'Root'),
        (p(.228,.280,-.029), p(.217,.263,-.055), 'Root')], 2, .008)
    # A lowered short cowl leaves the native face and ears visible.
    m.tube(s, 'Low continuous cowl', [p(-.165,-.041,.008),p(-.10,-.101,.070),
            p(.035,-.128,.090),p(.165,-.059,.01)], [.033,.038,.040,.033],
            [.029,.031,.031,.029], 2, sides=6)
    m.bead(s, 'Gathered fabric at nape', p(-.024,-.055,-.245), (.150,.058,.075), 0, sides=8)
    return s


def coat(s, male):
    B = s.B
    w = .239 if male else .217
    spine = ['Root_M','BackA_M','BackB_M','Chest_M']
    s.tube('Continuous tailored jerkin body', [B[n] for n in spine],
           [w*.84,w*.81,w*.94,w], [.124,.128,.139,.148], spine, 1, sides=10)
    s.tube('Upper cloth shoulder yoke', [B['Chest_M'], B['Chest_M']+p(y=.18), B['Neck_M']-p(y=.027)],
           [w,w*.94,.088], [.147,.124,.083], ['Chest_M','Chest_M','Neck_M'], 1, sides=10)
    s.tube('Continuous trouser pelvis', [B['Root_M']-p(y=.13), B['Root_M']+p(y=.08)],
           [w*.66,w*.95], [.11,.126], ['Root_M']*2, 0, sides=10)
    # Single large diagonal overlap: wide at the chest and firmly tucked at belt.
    ribbon(s, 'Broad diagonal cloth overlap', [
        (B['Root_M']+p(-w*.79,.044,.153), B['Root_M']+p(w*.75,.044,.148),'Root_M'),
        (B['BackA_M']+p(-w*.83,0,.156), B['BackA_M']+p(w*.74,0,.149),'BackA_M'),
        (B['BackB_M']+p(-w*.89,0,.163), B['BackB_M']+p(w*.42,0,.171),'BackB_M'),
        (B['Chest_M']+p(-w*.92,.126,.111), B['Chest_M']+p(w*.04,.126,.171),'Chest_M'),
        (B['Chest_M']+p(-w*.62,.236,.107), B['Chest_M']+p(-w*.25,.224,.132),'Chest_M')], 2)
    for side, sign in [('R',1),('L',-1)]:
        hip,knee,ankle=[n+'_'+side for n in ['Hip','Knee','Ankle']]
        s.tube('Fitted cloth trousers '+side, [B[hip],B[knee],B[ankle]],
               [.108,.079,.056], [.097,.072,.053], [hip,knee,ankle], 0, sides=8)
        # Broad tails end just below the hip; their center gap remains explicit.
        ribbon(s, 'Short split hem '+side, [
            (B['Root_M']+p(sign*.014,.052,.166), B['Root_M']+p(sign*w*.99,.052,.115),'Root_M'),
            (B['Root_M']+p(sign*.036,-.130,.155), B['Root_M']+p(sign*w*1.11,-.110,.074),{hip:.35,'Root_M':.65})], 2 if sign<0 else 1)
        ribbon(s, 'Continuous side hem '+side, [
            (B['Root_M']+p(sign*w*.90,.052,.114), B['Root_M']+p(sign*w*.88,.052,-.108),'Root_M'),
            (B['Root_M']+p(sign*w*1.10,-.110,.074), B['Root_M']+p(sign*w*1.04,-.124,-.098),{hip:.35,'Root_M':.65})],1)
        scap,sh,el,wr=[n+'_'+side for n in ['Scapula','Shoulder','Elbow','Wrist']]
        s.tube('Continuous sleeve '+side, [B[scap],B[sh],B[el],B[wr]+p(x=sign*.018)],
               [.096,.113,.080,.057], [.098,.110,.077,.058], [scap,sh,el,wr],0,sides=8,axis=(0,0,1))
        start=B[el]*.70+B[wr]*.30
        end=B[wr]+p(x=sign*.009)
        s.tube('Tapered one-piece leather bracer '+side, [start,B[el]*.32+B[wr]*.68,end],
               [.083,.074,.063],[.081,.072,.061],[{el:.75,wr:.25},{el:.30,wr:.70},wr],4,sides=8,axis=(0,0,1))
        q=B[el]*.51+B[wr]*.49
        s.tube('Single bracer retaining strap '+side, [q-p(x=sign*.020),q+p(x=sign*.020)],
               [.086]*2,[.084]*2,[{el:.55,wr:.45}]*2,6,sides=8,axis=(0,0,1))
        thumb='ThumbFinger1_'+side
        if thumb in B:
            m.bead(s,'Soft cuff thumb gusset '+side,B[thumb]-p(z=.009),(.030,.028,.028),4,{wr:.8,thumb:.2},sides=6)
        for part in ['MiddleToe1','MiddleToe2']:
            bone=part+'_'+side
            if bone in B:
                m.bead(s,'Concealed trouser stirrup '+bone,B[bone]+p(y=.018,z=-.035),(.009,.009,.012),0,bone,sides=6)
    # One shoulder drape, shaped as a cloth wedge over the sleeve.
    sh='Shoulder_L';sc='Scapula_L'
    solid(s,'Single short folded shoulder drape',[
        B[sc]+p(.030,.016,.073),B[sh]+p(-.045,.102,.023),B[sh]+p(-.151,-.029,.087),
        B[sh]+p(-.073,-.090,.124),B[sc]+p(-.025,-.024,.135),
        B[sc]+p(.016,.008,-.076),B[sh]+p(-.043,.086,-.095),B[sh]+p(-.146,-.037,-.076),
        B[sh]+p(-.065,-.088,-.053),B[sc]+p(-.025,-.039,-.083)],
        [(0,1,2,3,4),(9,8,7,6,5),(0,5,6,1),(1,6,7,2),(2,7,8,3),(3,8,9,4),(4,9,5,0)],
        [sc,sh,sh,sh,sc,sc,sh,sh,sh,sc], [2,1,2,1,1,2,1])
    s.tube('Single broad leather belt',[B['Root_M']+p(y=.031),B['Root_M']+p(y=.108)],
           [w*.99]*2,[.184]*2,['Root_M']*2,4,sides=10)
    q=B['Root_M']+p(-.028,.069,.202)
    m.panel(s,'Small functional belt buckle',[q+p(-.032,-.024),q+p(.032,-.024),q+p(.032,.024),q+p(-.032,.024)],.011,7,'Root_M')
    m.panel(s,'Buckle leather inset',[q+p(-.021,-.014,.012),q+p(.021,-.014,.012),q+p(.021,.014,.012),q+p(-.021,.014,.012)],.006,6,'Root_M')
    q=B['Root_M']+p(w*.89,-.020,.113)
    m.bead(s,'Single functional leather pouch',q,(.070,.090,.055),4,'Root_M',sides=6)
    m.panel(s,'One broad pouch flap',[q+p(-.067,.050,.042),q+p(.067,.050,.042),q+p(.055,-.006,.061),q+p(0,-.027,.066),q+p(-.055,-.006,.061)],.008,5,'Root_M')
    # The collar bridges to the retained native neck without a bright ring.
    s.tube('Soft low collar',[B['Neck_M']-p(y=.074),B['Neck_M']-p(y=.025)],
           [.107,.101],[.096,.088],['Neck_M']*2,0,sides=10)
    if 'Head_M' in B:
        m.bead(s,'Rear collar hinge',B['Head_M']+p(y=-.12,z=-.08),(.052,.031,.024),0,{'Head_M':.4,'Neck_M':.6},sides=6)


def boots(s):
    B=s.B
    for side in ['R','L']:
        hip,knee,ankle,toe,tip=[n+'_'+side for n in ['Hip','Knee','Ankle','MiddleToe1','MiddleToe2']]
        s.tube('Close trouser underlayer '+side,[B[hip],B[knee],B[ankle]],
               [.107,.078,.056],[.095,.071,.053],[hip,knee,ankle],0,sides=8)
        blend={knee:.30,ankle:.70}
        s.tube('Single supple leather boot shaft '+side,[B[ankle]+p(y=.196),B[ankle]+p(y=.108),B[ankle]-p(y=.055)],
               [.100,.094,.087],[.090,.086,.086],[blend,ankle,ankle],4,sides=8)
        s.tube('Same-leather rolled boot lip '+side,[B[ankle]+p(y=.171),B[ankle]+p(y=.202)],
               [.104,.102],[.094,.092],[blend]*2,4,sides=8)
        floor=B[toe][1]-.035
        centers=[p(B[ankle][0],floor+.073,B[ankle][2]-.065),
                 p(B[ankle][0],floor+.088,B[ankle][2]+.034),
                 p(B[toe][0],floor+.069,B[toe][2]),
                 p(B[tip][0],floor+.049,B[tip][2]+.012)]
        s.tube('Broad rounded leather boot foot '+side,centers,[.091,.111,.105,.072],
               [.062,.073,.053,.033],[ankle,ankle,toe,tip],4,sides=8)
        sole=[p(v[0],floor+.013,v[2]) for v in [centers[0],centers[2],centers[3]]]
        s.tube('Dark durable boot sole '+side,sole,[.096,.112,.075],[.015]*3,[ankle,toe,tip],6,sides=8)
        q=B[ankle]+p(y=.104)
        s.tube('Single boot retaining strap '+side,[q-p(y=.020),q+p(y=.020)],
               [.097]*2,[.091]*2,[ankle]*2,6,sides=8)


def dagger():
    s=m.rigid()
    # Straight spine and one clipped point; only the cutting side is pale.
    outline=[(-.045,.119),(.044,.119),(.044,.432),(-.034,.515),(-.045,.477)]
    inner=[(-.039,.123),(.035,.123),(.035,.428),(-.033,.500),(-.039,.474)]
    n=len(outline)
    verts=[(x,y,0) for x,y in outline]+[(x,y,.017) for x,y in inner]+[(x,y,-.017) for x,y in inner]
    faces=[];colors=[]
    for i in range(n):
        j=(i+1)%n
        faces += [(i,j,j+n,i+n),(j,i,i+2*n,j+2*n)]
        colors += [10 if i in [1,2] else 8]*2
    faces += [tuple(range(n,2*n)),tuple(range(3*n-1,2*n-1,-1))]
    colors += [9,8]
    solid(s,'Straight clipped dark steel blade',verts,faces,'Root',colors)
    m.tube(s,'Continuous fitted leather grip',[p(y=-.119),p(y=.124)],[.034,.034],[.027,.027],4,sides=8)
    m.tube(s,'Short iron bolster',[p(y=.110),p(y=.133)],[.048,.049],[.027,.027],8,sides=8)
    m.tube(s,'Plain dark pommel',[p(y=-.132),p(y=-.116)],[.037,.037],[.030,.030],6,sides=8)
    m.tube(s,'One warm grip binding',[p(y=.083),p(y=.105)],[.035,.035],[.028,.028],5,sides=8)
    return s


def transform(data, matrix):
    result=copy.deepcopy(data)
    result['positions']=(np.array(data['positions'])@matrix[:3,:3].T+matrix[:3,3]).tolist()
    normals=np.array(data['normals'])@np.linalg.inv(matrix[:3,:3])
    result['normals']=(normals/np.linalg.norm(normals,axis=1)[:,None]).tolist()
    return result


def rotation(axis, angle):
    c,s=math.cos(angle),math.sin(angle);r=np.eye(4)
    if axis=='Y':r[:3,:3]=[[c,0,s],[0,1,0],[-s,0,c]]
    else:r[:3,:3]=[[c,-s,0],[s,c,0],[0,0,1]]
    return r


def local_matrix(row):
    x,y,z,w=row['localRotation']
    r=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],
                [2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],
                [2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])
    mtx=np.eye(4);mtx[:3,:3]=r@np.diag(row['localScale']);mtx[:3,3]=row['localPosition']
    return mtx


def subset(data, pieces):
    result={k:[] for k in ['positions','normals','uvs','triangles']};out=[]
    for piece in pieces:
        start=len(result['positions']);a=piece['vertex_start'];b=a+piece['vertex_count']
        for field in ['positions','normals','uvs']:result[field]+=data[field][a:b]
        result['triangles'] += [[x-a+start for x in tri] for tri in data['triangles'] if a<=tri[0]<b]
        out.append(dict(piece,vertex_start=start))
    return result,out


def main():
    ap=argparse.ArgumentParser();ap.add_argument('--bindings',type=Path,required=True);args=ap.parse_args()
    previous=json.loads((BASE/'manifest.json').read_text())
    records=[]
    def emit(name,s,binding=None,data=None,pieces=None,palette='thief-street-v2-palette.png'):
        if data is None:
            m.orient_outward(s)
            data=copy.deepcopy(s.data)
            if binding is None:data={k:v for k,v in data.items() if k not in ['bone_names','weights','joints']}
        pieces=s.pieces if pieces is None else pieces
        save(name+'.source.json',data);save(name+'.pieces.json',pieces)
        if binding:m.write_glb(OUT/(name+'.glb'),data,{'bone_names':np.array(binding['bone_names']),'bindposes':np.array(binding['bindposes'])})
        else:m.write_static_glb(OUT/(name+'.glb'),data)
        records.append({'file':name+'.glb','sha256':digest(OUT/(name+'.glb')),
                        'packageCounterpart':'assets/'+name+'.glb','texture':palette,
                        'vertices':len(data['positions']),'triangles':len(data['triangles']),
                        'binding': None if binding is None else {k:binding[k] for k in ['rendererId','nativeMeshName','bone_names']}})
        return data
    for family,sex,filename in [('coat','male','male-armor.json'),('coat','female','female-armor.json'),('boots','shared','boots.json')]:
        path=args.bindings/filename;binding=json.loads(path.read_text())
        prev=next(x for x in previous['items'] if x['band']=='street' and x['family']==family)
        model=next(x for x in prev['models'] if x['sex']==sex)
        assert digest(path)==model['bindingSha256'], 'Binding must match the established Street source'
        s=Surface(binding['bone_names'],np.linalg.inv(binding['bindposes'])[:,:3,3])
        coat(s,sex=='male') if family=='coat' else boots(s)
        name='thief-'+family+'-street'+('-'+sex if family=='coat' else '')
        emit(name,s,binding)
        records[-1]['bindingSha256']=digest(path)
    s=crown();m.orient_outward(s)
    cap={k:copy.deepcopy(v) for k,v in s.data.items() if k not in ['bone_names','weights','joints']}
    matrix=np.eye(4);matrix[1,3]=-.55
    emit('thief-hood-street',s,data=transform(cap,matrix))
    emit('thief-hood-street-display',s,data=cap)
    s=dagger();m.orient_outward(s)
    knife={k:copy.deepcopy(v) for k,v in s.data.items() if k not in ['bone_names','weights','joints']}
    eq=rotation('Z',math.radians(55))@rotation('Y',math.pi/2)
    mounted=transform(knife,eq)
    emit('thief-street-twins',s,data=mounted)
    route=json.loads((BASE.parent/'native-route.json').read_text())
    rows={r['path']:r for r in next(t for t in route['templates'] if t['template']=='dualKnife')['hierarchy']}
    b=local_matrix(rows['dualKnife/Break'])
    for n,selected,mtx in [(1,s.pieces[:1],b),(2,s.pieces[1:],b@local_matrix(rows['dualKnife/Break/Break']))]:
        data,pieces=subset(mounted,selected)
        emit('thief-street-twins-fragment-'+str(n),s,data=transform(data,np.linalg.inv(mtx)),pieces=pieces)
    for path,x,ang,suffix in [('dualKnife',-.13,.24,'-display'),('offHandWeapon',.13,-.24,'-display-offhand')]:
        pose=rotation('Z',ang);pose[:3,3]=[x,-.17,0]
        emit('thief-street-twins'+suffix,s,data=transform(knife,np.linalg.inv(local_matrix(rows[path]))@pose))
    palette=Image.new('RGB',(len(COLORS)*16,16))
    for i,c in enumerate(COLORS):palette.paste(tuple(int(c[j:j+2],16) for j in [0,2,4]),(i*16,0,(i+1)*16,16))
    palette.save(OUT/'thief-street-v2-palette.png')
    save('manifest.json',{'schema':'ftkmf.thief-street-art-prototype.v2','generator':'build.py',
        'generatorSha256':digest(Path(__file__)),'palette':COLORS,'assets':records,
        'paletteFile':'thief-street-v2-palette.png','paletteSha256':digest(OUT/'thief-street-v2-palette.png'),
        'provenance':'Original geometry, normals, palette, UVs and weights. Reuses original Surface helper. Native data is limited to the already-pinned Street bone names, inverse binds, renderer paths and mount transforms. Native screenshots inform visual review only.',
        'mounts':{'headwear':'Existing helmKettle crown mount; authored face-center offset Y=-0.55.','daggers':'Existing dualKnife main and offhand renderer paths; identical GLB per hand; Z55 @ Y90 unchanged.'},
        'status':'Offline prototype only. Native fit, motion, ordinary gameplay, female and alternate profiles remain unverified.'})
    print('Built',len(records),'GLBs in prototype only')


if __name__=='__main__':main()
