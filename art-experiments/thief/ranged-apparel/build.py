#!/usr/bin/env python3
"""Author Thief bows, light apparel and trinkets from original faceted geometry."""
import argparse, copy, hashlib, importlib.util, json, math, shutil, sys
from pathlib import Path
import numpy as np
from PIL import Image

OUT=Path(__file__).resolve().parent
ROOT=OUT.parents[2]
PACKAGE=ROOT/'marketplace/packages/thief/assets'
spec=importlib.util.spec_from_file_location('original_surface',ROOT/'art-experiments/hearthveil-blacksmith/build_geometry.py')
lib=importlib.util.module_from_spec(spec);spec.loader.exec_module(lib)
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from export_ftk_glb import write_glb,write_static_glb
COLORS=['202A35','6A4A35','937458','355A7C','245968','A9996B','CCC8AF','693F55','52614C','849CA9','BCCDD1','303744']
lib.PALETTE=COLORS
Surface=lib.Surface
BANDS=['street','burglar','guild','masterwork','locksmith','nightblade','wayfarer']
NAMES={
 'bow':['Rooftop Bow','Alley Recurve','Guild Shortbow','Gloamwood Bow','Latchspring','Blackthorn','Farstep'],
 'coat':['Patched Jack',"Burglar's Jack",'Guild Leather','Masterwork Jack',"Locksmith's Coat",'Nightblade Jack',"Wayfarer's Coat"],
 'hood':['Street Neckerchief',"Burglar's Hood",'Guild Hood','Masterwork Cowl',"Locksmith's Hood",'Nightblade Cowl',"Wayfarer's Hood"],
 'boots':['Softstep Shoes',"Burglar's Boots",'Guild Treads','Masterwork Treads',"Locksmith's Steps",'Nightblade Steps',"Wayfarer's Treads"],
 'charm':['Bent Copper','Brass Pick','Guild Token','Silver Rook','Master Keyring','Snuffed Wick','Trail Compass']}
def p(x=0,y=0,z=0):return np.array([x,y,z],float)
def offset(v,x=0,y=0,z=0):return v+p(x,y,z)
def shade(i):return [3,0,4,3,4,0,8][i]
def trim(i):return [2,9,5,9,5,6,6][i]
def rigid():return Surface(['Root'],np.zeros((1,3)))
def tube(s,label,points,radii,depth,color,skin='Root',sides=8):
    s.tube(label,points,radii if isinstance(radii,list) else [radii]*len(points),depth if isinstance(depth,list) else [depth]*len(points),[skin]*len(points),color,sides=sides)
def bead(s,label,at,size,color,skin='Root',sides=8):s.ellipsoid(label,at,size,skin,color,sides=sides)
def ring(s,label,center,rx,ry,thick,color,skin='Root',steps=12):
    points=[center+p(rx*math.cos(j*math.tau/steps),ry*math.sin(j*math.tau/steps),0) for j in range(steps+1)]
    tube(s,label,points,thick,thick,color,skin,6)
def panel(s,label,outline,depth,color,skin='Root'):
    verts=[np.asarray(v) for v in outline];middle=np.mean(verts,axis=0)
    start=len(s.data['positions'])
    for n,a in enumerate(verts):
        b=verts[(n+1)%len(verts)]
        for tri in [[middle,a,b],[middle-p(z=depth),b-p(z=depth),a-p(z=depth)],
                    [a,a-p(z=depth),b-p(z=depth)],[a,b-p(z=depth),b]]:
            s.triangle(tri,[skin]*3,color)
    s.pieces.append({'name':label,'vertex_start':start,'vertex_count':len(s.data['positions'])-start})

