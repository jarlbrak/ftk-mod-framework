#!/usr/bin/env python3
"""Author the three original Thief artifacts and render their actual mesh icons."""
import copy
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import shutil
import sys
import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.geometry import tessellate_polygon

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[2]
PACKAGE = ROOT / 'marketplace/packages/thief/assets'

def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result

base = module('original_thief_geometry', OUT.parent / 'build.py')
icons = module('original_thief_icons', ROOT / 'art-experiments/paladin-equipment/render_icons.py')
COLORS = ['293B43','94B1B7','EDF0DE','927044','D9BC7C','244B50','63B7AA','1B1C23',
          '44404F','EAE1C5','BE8236','F1CA78','342B24','67513A','536345','93A27E']
base.COLORS = COLORS
base.OUT = OUT

def reset():
    base.DATA = {key: [] for key in ['positions','normals','uvs','triangles']}
    base.PIECES = []

def snapshot():
    return copy.deepcopy(base.DATA), copy.deepcopy(base.PIECES)

def prism(name, outline, depth, color, bevel=None):
    # Closed faceted extrusion, authored from a two-dimensional silhouette.
    n = len(outline)
    vertices = [(x,y,-depth/2) for x,y in outline] + [(x,y,depth/2) for x,y in outline]
    vectors=[Vector((x,y,0)) for x,y in outline]
    cap=[tuple(p if isinstance(p,int) else min(range(n),key=lambda i:(vectors[i]-p).length) for p in triangle)
         for triangle in tessellate_polygon([vectors])]
    faces = [tuple(reversed(t)) for t in cap] + [tuple(i+n for i in t) for t in cap]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    base.component(name,vertices,faces,[color]*len(faces))

def blade(name, outline, facecolor, edgecolor, depth=.033):
    n = len(outline);center = np.mean(outline,axis=0)
    verts = [(x,y,0) for x,y in outline]
    verts += [(*(center+(np.array(p)-center)*.65),depth) for p in outline]
    verts += [(*(center+(np.array(p)-center)*.65),-depth) for p in outline]
    verts += [(*center,depth*1.18),(*center,-depth*1.18)]
    faces=[];colors=[]
    for i in range(n):
        j=(i+1)%n
        faces += [(i,j,j+n,i+n),(i+n,j+n,3*n),(j,i,i+2*n,j+2*n),(j+2*n,i+2*n,3*n+1)]
        colors += [edgecolor,facecolor,edgecolor,facecolor]
    base.component(name,verts,faces,colors)

def ring(name,x,y,rx,ry,width,color):
    verts=[];n=12
    for z in [-.011,.011]:
        for inner in [False,True]:
            verts += [(x+math.cos(i*math.tau/n)*(rx-width*inner),
                       y+math.sin(i*math.tau/n)*(ry-width*inner),z) for i in range(n)]
    faces=[]
    for i in range(n):
        j=(i+1)%n
        faces += [(i,j,j+2*n,i+2*n),(i+n,i+3*n,j+3*n,j+n),
                  (i,i+n,j+n,j),(i+2*n,j+2*n,j+3*n,i+3*n)]
    base.component(name,verts,faces,[color]*len(faces))

def tube(name,points,width,depth,color):
    # Rectangular swept section, with finite width at each cap.
    p=np.array(points);vertices=[]
    for i,point in enumerate(p):
        tangent=p[min(i+1,len(p)-1)]-p[max(i-1,0)]
        normal=np.array([-tangent[1],tangent[0]])/np.linalg.norm(tangent)
        w=width[i] if isinstance(width,list) else width
        vertices += [(*(point+normal*s*w/2),z*depth/2) for s,z in [(-1,-1),(1,-1),(1,1),(-1,1)]]
    faces=[(3,2,1,0),tuple(range(len(vertices)-4,len(vertices)))]
    for i in range(len(p)-1):
        for j in range(4):faces.append((i*4+j,i*4+(j+1)%4,(i+1)*4+(j+1)%4,(i+1)*4+j))
    base.component(name,vertices,faces,[color]*len(faces))

