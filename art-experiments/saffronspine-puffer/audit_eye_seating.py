"""Original eye tips versus closed original body in every recorded pose; no camera claim."""
import json,hashlib
from pathlib import Path
import numpy as np
O=Path(__file__).resolve().parent;R=O.parent.parent;d=json.loads((O/'saffronspine.source.json').read_text());parts=json.loads((O/'saffronspine.pieces.json').read_text());v=np.c_[d['positions'],np.ones(len(d['positions']))];j=np.array(d['joints']);w=np.array(d['weights']);ref=np.load(R/'scratch/puffer-native-topology-analysis/reference-121509/reference.npz');body=parts[0];tri=np.array(d['triangles'])[:body['vertex_count']//3];eyes=[]
for q in parts:
 if q['name'].startswith('Small dark eye'):
  start=q['vertex_start'];eyes.append(start+int(v[start:start+q['vertex_count'],2].argmax()))
direction=np.array([1.,.173,.319]);direction/=np.linalg.norm(direction)
def inside(p,t):
 a=t[:,0];e1=t[:,1]-a;e2=t[:,2]-a;h=np.cross(direction,e2);det=np.einsum('ij,ij->i',e1,h);ok=abs(det)>1e-10;f=np.divide(1.,det,out=np.zeros_like(det),where=ok);s=p-a;u=f*np.einsum('ij,ij->i',s,h);q=np.cross(s,e1);vv=f*(q@direction);dist=f*np.einsum('ij,ij->i',e2,q);hits=dist[ok&(u>=0)&(vv>=0)&(u+vv<=1)&(dist>1e-7)];return len(np.unique(hits.round(7)))%2==1
reports=[]
for name,cid in [('pass','e6816644ce3045e9b67c087cd8bdf807'),('hit','bfd7bf821f604f9ba5b4e8488bc10c58'),('death','522a98de26de4111a8c89b4585b5506c')]:
 path=R/'scratch/mirewarden-game/model-test-output'/f'{cid}.json';frames=json.loads(path.read_text())['frames'];buried=[]
 for k,frame in enumerate(frames):
  world=np.array([b['localToWorld']for b in frame['bones']]).reshape(-1,4,4);m=np.linalg.inv(np.array(frame['rendererLocalToWorld']).reshape(4,4))@world@ref['bindposes'];p=sum(np.einsum('nij,nj->ni',m[j[:,slot]],v)*w[:,slot,None]for slot in range(4))[:,:3]
  for eye in eyes:
   if inside(p[eye],p[tri]):buried.append(dict(frame=k,eyeVertex=eye))
 reports.append(dict(capture=name,sha256=hashlib.sha256(path.read_bytes()).hexdigest(),frames=len(frames),eyeTipsInsideBody=buried))
r=dict(status='PASS'if not any(q['eyeTipsInsideBody']for q in reports)else'FAILED',method='Ray parity against closed original body triangles for frontmost bind vertex of each original dark eye, after full weighted skinning. Two tips tested in every frame. This does not prove full eye visibility, camera readability or intersection-free sockets.',captures=reports,glbSha256=hashlib.sha256((O/'saffronspine.glb').read_bytes()).hexdigest());(O/'eye-seating-audit.json').write_text(json.dumps(r,indent=2)+'\n');print(r['status'])
