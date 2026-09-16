"""Skin original art with recorded native bone matrices; no native surface output."""
import json, hashlib
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
capture=ROOT/'scratch/mirewarden-game/model-test-output/2c5bbc56aca1430899ca1539b504ebf3.json'
f=json.loads(capture.read_text())['frames'];s=json.loads((OUT/'lunacrest.source.json').read_text());r=np.load(ROOT/'scratch/skeleton-audit/121306/reference.npz');ib=r['bindposes'];v=np.c_[s['positions'],np.ones(len(s['positions']))];j=np.array(s['joints']);w=np.array(s['weights']);tri=np.array(s['triangles']);palette=['#'+x for x in ['1B2F54','314B76','526E95','8CA8BC','D2DDD5','F0EAD6','8E7097','B898B5','A68551','D6BA7B','1A263A','F7F3DE']];colors=[palette[min(11,int(s['uvs'][t[0]][0]*12))]for t in tri]
poses=[];centers=[]
for frame in f:
 world=np.array([b['localToWorld']for b in frame['bones']]).reshape(-1,4,4);m=np.linalg.inv(ib[0])@np.linalg.inv(world[0])@world@ib
 posed=np.zeros_like(v)
 for k in range(4):posed+=np.einsum('nij,nj->ni',m[j[:,k]],v)*w[:,k,None]
 poses.append(posed[:,:3]);a=m[0]@np.array([0,.6345,-1.0157,1]);b=m[5]@np.array([0,.6645,-1.0457,1]);centers.append(float(np.linalg.norm(a[:3]-b[:3])))
rot=[abs(float(x['bones'][5]['localRotation'][3]))for x in f];steps=sorted(set([0,int(np.argmin(rot)),int(np.argmax(rot)),60,119]));im=Image.new('RGB',(420*len(steps),500),(30,35,40));draw=ImageDraw.Draw(im)
for n,idx in enumerate(steps):
 p=poses[idx];screen=np.c_[p[:,2]+.3*p[:,0],-p[:,1]]*130+np.array([n*420+235,400]);depth=p[:,0]-.3*p[:,2]
 for t,c in sorted(zip(tri,colors),key=lambda item:float(depth[item[0]].mean())):draw.polygon([tuple(q)for q in screen[t]],fill=c)
 draw.text((n*420+20,20),'Native pose '+str(idx),fill='white')
im.save(OUT/'native-pose-study.png')
result=dict(method='Original source positions skinned by captured localToWorld @ native IBM, normalized by captured Root_M world inverse and rest Root_M. Source native vertices never used for drawing.',capture=str(capture.relative_to(ROOT)),capture_sha256=hashlib.sha256(capture.read_bytes()).hexdigest(),source_sha256=hashlib.sha256((OUT/'lunacrest.source.json').read_bytes()).hexdigest(),frames=len(f),pictured_frames=steps,hinge_center_distance_min=min(centers),hinge_center_distance_max=max(centers),scope='Offline native-pose geometry diagnostic; does not establish live material, culling, native death visibility, or cleanup.')
(OUT/'native-pose-audit.json').write_text(json.dumps(result,indent=2)+'\n');print(result)
