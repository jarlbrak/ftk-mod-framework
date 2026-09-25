#!/usr/bin/env python3
"""Independently check the five rigid exports, original topology and route transforms."""
from collections import Counter
import hashlib
import json
from pathlib import Path
import struct
import numpy as np
from PIL import Image

OUT=Path(__file__).resolve().parent
ROOT=OUT.parents[1]


def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    manifest=json.loads((OUT/'manifest.json').read_text())
    assert digest(OUT/manifest['generator'])==manifest['generatorSha256']
    for name,sha in manifest['dependencies'].items():assert digest(ROOT/name)==sha,name
    package=ROOT/'marketplace/packages/thief/assets'
    provenance=json.loads((package/'street-twins.provenance.json').read_text())['files']
    results=[]
    for name,sha in manifest['files'].items():
        assert digest(OUT/name)==sha,name
        if name in provenance:
            assert digest(package/name)==sha==provenance[name]['sha256'],name
        if not name.endswith('.glb'):continue
        raw=(OUT/name).read_bytes()
        assert struct.unpack_from('<III',raw)==(0x46546c67,2,len(raw))
        jlen,jkind=struct.unpack_from('<II',raw,12)
        assert jkind==0x4e4f534a
        doc=json.loads(raw[20:20+jlen]);blen,bkind=struct.unpack_from('<II',raw,20+jlen)
        binary=raw[28+jlen:]
        assert bkind==0x004e4942 and len(binary)==blen
        assert 'skins' not in doc and len(doc['meshes'])==1
        assert len(doc['meshes'][0]['primitives'])==1
        primitive=doc['meshes'][0]['primitives'][0]
        assert set(primitive['attributes'])=={'POSITION','NORMAL','TEXCOORD_0'}
        def read(index):
            acc=doc['accessors'][index];view=doc['bufferViews'][acc['bufferView']]
            width={'VEC3':3,'VEC2':2,'SCALAR':1}[acc['type']]
            dtype={5126:'<f4',5123:'<u2'}[acc['componentType']]
            return np.frombuffer(binary,dtype=dtype,count=acc['count']*width,
                offset=view.get('byteOffset',0)+acc.get('byteOffset',0)).reshape(-1,width)
        pos=read(primitive['attributes']['POSITION']);normal=read(primitive['attributes']['NORMAL'])
        uv=read(primitive['attributes']['TEXCOORD_0']);tri=read(primitive['indices']).reshape(-1,3)
        key=Path(name).stem;source=json.loads((OUT/(key+'.source.json')).read_text())
        for field,actual in [('positions',pos),('normals',normal),('uvs',uv),('triangles',tri)]:
            assert np.isfinite(actual).all() and np.allclose(actual,source[field],atol=1e-7),(key,field)
        assert 0<len(pos)<65535 and tri.max()<len(pos)
        assert np.allclose(np.linalg.norm(normal,axis=1),1,atol=1e-6)
        assert ((uv>=0)&(uv<=1)).all()
        a,b,c=(pos[tri[:,i]] for i in range(3));cross=np.cross(b-a,c-a)
        assert (np.einsum('ij,ij->i',cross,normal[tri[:,0]])>1e-10).all(),key
        pieces=json.loads((OUT/(key+'.pieces.json')).read_text());covered=[]
        for piece in pieces:
            start=piece['firstTriangle'];end=start+piece['triangleCount'];covered+=list(range(start,end))
            volume=np.einsum('ij,ij->i',a[start:end],np.cross(b[start:end],c[start:end])).sum()/6
            assert volume>0,(key,piece['name'],'inward or empty')
            edges=Counter()
            for triangle in tri[start:end]:
                p=[tuple(round(float(v),6) for v in pos[i]) for i in triangle]
                for i in range(3):edges[tuple(sorted((p[i],p[(i+1)%3])))]+=1
            assert all(count==2 for count in edges.values()),(key,piece['name'],'nonmanifold')
        assert covered==list(range(len(tri)))
        results.append({'file':name,'triangles':len(tri),'closedOutwardComponents':len(pieces),'status':'PASS'})
    assert len(results)==5
    weapon=manifest['weapon'];original=json.loads((OUT/(weapon['key']+'.source.json')).read_text())
    def triangles(data,matrix=np.eye(4)):
        points=np.array(data['positions'])@matrix[:3,:3].T+matrix[:3,3]
        return Counter(tuple(tuple(round(v,6) for v in points[i]) for i in tri) for tri in data['triangles'])
    union=Counter()
    for fragment in weapon['fragments']:
        data=json.loads((OUT/(fragment['key']+'.source.json')).read_text())
        union.update(triangles(data,np.array(fragment['fragmentToWeaponLocal'])))
    assert union==triangles(original),'fragment reconstruction'
    display_errors=[]
    for display in weapon['displays']:
        data=json.loads((OUT/(display['key']+'.source.json')).read_text())
        actual_matrix=np.array(display['rendererToDisplayRoot'])
        expected_matrix=np.array(display['authoredBladeToDisplayRoot'])
        actual=np.array(data['positions'])@actual_matrix[:3,:3].T+actual_matrix[:3,3]
        expected=np.array(original['positions'])@expected_matrix[:3,:3].T+expected_matrix[:3,3]
        assert data['triangles']==original['triangles'],'display topology'
        # Blender matrices and the runtime vertex buffers use float32. Check error
        # directly; decimal rounding at half-way values creates false mismatches.
        assert np.allclose(actual,expected,atol=1e-7,rtol=0),'display reconstruction'
        display_errors.append(float(np.max(np.abs(actual-expected))))
    icon=Image.open(OUT/'thief-street-twins-icon.png')
    assert icon.size==(256,256) and icon.mode=='RGBA'
    bounds=icon.getchannel('A').getbbox()
    assert bounds and 0<bounds[0]<bounds[2]<256 and 0<bounds[1]<bounds[3]<256,bounds
    report={'status':'PASS','validatorSha256':digest(Path(__file__)),
            'manifestSha256':digest(OUT/'manifest.json'),'assets':results,'iconAlphaBounds':list(bounds),
            'maximumDisplayReconstructionError':max(display_errors),
            'checks':['Independent GLB decode and source equality','Closed outward components, winding, UVs and unit normals','Exact-once transformed fragment reconstruction','Display transform reconstruction','Package/provenance hashes and icon framing'],
            'limitations':'Offline source/export only. Off-hand API, in-game fit, native attack motion, break lifetime, card framing and art acceptance remain unverified.'}
    (OUT/'validation.json').write_text(json.dumps(report,indent=2)+'\n')
    print('PASS: five GLBs, fragment/display transforms, provenance and icon')


if __name__=='__main__':main()
