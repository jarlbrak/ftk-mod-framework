"""Read-only native head-attached PortraitCam eye-ray comparison, not live portrait proof."""
import argparse,json,hashlib
from pathlib import Path
import numpy as np,UnityPy
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
p=argparse.ArgumentParser();p.add_argument('--assets',type=Path,required=True);args=p.parse_args()
e=UnityPy.load(str(args.assets));a=next(f for f in e.files.values() if Path(f.name).name==args.assets.name)
def local(t):
 q=t.m_LocalRotation;x,y,z,w=q.x,q.y,q.z,q.w;m=np.eye(4);m[:3,:3]=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])@np.diag([t.m_LocalScale.x,t.m_LocalScale.y,t.m_LocalScale.z]);m[:3,3]=[t.m_LocalPosition.x,t.m_LocalPosition.y,t.m_LocalPosition.z];return m
def world(t):return world(t.m_Father.read())@local(t) if t.m_Father else local(t)
r=a.objects[121467].read();bones=[b.read()for b in r.m_Bones];head=next(t for t in bones if t.m_GameObject.read().m_Name=='Head_M');cam=a.objects[90260].read();assert cam.m_GameObject.read().m_Name=='PortraitCam' and cam.m_Father.path_id==head.object_reader.path_id
ref=np.load(ROOT/'scratch/skeleton-audit/121467/reference.npz');hi=ref['bone_names'].tolist().index('Head_M');camera=np.linalg.inv(ref['bindposes'][hi])@np.linalg.inv(world(head))@world(cam);origin=camera[:3,3]
result=dict(method='Resolve exact native head-attached marker into bind space using inverse(bindHead) @ inverse(sourceHeadWorld) @ sourcePortraitWorld. Cast rays to eye center plus eight half-radius XY samples against original Head_M-rigid triangles, excluding target eye itself.',camera_transform_id=90260,head_transform_id=head.object_reader.path_id,camera_bind_matrix=camera.tolist(),limitations='Only head-rigid geometry; excludes pose-dependent other-body/jaw occlusion, actual offscreen FOV/crop, shader/light and native portrait animation. Sample visibility is not a quality verdict.',versions=[])
for directory,name in [(ROOT/'art-experiments/honeyback-bear','honeyback'),(OUT,'honeyback_portrait_v2')]:
 source=directory/f'{name}.source.json';d=json.loads(source.read_text());v=np.array(d['positions']);tri=np.array(d['triangles']);pieces=json.loads((directory/f'{name}.pieces.json').read_text());j=np.array(d['joints']);w=np.array(d['weights']);rigid=((j==hi)&(w>.999)).any(1);valid=rigid[tri].all(1);T=v[tri];e1=T[:,1]-T[:,0];e2=T[:,2]-T[:,0];rows=[]
 for piece in pieces:
  if not piece['name'].startswith('Amber eye'):continue
  start=piece['vertex_start'];end=start+piece['vertex_count'];points=v[start:end];center=points.mean(0);radius=(points.max(0)-points.min(0))*.25;samples=[]
  for dx,dy in [(0,0),(-1,-1),(-1,0),(-1,1),(0,-1),(0,1),(1,-1),(1,0),(1,1)]:
   target=center+np.array([dx*radius[0],dy*radius[1],0]);direction=target-origin;length=np.linalg.norm(direction);direction/=length;h=np.cross(direction,e2);det=(e1*h).sum(1);good=abs(det)>1e-9;f=np.zeros(len(det));f[good]=1/det[good];s=origin-T[:,0];u=f*(s*h).sum(1);q=np.cross(s,e1);vv=f*(q*direction).sum(1);dist=f*(e2*q).sum(1);exclude=(tri[:,0]>=start)&(tri[:,0]<end);hits=np.where(good&valid&~exclude&(u>=0)&(vv>=0)&(u+vv<=1)&(dist>0)&(dist<length-1e-5))[0];names=sorted(set(next(pc['name']for pc in pieces if pc['vertex_start']<=tri[k,0]<pc['vertex_start']+pc['vertex_count'])for k in hits));samples.append(dict(offset=[dx,dy],unobstructed=not names,occluders=names))
  rows.append(dict(eye=piece['name'],center=center.tolist(),unobstructed_samples=sum(s['unobstructed']for s in samples),total_samples=len(samples),samples=samples))
 result['versions'].append(dict(source=str(source.relative_to(ROOT)),source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),eyes=rows))
result['source_assets_sha256']=hashlib.file_digest(args.assets.open('rb'),'sha256').hexdigest()
result['reference_sha256']=hashlib.sha256((ROOT/'scratch/skeleton-audit/121467/reference.npz').read_bytes()).hexdigest()
(OUT/'portrait-visibility-audit.json').write_text(json.dumps(result,indent=2)+'\n')
for version in result['versions']:print(version['source'],[(r['eye'],r['unobstructed_samples'],r['samples'][0])for r in version['eyes']])
