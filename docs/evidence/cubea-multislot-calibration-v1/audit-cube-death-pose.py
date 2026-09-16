import json,struct,hashlib
from pathlib import Path
import numpy as np
root=Path(__file__).resolve().parents[1]
glb=root/'tools/ai-model-pipeline/multi-primitive-tests/cube-fixture/cubea_material_layers_v1.glb'
cap=root/'scratch/mirewarden-game/model-test-output/cd585a4165c8428fac8bfba859d509f6.json'
b=glb.read_bytes(); n,kind=struct.unpack_from('<II',b,12);doc=json.loads(b[20:20+n]);data=b[28+n:]
def read(i):
 a=doc['accessors'][i];v=doc['bufferViews'][a['bufferView']];assert 'byteStride' not in v
 w={'VEC3':3,'VEC4':4,'MAT4':16,'SCALAR':1}[a['type']]
 return np.frombuffer(data,dtype={5126:'<f4',5123:'<u2',5125:'<u4'}[a['componentType']],count=a['count']*w,offset=v.get('byteOffset',0)+a.get('byteOffset',0)).reshape(-1,w)
p=doc['meshes'][0]['primitives'];assert len(p)==2 and p[0]['attributes']==p[1]['attributes']
a=p[0]['attributes'];pos=read(a['POSITION']);j=read(a['JOINTS_0']).astype(int);w=read(a['WEIGHTS_0']);ibm=read(doc['skins'][0]['inverseBindMatrices']).reshape(-1,4,4).transpose(0,2,1)
names=[doc['nodes'][i]['name']for i in doc['skins'][0]['joints']];assert names==['Root_M','jellyCubeMid','jellyCubeTop']
raw=json.loads(cap.read_text());rows=[]
for idx,f in enumerate(raw['frames']):
 assert [x['name']for x in f['bones']]==names
 mats=np.array([np.array(x['localToWorld']).reshape(4,4)@ibm[k] for k,x in enumerate(f['bones'])]);h=np.column_stack([pos,np.ones(len(pos))]);v=np.zeros((len(pos),3))
 for slot in range(4):v+=np.einsum('nij,nj->ni',mats[j[:,slot]],h)[:,:3]*w[:,slot,None]
 rows.append({'frameIndex':idx,'worldMin':v.min(0).tolist(),'worldMax':v.max(0).tolist(),'rendererOriginY':f['rendererLocalToWorld'][7],'boneWorldY':[x['localToWorld'][7]for x in f['bones']],'rendererEnabled':f['enabled'],'clips':[c['name']for l in f['animator']['layers']for c in l['playing']]})
out={'status':'offline_skin_projection_of_observed_live_bone_matrices','glbSha256':hashlib.sha256(b).hexdigest(),'captureSha256':hashlib.sha256(cap.read_bytes()).hexdigest(),'frames':rows,'limits':['Analytical skin projection, not Unity BakeMesh readback.','World heights do not independently measure terrain height.','Reviewed death frames 40 and 60 contain native green debris but no discernible calibration blocks; enabled renderer does not establish body visibility.','This does not validate finished art or resource disposal.']}
(root/'scratch/cube-materials-death-pose-audit.json').write_text(json.dumps(out,indent=2)+'\n')
for i in [0,20,25,30,40,60,119]:print(json.dumps(rows[i]))
