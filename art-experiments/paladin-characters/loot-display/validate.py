#!/usr/bin/env python3
"""Independently verify original static display exports against authored surfaces."""
import hashlib,json,struct
from pathlib import Path
import numpy as np
OUT=Path(__file__).resolve().parent
ROOT=OUT.parents[2]
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
 original_path=ROOT/record['originalSource']
 assert hashlib.sha256(original_path.read_bytes()).hexdigest()==record['originalSourceSha256'],KEY
 original_pieces_path=original_path.with_name(original_path.name.replace('.source.json','.pieces.json'))
 assert hashlib.sha256(original_pieces_path.read_bytes()).hexdigest()==record['originalPiecesSha256'],KEY
 original=json.loads(original_path.read_text());original_pieces=json.loads(original_pieces_path.read_text())
 original_triangles=np.array(original['triangles']);closed_count=0;open_count=0
 pieces=json.loads((OUT/(KEY+'.pieces.json')).read_text())
 for piece in pieces:
  start=piece['vertex_start'];end=start+piece['vertex_count'];source_start=piece['source_vertex_start'];source_end=source_start+piece['source_vertex_count']
  assert any(item['name']==piece['name'] and item['vertex_start']==source_start and item['vertex_count']==piece['source_vertex_count'] for item in original_pieces),(KEY,piece['name'])
  expected=(np.array(original['positions'][source_start:source_end])-record['originalCenter'])*record['uniformScale']
  assert np.allclose(p[start:end],expected,atol=1e-7),(KEY,piece['name'],'source positions')
  for field,array in [('normals',n),('uvs',u)]:
   assert np.allclose(array[start:end],original[field][source_start:source_end],atol=1e-7),(KEY,piece['name'],field)
  mask=((original_triangles>=source_start)&(original_triangles<source_end)).all(axis=1)
  expected_triangles=original_triangles[mask]-source_start+start
  lo=piece['triangle_start'];hi=lo+piece['triangle_count'];piece_triangles=t[lo:hi]
  assert np.array_equal(piece_triangles,expected_triangles),(KEY,piece['name'],'source triangle winding')
  # Cloth openings and fitted toe/instep shells have no enclosed volume.
  # Apply the volume invariant only when welded triangle edges form a closed surface.
  edges={}
  for triangle in piece_triangles:
   vertices=[tuple(p[i]) for i in triangle]
   for left,right in zip(vertices,vertices[1:]+vertices[:1]):
    edge=tuple(sorted((left,right)));count,balance=edges.get(edge,(0,0))
    edges[edge]=(count+1,balance+(1 if left<right else -1))
  closed=bool(edges) and all(count==2 and balance==0 for count,balance in edges.values())
  if closed:
   assert np.einsum('ij,ij->i',a[lo:hi],np.cross(b[lo:hi],c[lo:hi])).sum()>0,(KEY,piece['name'],'closed volume')
   closed_count+=1
  else:open_count+=1
 results.append({'key':KEY,'vertices':len(p),'triangles':len(t),'closedPieces':closed_count,'openOrCompoundPieces':open_count,'status':'PASS'})
assert len(results)==12
report={'status':'PASS','scope':'Independent binary/display/source projection equality, finite arrays, indices, UVs, normals and original triangle winding. Positive volume for topologically closed pieces; open cloth and plate shells retain their source surfaces. No native card fit or runtime claim.','assets':results}
(OUT/'validation.json').write_text(json.dumps(report,indent=2)+'\n')
print('PASS: 12 original static loot display GLBs')
