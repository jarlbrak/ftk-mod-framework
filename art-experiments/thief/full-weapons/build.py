#!/usr/bin/env python3
"""Build the complete original Thief weapon family without touching apparel."""
import copy, importlib.util, json, math, shutil, sys
from pathlib import Path
import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.geometry import tessellate_polygon

OUT=Path(__file__).resolve().parent
ROOT=OUT.parents[2]
PACKAGE=ROOT/'marketplace/packages/thief/assets'
def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path)
    m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
g=module('weapon_primitives',OUT.parent/'build.py')
a=module('artifact_primitives',OUT.parent/'artifacts/build.py')
a.base=g
g.OUT=OUT
BANDS=['street','burglar','guild','masterwork','locksmith','nightblade','wayfarer']
PALETTES=[
 ['39444A','798C93','AAB8B7','302A26','654934','443535','647071','9A8156'],
 ['354248','819397','B0BDBA','2E2924','705039','3F5051','74837F','A68B5C'],
 ['34434E','8799A0','B8C1BB','28333C','455B69','374857','879995','AD925F'],
 ['3B414B','8C9AA4','BDC5C0','303039','565262','454951','959CA2','B1A68A'],
 ['35484B','849B9B','B7C1B1','2A3E3E','436164','384C50','92A49B','AC925C'],
 ['323741','687987','A5B0B2','302730','57404C','3D3644','8B858F','A18E70'],
 ['3E4B45','87998C','B9C4AD','353028','6D5F40','465C49','8F9B79','AA986D']]
BOW_PALETTE=['30343A','705039','9A7952','3C5261','385C5D','A38B5B','AEA88F','624451','536246','7B8D95','A8B6B6','383C47']
ART_PALETTE=['34434A','839A9E','B9C3B7','89704C','AD945F','334D50','6C9F95','343640','514854','ABA58F','A77937','C49E56','574431','88704E','536247','8B9775']
RECORDS=[]

def reset(colors):
    g.COLORS=colors;g.DATA={f:[] for f in ['positions','normals','uvs','triangles']};g.PIECES=[]
def snap():return copy.deepcopy(g.DATA),copy.deepcopy(g.PIECES)
def palette(name,colors):
    image=bpy.data.images.new(name,width=len(colors)*16,height=16)
    image.pixels=[v for _ in range(16) for c in colors for _ in range(16) for v in [*(int(c[i:i+2],16)/255 for i in [0,2,4]),1]]
    image.filepath_raw=str(OUT/name);image.file_format='PNG';image.save();g.strip_metadata(OUT/name)
def clipped_blade(name,outline,face=1,back=0,edge=2,thickness=.017):
    # A broad steel face occupies most of the blade. Only the cutting side is bright.
    n=len(outline);center=np.mean(outline,axis=0)
    inner=[center+(np.array(p)-center)*.89 for p in outline]
    vertices=[(x,y,0) for x,y in outline]+[(x,y,z) for z in [thickness,-thickness] for x,y in inner]
    faces=[];colors=[]
    for i in range(n):
        j=(i+1)%n
        faces += [(i,j,j+n,i+n),(j,i,i+2*n,j+2*n)]
        colors += [edge if i in (1,2) else back]*2
    # The key tooth is concave; triangulate caps without crossing its notch.
    vectors=[Vector((float(x),float(y),0)) for x,y in inner]
    cap=[tuple(p if isinstance(p,int) else min(range(n),key=lambda i:(vectors[i]-p).length) for p in tri) for tri in tessellate_polygon([vectors])]
    faces += [tuple(i+n for i in tri) for tri in cap]+[tuple(i+2*n for i in reversed(tri)) for tri in cap]
    colors += [face]*len(cap)+[back]*len(cap)
    g.component(name,vertices,faces,colors)
