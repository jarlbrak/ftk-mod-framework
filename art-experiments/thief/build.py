#!/usr/bin/env python3
"""Build original Street Twins, their native-local fragments, displays and icon."""
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
from mathutils import Matrix, Quaternion, Vector

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[1]
PACKAGE = ROOT / 'marketplace/packages/thief/assets'
sys.path.insert(0, str(ROOT / 'tools/ai-model-pipeline'))
from export_ftk_glb import write_static_glb

COLORS = ['34434C', '8EA6AE', 'CDD6CE', '392B24', '795237', '304D57', '719192', 'BCA06B']
KEY = 'thief-street-twins'
DATA = {key: [] for key in ['positions', 'normals', 'uvs', 'triangles']}
PIECES = []


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def save(name, value):
    (OUT / name).write_text(json.dumps(value, indent=2) + '\n')


def component(name, vertices, faces, colors):
    vertices = np.array(vertices, dtype=float)
    triangles = [(face[0], face[i], face[i + 1], color)
                 for face, color in zip(faces, colors) for i in range(1, len(face) - 1)]
    volume = sum(np.dot(vertices[a], np.cross(vertices[b], vertices[c])) for a,b,c,_ in triangles) / 6
    if volume < 0:
        triangles = [(a,c,b,color) for a,b,c,color in triangles]
    start = len(DATA['triangles'])
    for a,b,c,color in triangles:
        points = vertices[[a,b,c]]
        normal = np.cross(points[1] - points[0], points[2] - points[0])
        normal /= np.linalg.norm(normal)
        indices = []
        for point in points:
            indices.append(len(DATA['positions']))
            DATA['positions'].append(point.tolist())
            DATA['normals'].append(normal.tolist())
            DATA['uvs'].append([(color + .5) / len(COLORS), .5])
        DATA['triangles'].append(indices)
    PIECES.append({'name': name, 'firstTriangle': start, 'triangleCount': len(DATA['triangles']) - start})


def cylinder(name, y, radius, length, color, squash=1):
    vertices = [(math.cos(i * math.tau / 8) * radius, y + side * length / 2,
                 math.sin(i * math.tau / 8) * radius * squash)
                for side in [-1,1] for i in range(8)]
    faces = [tuple(range(7,-1,-1)), tuple(range(8,16))]
    faces += [(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)]
    component(name, vertices, faces, [color] * len(faces))


def blade():
    # A broad working dagger: long bright cutting planes remain legible at combat scale.
    outline = [(-.062,.126),(.062,.126),(.088,.238),(.074,.370),(.012,.550),(-.040,.462),(-.082,.286)]
    center = np.array([0,.288])
    edge = [(x,y,0) for x,y in outline]
    vertices = edge + [(*(center + (np.array(p)-center)*.64),.025) for p in outline]
    vertices += [(*(center + (np.array(p)-center)*.64),-.025) for p in outline]
    vertices += [(0,.288,.035),(0,.288,-.035)]
    faces, colors = [], []
    for i in range(7):
        j=(i+1)%7
        faces += [(i,j,j+7,i+7),(i+7,j+7,21),(j,i,i+14,j+14),(j+14,i+14,22)]
        colors += [2,1 if i<4 else 0,2,0 if i<4 else 1]
    component('Broad clipped steel blade with bright cutting planes',vertices,faces,colors)
    cylinder('Dark leather hand grip',-.009,.037,.232,3,.80)
    for i in range(4):
        cylinder('Broad diagonal-tone leather wrap %d'%i,-.090+i*.048,.040,.028,4,.80)
    cylinder('Slate teal upper binding',.080,.043,.033,5,.80)
    cylinder('Pale repaired binding lip',.098,.044,.010,6,.80)
    cylinder('Octagonal flared brass guard',.119,.098,.032,7,.54)
    cylinder('Dark guard undercut',.104,.065,.012,0,.70)
    cylinder('Faceted iron pommel',-.137,.049,.034,0,.82)
    cylinder('Brass pommel edge',-.154,.050,.009,7,.82)


