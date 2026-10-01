"""Read the single-primitive source and strict FTK meshes used by this campaign."""
import json,struct
import numpy as np

def decode(path):
 raw=path.read_bytes();size=struct.unpack_from('<I',raw,12)[0];d=json.loads(raw[20:20+size]);blob=raw[28+size:]
 def read(i):
  a=d['accessors'][i];v=d['bufferViews'][a['bufferView']];w={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']]
  return np.frombuffer(blob,dtype={5126:'<f4',5123:'<u2',5125:'<u4',5121:'u1'}[a['componentType']],offset=v.get('byteOffset',0)+a.get('byteOffset',0),count=a['count']*w).reshape(-1,w).copy()
 pr=d['meshes'][0]['primitives'][0];a=pr['attributes'];out={k:read(v) for k,v in a.items()};out['triangles']=read(pr['indices']).reshape(-1,3)
 if 'skins' in d:
  sk=d['skins'][0];out['bone_names']=[d['nodes'][i]['name'] for i in sk['joints']];out['bindposes']=read(sk['inverseBindMatrices']).reshape(-1,4,4).transpose(0,2,1)
 return out

def topology(p, f):
    _, welded = np.unique(np.round(p, 5), axis=0, return_inverse=True)
    triangles = welded[f]
    edges = np.sort(np.concatenate([triangles[:, [0, 1]], triangles[:, [1, 2]], triangles[:, [2, 0]]]), axis=1)
    _, counts = np.unique(edges, axis=0, return_counts=True)
    return {'boundaryEdges': int((counts == 1).sum()), 'nonmanifoldEdges': int((counts > 2).sum())}
