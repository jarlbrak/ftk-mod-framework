#!/usr/bin/env python3
"""Independently decode accessory GLBs and check closed original components."""
from collections import Counter
import hashlib
import json
from pathlib import Path
import struct
import numpy as np
from PIL import Image
OUT=Path(__file__).resolve().parent
ROOT=OUT.parents[1]
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def save(p,d):p.write_text(json.dumps(d,indent=2)+'\n')
def main(compare=None):
    manifest=json.loads((OUT/'manifest.json').read_text())
    assert digest(OUT/manifest['generator'])==manifest['generatorSha256']
    for name,sha in manifest['dependencies'].items():assert digest(ROOT/name)==sha,name
    for name,sha in manifest['files'].items():assert digest(OUT/name)==sha,name
    results=[]
    assert len(manifest['assets'])==12
    assert len({r['key'] for r in manifest['assets']})==12
    for item in manifest['assets']:
        key=item['key'];raw=(OUT/(key+'.glb')).read_bytes()
        assert struct.unpack_from('<III',raw)==(0x46546c67,2,len(raw))
        jlen,jkind=struct.unpack_from('<II',raw,12);assert jkind==0x4e4f534a
        doc=json.loads(raw[20:20+jlen]);blen,bkind=struct.unpack_from('<II',raw,20+jlen);binary=raw[28+jlen:]
        assert bkind==0x004e4942 and len(binary)==blen and 'skins' not in doc
        assert len(doc['meshes'])==1 and len(doc['meshes'][0]['primitives'])==1
        primitive=doc['meshes'][0]['primitives'][0]
        assert set(primitive['attributes'])=={'POSITION','NORMAL','TEXCOORD_0'}
        def read(index):
            acc=doc['accessors'][index];view=doc['bufferViews'][acc['bufferView']]
            dtype={5126:'<f4',5123:'<u2'}[acc['componentType']];n={'SCALAR':1,'VEC2':2,'VEC3':3}[acc['type']]
            return np.frombuffer(binary,dtype=dtype,count=acc['count']*n,offset=view.get('byteOffset',0)+acc.get('byteOffset',0)).reshape(-1,n)
        pos=read(primitive['attributes']['POSITION']);norm=read(primitive['attributes']['NORMAL']);uv=read(primitive['attributes']['TEXCOORD_0']);tri=read(primitive['indices']).reshape(-1,3)
        source=json.loads((OUT/(key+'.source.json')).read_text())
        for field,actual in [('positions',pos),('normals',norm),('uvs',uv),('triangles',tri)]:assert np.allclose(source[field],actual,atol=1e-7),(key,field)
        assert 0<len(pos)<65535 and np.isfinite(pos).all() and tri.max()<len(pos)
        assert np.isfinite(norm).all() and np.allclose(np.linalg.norm(norm,axis=1),1,atol=1e-5)
        assert ((uv>=0)&(uv<=1)).all() and np.allclose(uv[:,1],.5)
        # Exact inverse root quaternion restores authored front orientation, without reflecting winding.
        t=np.array(source['authorToRuntime']);assert np.allclose(t[:3,:3].T@t[:3,:3],np.eye(3),atol=1e-6)
        assert np.isclose(np.linalg.det(t[:3,:3]),1,atol=1e-6)
        if item['slot']=='trinket':assert np.allclose(t[:3,:3],np.diag([-1,1,-1]),atol=1e-6)
        else:assert np.allclose(t,np.eye(4))
        a,b,c=(pos[tri[:,i]] for i in range(3));cross=np.cross(b-a,c-a)
        assert (np.linalg.norm(cross,axis=1)>1e-10).all(),key
        agreement=np.einsum('ij,ij->i',cross,norm[tri[:,0]]);assert (agreement>0).all(),key
        pieces=json.loads((OUT/(key+'.pieces.json')).read_text());expected=0
        for piece in pieces:
            start=piece['firstTriangle'];end=start+piece['triangleCount'];assert start==expected and end>start;expected=end
            volume=float(np.einsum('ij,ij->i',a[start:end],np.cross(b[start:end],c[start:end])).sum()/6)
            assert volume>0,(key,piece['name'],'volume',volume)
            edges=Counter()
            for triangle in tri[start:end]:
                pts=[tuple(round(float(v),6) for v in pos[idx]) for idx in triangle]
                for i in range(3):edges[tuple(sorted((pts[i],pts[(i+1)%3])))]+=1
            assert all(count==2 for count in edges.values()),(key,piece['name'],'open/nonmanifold')
        assert expected==len(tri)
        icon=Image.open(OUT/(key+'-icon.png'));assert icon.mode=='RGBA' and icon.size==(256,256)
        bounds=icon.getchannel('A').getbbox();assert bounds and bounds[0]>0 and bounds[1]>0 and bounds[2]<256 and bounds[3]<256,(key,bounds)
        results.append({'key':key,'vertices':len(pos),'triangles':len(tri),'closedPositiveVolumeComponents':len(pieces),'minimumTriangleNormalDot':float(agreement.min()),'iconAlphaBounds':bounds,'status':'PASS'})
    palette=Image.open(OUT/'paladin-accessory-palette.png');assert palette.size==(17,1)
    save(OUT/'validation.json',{'status':'PASS','validatorSha256':digest(Path(__file__)),'scope':'Independent binary/source equality, single unskinned primitive, finite arrays, normalized normals, indices, UVs, winding, closed positive-volume components, transform handedness and transparent icon bounds. Does not establish native display fit, binding, gameplay or lifecycle.','assets':results})
    if compare:
        previous=json.loads(Path(compare).read_text());assert previous['generatorSha256']==manifest['generatorSha256']
        differences=[name for name,sha in manifest['files'].items() if previous['files'].get(name)!=sha];assert not differences,differences
        save(OUT/'reproducibility.json',{'status':'PASS','generatorSha256':manifest['generatorSha256'],'validatorSha256':digest(Path(__file__)),'comparedFiles':len(manifest['files']),'differences':differences,'scope':'Two complete Blender builds produce identical GLBs, editable source/piece maps, 256-square icons and palette. No game process or deployment.'})
    print('PASS: twelve independently verified original accessory assets')
if __name__=='__main__':
    import argparse
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--compare-manifest',type=Path);main(p.parse_args().compare_manifest)
