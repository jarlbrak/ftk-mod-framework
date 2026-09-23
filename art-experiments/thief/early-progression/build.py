#!/usr/bin/env python3
"""Author six original paired dagger sets using the strict static FTK exporter."""
import copy
import importlib.util
import json
import math
from pathlib import Path
import shutil
import sys
import bpy
import numpy as np
from mathutils import Matrix, Vector

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[2]
PACKAGE = ROOT / 'marketplace/packages/thief/assets'
spec = importlib.util.spec_from_file_location('street_geometry', OUT.parent / 'build.py')
g = importlib.util.module_from_spec(spec)
spec.loader.exec_module(g)
g.OUT = OUT
spec = importlib.util.spec_from_file_location('icons', ROOT / 'art-experiments/paladin-equipment/render_icons.py')
icons = importlib.util.module_from_spec(spec)
spec.loader.exec_module(icons)

# Faceted silhouettes are independently authored, not scaled native geometry.
SETS = [
    ('burglar', 'Windowfangs', ['34434C','91A4AD','D2D9CB','352A24','71513B','3C5E66','92B0AC','BDA375'],
     [(-.064,.124),(.064,.124),(.103,.250),(.085,.482),(.020,.690),(-.040,.594),(-.092,.318)]),
    ('guild', 'Guild Twin Daggers', ['2D4050','93A9BA','D9DCCD','23364B','4D6481','304D64','A5C3C3','CEAF72'],
     [(-.070,.124),(.070,.124),(.106,.265),(.061,.524),(0,.725),(-.061,.524),(-.106,.265)]),
    ('masterwork', 'Velvet Fangs', ['34404D','9CACBE','DDE1D8','262C37','586576','45495F','A6B4C7','C7C1AD'],
     [(-.077,.124),(.077,.124),(.119,.214),(.082,.311),(.046,.547),(0,.765),(-.046,.547),(-.082,.311),(-.119,.214)]),
    ('locksmith', "Locksmith's Picks", ['2D4852','8CAAB1','DBDCC6','214951','477E85','527D82','DEDABF','D0AE65'],
     [(-.055,.124),(.055,.124),(.087,.276),(.067,.489),(.017,.758),(-.049,.619),(-.077,.302)]),
    ('nightblade', 'Nightglass Twins', ['2D2E3E','737D92','D8D6C8','332532','734654','403547','C5B4BC','C7AE8C'],
     [(-.075,.124),(.075,.124),(.125,.326),(.095,.543),(.008,.755),(-.065,.613),(-.109,.371)]),
    ('wayfarer', 'Trailbreakers', ['3E514E','91AA9D','D6DECB','332F24','756949','526F5D','B9C6A1','C6B184'],
     [(-.069,.124),(.069,.124),(.118,.272),(.108,.484),(.035,.731),(-.060,.579),(-.090,.319)]),
]


