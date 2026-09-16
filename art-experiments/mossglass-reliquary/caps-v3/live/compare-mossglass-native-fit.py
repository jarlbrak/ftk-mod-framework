"""Read-only native/authored skin bounds comparison; never outputs native surface arrays."""
import json,struct,hashlib
from pathlib import Path
import numpy as np
R=Path.cwd();O=R/'scratch/mossglass-native-fit';O.mkdir(exist_ok=True);g=R/'art-experiments/mossglass-reliquary/caps-v3/mossglass_caps_v3.glb';rpath=R/'scratch/cube-topology-analysis/reference-121012/reference.npz';ref=np.load(rpath);raw=g.read_bytes();n=struct.unpack_from('<I',raw,12)[0];d=json.loads(raw[20:20+n]);b=raw[28+n:]
def read(i):
 a=d['accessors'][i];v=d['bufferViews'][a['bufferView']];w={'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16,'SCALAR':1}[a['type']];return np.frombuffer(b,dtype={5126:'<f4',5123:'<u2'}[a['componentType']],offset=v.get('byteOffset',0)+a.get('byteOffset',0),count=a['count']*w).reshape(-1,w)
a=d['meshes'][0]['primitives'][0]['attributes'];custom={'positions':read(a['POSITION']),'joints':read(a['JOINTS_0']).astype(int),'weights':read(a['WEIGHTS_0']),'bindposes':read(d['skins'][0]['inverseBindMatrices']).reshape(-1,4,4).transpose(0,2,1)};assert np.allclose(custom['bindposes'],ref['bindposes'],atol=1e-7)
reports=[]
for label,cid in [('pass','d384c07303b74e58b8985855d682ff4d'),('hit','50bada2f3f4d4eedb114bf002c36d32c'),('death','8e42687debfe4706bc4792980e7e15c3')]:
 p=R/f'scratch/mirewarden-game/model-test-output/{cid}.json';cap=json.loads(p.read_text());frames=[]
 for i,f in enumerate(cap['frames']):
  world=np.array([x['localToWorld']for x in f['bones']]).reshape(-1,4,4);renderer=np.array(f['rendererLocalToWorld']).reshape(4,4);row={'frame':i,'rendererOrigin':renderer[:3,3].tolist(),'clips':[x['name']for l in f['animator']['layers']for x in l['playing']]}
  for kind,data in [('original',custom),('native',ref)]:
   h=np.c_[data['positions'],np.ones(len(data['positions']))];j=data['joints'].astype(int);w=data['weights'];m=world@data['bindposes'];pos=sum(np.einsum('nij,nj->ni',m[j[:,k]],h)*w[:,k,None]for k in range(4))[:,:3];local=(np.c_[pos,np.ones(len(pos))]@np.linalg.inv(renderer).T)[:,:3];row[kind]=dict(worldMin=pos.min(0).tolist(),worldMax=pos.max(0).tolist(),rendererLocalMin=local.min(0).tolist(),rendererLocalMax=local.max(0).tolist())
  row['originalMinusNativeTopWorldY']=row['original']['worldMax'][1]-row['native']['worldMax'][1];frames.append(row)
 report=dict(action=label,capture=str(p.relative_to(R)),captureSha256=hashlib.sha256(p.read_bytes()).hexdigest(),frames=frames,selected=[frames[i]for i in [0,30,40,60,119]],maxOriginalTopExcessY=max(x['originalMinusNativeTopWorldY']for x in frames),maxNativeTopFrame=max(frames,key=lambda x:x['native']['worldMax'][1])['frame'],maxOriginalTopFrame=max(frames,key=lambda x:x['original']['worldMax'][1])['frame']);reports.append(report)
r=dict(status='READ_ONLY_ORIGINAL_VS_NATIVE_POSED_BOUNDS',glbSha256=hashlib.sha256(raw).hexdigest(),referenceSha256=hashlib.sha256(rpath.read_bytes()).hexdigest(),runs=reports,method='Apply each mesh full own native-palette weights/IBMs to exactly the same captured world bone matrices; compare world and renderer-local bounds. Reference mesh remains local-only, output contains bounds not geometry.',limits=['Analytical skinning, not Unity BakeMesh/camera projection.','Actual camera view/projection and terrain height absent from capture.','Native mesh not shown live in this substituted enemy; inferred native bounds only.','Scale choice needs original live framing comparison; bounds alone do not prove HUD clearance.'])
(O/'comparison.json').write_text(json.dumps(r,indent=2)+'\n')
for x in reports:
 print(x['action'],'maxTopExcess',x['maxOriginalTopExcessY'],'nativeMaxFrame',x['maxNativeTopFrame'],'ownMaxFrame',x['maxOriginalTopFrame'])
 for f in x['selected']:print(f['frame'],'nativeY',[f['native']['worldMin'][1],f['native']['worldMax'][1]],'originalY',[f['original']['worldMin'][1],f['original']['worldMax'][1]],'excess',f['originalMinusNativeTopWorldY'])
