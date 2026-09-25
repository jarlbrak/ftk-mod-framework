#!/usr/bin/env python3
"""Independently decode artifact exports and check topology and reconstruction."""
from collections import Counter
import hashlib
import json
from pathlib import Path
import struct
import numpy as np
from PIL import Image

OUT=Path(__file__).resolve().parent
ROOT=OUT.parents[2]
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def source(key):return json.loads((OUT/(key+'.source.json')).read_text())
def points(data,matrix):
    m=np.array(matrix);return np.array(data['positions'])@m[:3,:3].T+m[:3,3]
def triangle_set(data,matrix=np.eye(4)):
    p=points(data,matrix)
    return Counter(tuple(tuple(round(float(x),5) for x in p[i]) for i in t) for t in data['triangles'])

def main():
    manifest=json.loads((OUT/'manifest.json').read_text());results=[]
    assert digest(OUT/'build.py')==manifest['generatorSha256']
    for path,sha in manifest['dependencies'].items():assert digest(ROOT/path)==sha,path
    package=ROOT/'marketplace/packages/thief/assets'
    provenance=json.loads((package/'thief-artifacts.provenance.json').read_text())['files']
    for name,sha in manifest['files'].items():
        assert digest(OUT/name)==sha,name
        if name in provenance:assert digest(package/name)==sha==provenance[name]['sha256']
        if not name.endswith('.glb'):continue
        raw=(OUT/name).read_bytes();assert struct.unpack_from('<III',raw)==(0x46546c67,2,len(raw))
        jlen,kind=struct.unpack_from('<II',raw,12);assert kind==0x4e4f534a
        doc=json.loads(raw[20:20+jlen]);blen,kind=struct.unpack_from('<II',raw,20+jlen);binary=raw[28+jlen:]
        assert kind==0x004e4942 and len(binary)==blen
        assert 'skins' not in doc and len(doc['meshes'])==1
        primitives=doc['meshes'][0]['primitives'];assert len(primitives)==1
        primitive=primitives[0];assert set(primitive['attributes'])=={'POSITION','NORMAL','TEXCOORD_0'}
        def read(index):
            acc=doc['accessors'][index];view=doc['bufferViews'][acc['bufferView']]
            width={'SCALAR':1,'VEC2':2,'VEC3':3}[acc['type']]
            return np.frombuffer(binary,dtype={5126:'<f4',5123:'<u2'}[acc['componentType']],
                count=acc['count']*width,offset=view.get('byteOffset',0)+acc.get('byteOffset',0)).reshape(-1,width)
        p=read(primitive['attributes']['POSITION']);n=read(primitive['attributes']['NORMAL']);uv=read(primitive['attributes']['TEXCOORD_0']);t=read(primitive['indices']).reshape(-1,3)
        key=Path(name).stem;expected=source(key)
        for field,actual in [('positions',p),('normals',n),('uvs',uv),('triangles',t)]:
            assert np.isfinite(actual).all() and np.allclose(actual,expected[field],atol=1e-7),(name,field)
        assert 0<len(p)<65535 and t.max()<len(p)
        assert np.allclose(np.linalg.norm(n,axis=1),1,atol=1e-6) and ((uv>=0)&(uv<=1)).all()
        a,b,c=(p[t[:,i]] for i in range(3));cross=np.cross(b-a,c-a)
        assert (np.einsum('ij,ij->i',cross,n[t[:,0]])>1e-11).all(),name
        pieces=json.loads((OUT/(key+'.pieces.json')).read_text());covered=[]
        for piece in pieces:
            start=piece['firstTriangle'];end=start+piece['triangleCount'];covered+=list(range(start,end))
            volume=np.einsum('ij,ij->i',a[start:end],np.cross(b[start:end],c[start:end])).sum()/6
            assert volume>0,(name,piece['name'],'inward volume')
            edges=Counter()
            for tri in t[start:end]:
                v=[tuple(round(float(x),6) for x in p[i]) for i in tri]
                for i in range(3):edges[tuple(sorted((v[i],v[(i+1)%3])))]+=1
            assert all(count==2 for count in edges.values()),(name,piece['name'],'open topology')
        assert covered==list(range(len(t)))
        results.append({'file':name,'vertices':len(p),'triangles':len(t),'closedOutwardComponents':len(pieces)})
    icons=[]
    for artifact in manifest['artifacts']:
        key=artifact['key']
        if 'string' in artifact:
            string=artifact['string']
            assert np.allclose(points(source(string['key']),string['stringToWeaponLocal']),
                               np.array(source(key+'-string-authored')['positions']),atol=2e-7,rtol=0)
        if artifact['fragments']:
            union=Counter()
            for fragment in artifact['fragments']:union.update(triangle_set(source(fragment['key']),fragment['fragmentToWeaponLocal']))
            assert union==triangle_set(source(key)),(key,'fragment reconstruction')
        for display in artifact['displays']:
            authored=source(display['sourceKey']);actual=source(display['key'])
            assert actual['triangles']==authored['triangles']
            assert np.allclose(points(actual,display['rendererToDisplayRoot']),points(authored,display['authoredToDisplayRoot']),atol=2e-7,rtol=0)
        icon=Image.open(OUT/(key+'-icon.png'));assert icon.mode=='RGBA' and icon.size==(256,256)
        bounds=icon.getchannel('A').getbbox();assert bounds and min(bounds[:2])>0 and max(bounds[2:])<256,(key,bounds)
        icons.append({'file':key+'-icon.png','alphaBounds':list(bounds)})
    assert len(manifest['artifacts'])==3 and len(icons)==3
    report={'status':'PASS','validatorSha256':digest(Path(__file__)),'manifestSha256':digest(OUT/'manifest.json'),'assets':results,'icons':icons,
            'checks':['GLB binary independently decoded against source','Finite unit normals, winding, closed outward components, UVs and 16-bit indices','Exact-once main mesh break reconstruction','Display and bow string local transform reconstruction','Packaged source hashes','Transparent icon framing'],
            'scope':'Offline original art only. Native fit, draw and attack motion, runtime materials, lifecycle and in-game visual acceptance remain unverified.'}
    (OUT/'validation.json').write_text(json.dumps(report,indent=2)+'\n')
    print('PASS:',len(results),'GLBs, 3 icons, closed geometry, transforms and package provenance')

if __name__=='__main__':main()