def prism(name, outline, depth, color):
    n = len(outline)
    vertices = [(x,y,z) for z in [-depth/2,depth/2] for x,y in outline]
    faces = [tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    g.component(name, vertices, faces, [color]*len(faces))


def ring(name, cx, cy, radius, tube, color):
    vertices = [(cx+(radius+tube*math.cos(j*math.tau/6))*math.cos(i*math.tau/12),
                 cy+(radius+tube*math.cos(j*math.tau/6))*math.sin(i*math.tau/12),
                 tube*math.sin(j*math.tau/6)) for i in range(12) for j in range(6)]
    faces = [(i*6+j,((i+1)%12)*6+j,((i+1)%12)*6+(j+1)%6,i*6+(j+1)%6)
             for i in range(12) for j in range(6)]
    g.component(name, vertices, faces, [color]*len(faces))


def shape(band, outline):
    outline = [(x*.88,.124+(y-.124)*.75) for x,y in outline]
    n = len(outline)
    center = np.mean(outline,axis=0)
    vertices = [(x,y,0) for x,y in outline]
    vertices += [(*(center+(np.array(p)-center)*.64),z) for z in [.025,-.025] for p in outline]
    vertices += [(*center,.036),(*center,-.036)]
    faces, colors = [], []
    for i in range(n):
        j=(i+1)%n
        faces += [(i,j,j+n,i+n),(i+n,j+n,3*n),(j,i,i+2*n,j+2*n),(j+2*n,i+2*n,3*n+1)]
        colors += [2,1,2,0]
    g.component(band+' original bevel blade',vertices,faces,colors)
    blade_count=len(g.PIECES)
    g.cylinder('Fitted octagonal leather grip',-.005,.034,.225,3,.79)
    wraps=7 if band in ('nightblade','wayfarer') else 5
    for i in range(wraps):
        g.cylinder('Raised binding %02d'%i,-.095+i*.185/(wraps-1),.037,.016 if wraps==7 else .023,4 if i%2 else 5,.79)
    g.cylinder('Top collar',.108,.040,.026,7,.78)
    if band=='burglar':
        prism('Compact angled iron quillons',[(-.078,.093),(.076,.104),(.065,.132),(-.066,.121)],.037,0)
        g.cylinder('Flat practical pommel',-.134,.043,.030,0,.76)
    elif band=='guild':
        prism('Matching guild crossguard',[(-.091,.104),(.091,.104),(.077,.132),(-.077,.132)],.034,7)
        g.cylinder('Octagonal guild seal pommel',-.136,.047,.033,7,.77)
        prism('Rook seal body',[(-.016,-.149),(.016,-.149),(.013,-.115),(-.013,-.115)],.075,5)
        for x in [-.017,0,.017]:
            prism('Rook seal crenel',[(x-.006,-.121),(x+.006,-.121),(x+.006,-.107),(x-.006,-.107)],.078,7)
    elif band=='masterwork':
        prism('Swept balanced guard',[(-.105,.093),(-.079,.114),(.079,.114),(.105,.093),(.085,.139),(-.085,.139)],.036,7)
        g.cylinder('Faceted steel pommel',-.135,.048,.034,0,.72)
        prism('Pommel balance diamond',[(-.023,-.134),(0,-.165),(.023,-.134),(0,-.111)],.077,2)
    elif band=='locksmith':
        prism('Compact brass lock guard',[(-.082,.106),(.082,.106),(.082,.126),(-.082,.126)],.036,7)
        for x in [-.067,.067]:
            prism('Guard lock tooth',[(x-.012,.086),(x+.012,.086),(x+.012,.106),(x-.012,.106)],.038,7)
        ring('Open key bow pommel',0,-.155,.038,.009,7)
        g.cylinder('Key bow collar',-.113,.037,.015,7,.8)
    elif band=='nightblade':
        prism('Offset ivory guard',[(-.083,.101),(.077,.112),(.062,.135),(-.060,.122)],.032,2)
        prism('Angled charcoal pommel',[(-.031,-.107),(.032,-.118),(.021,-.157),(-.023,-.147)],.048,0)
        g.cylinder('Wine grip finishing thread',-.097,.038,.011,4,.8)
    else:
        prism('Field guard with clipped corners',[(-.084,.108),(-.068,.099),(.068,.099),(.084,.108),(.066,.131),(-.066,.131)],.035,6)
        g.cylinder('Hardwood octagonal pommel',-.133,.044,.033,3,.80)
        prism('Pale trail chevron',[(-.022,-.129),(0,-.146),(.022,-.129),(0,-.119)],.075,6)
        ring('Small cord eye',0,-.166,.018,.006,7)
    return blade_count


def emit(band,name,colors,outline):
    g.DATA={field:[] for field in ['positions','normals','uvs','triangles']};g.PIECES=[];g.COLORS=colors
    key='thief-twins-'+band
    blade_count=shape(band,outline)
    equipped=g.equipped_orientation()
    g.DATA=g.transform(g.DATA,equipped)
    g.write(key,g.DATA,g.PIECES)
    route=json.loads((OUT.parent/'native-route.json').read_text())
    rows={r['path']:r for r in next(t for t in route['templates'] if t['template']=='dualKnife')['hierarchy']}
    b=g.local_matrix(rows['dualKnife/Break'])
    fragments=[]
    for number,selected,matrix,path in [(1,g.PIECES[:blade_count],b,'Break'),(2,g.PIECES[blade_count:],b@g.local_matrix(rows['dualKnife/Break/Break']),'Break/Break')]:
        data,pieces=g.subset(selected);fk=key+'-fragment-'+str(number)
        g.write(fk,g.transform(data,np.linalg.inv(np.array(matrix))),pieces)
        fragments.append({'key':fk,'path':path,'fragmentToWeaponLocal':[list(r) for r in matrix]})
    combined={field:[] for field in g.DATA};displays=[]
    for path,x,angle,suffix in [('dualKnife',-.14,.20,'-display'),('offHandWeapon',.14,-.20,'-display-offhand')]:
        pose=Matrix.Translation(Vector((x,-.19,0)))@Matrix.Rotation(angle,4,'Z')@equipped.inverted()
        data=g.transform(g.DATA,pose);dk=key+suffix;local=g.local_matrix(rows[path])
        g.write(dk,g.transform(data,np.linalg.inv(np.array(local))),g.PIECES)
        displays.append({'key':dk,'path':path,'rendererToDisplayRoot':[list(r) for r in local], 'authoredBladeToDisplayRoot':[list(r) for r in pose]})
        offset=len(combined['positions'])
        for field in ['positions','normals','uvs']:combined[field]+=data[field]
        combined['triangles'] += [[v+offset for v in tri] for tri in data['triangles']]
    g.save(key+'-icon.source.json',combined)
    mats=icons.reset(colors)
    points=icons.add_source(OUT/(key+'-icon.source.json'),mats)
    bpy.context.scene.view_settings.view_transform='Standard'
    icons.render(OUT/(key+'-icon.png'),points)
    g.strip_metadata(OUT/(key+'-icon.png'))
    scene=bpy.context.scene;scene.render.resolution_x=768;scene.render.resolution_y=768
    scene.render.filepath=str(OUT/(key+'-preview.png'));bpy.ops.render.render(write_still=True)
    g.strip_metadata(OUT/(key+'-preview.png'))
    palette=bpy.data.images.new(key+' palette',width=len(colors),height=1)
    palette.pixels=[v for c in colors for v in [*(int(c[i:i+2],16)/255 for i in [0,2,4]),1]]
    palette.filepath_raw=str(OUT/(key+'-palette.png'));palette.file_format='PNG';palette.save()
    return {'id':'thief_twins_'+band,'name':name,'key':key,'template':'dualKnife','path':'.','offHandPath':'.','fragments':fragments,'displays':displays,'palette':key+'-palette.png','icon':key+'-icon.png'}


def main():
    assets=[emit(*row) for row in SETS]
    files={p.name:g.digest(p) for p in sorted(OUT.iterdir()) if p.suffix=='.glb' or p.name.endswith(('.source.json','.pieces.json','-icon.png','-palette.png','-preview.png'))}
    dependencies=[OUT.parent/'build.py',OUT.parent/'native-route.json',ROOT/'tools/ai-model-pipeline/export_ftk_glb.py',ROOT/'art-experiments/paladin-equipment/render_icons.py']
    provenance='Original authored geometry, normals, UVs, palette and source-rendered icons. No native surfaces, extracted textures or external artwork are used. Native metadata supplies renderer paths and local transforms only.'
    manifest={'schema':'ftkmf.thief-paired-progression.v1','generator':'build.py','generatorSha256':g.digest(Path(__file__)),'dependencies':{str(p.relative_to(ROOT)):g.digest(p) for p in dependencies},'provenance':provenance,'status':'Offline art assets. No live fit, animation, card framing or integration acceptance claimed.','assets':assets,'files':files}
    g.save('manifest.json',manifest)
    PACKAGE.mkdir(parents=True,exist_ok=True);packaged={}
    for name,sha in files.items():
        if name.endswith(('.glb','-icon.png','-palette.png')):
            shutil.copyfile(OUT/name,PACKAGE/name)
            packaged[name]={'source':str((OUT/name).relative_to(ROOT)),'sha256':sha}
    (PACKAGE/'paired-progression.provenance.json').write_text(json.dumps({'provenance':provenance,'files':packaged},indent=2)+'\n')
    print('PASS: six paired dagger sets, 30 GLBs, six palettes and icons')


if __name__=='__main__':main()
