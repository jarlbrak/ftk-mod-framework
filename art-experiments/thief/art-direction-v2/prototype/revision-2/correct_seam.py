#!/usr/bin/env python3
"""Attach the closure seam to the original authored jerkin, preserving review bytes."""
import copy
import hashlib
import json
import sys
from pathlib import Path
import numpy as np

HERE=Path(__file__).resolve().parent
OUT=HERE/'seam-correction'
ROOT=next(p for p in HERE.parents if (p/'FTKModFramework').is_dir())
sys.path.insert(0,str(HERE))
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
import validate
from export_ftk_glb import write_glb


def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    OUT.mkdir(exist_ok=True)
    original=json.loads((HERE/'manifest.json').read_text())
    manifest=copy.deepcopy(original);manifest['assets']=[]
    for sex in ['male','female']:
        name='thief-coat-street-'+sex
        source=HERE/(name+'.source.json');data=json.loads(source.read_text())
        pieces=json.loads((HERE/(name+'.pieces.json')).read_text())
        pos=np.array(data['positions']);tri=np.array(data['triangles'])
        # Only this project's own garment surfaces are queried, never native art.
        surfaces=[r for r in pieces if r['name'] in ['Continuous tailored jerkin body','Upper cloth shoulder yoke']]
        faces=pos[[t for t in tri if any(r['vertex_start']<=t[0]<r['vertex_start']+r['vertex_count'] for r in surfaces)]]
        seam=next(r for r in pieces if r['name']=='Narrow diagonal folded closure')
        first=seam['vertex_start'];last=first+seam['vertex_count']
        front={}
        for q in pos[first:last]:front[tuple(q[:2])]=max(front.get(tuple(q[:2]),-100),q[2])
        for index in range(first,last):
            q=pos[index];hits=[]
            for face in faces:
                matrix=np.column_stack([face[1,:2]-face[0,:2],face[2,:2]-face[0,:2]])
                if abs(np.linalg.det(matrix))<1e-9:continue
                u,v=np.linalg.solve(matrix,q[:2]-face[0,:2])
                if u>=-1e-6 and v>=-1e-6 and u+v<=1+1e-6:
                    hits.append(face[0,2]+u*(face[1,2]-face[0,2])+v*(face[2,2]-face[0,2]))
            assert hits,'Seam point must intersect the original garment'
            pos[index,2]=max(hits)+.004-(front[tuple(q[:2])]-q[2])
        data['positions']=pos.tolist()
        for t in tri:
            if not first<=t[0]<last:continue
            normal=np.cross(pos[t[1]]-pos[t[0]],pos[t[2]]-pos[t[0]])
            normal/=np.linalg.norm(normal)
            for i in t:data['normals'][i]=normal.tolist()
        doc,read=validate.decode(HERE/(name+'.glb'));skin=doc['skins'][0]
        binding={'bone_names':np.array(data['bone_names']),
                 'bindposes':read(skin['inverseBindMatrices']).reshape(-1,4,4).transpose(0,2,1)}
        (OUT/(name+'.source.json')).write_text(json.dumps(data,indent=2)+'\n')
        (OUT/(name+'.pieces.json')).write_text(json.dumps(pieces,indent=2)+'\n')
        write_glb(OUT/(name+'.glb'),data,binding)
        record=copy.deepcopy(next(r for r in original['assets'] if r['file']==name+'.glb'))
        record['nativeReviewedPredecessorSha256']=record['sha256']
        record['sha256']=digest(OUT/(name+'.glb'));manifest['assets'].append(record)
    manifest['generator']='../correct_seam.py';manifest['generatorSha256']=digest(Path(__file__))
    manifest['paletteFile']='../thief-street-v2-palette.png'
    manifest['status']='Unapproved experimental correction only. Native-reviewed revision 2 is preserved byte-for-byte in the parent directory. This correction attaches its closure seam to the authored cloth and has not been tested in game. All broader art concerns remain open.'
    (OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    validate.OUT=OUT;validate.main()


if __name__=='__main__':main()