def transform(data, matrix):
    result = copy.deepcopy(data)
    m = np.array(matrix, dtype=float)
    result['positions'] = (np.array(data['positions']) @ m[:3,:3].T + m[:3,3]).tolist()
    normals = np.array(data['normals']) @ np.linalg.inv(m[:3,:3])
    result['normals'] = (normals / np.linalg.norm(normals,axis=1)[:,None]).tolist()
    return result


def local_matrix(row):
    x,y,z,w=row['localRotation']
    return Matrix.LocRotScale(Vector(row['localPosition']),Quaternion((w,x,y,z)),Vector(row['localScale']))


def subset(selected):
    result = {key: [] for key in DATA}
    pieces=[]
    for piece in selected:
        start=len(result['triangles'])
        for triangle in DATA['triangles'][piece['firstTriangle']:piece['firstTriangle']+piece['triangleCount']]:
            indices=[]
            for index in triangle:
                indices.append(len(result['positions']))
                for field in ['positions','normals','uvs']:result[field].append(DATA[field][index])
            result['triangles'].append(indices)
        pieces.append(dict(piece,firstTriangle=start))
    return result,pieces


def write(key,data,pieces):
    save(key+'.source.json',data)
    save(key+'.pieces.json',pieces)
    write_static_glb(OUT/(key+'.glb'),data)


def strip_metadata(path):
    raw=path.read_bytes();chunks=[raw[:8]];offset=8
    while offset<len(raw):
        length=struct.unpack_from('>I',raw,offset)[0];kind=raw[offset+4:offset+8]
        if kind not in (b'tEXt',b'iTXt',b'zTXt'):chunks.append(raw[offset:offset+length+12])
        offset+=length+12
    path.write_bytes(b''.join(chunks))


def equipped_orientation():
    # Native idle mount points +Y forward and +X upward in both hands.
    # Tip the original dagger down and expose its broad cutting face.
    return Matrix.Rotation(math.radians(55),4,'Z') @ Matrix.Rotation(math.pi/2,4,'Y')