def ordinary(band,i):
    reset(PALETTES[i])
    lengths=[.505,.535,.565,.595,.574,.592,.560]
    widths=[.045,.051,.050,.055,.041,.044,.059]
    length=lengths[i];w=widths[i]
    shoulder=.33 if i==0 else .35
    outline=[(-w,.122),(w,.122),(w*1.10,shoulder),(-w*.18,length),(-w,shoulder+.018)]
    if band=='wayfarer':outline=[(-w,.122),(w,.122),(w*1.15,.302),(w*.80,.428),(-.008,length),(-w,.354)]
    clipped_blade('Straight spine clipped working blade',outline)
    g.cylinder('Fitted dark leather grip',-.005,.034,.234,3,.78)
    for y in ([-.082,.079] if i==0 else [-.078,-.025,.029,.080]):g.cylinder('Leather grip winding',y,.035,.014,4,.79)
    g.cylinder('Compact steel bolster',.118,.049 if i<2 else .057,.025,0,.65)
    if i==0:g.cylinder('One repaired warm leather binding',.086,.036,.025,4,.8)
    if band in ['guild','masterwork','locksmith']:
        wguard=.073 if band=='masterwork' else .065
        a.prism('Short brass finger stop',[(-wguard,.110),(wguard,.110),(wguard*.87,.131),(-wguard*.87,.131)],.027,7)
    elif band=='nightblade':a.prism('Angled dark finger stop',[(-.059,.109),(.059,.119),(.051,.133),(-.051,.123)],.026,0)
    elif band=='wayfarer':a.prism('Broad field finger stop',[(-.064,.110),(.064,.110),(.052,.135),(-.052,.135)],.029,6)
    if band=='locksmith':
        g.cylinder('Key bow collar',-.125,.032,.025,7,.8);a.ring('Open key bow pommel',0,-.160,.031,.034,.009,7)
    else:
        g.cylinder('Low faceted pommel',-.133,.038 if i<2 else .043,.024,0 if i<2 or i==5 else 7,.80)
        if band=='guild':a.prism('Small guild seal',[(-.015,-.142),(.015,-.142),(.015,-.120),(-.015,-.120)],.071,5)
        if band=='masterwork':a.prism('Steel balance diamond',[(-.018,-.132),(0,-.153),(.018,-.132),(0,-.117)],.075,1)
        if band=='wayfarer':a.ring('Small cord eye',0,-.159,.013,.014,.004,7)
    return snap()

def artifact(kind,short):
    reset(ART_PALETTE)
    if kind=='skeleton-key':
        # A clipped stepped blade and a compact pick retain the key pair's asymmetry.
        outline=([(-.037,.123),(.044,.123),(.050,.336),(.074,.404),(-.006,.520),(-.038,.355)] if short else
                 [(-.046,.123),(.046,.123),(.046,.209),(.067,.209),(.067,.239),(.046,.239),(.045,.430),(-.008,.604),(-.046,.431)])
        clipped_blade('Compact pick blade' if short else 'Stepped key blade',outline,1,0,2)
        a.grip(0,5,3)
        a.prism('Low stepped lock guard',[(-.059,.108),(.060,.108),(.060,.128),(.030,.128),(.030,.139),(-.030,.139),(-.030,.128),(-.059,.128)],.026,3)
        a.ring('Open key bow pommel',0,-.166,.036 if short else .045,.035,.010,4)
        a.prism('Small patinated inset',[(-.010,.076),(0,.091),(.010,.076),(0,.061)],.058,6)
    else:
        outline=[(-.047,.124),(.047,.124),(.061,.334),(.043,.454),(-.008,.553 if short else .604),(-.047,.404)]
        clipped_blade('Blackened clipped candle blade',outline,7,0,2,.018)
        a.prism('Narrow non-emissive amber inlay',[(-.006,.167),(.006,.167),(.006,.405),(0,.454),(-.006,.405)],.040,10 if short else 11)
        a.grip(8 if short else 9,0 if short else 3,3)
        a.tube('Short snuffer finger stop',[(-.057,.137),(-.044,.107),(0,.103),(.044,.107),(.057,.137)],.013,.023,3)
        a.prism('Compact snuffer pommel',[(-.022,-.131),(.022,-.131),(.031,-.167),(-.031,-.167)],.031,3)
        g.cylinder('Dull brass snuffer rim',-.166,.033,.009,4,.6)
    return snap()