def bow(i):
    s=rigid();length=[.64,.68,.66,.70,.65,.74,.71][i]
    # Each branch changes limb sweep, tips, grip and reinforcement construction.
    profiles=[[(0,0),(.045,.22),(.10,.48),(.19,.78),(.22,1)],
              [(0,0),(.04,.25),(.14,.58),(.24,.83),(.18,1)],
              [(0,0),(.03,.22),(.11,.60),(.20,.84),(.15,1)],
              [(0,0),(.03,.25),(.13,.56),(.23,.83),(.16,1)],
              [(0,0),(.035,.24),(.12,.59),(.19,.83),(.13,1)],
              [(0,0),(.035,.27),(.16,.64),(.25,.89),(.21,1)],
              [(0,0),(.025,.22),(.095,.54),(.18,.83),(.12,1)]]
    for sign in [-1,1]:
        pts=[p(x,sign*y*length) for x,y in profiles[i]]
        tube(s,'Continuous carved limb '+str(sign),pts,[.038,.041,.035,.024,.013],[.030,.028,.023,.018,.009],11 if i==5 else 1)
        if i>=2:
            tube(s,'Laminated pale back '+str(sign),[v+p(z=.027) for v in pts[1:-1]],[.016,.014,.009],.004,trim(i),sides=6)
        tube(s,'Tip nock '+str(sign),[pts[-1]-p(y=sign*.03),pts[-1]+p(y=sign*.008)],.018,.013,trim(i),sides=6)
        if i==4:
            for j in range(3):bead(s,'Latchspring brass stud',pts[1]+p(y=sign*j*.045,z=.03),(.014,.014,.007),5,sides=6)
        if i==5:
            panel(s,'Blackthorn raised thorn', [pts[2]+p(-.025,-sign*.035,.015),pts[2]+p(-.07,sign*.015,.015),pts[2]+p(.01,sign*.045,.015)],.015,0)
    end=profiles[i][-1][0]
    tube(s,'Unbent display bowstring',[p(end,-length),p(end,0),p(end,length)],.0045,.0045,6,sides=6)
    tube(s,'Wrapped central grip',[p(y=-.10),p(y=.10)],.044,.034,shade(i))
    for j in range(5):tube(s,'Separate grip wrap '+str(j),[p(y=-.088+j*.04),p(y=-.065+j*.04)],.046,.037,trim(i) if j==0 else shade(i),sides=8)
    if i in [2,3,4,6]:
        panel(s,'Inset grip badge',[p(-.018,-.025,.04),p(.018,-.025,.04),p(.018,.023,.04),p(0,.037,.04),p(-.018,.023,.04)],.008,trim(i))
    return s

def charm(i):
    s=rigid()
    if i in [0,2,6]:
        bead(s,'Faceted coin or compass case',p(),(.17,.17,.026),2 if i==0 else 5,sides=12)
        ring(s,'Raised metal rim',p(z=.026),.147,.147,.009,2 if i==0 else 5)
        if i==0:
            panel(s,'Bent copper face',[p(-.10,-.06,.03),p(.02,-.08,.052),p(.10,.07,.034),p(-.06,.10,.028)],.009,2)
            tube(s,'Coin wear slash',[p(-.075,.01,.045),p(.065,.055,.044)],.008,.005,1)
        if i==2:
            panel(s,'Guild rook relief',[p(-.06,-.09,.04),p(.06,-.09,.04),p(.04,.065,.04),p(.075,.075,.04),p(.075,.11,.04),p(-.075,.11,.04),p(-.075,.075,.04),p(-.04,.065,.04)],.012,4)
        if i==6:
            bead(s,'Compass dark dial',p(z=.035),(.121,.121,.008),4,sides=12)
            panel(s,'Ivory north needle',[p(-.031,-.008,.05),p(0,.108,.05),p(.031,-.008,.05)],.007,6)
            panel(s,'Brass south needle',[p(-.026,-.008,.05),p(.026,-.008,.05),p(0,-.090,.05)],.007,5)
    elif i==1:
        ring(s,'Brass pick handle',p(y=.13),.065,.055,.017,5)
        tube(s,'Slim brass pick shank',[p(y=.08),p(y=-.18),p(.065,-.16)],.018,.016,5,sides=6)
    elif i==3:
        tube(s,'Silver rook pedestal',[p(y=-.12),p(y=-.085)],.11,.08,10)
        tube(s,'Tapered rook tower',[p(y=-.085),p(y=.095)],[.075,.054],[.063,.052],10)
        tube(s,'Rook battlement collar',[p(y=.09),p(y=.13)],.092,.067,10)
        for x in [-.066,0,.066]:bead(s,'Rook crenellation',p(x,.155),(.023,.033,.06),10,sides=4)
    elif i==4:
        ring(s,'Master ring',p(y=.08),.13,.105,.018,5)
        for j in range(3):
            x=-.085+j*.08
            ring(s,'Individual key bow '+str(j),p(x,.025,.018+j*.012),.027,.032,.009,5)
            tube(s,'Key stem '+str(j),[p(x,-.007,.018+j*.012),p(x,-.17-j*.02,.018+j*.012)],.010,.009,5,sides=6)
            tube(s,'Distinct key tooth '+str(j),[p(x,-.14-j*.02,.018+j*.012),p(x+.04,-.14-j*.02,.018+j*.012)],.012,.011,5,sides=4)
    else:
        tube(s,'Dark candle socket',[p(y=-.10),p(y=-.05)],.11,.085,0)
        tube(s,'Short extinguished candle',[p(y=-.055),p(y=.10)],[.055,.047],[.046,.041],6)
        tube(s,'Bent black wick',[p(y=.10),p(.012,.15),p(.030,.16)],.009,.008,0,sides=6)
        tube(s,'Wax trickle',[p(.048,.08,.016),p(.051,-.015,.016)],[.010,.007],[.009,.006],6,sides=6)
    if i in [0,2,6]:tube(s,'Attached suspension lug',[p(y=.14,z=-.005),p(y=.197,z=-.005)],.013,.012,5,sides=6)
    if i==5:tube(s,'Wire socket bail',[p(.085,-.075,-.024),p(.115,.09,-.024),p(0,.196,-.024)],.009,.009,5,sides=6)
    ring(s,'Cord eye',p(y=.225),.028,.034,.008,5)
    return s

