"""Verify original analytic cube corners and source-to-GLB equality without native game files."""
import argparse,json,struct,hashlib,itertools
from pathlib import Path
import numpy as np
p=argparse.ArgumentParser();p.add_argument('--fixture-dir',type=Path,default=Path(__file__).with_name('cube-fixture'));a=p.parse_args();o=a.fixture_dir;d=json.loads((o/'cubea_material_layers_v1.source.json').read_text());raw=(o/'cubea_material_layers_v1.glb').read_bytes();n=struct.unpack_from('<I',raw,12)[0];g=json.loads(raw[20:20+n]);binary=raw[28+n:]
def read(i):
 t=g['accessors'][i];v=g['bufferViews'][t['bufferView']];width={'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16,'SCALAR':1}[t['type']];dtype={5123:'<u2',5126:'<f4'}[t['componentType']];return np.frombuffer(binary,dtype=dtype,count=t['count']*width,offset=v.get('byteOffset',0)+t.get('byteOffset',0)).reshape(-1,width)
assert d['bone_names']==['Root_M','jellyCubeMid','jellyCubeTop'];assert [g['nodes'][i]['name']for i in g['skins'][0]['joints']]==d['bone_names'];assert len(g['meshes'][0]['primitives'])==len(d['primitives'])==2
pos=np.array(d['positions']);j=np.array(d['joints']);w=np.array(d['weights']);assert len(pos)==72
for i,primitive in enumerate(g['meshes'][0]['primitives']):
 assert primitive['material']==i;faces=np.array(d['primitives'][i]['triangles']);assert faces.shape==(12,3);assert np.array_equal(read(primitive['indices']).ravel(),faces.ravel());ix=np.unique(faces);size=.35+i*.30;center=.45+i*.8;corners=np.array([[x*size/2,center+y*size/2,z*size/2]for x,y,z in itertools.product([-1,1],repeat=3)]);assert all(np.min(np.linalg.norm(corners-v,axis=1))<1e-6 for v in pos[ix]);assert (j[ix,0]==i+1).all()and(w[ix,0]==1).all()
 for attribute,key in [('POSITION','positions'),('NORMAL','normals'),('TEXCOORD_0','uvs'),('JOINTS_0','joints'),('WEIGHTS_0','weights')]:assert np.allclose(read(primitive['attributes'][attribute]),d[key],rtol=0,atol=1e-7)
manifest=json.loads((o/'manifest.json').read_text())
for name,pin in manifest['assets'].items():assert hashlib.sha256((o/name).read_bytes()).hexdigest()==pin['sha256']
print('PASS original16 analytic corners/72 flat vertices,2x12 triangles,2 exact weighted parts,full3 palette,source-to-GLB arrays and packaged hashes. No native/live claim.')