def bow(i,artifact_bow=False):
    reset(ART_PALETTE if artifact_bow else BOW_PALETTE)
    lengths=[.59,.62,.61,.64,.60,.65,.63,.625]
    profiles=[[(0,0),(.035,.24),(.09,.56),(.16,.82),(.19,1)],
              [(0,0),(.03,.24),(.11,.58),(.21,.85),(.15,1)],
              [(0,0),(.028,.23),(.105,.57),(.19,.84),(.13,1)],
              [(0,0),(.032,.23),(.12,.55),(.22,.82),(.135,1)],
              [(0,0),(.025,.25),(.105,.59),(.175,.84),(.105,1)],
              [(0,0),(.025,.28),(.13,.60),(.22,.88),(.175,1)],
              [(0,0),(.022,.23),(.085,.55),(.165,.82),(.095,1)],
              [(0,0),(.02,.24),(.10,.54),(.175,.78),(.16,.91),(.075,1)]]
    if artifact_bow:i=7
    height=lengths[i];profile=profiles[i];wood=12 if artifact_bow else 11 if i==5 else 1
    trim=9 if artifact_bow else [2,2,5,9,5,9,6][i]
    grip=14 if artifact_bow else [3,0,4,7,4,0,8][i]
    for sign in [-1,1]:
        points=[(x,sign*y*height) for x,y in profile]
        widths=[.058,.063,.055,.041,.021] if i!=7 else [.059,.063,.057,.043,.030,.018]
        a.tube('Carved limb '+str(sign),points,widths,.041,wood)
        if i>=2:a.tube('Laminated limb back '+str(sign),[(x-.012,y) for x,y in points[1:-1]],.013,.044,trim)
        end=points[-1]
        a.tube('Bound tip nock '+str(sign),[(end[0],end[1]-sign*.020),(end[0],end[1]+sign*.007)],.026,.043,trim)
        if i==4:
            a.prism('Brass latch plate '+str(sign),[(.015,sign*.115),(.038,sign*.115),(.045,sign*.180),(.020,sign*.177)],.046,5)
        if i==5:
            a.prism('Small blackthorn spur '+str(sign),[(.110,sign*.335),(.075,sign*.376),(.145,sign*.400)],.037,0)
        if artifact_bow:
            a.tube('Warm heartwood seam '+str(sign),[(x+.013,y) for x,y in points[1:-1]],.008,.047,13)
    g.cylinder('Dark wrapped central grip',0,.035,.195,grip,.78)
    for y in [-.079,-.035,.009,.053,.087]:g.cylinder('Broad leather grip wrap',y,.036,.014,grip,.8)
    for y in [-.103,.103]:g.cylinder('Grip binding collar',y,.037,.015,trim,.80)
    if artifact_bow:
        a.prism('Small brass trail arrow',[(-.017,-.035),(.017,-.035),(.017,.003),(.026,.008),(0,.035),(-.026,.008),(-.017,.003)],.061,4)
        a.prism('Single tucked moss grip tab',[(-.027,-.019),(-.027,.016),(-.076,.033),(-.092,.018),(-.070,-.008)],.020,14)
    elif i in [2,3,4,6]:a.prism('Inset grip badge',[(-.012,-.022),(.012,-.022),(.012,.014),(0,.026),(-.012,.014)],.060,trim)
    a.tube('Taut original bowstring',[(profile[-1][0],-height),(profile[-1][0],height)],.0038,.0038,9 if artifact_bow else 6)
    return snap()

def write(name,data,pieces):g.write(name,data,pieces)
def select(data,pieces,selected):
    g.DATA=data;g.PIECES=pieces;return g.subset(selected)
