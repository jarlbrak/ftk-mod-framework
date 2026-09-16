"""Skin original art with recorded native bone matrices; no native surface output."""
import json, hashlib, argparse
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
ap=argparse.ArgumentParser();ap.add_argument('--capture',type=Path);ap.add_argument('--label',default='pass');args=ap.parse_args()
capture=args.capture or ROOT/'scratch/mirewarden-game/model-test-output/9c3bd3a236ae454c8a56ae35949cfa57.json'
capture=capture.resolve()
f=json.loads(capture.read_text())['frames'];s=json.loads((OUT/'resinmaw.source.json').read_text());r=np.load(ROOT/'scratch/skeleton-audit/121344/reference.npz');ib=r['bindposes'];v=np.c_[s['positions'],np.ones(len(s['positions']))];j=np.array(s['joints']);w=np.array(s['weights']);tri=np.array(s['triangles']);palette=['#'+x for x in ['392B23','69412A','97592A','BF7C32','DCA84B','E9CB78','465949','788C59','E6D8B2','B7B698','201F20','F4E5B8']];colors=[palette[min(11,int(s['uvs'][t[0]][0]*12))]for t in tri]
poses=[];centers=[]
for frame in f:
 world=np.array([b['localToWorld']for b in frame['bones']]).reshape(-1,4,4);m=np.linalg.inv(ib[0])@np.linalg.inv(world[0])@world@ib
 posed=np.zeros_like(v)
 for k in range(4):posed+=np.einsum('nij,nj->ni',m[j[:,k]],v)*w[:,k,None]
 poses.append(posed[:,:3]);centers.append(0.)
rot=[abs(float(x['bones'][14]['localRotation'][3]))for x in f];steps=sorted(set([0,int(np.argmin(rot)),int(np.argmax(rot)),60,119]));im=Image.new('RGB',(420*len(steps),500),(30,35,40));draw=ImageDraw.Draw(im)
projected=[np.c_[p[:,2]+.3*p[:,0],-p[:,1]]for p in poses];bounds=np.concatenate([projected[k]for k in steps]);lo=bounds.min(0);hi=bounds.max(0);scale=min(370/(hi[0]-lo[0]),420/(hi[1]-lo[1]))
for n,idx in enumerate(steps):
 panel=Image.new('RGB',(420,500),(30,35,40));pd=ImageDraw.Draw(panel);p=poses[idx];screen=(projected[idx]-(lo+hi)/2)*scale+np.array([210,270]);depth=p[:,0]-.3*p[:,2]
 for t,c in sorted(zip(tri,colors),key=lambda item:float(depth[item[0]].mean())):pd.polygon([tuple(q)for q in screen[t]],fill=c)
 pd.text((20,20),'Native pose '+str(idx),fill='white');im.paste(panel,(n*420,0))
im.save(OUT/('native-'+args.label+'-pose-study.png'))
worst=dict(ratio=0)
pieces=json.loads((OUT/'resinmaw.pieces.json').read_text())
for frame_no,pos in enumerate(poses):
 for a,b in [(0,1),(1,2),(2,0)]:
  rest=np.linalg.norm(v[tri[:,b],:3]-v[tri[:,a],:3],axis=1);posed=np.linalg.norm(pos[tri[:,b]]-pos[tri[:,a]],axis=1);ratio=posed/rest;i=int(ratio.argmax())
  if ratio[i]>worst['ratio']:
   first=int(tri[i,a]);piece=next(q['name']for q in pieces if q['vertex_start']<=first<q['vertex_start']+q['vertex_count']);worst=dict(ratio=float(ratio[i]),frame=frame_no,triangle=i,piece=piece,rest_edge_length=float(rest[i]),posed_edge_length=float(posed[i]))
result=dict(method='Original source positions skinned by captured localToWorld @ native IBM, normalized by captured Root_M world inverse and rest Root_M. Source native vertices never used for drawing.',capture=str(capture.relative_to(ROOT)),capture_sha256=hashlib.sha256(capture.read_bytes()).hexdigest(),source_sha256=hashlib.sha256((OUT/'resinmaw.source.json').read_bytes()).hexdigest(),frames=len(f),pictured_frames=steps,max_triangle_edge_stretch=float(max(np.max(np.linalg.norm(p[tri[:,b]]-p[tri[:,a]],axis=1)/np.linalg.norm(v[tri[:,b],:3]-v[tri[:,a],:3],axis=1)) for p in poses for a,b in [(0,1),(1,2),(2,0)])), renderer_active_frames=sum(bool(x['active']) for x in f), worst_edge=worst,scope='Offline native-pose geometry diagnostic; does not establish live material, culling, native death visibility, or cleanup.')
(OUT/('native-'+args.label+'-pose-audit.json')).write_text(json.dumps(result,indent=2)+'\n');print(result)