def grip(color,wrap,pommel=3):
    base.cylinder('Octagonal hand grip',-.005,.035,.22,color,.76)
    for i in range(6):base.cylinder('Raised grip wrap %d'%i,-.093+i*.034,.037,.009,wrap,.78)
    base.cylinder('Brass blade collar',.108,.038,.033,pommel,.80)
    base.cylinder('Brass pommel collar',-.126,.035,.022,pommel,.78)

def key_blade(short=False):
    reset()
    outline = ([(-.062,.124),(.066,.124),(.083,.360),(.128,.495),(.034,.641),(-.064,.499)] if short else
               [(-.066,.124),(.069,.124),(.075,.219),(.103,.219),(.103,.267),(.074,.267),(.061,.552),(.0,.790),(-.067,.577)])
    outline=[(x*.88,.124+(y-.124)*.75) for x,y in outline]
    blade('Pick blade' if short else 'Stepped long key blade',outline,1,2)
    grip(0,5)
    prism('Compact stepped brass guard',[(-.066,.1),(.064,.1),(.064,.127),(.035,.127),(.035,.142),(-.038,.142),(-.038,.127),(-.066,.127)],.028,3)
    ring('Open key bow pommel',0,-.171,.046 if short else .056,.047,.014,4)
    prism('Turquoise key inset',[(-.012,.079),(0,.095),(.012,.079),(0,.063)],.063,6)
    return snapshot()

def candle_blade(short=False):
    reset()
    outline=[(-.075,.124),(.071,.124),(.120,.348),(.090,.568),(.005,.720 if short else .785),(-.071,.614),(-.118,.349)]
    if short:outline=[(x*.85,y*.9+.014) for x,y in outline]
    outline=[(x*.88,.124+(y-.124)*.75) for x,y in outline]
    blade('Blackened leaf with pale honed edge',outline,7,2,.032)
    # A narrow original channel is a solid inlay, with no fire or emitter.
    prism('Thin amber light channel',[(-.010,.165),(.010,.165),(.013,.385),(0,.472),(-.013,.385)],.090,10 if short else 11)
    grip(8 if short else 9,0 if short else 9)
    tube('Hooked snuffer guard' if short else 'Candle cup guard',
         [(-.077,.147),(-.058,.108),(-.034,.099),(.035,.099),(.060,.127),(.057,.159)] if short else
         [(-.066,.134),(-.053,.100),(0,.092),(.053,.100),(.066,.134)],.017,.028,3)
    prism('Faceted snuffer pommel',[(-.025,-.130),(.025,-.130),(.043,-.184),(-.043,-.184)],.036,3)
    base.cylinder('Snuffer rim',-.182,.046,.010,4,.55)
    return snapshot()

def bow():
    reset()
    path=[(0,0),(.025,.15),(.092,.29),(.158,.41),(.172,.51),(.128,.585),(.075,.613)]
    widths=[.062,.066,.061,.052,.040,.028,.017]
    for side in [-1,1]:
        limb=[(x,y*side) for x,y in path]
        tube('Weathered recurved %s limb'%side,limb,widths,.045,12)
        tube('Pale laminated %s reinforcement'%side,[(x-.018,y*side) for x,y in path[1:-1]], [.015,.015,.013,.009,.008],.050,9)
        tube('Faceted heartwood edge %s'%side,[(x+.016,y*side) for x,y in path[1:]],.009,.049,13)
    base.cylinder('Moss green bow grip',0,.037,.18,14,.84)
    for y in [-.085,-.055,.055,.085]:base.cylinder('Pale grip winding',y,.039,.013,15,.86)
    # Both anchored tails are short and face away from the string.
    for side in [-1,1]:
        prism('Split green grip tab %s'%side,[(-.033,-.026),(-.032,.032),(-.086,.046+side*.017),(-.111,.029+side*.018),(-.087,.002+side*.016)],.012,14 if side==1 else 15)
    prism('Small brass trail arrow',[(-.021,-.037),(.021,-.037),(.021,.002),(.035,.009),(0,.041),(-.035,.009),(-.021,.002)],.067,4)
    tube('Taut bowstring',[(.075,-.613),(.075,.613)],.0035,.0035,9)
    return snapshot()

def write(name,data,pieces):
    base.write(name,data,pieces)

def transformed(data,matrix):return base.transform(data,matrix)