def orient_outward(s):
    # Components are individually closed; correct their signed exterior independently.
    positions=np.array(s.data['positions'])
    for piece in s.pieces:
        low=piece['vertex_start'];high=low+piece['vertex_count']
        tris=[tri for tri in s.data['triangles'] if low<=tri[0]<high]
        volume=sum(np.dot(positions[a],np.cross(positions[b],positions[c])) for a,b,c in tris)/6
        if volume<0:
            for tri in tris:tri.reverse()
            for n in range(low,high):s.data['normals'][n]=(-np.array(s.data['normals'][n])).tolist()

def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def save(path,data):path.write_text(json.dumps(data,separators=(',',':'))+'\n')
def presentation(s,family):
    data={k:copy.deepcopy(v) for k,v in s.data.items() if k not in ['joints','weights','bone_names']}
    removed=set()
    for piece in s.pieces:
        label=piece['name']
        if family=='boots' and label.startswith('Close trouser') or family=='coat' and any(x in label for x in ['Fitted cloth trousers','Continuous trouser pelvis','Glove or trouser underlining','Rear collar lining']):
            removed.update(range(piece['vertex_start'],piece['vertex_start']+piece['vertex_count']))
        if family=='coat' and any(x in label for x in ['Fitted sleeve','Shaped leather bracer','Bracer retaining strap','Soft layered shoulder mantle','Shoulder mantle rim']):
            side='R' if any(token in label for token in [' R',' R0']) else 'L';sign=1 if side=='R' else -1
            anchor=s.B['Scapula_'+side];angle=-sign*math.radians(62)
            rot=np.array([[math.cos(angle),-math.sin(angle),0],[math.sin(angle),math.cos(angle),0],[0,0,1]])
            for index in range(piece['vertex_start'],piece['vertex_start']+piece['vertex_count']):
                data['positions'][index]=(rot@(np.array(data['positions'][index])-anchor)+anchor).tolist()
                data['normals'][index]=(rot@data['normals'][index]).tolist()
    keep=[n for n in range(len(data['positions'])) if n not in removed];mapping={v:i for i,v in enumerate(keep)}
    for field in ['positions','normals','uvs']:data[field]=[data[field][n] for n in keep]
    data['triangles']=[[mapping[n] for n in t] for t in data['triangles'] if not any(n in removed for n in t)]
    return data
