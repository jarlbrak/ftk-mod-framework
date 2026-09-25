#!/usr/bin/env python3
"""Decode the exported weapon GLBs independently of construction code."""
from collections import Counter
import hashlib,json,struct
from pathlib import Path
import numpy as np
OUT=Path(__file__).resolve().parent;ROOT=OUT.parents[2];PACKAGE=ROOT/'marketplace/packages/thief/assets'
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def decode(path):
    raw=path.read_bytes();assert struct.unpack_from('<III',raw)==(0x46546c67,2,len(raw))
    jlen,kind=struct.unpack_from('<II',raw,12);assert kind==0x4e4f534a
    doc=json.loads(raw[20:20+jlen]);blen,kind=struct.unpack_from('<II',raw,20+jlen);binary=raw[28+jlen:]
    assert kind==0x004e4942 and len(binary)==blen
    assert 'skins' not in doc and len(doc['meshes'])==1
    primitives=doc['meshes'][0]['primitives'];assert len(primitives)==1
    primitive=primitives[0];assert set(primitive['attributes'])=={'POSITION','NORMAL','TEXCOORD_0'}
    def read(index):
        acc=doc['accessors'][index];view=doc['bufferViews'][acc['bufferView']];width={'SCALAR':1,'VEC2':2,'VEC3':3}[acc['type']]
        return np.frombuffer(binary,dtype={5126:'<f4',5123:'<u2'}[acc['componentType']],count=acc['count']*width,offset=view.get('byteOffset',0)+acc.get('byteOffset',0)).reshape(-1,width)
    return {'positions':read(primitive['attributes']['POSITION']),'normals':read(primitive['attributes']['NORMAL']),'uvs':read(primitive['attributes']['TEXCOORD_0']),'triangles':read(primitive['indices']).reshape(-1,3)}
def points(data,matrix):
    m=np.array(matrix);return np.array(data['positions'])@m[:3,:3].T+m[:3,3]
def triangles(data,matrix=np.eye(4)):
    p=points(data,matrix)
    return Counter(tuple(tuple(round(float(x),5) for x in p[i]) for i in t) for t in data['triangles'])
def main():
    from PIL import Image
    manifest=json.loads((OUT/'manifest.json').read_text());results=[];decoded={}
    assert digest(OUT/'build.py')==manifest['generatorSha256']
    for path,sha in manifest['dependencies'].items():assert digest(ROOT/path)==sha,path
    for name,sha in manifest['files'].items():
        assert digest(OUT/name)==sha,name
        if not name.endswith('.glb'):continue
        data=decode(OUT/name);decoded[Path(name).stem]=data
        expected=json.loads((OUT/(Path(name).stem+'.source.json')).read_text())
        for k,v in data.items():assert np.isfinite(v).all() and np.allclose(v,expected[k],atol=1e-7),(name,k)
        p,n,uv,t=(data[k] for k in ['positions','normals','uvs','triangles'])
        assert 0<len(p)<65535 and t.max()<len(p)
        assert np.allclose(np.linalg.norm(n,axis=1),1,atol=1e-6) and ((uv>=0)&(uv<=1)).all()
        a,b,c=(p[t[:,i]] for i in range(3));cross=np.cross(b-a,c-a)
        assert (np.einsum('ij,ij->i',cross,n[t[:,0]])>1e-11).all(),name
        pieces=json.loads((OUT/(Path(name).stem+'.pieces.json')).read_text());covered=[]
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
    entries={e['id']:e for e in json.loads((PACKAGE.parent/'content.json').read_text())['entries'] if e.get('kind')=='weapon'}
    assert set(entries)=={w['id'] for w in manifest['weapons']} and len(entries)==17
    icons=[];max_error=0
    for w in manifest['weapons']:
        main=decoded[w['mainKey']];union=Counter()
        for f in w['fragments']:union.update(triangles(decoded[f['key']],f['fragmentToWeaponLocal']))
        assert union==triangles(main),(w['id'],'fragment reconstruction')
        for d in w['displays']:
            actual=points(decoded[d['key']],d['rendererToDisplayRoot']);expected=points(decoded[d['sourceKey']],d['authoredToDisplayRoot'])
            assert np.allclose(actual,expected,atol=4e-7,rtol=0),(w['id'],'display reconstruction')
            max_error=max(max_error,float(np.max(np.abs(actual-expected))))
        if 'string' in w:
            s=w['string'];assert np.allclose(points(decoded[s['key']],s['stringToWeaponLocal']),decoded[s['sourceKey']]['positions'],atol=4e-7,rtol=0)
        for group,assignments in w['assignments'].items():
            actual=entries[w['id']][group]
            assert actual==assignments,(w['id'],group,'content assignments')
            for item in assignments:
                for field in ['model','texture']:assert digest(PACKAGE/Path(item[field]).name)==digest(OUT/Path(item[field]).name),(w['id'],item[field])
        if (OUT/w['icon']).exists():
            im=Image.open(OUT/w['icon']);assert im.mode=='RGBA' and im.size==(256,256)
            box=im.getchannel('A').getbbox();assert box and 0<box[0]<box[2]<256 and 0<box[1]<box[3]<256,(w['id'],box)
            assert digest(PACKAGE/w['icon'])==digest(OUT/w['icon'])
            icons.append({'id':w['id'],'alphaBounds':box})
    assert len(icons)==17, 'All production icons must exist and pass'
    if 'render' in manifest:
        assert digest(OUT/'render.py')==manifest['render']['generatorSha256']
        assert digest(Path(__file__))==manifest['render']['decoderSha256']
        assert len(manifest['render']['previews'])==85
        for preview in manifest['render']['previews']:
            assert digest(OUT/preview['file'])==preview['sha256']
    report={'status':'PASS','manifestSha256':digest(OUT/'manifest.json'),'validatorSha256':digest(Path(__file__)),'weapons':17,'exports':results,'icons':icons,
       'maximumDisplayReconstructionError':max_error,'checks':['Independent GLB decode equals editable geometry','Closed outward components; winding; unit normals; finite UVs','Fragment union reconstructs main weapon','Display and string transforms reconstruct exact source','All 17 production definitions match model/texture assignments and package hashes'],
       'scope':'Offline evidence only. Native material, avatar fit, motion, bow draw, fragment lifecycle, actual UI readability and user art approval remain unverified.'}
    (OUT/'validation.json').write_text(json.dumps(report,indent=2)+'\n');print('PASS:',len(results),'GLBs, 17 weapon definitions,',len(icons),'icons')
if __name__=='__main__':main()