def combined(parts):return a.combine(parts)
def trans(data,matrix):return g.transform(data,matrix)
def emit(entry,main,pieces,off=None,offpieces=None,palette_name=None,colors=None):
    key=Path(entry['icon']).name.removesuffix('-icon.png')
    paired=entry['template']=='dualKnife';records=[];displays=[];fragments=[]
    eq=g.equipped_orientation() if paired else Matrix.Identity(4)
    source_main=trans(main,eq);source_off=trans(off,eq) if off is not None else source_main
    offpieces=offpieces or pieces
    mainkey=Path(entry['itemModels'][0]['model']).stem
    if paired:
        write(mainkey,source_main,pieces)
        offkey=Path(entry['offHandModels'][0]['model']).stem
        if offkey!=mainkey:write(offkey,source_off,offpieces)
        route=json.loads((OUT.parent/'native-route.json').read_text())
        rows={r['path']:r for r in next(t for t in route['templates'] if t['template']=='dualKnife')['hierarchy']}
        fragment_matrices=[g.local_matrix(rows['dualKnife/Break']),g.local_matrix(rows['dualKnife/Break'])@g.local_matrix(rows['dualKnife/Break/Break'])]
        cut=2 if 'candles_end' in entry['id'] else 1
        selections=[pieces[:cut],pieces[cut:]]
        for d,(source,sourcekey,pp,x,angle,path) in zip(entry['displayModels'],[(source_main,mainkey,pieces,-.115,.20,'dualKnife'),(source_off,offkey,offpieces,.115,-.20,'offHandWeapon')]):
            pose=Matrix.Translation(Vector((x,-.18,0)))@Matrix.Rotation(angle,4,'Z')@eq.inverted()
            matrix=g.local_matrix(rows[path]);name=Path(d['model']).stem
            displayed=trans(source,pose);displays.append(displayed)
            write(name,trans(displayed,matrix.inverted()),pp)
            records.append({'key':name,'sourceKey':sourcekey,'rendererToDisplayRoot':[list(r) for r in matrix],'authoredToDisplayRoot':[list(r) for r in pose]})
    else:
        string,stringpieces=select(main,pieces,pieces[-1:]);source_main,pieces=select(main,pieces,pieces[:-1])
        write(mainkey,source_main,pieces)
        rows={r['path']:r for r in json.loads((OUT.parent/'ranged-apparel/bow-route.json').read_text())['rows']}
        def bm(path):
            r=rows['bowShort/'+path];return g.local_matrix({'localPosition':r['position'],'localRotation':r['rotation'],'localScale':r['scale']})
        sm=bm('shortbow/shortbowString');weapon=bm('shortbow')
        skey=Path(entry['itemModels'][1]['model']).stem
        write(skey,trans(string,sm.inverted()),stringpieces)
        write(key+'-string-authored',string,stringpieces)
        lower=[p for p in pieces if p['name'].endswith('-1')];selections=[[p for p in pieces if p not in lower],lower]
        fragment_matrices=[bm('shortbow/Break'),bm('shortbow/Break')@bm('shortbow/Break/Break')]
        for d,source,pp,sourcekey,matrix in [(entry['displayModels'][0],source_main,pieces,mainkey,weapon),(entry['displayModels'][1],string,stringpieces,key+'-string-authored',weapon@sm)]:
            name=Path(d['model']).stem;write(name,trans(source,matrix.inverted()),pp);displays.append(source)
            records.append({'key':name,'sourceKey':sourcekey,'rendererToDisplayRoot':[list(r) for r in matrix],'authoredToDisplayRoot':[list(r) for r in Matrix.Identity(4)]})
        # Legacy complete bow exports remain original and synchronized for tooling.
        if mainkey!=key:write(key,combined([source_main,string]),pieces+[{**stringpieces[0],'firstTriangle':len(source_main['triangles'])}])
        if key+'-display' not in [r['key'] for r in records]:write(key+'-display',combined([source_main,string]),pieces+[{**stringpieces[0],'firstTriangle':len(source_main['triangles'])}])
    for index,(selected,matrix) in enumerate(zip(selections,fragment_matrices),1):
        data,pp=select(source_main,pieces,selected);name=key+'-fragment-'+str(index)
        write(name,trans(data,matrix.inverted()),pp)
        fragments.append({'key':name,'fragmentToWeaponLocal':[list(r) for r in matrix]})
    g.save(key+'-icon.source.json',combined(displays));palette(palette_name,colors)
    assignments={k:copy.deepcopy(entry[k]) for k in ['itemModels','offHandModels','displayModels'] if k in entry}
    for group in assignments.values():
        for item in group:item['texture']='assets/'+palette_name
    record={'id':entry['id'],'name':entry['displayName'],'key':key,'family':'paired' if paired else 'bow','template':entry['template'],
            'levelBand':[entry['fields']['minlevel'],entry['fields']['maxlevel']],'rarity':entry['fields']['rarity'],
            'mainKey':mainkey,'palette':palette_name,'colors':colors,'icon':key+'-icon.png','assignments':assignments,'fragments':fragments,'displays':records,
            'equippedOrientation':[list(r) for r in eq]}
    if not paired:record['string']={'key':skey,'sourceKey':key+'-string-authored','stringToWeaponLocal':[list(r) for r in sm]}
    RECORDS.append(record)

