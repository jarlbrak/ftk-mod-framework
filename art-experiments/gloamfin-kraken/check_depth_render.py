"""Original-only orthographic depth raster check; compares against approximate painter study."""
import json
from pathlib import Path
import numpy as np
from PIL import Image
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
d=json.loads((OUT/'gloamfin.source.json').read_text());r=np.load(ROOT/'scratch/skeleton-audit/121260/reference.npz');f=json.load(open(ROOT/'scratch/kraken-skin-live-v2/appear.json'))['frames'];audit=json.load(open(OUT/'endpoint-pose-audit.json'));c=audit['camera'];eye=np.array(c['position']);center=np.array(c['lookAt']);direction=eye-center;direction/=np.linalg.norm(direction);up=np.array(c['up']);right=np.cross(up,direction);view=np.array([right,up,direction]);v=np.c_[d['positions'],np.ones(len(d['positions']))];j=np.array(d['joints']);w=np.array(d['weights']);tri=np.array(d['triangles']);palette=Image.open(OUT/'gloamfin_basecolor.png');colors=[palette.getpixel((int(d['uvs'][t[0]][0]*palette.width),16))for t in tri];sheet=Image.new('RGB',(2048,512))
for slot,k in enumerate([0,40,41,112]):
 models=f[k]['endpointPolicy']['output']['prefabRootModels'];m=np.array([models[next(p for p in models if p.split('/')[-1]==n)]for n in d['bone_names']])@r['bindposes'];pos=sum(np.einsum('nij,nj->ni',m[j[:,z]],v)*w[:,z,None]for z in range(4))[:,:3];proj=(pos-center)@view.T;screen=np.c_[proj[:,0],-proj[:,1]]*(256/c['orthographicSize'])+256;depth=np.full((512,512),-np.inf);image=np.zeros((512,512,3),dtype=np.uint8)+[25,32,39]
 for t,color in zip(tri,colors):
  pts=screen[t];lo=np.maximum(0,np.floor(pts.min(0)).astype(int));hi=np.minimum(511,np.ceil(pts.max(0)).astype(int))
  if (hi<lo).any():continue
  a,b,cc=pts;den=(b[1]-cc[1])*(a[0]-cc[0])+(cc[0]-b[0])*(a[1]-cc[1])
  if abs(den)<1e-10:continue
  xx,yy=np.meshgrid(np.arange(lo[0],hi[0]+1)+.5,np.arange(lo[1],hi[1]+1)+.5);u=((b[1]-cc[1])*(xx-cc[0])+(cc[0]-b[0])*(yy-cc[1]))/den;vv=((cc[1]-a[1])*(xx-cc[0])+(a[0]-cc[0])*(yy-cc[1]))/den;ww=1-u-vv;z=u*proj[t[0],2]+vv*proj[t[1],2]+ww*proj[t[2],2];target=depth[lo[1]:hi[1]+1,lo[0]:hi[0]+1];mask=(u>=0)&(vv>=0)&(ww>=0)&(z>target);target[mask]=z[mask];image[lo[1]:hi[1]+1,lo[0]:hi[0]+1][mask]=color
 im=Image.fromarray(image.astype(np.uint8));im.save(OUT/f'endpoint-depth-{k:03d}.png');sheet.paste(im,(slot*512,0))
sheet.save(OUT/'endpoint-depth-comparison.png')
