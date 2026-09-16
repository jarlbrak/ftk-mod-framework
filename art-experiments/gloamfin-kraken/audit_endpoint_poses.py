"""Skin ORIGINAL blockout against pinned owned-fixture endpoint matrices, not native surfaces."""
import json,hashlib,math
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
source=OUT/'gloamfin.source.json';d=json.loads(source.read_text());ref=ROOT/'scratch/skeleton-audit/121260/reference.npz';r=np.load(ref);v=np.c_[d['positions'],np.ones(len(d['positions']))];j=np.array(d['joints']);w=np.array(d['weights']);tri=np.array(d['triangles']);names=d['bone_names'];soft=(w>.0001).sum(1)>1
poses=[];rigid_error=0.;inputs=[]
for filename in ['appear.json','appear-repeat.json']:
 path=ROOT/'scratch/kraken-skin-live-v2'/filename;capture=json.loads(path.read_text());assert capture['ok'] and len(capture['frames'])==241;inputs.append(dict(path=str(path.relative_to(ROOT)),sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
 for frame in capture['frames']:
  models=frame['endpointPolicy']['output']['prefabRootModels'];mat=np.array([models[next(p for p in models if p.split('/')[-1]==n)]for n in names])@r['bindposes'];pos=sum(np.einsum('nij,nj->ni',mat[j[:,k]],v)*w[:,k,None]for k in range(4))[:,:3];poses.append(pos)
  dominant=j[np.arange(len(j)),w.argmax(1)];rigid=np.einsum('nij,nj->ni',mat[dominant],v)[:,:3];rigid_error=max(rigid_error,float(np.linalg.norm(pos[soft]-rigid[soft],axis=1).max()))
allpoints=np.concatenate(poses);center=(allpoints.min(0)+allpoints.max(0))/2;direction=np.array([.45,.12,1.]);direction/=np.linalg.norm(direction);right=np.cross(np.array([0.,1.,0.]),direction);right/=np.linalg.norm(right);up=np.cross(direction,right);view=np.array([right,up,direction]);project=(allpoints-center)@view.T;ortho=math.ceil(float(np.max(np.abs(project[:,:2])))*1.15*10)/10;camera=dict(position=(center+direction*25).tolist(),lookAt=center.tolist(),up=up.tolist(),orthographic=True,orthographicSize=ortho,nearClipPlane=.1,farClipPlane=60.,width=512,height=512)
palette=Image.open(OUT/'gloamfin_basecolor.png');colors=[palette.getpixel((int(d['uvs'][t[0]][0]*palette.width),16))for t in tri];target=ROOT/'scratch/gloamfin-endpoint-poses';target.mkdir(exist_ok=True)
steps=[0,16,28,40,41,80,104,105,112,119,120,240];sheet=Image.new('RGB',(512*4,512*3),(25,32,39))
for k,pos in enumerate(poses[:241]):
 proj=(pos-center)@view.T;screen=np.c_[proj[:,0],-proj[:,1]]*(256/ortho)+256;im=Image.new('RGB',(512,512),(25,32,39));draw=ImageDraw.Draw(im)
 depth=np.full((512,512),-np.inf);pixels=np.zeros((512,512,3),dtype=np.uint8)+[25,32,39]
 for t,color in zip(tri,colors):
  pts=screen[t];lo=np.maximum(0,np.floor(pts.min(0)).astype(int));hi=np.minimum(511,np.ceil(pts.max(0)).astype(int))
  if (hi<lo).any():continue
  a,b,cc=pts;den=(b[1]-cc[1])*(a[0]-cc[0])+(cc[0]-b[0])*(a[1]-cc[1])
  if abs(den)<1e-10:continue
  xx,yy=np.meshgrid(np.arange(lo[0],hi[0]+1)+.5,np.arange(lo[1],hi[1]+1)+.5);u=((b[1]-cc[1])*(xx-cc[0])+(cc[0]-b[0])*(yy-cc[1]))/den;vv=((cc[1]-a[1])*(xx-cc[0])+(a[0]-cc[0])*(yy-cc[1]))/den;ww=1-u-vv;z=u*proj[t[0],2]+vv*proj[t[1],2]+ww*proj[t[2],2];tile=depth[lo[1]:hi[1]+1,lo[0]:hi[0]+1];mask=(u>=0)&(vv>=0)&(ww>=0)&(z>tile);tile[mask]=z[mask];pixels[lo[1]:hi[1]+1,lo[0]:hi[0]+1][mask]=color
 im=Image.fromarray(pixels.astype(np.uint8));draw=ImageDraw.Draw(im)
 draw.text((12,12),'Owned endpoint step '+str(k),fill='white');im.save(target/f'{k:04d}.png')
 if k in steps:i=steps.index(k);sheet.paste(im,((i%4)*512,(i//4)*512))
sheet.save(OUT/'endpoint-contact-sheet.png')
rest=np.array(d['positions']);restarea=np.linalg.norm(np.cross(rest[tri[:,1]]-rest[tri[:,0]],rest[tri[:,2]]-rest[tri[:,0]]),axis=1);minarea=1.;maxedge=1.
for pos in poses:
 area=np.linalg.norm(np.cross(pos[tri[:,1]]-pos[tri[:,0]],pos[tri[:,2]]-pos[tri[:,0]]),axis=1);minarea=min(minarea,float((area/restarea).min()))
 for a,b in [(0,1),(1,2),(2,0)]:maxedge=max(maxedge,float((np.linalg.norm(pos[tri[:,a]]-pos[tri[:,b]],axis=1)/np.linalg.norm(rest[tri[:,a]]-rest[tri[:,b]],axis=1)).max()))
pieces=json.loads((OUT/'gloamfin.pieces.json').read_text());rows=[]
for piece in pieces:
 a=piece['vertex_start'];b=a+piece['vertex_count'];rows.append(dict(name=piece['name'],vertices=b-a,multiboneVertices=int(soft[a:b].sum())))
result=dict(schema='ftkmf.original-organic-endpoint-study.v1',source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),reference_sha256=hashlib.sha256(ref.read_bytes()).hexdigest(),inputs=inputs,poses=482,unique_steps=241,weight_report=dict(vertices=len(v),multiboneVertices=int(soft.sum()),multiboneFraction=float(soft.mean()),maximumInfluences=int((w>0).sum(1).max()),maximumBlendVersusDominantRigidDifference=rigid_error,pieces=rows),minimumPosedToRestTriangleAreaRatio=minarea,maximumPosedToRestEdgeLengthRatio=maxedge,originalPosedBoundsMin=allpoints.min(0).tolist(),originalPosedBoundsMax=allpoints.max(0).tolist(),camera=camera,frame_directory=str(target.relative_to(ROOT)),scope='Offline original-only skin calculation from pinned owned endpoint output; not live BakeMesh comparison, visual acceptance, native effects, continuous-controller acceptance or production adapter.')
(OUT/'endpoint-pose-audit.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({k:v for k,v in result.items()if k!='weight_report'},indent=2));print(result['weight_report']['multiboneVertices'],rigid_error)
