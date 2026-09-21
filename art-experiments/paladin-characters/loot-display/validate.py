#!/usr/bin/env python3
"""Independently verify original static display exports against authored surfaces."""
import hashlib,json,struct
from pathlib import Path
import numpy as np
OUT=Path(__file__).resolve().parent
manifest=json.loads((OUT/'manifest.json').read_text())
for name,sha in manifest['files'].items():assert hashlib.sha256((OUT/name).read_bytes()).hexdigest()==sha,name
assert hashlib.sha256((OUT/'build.py').read_bytes()).hexdigest()==manifest['generatorSha256']
results=[]
for record in manifest['assets']:
 KEY=record['key']
 raw=(OUT/(KEY+'.glb')).read_bytes();assert struct.unpack_from('<III',raw)==(0x46546c67,2,len(raw))
 n,kind=struct.unpack_from('<II',raw,12);assert kind==0x4e4f534a
 doc=json.loads(raw[20:20+n]);size,kind=struct.unpack_from('<II',raw,20+n);binary=raw[28+n:];assert kind==0x004e4942 and size==len(binary)
 assert 'skins' not in doc and len(doc['meshes'])==1
 primitive=doc['meshes'][0]['primitives'][0]
 def read(index):
  a=doc['accessors'][index];v=doc['bufferViews'][a['bufferView']];components={'SCALAR':1,'VEC2':2,'VEC3':3}[a['type']]
  return np.frombuffer(binary,dtype={5126:'<f4',5123:'<u2'}[a['componentType']],count=a['count']*components,offset=v.get('byteOffset',0)+a.get('byteOffset',0)).reshape(-1,components)
 src=json.loads((OUT/(KEY+'.source.json')).read_text())
 arrays={k:read(primitive['attributes'][v]) for k,v in [('positions','POSITION'),('normals','NORMAL'),('uvs','TEXCOORD_0')]};arrays['triangles']=read(primitive['indices']).reshape(-1,3)
 for k,v in arrays.items():assert np.allclose(v,src[k],atol=1e-7),k
 p,n,u,t=[arrays[k] for k in ['positions','normals','uvs','triangles']]
 assert np.isfinite(p).all() and t.max()<len(p) and np.allclose(np.linalg.norm(n,axis=1),1,atol=1e-5)
 assert ((u>=0)&(u<=1)).all()
 a,b,c=[p[t[:,i]] for i in range(3)];cross=np.cross(b-a,c-a);assert (np.einsum('ij,ij->i',cross,n[t[:,0]])>0).all()
 pieces=json.loads((OUT/(KEY+'.pieces.json')).read_text())
 for piece in pieces:
  lo=piece['vertex_start']//3;hi=lo+piece['vertex_count']//3;assert np.einsum('ij,ij->i',a[lo:hi],np.cross(b[lo:hi],c[lo:hi])).sum()>0,piece['name']
 results.append({'key':KEY,'vertices':len(p),'triangles':len(t),'closedPieces':len(pieces),'status':'PASS'})
assert len(results)==12
report={'status':'PASS','scope':'Independent binary/source equality, finite arrays, indices, UVs, normals, winding and positive piece volume. No native card fit or runtime claim.','assets':results}
(OUT/'validation.json').write_text(json.dumps(report,indent=2)+'\n')
print('PASS: 12 original static loot display GLBs')
