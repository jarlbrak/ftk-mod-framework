"""Original two-material Mournglass geometry against captured native bone transforms; no native surface drawing."""
import json,hashlib,argparse
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent;ap=argparse.ArgumentParser();ap.add_argument('--capture',type=Path,required=True);ap.add_argument('--label',default='pass');ap.add_argument('--steps',type=int,nargs=6,default=[0,42,48,54,60,80]);ap.add_argument('--keep-root-motion',action='store_true');a=ap.parse_args();cap=a.capture.resolve();f=json.load(open(cap))['frames'];d=json.load(open(OUT/'mournglass.source.json'));r=np.load(ROOT/'scratch/skeleton-audit/121008/reference.npz');v=np.c_[d['positions'],np.ones(len(d['positions']))];j=np.array(d['joints']);w=np.array(d['weights']);tri=np.array(d['triangles']);poses=[];travel=[];maxedge=1.;worst=None;pieces=json.load(open(OUT/'mournglass.pieces.json'))
assert list(r['bone_names'])==d['bone_names']
assert len(f)>0
expected=(f[0]['instanceId'],f[0]['celInstanceId'],f[0]['ownerInstanceId'],f[0]['boneSignature'])
for frame_index,x in enumerate(f):
 assert [b['name'] for b in x['bones']]==d['bone_names']
 assert (x['instanceId'],x['celInstanceId'],x['ownerInstanceId'],x['boneSignature'])==expected
 world=np.array([b['localToWorld']for b in x['bones']]).reshape(-1,4,4);m=np.linalg.inv(np.array(x['rendererLocalToWorld']).reshape(4,4))@world@r['bindposes'];p=sum(np.einsum('nij,nj->ni',m[j[:,k]],v)*w[:,k,None]for k in range(4))[:,:3];travel.append(p);m=np.linalg.inv(r['bindposes'][0])@np.linalg.inv(world[0])@world@r['bindposes'];p=sum(np.einsum('nij,nj->ni',m[j[:,k]],v)*w[:,k,None]for k in range(4))[:,:3];p=travel[-1] if a.keep_root_motion else p;poses.append(p)
 for aa,b in [(0,1),(1,2),(2,0)]:
  rest=np.linalg.norm(v[tri[:,aa],:3]-v[tri[:,b],:3],axis=1);current=np.linalg.norm(p[tri[:,aa]]-p[tri[:,b]],axis=1);ratios=current/rest;idx=int(ratios.argmax())
  if ratios[idx]>maxedge:
   maxedge=float(ratios[idx]);vertex=int(tri[idx,aa]);piece=next(q['name']for q in pieces if q['vertex_start']<=vertex<q['vertex_start']+q['vertex_count']);worst=dict(frame=frame_index,piece=piece,triangle=idx,vertexIndices=[vertex,int(tri[idx,b])],restLength=float(rest[idx]),posedLength=float(current[idx]),ratio=maxedge)
steps=a.steps;assert all(0<=k<len(f) for k in steps);direction=np.array([.55,.25,1.]);direction/=np.linalg.norm(direction);right=np.cross([0,1,0],direction);right/=np.linalg.norm(right);up=np.cross(direction,right);view=np.array([right,up,direction]);allp=np.concatenate(poses);center=(allp.min(0)+allp.max(0))/2;project=(allp-center)@view.T;ortho=float(abs(project[:,:2]).max())*1.15;sheet=Image.new('RGB',(512*3,512*2));textures=[Image.open(OUT/f'mournglass.slot{k}.png').convert('RGB') for k in range(2)]
slot_by_triangle={tuple(t):slot for slot,p in enumerate(d['primitives']) for t in p['triangles']}
assert len(slot_by_triangle)==len(tri)
colors=[]
for t in tri:
 texture=textures[slot_by_triangle[tuple(t)]];u,vv=d['uvs'][t[0]]
 colors.append(texture.getpixel((min(texture.width-1,int((u%1)*texture.width)),min(texture.height-1,int((vv%1)*texture.height)))))
for slot,k in enumerate(steps):
 proj=(poses[k]-center)@view.T;screen=np.c_[proj[:,0],-proj[:,1]]*(256/ortho)+256;depth=np.full((512,512),-np.inf);image=np.zeros((512,512,3),dtype=np.uint8)+[30,35,40]
 for t,color in zip(tri,colors):
  pts=screen[t];lo=np.maximum(0,np.floor(pts.min(0)).astype(int));hi=np.minimum(511,np.ceil(pts.max(0)).astype(int))
  if(hi<lo).any():continue
  aa,b,c=pts;den=(b[1]-c[1])*(aa[0]-c[0])+(c[0]-b[0])*(aa[1]-c[1])
  if abs(den)<1e-10:continue
  xx,yy=np.meshgrid(np.arange(lo[0],hi[0]+1)+.5,np.arange(lo[1],hi[1]+1)+.5);u=((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/den;vv=((c[1]-aa[1])*(xx-c[0])+(aa[0]-c[0])*(yy-c[1]))/den;ww=1-u-vv;z=u*proj[t[0],2]+vv*proj[t[1],2]+ww*proj[t[2],2];tile=depth[lo[1]:hi[1]+1,lo[0]:hi[0]+1];mask=(u>=0)&(vv>=0)&(ww>=0)&(z>tile);tile[mask]=z[mask];image[lo[1]:hi[1]+1,lo[0]:hi[0]+1][mask]=color
 im=Image.fromarray(image.astype(np.uint8));ImageDraw.Draw(im).text((10,10),'Native '+a.label+' pose '+str(k)+(' [renderer hidden]' if not f[k]['enabled'] else ''),fill='white');sheet.paste(im,((slot%3)*512,(slot//3)*512))
sheet.save(OUT/f'native-{a.label}-pose-study.png');report=dict(capture=str(cap.relative_to(ROOT)),captureSha256=hashlib.sha256(cap.read_bytes()).hexdigest(),sourceSha256=hashlib.sha256((OUT/'mournglass.source.json').read_bytes()).hexdigest(),frames=len(f),sampledFrames=steps,maximumEdgeStretch=maxedge,worstEdge=worst,posedBoundsMin=allp.min(0).tolist(),posedBoundsMax=allp.max(0).tolist(),nativeTravelBoundsMin=np.concatenate(travel).min(0).tolist(),nativeTravelBoundsMax=np.concatenate(travel).max(0).tolist(),glbSha256=hashlib.sha256((OUT/'mournglass.glb').read_bytes()).hexdigest(),rootMotionRetained=a.keep_root_motion,method=('Renderer-local travel retained in sheet. ' if a.keep_root_motion else 'Root_M-normalized sheet. ')+'Original source skinned with full captured bone matrices and native IBMs, Anatomy mode removes Root_M transform for deformation inspection; keep-root-motion mode retains renderer-local travel in the sheet. Travel bounds always retain renderer-local motion. Orthographic depth raster with fixed all-frame bounds. Mesh-space anatomy front+Z/upY.',captureReportedOk=json.load(open(cap)).get('ok'),captureError=json.load(open(cap)).get('error'),materialMethod='Per-triangle first-vertex nearest texel from each original material slot; no UV-scroll/shader/light simulation.',scope='Offline native pose study; no material/culling/portrait/whole-controller or live original-art acceptance.');(OUT/f'native-{a.label}-pose-audit.json').write_text(json.dumps(report,indent=2)+'\n');print(report)