def combine(parts):
    result={k:[] for k in parts[0]}
    for data in parts:
        offset=len(result['positions'])
        for field in ['positions','normals','uvs']:result[field]+=data[field]
        result['triangles'] += [[v+offset for v in tri] for tri in data['triangles']]
    return result

def render(name,source):
    base.save(name+'-icon.source.json',source)
    mats=icons.reset(COLORS)
    for i,mat in enumerate(mats):
        shader=mat.node_tree.nodes.get('Principled BSDF')
        shader.inputs['Metallic'].default_value=.5 if i in [1,2,3,4,6,10,11] else 0
        shader.inputs['Roughness'].default_value=.43 if i in [1,2,3,4,6,10,11] else .78
    points=icons.add_source(OUT/(name+'-icon.source.json'),mats)
    bpy.context.scene.view_settings.view_transform='Standard'
    icons.render(OUT/(name+'-icon.png'),points)
    base.strip_metadata(OUT/(name+'-icon.png'))
    scene=bpy.context.scene;scene.render.resolution_x=768;scene.render.resolution_y=768
    scene.render.filepath=str(OUT/(name+'-preview.png'));bpy.ops.render.render(write_still=True)
    base.strip_metadata(OUT/(name+'-preview.png'))

def main():
    OUT.mkdir(parents=True,exist_ok=True)
    route=json.loads((OUT.parent/'native-route.json').read_text())
    rows={r['path']:r for r in next(r for r in route['templates'] if r['template']=='dualKnife')['hierarchy']}
    artifacts=[]
    for suffix,title,author in [('skeleton-key','The Skeleton Key',key_blade),('candles-end',"Candle's End",candle_blade)]:
        key='thief-twins-'+suffix
        mainmesh,mainpieces=author(False);offmesh,offpieces=author(True)
        equipped=base.equipped_orientation()
        mainmesh=transformed(mainmesh,equipped);offmesh=transformed(offmesh,equipped)
        write(key,mainmesh,mainpieces);write(key+'-offhand',offmesh,offpieces)
        record={'id':key.replace('-','_'),'name':title,'key':key,'template':'dualKnife','main':key+'.glb','offhand':key+'-offhand.glb','fragments':[],'displays':[]}
        base.DATA=mainmesh;base.PIECES=mainpieces
        matrices=[base.local_matrix(rows['dualKnife/Break']),base.local_matrix(rows['dualKnife/Break'])@base.local_matrix(rows['dualKnife/Break/Break'])]
        blade_parts=2 if suffix=='candles-end' else 1
        for i,(selection,path,matrix) in enumerate([(mainpieces[:blade_parts],'Break',matrices[0]),(mainpieces[blade_parts:],'Break/Break',matrices[1])],1):
            data,pieces=base.subset(selection);name=key+'-fragment-'+str(i)
            write(name,transformed(data,matrix.inverted()),pieces)
            record['fragments'].append({'key':name,'path':path,'fragmentToWeaponLocal':[list(r) for r in matrix]})
        displayed=[]
        for data,pieces,path,x,angle,suffix in [(mainmesh,mainpieces,'dualKnife',-.14,.20,'-display'),(offmesh,offpieces,'offHandWeapon',.14,-.20,'-display-offhand')]:
            pose=Matrix.Translation(Vector((x,-.17,0)))@Matrix.Rotation(angle,4,'Z')@equipped.inverted()
            posed=transformed(data,pose);displayed.append(posed)
            matrix=base.local_matrix(rows[path]);name=key+suffix
            write(name,transformed(posed,matrix.inverted()),pieces)
            record['displays'].append({'key':name,'sourceKey':key if path=='dualKnife' else key+'-offhand','path':path,'rendererToDisplayRoot':[list(r) for r in matrix],'authoredToDisplayRoot':[list(r) for r in pose]})
        render(key,combine(displayed));artifacts.append(record)
    key='thief-bow-unlost-road';data,pieces=bow()
    base.DATA=data;base.PIECES=pieces
    body,bodypieces=base.subset(pieces[:-1]);string,stringpieces=base.subset(pieces[-1:])
    write(key,body,bodypieces)
    base.save(key+'-string-authored.source.json',string)
    base.save(key+'-string-authored.pieces.json',stringpieces)
    bowroute=OUT.parent/'ranged-apparel/bow-route.json'
    bowrows={r['path']:r for r in json.loads(bowroute.read_text())['rows']}
    def bowmatrix(path):
        r=bowrows['bowShort/'+path]
        return base.local_matrix({'localPosition':r['position'],'localRotation':r['rotation'],'localScale':r['scale']})
    stringmatrix=bowmatrix('shortbow/shortbowString')
    write(key+'-string',transformed(string,stringmatrix.inverted()),stringpieces)
    record={'id':'thief_bow_unlost_road','name':'The Unlost Road','key':key,'template':'bowShort','main':key+'.glb',
            'string':{'key':key+'-string','path':'shortbowString','stringToWeaponLocal':[list(r) for r in stringmatrix]},'fragments':[],'displays':[],
            'routeMetadata':str(bowroute.relative_to(ROOT)),
            'routeStatus':'Exact renderer local transforms only. Authored bow endpoints require a later native draw/anchor fitting pass; no draw compatibility claimed.'}
    base.DATA=body;base.PIECES=bodypieces
    for i,(selected,path,matrix) in enumerate([(bodypieces[:3],'Break',bowmatrix('shortbow/Break')),
            (bodypieces[3:],'Break/Break',bowmatrix('shortbow/Break')@bowmatrix('shortbow/Break/Break'))],1):
        frag,fragpieces=base.subset(selected);name=key+'-fragment-'+str(i)
        write(name,transformed(frag,matrix.inverted()),fragpieces)
        record['fragments'].append({'key':name,'path':path,'fragmentToWeaponLocal':[list(r) for r in matrix]})
    for mesh,meshpieces,path,sourcekey,suffix,matrix in [(body,bodypieces,'shortbow',key,'-display',bowmatrix('shortbow')),
            (string,stringpieces,'shortbow/shortbowString',key+'-string-authored','-display-string',bowmatrix('shortbow')@stringmatrix)]:
        name=key+suffix;write(name,transformed(mesh,matrix.inverted()),meshpieces)
        record['displays'].append({'key':name,'sourceKey':sourcekey,'path':path,'rendererToDisplayRoot':[list(r) for r in matrix],
                                  'authoredToDisplayRoot':[list(r) for r in Matrix.Identity(4)]})
    render(key,data);artifacts.append(record)
    palette=bpy.data.images.new('Original artifact palette',width=len(COLORS),height=1)
    palette.pixels=[v for c in COLORS for v in [*(int(c[i:i+2],16)/255 for i in [0,2,4]),1]]
    palette.filepath_raw=str(OUT/'thief-artifact-palette.png');palette.file_format='PNG';palette.save()
    files={p.name:base.digest(p) for p in sorted(OUT.iterdir()) if p.suffix=='.glb' or p.name.endswith(('.source.json','.pieces.json','-icon.png','-palette.png','-preview.png'))}
    dependencies=[OUT.parent/'build.py',OUT.parent/'native-route.json',bowroute,ROOT/'tools/ai-model-pipeline/export_ftk_glb.py',ROOT/'art-experiments/paladin-equipment/render_icons.py']
    manifest={'schema':'ftkmf.thief-original-artifacts.v1','generator':'build.py','generatorSha256':base.digest(Path(__file__)),
              'dependencies':{str(p.relative_to(ROOT)):base.digest(p) for p in dependencies},'palette':'thief-artifact-palette.png','artifacts':artifacts,'files':files,
              'provenance':'Original authored vertices, faces, palette, UVs and geometry-rendered icons. No native surface data, copied commercial art, or image-generated model is used. Native metadata supplies only exact renderer identity and local transforms.',
              'scope':'Offline art production only. No deployment, native fit, motion, material, lifecycle or in-game visual acceptance claimed.'}
    base.save('manifest.json',manifest);PACKAGE.mkdir(parents=True,exist_ok=True)
    packaged={}
    for name,sha in files.items():
        if name.endswith(('.glb','-icon.png','-palette.png')):
            shutil.copyfile(OUT/name,PACKAGE/name)
            packaged[name]={'source':str((OUT/name).relative_to(ROOT)),'sha256':sha}
    (PACKAGE/'thief-artifacts.provenance.json').write_text(json.dumps({'provenance':manifest['provenance'],'files':packaged},indent=2)+'\n')
    print('PASS: original art exported for all three artifacts')

if __name__=='__main__':main()