def main():
    ap=argparse.ArgumentParser();ap.add_argument('--bindings',type=Path,required=True);ap.add_argument('--apparel-only',action='store_true');args=ap.parse_args()
    OUT.mkdir(parents=True,exist_ok=True);PACKAGE.mkdir(parents=True,exist_ok=True)
    previous=json.loads((OUT/'manifest.json').read_text()) if (OUT/'manifest.json').exists() else {}
    records=[r for r in previous.get('items',[]) if r['family']=='bow'] if args.apparel_only else []
    for i,band in enumerate(BANDS):
        for family in (['coat','hood','boots','charm'] if args.apparel_only else ['bow','coat','hood','boots','charm']):
            lib.PALETTE=COLORS if family=='bow' else redesign.COLORS
            key='thief-'+family+'-'+band;models=[]
            for sex in (['female','male'] if family=='coat' else ['shared']):
                binding=None;s=None;name=key+('-'+sex if family=='coat' else '')
                if family in ['coat','boots']:
                    binding_path=args.bindings/(sex+'-armor.json' if family=='coat' else 'boots.json')
                    binding=json.loads(binding_path.read_text());binds=np.array(binding['bindposes'])
                    prior=next((r for r in previous.get('items',[]) if r['id']==key.replace('-','_')),None)
                    if prior:
                        old=next(v for v in prior['models'] if v['sex']==sex)
                        assert digest(binding_path)==old['bindingSha256'], 'Exact established binding required'
                        assert binding['rendererId']==old['rendererId'] and binding['nativeMeshName']==old['nativeMeshName']
                    s=Surface(binding['bone_names'],np.linalg.inv(binds)[:,:3,3])
                    coat(s,i,sex=='male') if family=='coat' else boots(s,i)
                else:s={'bow':bow,'hood':hood,'charm':charm}[family](i)
                orient_outward(s)
                data=s.data
                if not binding:data={k:v for k,v in data.items() if k not in ['joints','weights','bone_names']}
                # Native helmKettle mounts at the crown. Authored headwear uses a
                # face-centered studio origin; only equipped geometry changes space.
                if family=='hood':
                    data=copy.deepcopy(data)
                    data['positions']=[(np.array(v)+p(y=-.55)).tolist() for v in data['positions']]
                save(OUT/(name+'.source.json'),data);save(OUT/(name+'.pieces.json'),s.pieces)
                if binding:write_glb(OUT/(name+'.glb'),data,{'bone_names':np.array(binding['bone_names']),'bindposes':binds})
                else:write_static_glb(OUT/(name+'.glb'),data)
                model={'file':name+'.glb','source':name+'.source.json','sex':sex,'vertices':len(data['positions']),'triangles':len(data['triangles'])}
                if binding:model.update(bindingInput=binding_path.name,bindingSha256=digest(binding_path),rendererId=binding['rendererId'],nativeMeshName=binding['nativeMeshName'])
                models.append(model)
                if sex in ['shared','female']:
                    display=presentation(s,family)
                    save(OUT/(key+'-display.source.json'),display)
                    write_static_glb(OUT/(key+'-display.glb'),display)
            records.append({'id':key.replace('-','_'),'name':NAMES[family][i],'family':family,'band':band,'models':models,'display':key+'-display.glb','icon':key+'-icon.png','palette':'thief-ranged-apparel-palette.png' if family=='bow' else 'thief-apparel-palette.png','motif':None if family=='bow' else redesign.MOTIFS[i]})
    for filename,colors in [('thief-apparel-palette.png',redesign.COLORS)]+([] if args.apparel_only else [('thief-ranged-apparel-palette.png',COLORS)]):
        image=Image.new('RGB',(len(colors)*16,16))
        for i,c in enumerate(colors):image.paste(tuple(int(c[j:j+2],16) for j in [0,2,4]),(i*16,0,(i+1)*16,16))
        image.save(OUT/filename)
    records.sort(key=lambda r:(BANDS.index(r['band']),['bow','coat','hood','boots','charm'].index(r['family'])))
    manifest=dict(previous)
    manifest.update(schema='ftkmf.thief-ranged-apparel-art.v1',generator='build.py',generatorSha256=digest(Path(__file__)),palette=COLORS,
        apparelPalette=redesign.COLORS,items=records,
        status='Open-face apparel art candidate. Offline validation and renders do not establish native fit, motion, appearance selection, displays or art approval. Previous native-fit records cover superseded bytes.',
        provenance='Original geometry, normals, palette, UVs, weights and rendered icons. Generic original Surface helper reused. No native surface, texture, bounds, UV, weights or animation data used. Only pinned bone names and inverse binds inform apparel joints.')
    save(OUT/'manifest.json',manifest)
    print('Authored',len(records),'items with',sum(len(r['models']) for r in records),'equipped/source GLBs and corresponding presentation GLBs')

import redesign
redesign.install(sys.modules[__name__])

if __name__=='__main__':main()