def main():
    global DATA
    blade()
    equipped=equipped_orientation()
    DATA=transform(DATA,equipped)
    write(KEY,DATA,PIECES)
    route=json.loads((OUT/'native-route.json').read_text())
    template=next(r for r in route['templates'] if r['template']=='dualKnife')
    rows={r['path']:r for r in template['hierarchy']}
    break_matrix=local_matrix(rows['dualKnife/Break'])
    fragments=[]
    for i,(selected,matrix,path) in enumerate([(PIECES[:1],break_matrix,'Break'),
            (PIECES[1:],break_matrix@local_matrix(rows['dualKnife/Break/Break']),'Break/Break')],1):
        data,pieces=subset(selected);name=KEY+'-fragment-'+str(i)
        write(name,transform(data,np.linalg.inv(np.array(matrix))),pieces)
        fragments.append({'key':name,'path':path,'fragmentToWeaponLocal':[list(row) for row in matrix]})
    pair=[];displays=[]
    for path,x,angle,suffix in [('dualKnife',-.13,.24,'-display'),('offHandWeapon',.13,-.24,'-display-offhand')]:
        # Original display composition is centered from authored landmarks, without native bounds.
        pose=Matrix.Translation(Vector((x,-.17,0)))@Matrix.Rotation(angle,4,'Z')@equipped.inverted()
        posed=transform(DATA,pose);pair.append(posed)
        name=KEY+suffix
        matrix=local_matrix(rows[path])
        write(name,transform(posed,np.linalg.inv(np.array(matrix))),PIECES)
        displays.append({'key':name,'path':path,'rendererToDisplayRoot':[list(row) for row in matrix],
                         'authoredBladeToDisplayRoot':[list(row) for row in pose]})
    icon_source={key:[] for key in DATA}
    for data in pair:
        offset=len(icon_source['positions'])
        for field in ['positions','normals','uvs']:icon_source[field]+=data[field]
        icon_source['triangles'] += [[v+offset for v in tri] for tri in data['triangles']]
    save(KEY+'-icon.source.json',icon_source)
    spec=importlib.util.spec_from_file_location('original_icon_renderer',ROOT/'art-experiments/paladin-equipment/render_icons.py')
    icons=importlib.util.module_from_spec(spec);spec.loader.exec_module(icons)
    mats=icons.reset(COLORS)
    for index,mat in enumerate(mats):
        shader=mat.node_tree.nodes.get('Principled BSDF')
        shader.inputs['Roughness'].default_value=.72 if index in [3,4,5,6] else .44
        shader.inputs['Metallic'].default_value=0 if index in [3,4,5,6] else .5
    points=icons.add_source(OUT/(KEY+'-icon.source.json'),mats)
    bpy.context.scene.view_settings.view_transform='Standard'
    icons.render(OUT/(KEY+'-icon.png'),points);strip_metadata(OUT/(KEY+'-icon.png'))
    # A larger three-quarter preview uses the same exported source and lighting.
    scene=bpy.context.scene;scene.render.resolution_x=768;scene.render.resolution_y=768
    scene.render.filepath=str(OUT/'street-twins-preview.png');bpy.ops.render.render(write_still=True)
    strip_metadata(OUT/'street-twins-preview.png')
    palette=bpy.data.images.new('Original street palette',width=len(COLORS),height=1)
    palette.pixels=[value for color in COLORS for value in [*(int(color[i:i+2],16)/255 for i in [0,2,4]),1]]
    palette.filepath_raw=str(OUT/'thief-street-palette.png');palette.file_format='PNG';palette.save()
    files={p.name:digest(p) for p in sorted(OUT.iterdir())
           if p.suffix=='.glb' or p.name.endswith(('.source.json','.pieces.json','-icon.png','-palette.png'))}
    manifest={'schema':'ftkmf.thief-original-street-twins.v1','generator':'build.py',
              'generatorSha256':digest(Path(__file__)),
              'dependencies':{str(p.relative_to(ROOT)):digest(p) for p in [ROOT/'tools/ai-model-pipeline/export_ftk_glb.py',ROOT/'art-experiments/paladin-equipment/render_icons.py',OUT/'native-route.json']},
              'provenance':'All geometry, bevels, normals, UVs, palette, proportions and icon pixels are original authored outputs. No image generation, external art or native surface data is used. Native metadata supplies only renderer paths and local transforms.',
              'space':'Unity Y up. Original grip center near Y=0, authored tip Y=0.550 before native-local orientation. Downward blade orientation reviewed in native male idle; other poses remain open.',
              'status':'Revised original assets with explicit off-hand package assignments. Native male idle review is separate from remaining motion, body variants, and user art acceptance.',
              'equippedOrientation':{'rotationOrder':'Z55 @ Y90 degrees','matrix':[list(row) for row in equipped]},
              'weapon':{'key':KEY,'path':'.','offHandPath':'.','fragments':fragments,'displays':displays},
              'files':files}
    save('manifest.json',manifest)
    PACKAGE.mkdir(parents=True,exist_ok=True)
    provenance={}
    for name,sha in files.items():
        if name.endswith(('.glb','-icon.png','-palette.png')):
            shutil.copyfile(OUT/name,PACKAGE/name)
            provenance[name]={'source':str((OUT/name).relative_to(ROOT)),'sha256':sha}
    (PACKAGE/'street-twins.provenance.json').write_text(json.dumps({'provenance':manifest['provenance'],'files':provenance},indent=2)+'\n')
    print('PASS: five original rigid GLBs, editable sources, piece maps, palette and paired icon')


if __name__=='__main__':main()