def main():
    entries=[e for e in json.loads((PACKAGE.parent/'content.json').read_text())['entries'] if e.get('kind')=='weapon']
    assert len(entries)==17
    for entry in entries:
        name=entry['id'];band=name.rsplit('_',1)[-1]
        if entry['fields']['rarity']=='artifact':
            if entry['template']=='dualKnife':
                kind='skeleton-key' if 'skeleton' in name else 'candles-end';data,pp=artifact(kind,False);off,op=artifact(kind,True)
                emit(entry,data,pp,off,op,'thief-artifact-palette.png',ART_PALETTE)
            else:
                data,pp=bow(7,True);emit(entry,data,pp,palette_name='thief-artifact-palette.png',colors=ART_PALETTE)
        elif entry['template']=='dualKnife':
            i=BANDS.index(band);data,pp=ordinary(band,i)
            pn='thief-street-palette.png' if i==0 else 'thief-twins-'+band+'-palette.png'
            emit(entry,data,pp,palette_name=pn,colors=PALETTES[i])
        else:
            data,pp=bow(BANDS.index(band));emit(entry,data,pp,palette_name='thief-bow-palette.png',colors=BOW_PALETTE)
    dependencies=[OUT.parent/'build.py',OUT.parent/'artifacts/build.py',OUT.parent/'native-route.json',OUT.parent/'ranged-apparel/bow-route.json',ROOT/'tools/ai-model-pipeline/export_ftk_glb.py']
    manifest={'schema':'ftkmf.thief-full-weapons.v1','generator':'build.py','generatorSha256':g.digest(Path(__file__)),
      'dependencies':{str(p.relative_to(ROOT)):g.digest(p) for p in dependencies},'weapons':RECORDS,
      'provenance':'Original authored geometry, palette and rendered icons. Native data supplies renderer identities and local transforms only. No extracted surfaces or commercial art.',
      'scope':'Offline export and studio review. Native binding, fit, animation, draw/string response, break lifecycle and user acceptance remain separate gates.'}
    manifest['files']={p.name:g.digest(p) for p in sorted(OUT.iterdir()) if p.suffix=='.glb' or p.name.endswith(('.source.json','.pieces.json','-palette.png'))}
    g.save('manifest.json',manifest)
    for name in manifest['files']:
        if name.endswith(('.glb','-palette.png')) and not name.endswith('-string-authored.glb'):shutil.copyfile(OUT/name,PACKAGE/name)
    print('PASS:',len(RECORDS),'weapons built')
if __name__=='__main__':main()
