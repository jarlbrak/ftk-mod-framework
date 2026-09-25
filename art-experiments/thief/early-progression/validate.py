#!/usr/bin/env python3
"""Decode the exported GLBs without the authoring module and verify their contract."""
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


def main():
    manifest=json.loads((OUT/'manifest.json').read_text())
    assert digest(OUT/manifest['generator'])==manifest['generatorSha256']
    for path,sha in manifest['dependencies'].items():assert digest(ROOT/path)==sha,path
    package=ROOT/'marketplace/packages/thief/assets'
    copies=json.loads((package/'paired-progression.provenance.json').read_text())['files']
    exports=[]
    for name,sha in manifest['files'].items():
        assert digest(OUT/name)==sha,name
        if name in copies:assert digest(package/name)==sha==copies[name]['sha256'],name
        if not name.endswith('.glb'):continue
        raw=(OUT/name).read_bytes()
        assert struct.unpack_from('<III',raw)==(0x46546c67,2,len(raw))
        length,kind=struct.unpack_from('<II',raw,12);assert kind==0x4e4f534a
        doc=json.loads(raw[20:20+length]);binary_length,kind=struct.unpack_from('<II',raw,20+length)
        binary=raw[28+length:];assert kind==0x004e4942 and binary_length==len(binary)
        assert 'skins' not in doc and len(doc['meshes'])==1 and len(doc['meshes'][0]['primitives'])==1
        primitive=doc['meshes'][0]['primitives'][0]
        assert set(primitive['attributes'])=={'POSITION','NORMAL','TEXCOORD_0'}
        def read(index):
            acc=doc['accessors'][index];view=doc['bufferViews'][acc['bufferView']]
            width={'VEC3':3,'VEC2':2,'SCALAR':1}[acc['type']]
            return np.frombuffer(binary,dtype={5126:'<f4',5123:'<u2'}[acc['componentType']],count=acc['count']*width,offset=view.get('byteOffset',0)+acc.get('byteOffset',0)).reshape(-1,width)
        pos=read(primitive['attributes']['POSITION']);normal=read(primitive['attributes']['NORMAL'])
        uv=read(primitive['attributes']['TEXCOORD_0']);tri=read(primitive['indices']).reshape(-1,3)
        key=Path(name).stem;source=json.loads((OUT/(key+'.source.json')).read_text())
        for field,actual in [('positions',pos),('normals',normal),('uvs',uv),('triangles',tri)]:
            assert np.isfinite(actual).all() and np.allclose(actual,source[field],atol=1e-7),(name,field)
        assert 0<len(pos)<65535 and tri.max()<len(pos)
        assert np.allclose(np.linalg.norm(normal,axis=1),1,atol=1e-6)
        assert ((uv>=0)&(uv<=1)).all()
        a,b,c=[pos[tri[:,i]] for i in range(3)]
        assert (np.einsum('ij,ij->i',np.cross(b-a,c-a),normal[tri[:,0]])>1e-11).all(),name
        covered=[];pieces=json.loads((OUT/(key+'.pieces.json')).read_text())
        for piece in pieces:
            start=piece['firstTriangle'];end=start+piece['triangleCount'];covered+=list(range(start,end))
            assert np.einsum('ij,ij->i',a[start:end],np.cross(b[start:end],c[start:end])).sum()/6>0,(name,piece['name'])
            edges=Counter()
            for triangle in tri[start:end]:
                p=[tuple(round(float(v),6) for v in pos[i]) for i in triangle]
                for i in range(3):edges[tuple(sorted((p[i],p[(i+1)%3])))]+=1
            assert all(v==2 for v in edges.values()),(name,piece['name'],'nonmanifold')
        assert covered==list(range(len(tri)))
        exports.append({'file':name,'triangles':len(tri),'closedComponents':len(pieces),'status':'PASS'})
    assert len(exports)==30
    reviews=[]
    def points(data,matrix):return np.array(data['positions'])@matrix[:3,:3].T+matrix[:3,3]
    def triangles(data,matrix=np.eye(4)):
        pos=points(data,matrix)
        return Counter(tuple(tuple(round(v,6) for v in pos[i]) for i in tri) for tri in data['triangles'])
    for asset in manifest['assets']:
        original=json.loads((OUT/(asset['key']+'.source.json')).read_text());union=Counter()
        for fragment in asset['fragments']:
            data=json.loads((OUT/(fragment['key']+'.source.json')).read_text())
            union.update(triangles(data,np.array(fragment['fragmentToWeaponLocal'])))
        assert union==triangles(original),(asset['key'],'fragment reconstruction')
        errors=[]
        for display in asset['displays']:
            data=json.loads((OUT/(display['key']+'.source.json')).read_text())
            actual=points(data,np.array(display['rendererToDisplayRoot']))
            expected=points(original,np.array(display['authoredBladeToDisplayRoot']))
            assert data['triangles']==original['triangles'] and np.allclose(actual,expected,atol=1e-7,rtol=0)
            errors.append(float(np.max(np.abs(actual-expected))))
        icon=Image.open(OUT/asset['icon']);assert icon.size==(256,256) and icon.mode=='RGBA'
        bounds=icon.getchannel('A').getbbox()
        assert bounds and 0<bounds[0]<bounds[2]<256 and 0<bounds[1]<bounds[3]<256,bounds
        reviews.append({'id':asset['id'],'iconAlphaBounds':bounds,'maxDisplayError':max(errors),'fragmentReconstruction':'PASS'})
    report={'status':'PASS','validatorSha256':digest(Path(__file__)),'manifestSha256':digest(OUT/'manifest.json'),'exports':exports,'items':reviews,'limitations':'Offline original art and source/export checks only. Live fit, native motion, item-card framing and gameplay remain untested.'}
    (OUT/'validation.json').write_text(json.dumps(report,indent=2)+'\n')
    print('PASS: 30 independent GLB decodes, closed outward components, six fragment/display reconstructions, six icon frames and package hashes')


if __name__=='__main__':main()
