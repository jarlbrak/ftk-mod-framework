#!/usr/bin/env python3
"""Fit original shield loot meshes into the live-verified original novice shield envelope."""
import argparse,hashlib,json,sys
from pathlib import Path
import numpy as np
OUT=Path(__file__).resolve().parent
ROOT=OUT.parents[2]
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from export_ftk_glb import write_static_glb
BINDING=ROOT/'art-experiments/paladin-characters/loot-display/display-route-bindings.json'
SETS=['novice','oathkeeper','highward','mercy','censure','verdict']


def matrix(row):
 p=row['m_LocalPosition'];q=row['m_LocalRotation'];s=row['m_LocalScale']
 x,y,z,w=[q[k] for k in 'xyzw']
 norm=np.sqrt(x*x+y*y+z*z+w*w);x,y,z,w=np.array([x,y,z,w])/norm
 rotation=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],
                    [2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],
                    [2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])
 result=np.eye(4);result[:3,:3]=rotation@np.diag([s[k] for k in 'xyz']);result[:3,3]=[p[k] for k in 'xyz'];return result


def transform(points,m):return (np.c_[points,np.ones(len(points))]@m.T)[:,:3]


def build(output):
 output.mkdir(parents=True,exist_ok=True)
 binding=json.loads(BINDING.read_text())['rigidEquipmentRenderers']
 one=matrix(next(v for v in binding if v['path']=='shieldBlacksmith01'))
 two=one
 reference=OUT/'reference-envelope.json'
 envelope=json.loads(reference.read_text())
 assert envelope['bindingMetadataSha256']==hashlib.sha256(BINDING.read_bytes()).hexdigest()
 lo=np.array(envelope['referenceRootMin']);hi=np.array(envelope['referenceRootMax'])
 assert np.isfinite(lo).all() and np.isfinite(hi).all() and (hi>lo).all()
 target_center=(lo+hi)/2;extent=hi-lo
 records=[];files={}
 for tier in SETS:
  key='paladin-shield-'+tier+'-display';original=OUT.parent/('paladin-shield-'+tier+'.source.json')
  data=json.loads(original.read_text());points=np.array(data['positions']);posed=transform(points,two)
  center=(posed.min(axis=0)+posed.max(axis=0))/2
  # X/Y determine the visible card envelope. A 5% margin stays inside the verified novice shield card.
  scale=float(min(extent[:2]/(posed.max(axis=0)-posed.min(axis=0))[:2])*.95)
  fit=np.eye(4);fit[:3,:3]*=scale;fit[:3,3]=target_center-scale*center
  author_to_display=np.linalg.inv(two)@fit@two
  data['positions']=transform(points,author_to_display).tolist()
  normals=np.array(data['normals'])@np.linalg.inv(author_to_display[:3,:3]);normals/=np.linalg.norm(normals,axis=1)[:,None];data['normals']=normals.tolist()
  final=transform(np.array(data['positions']),two)
  assert (final.min(axis=0)[:2]>=lo[:2]-1e-7).all() and (final.max(axis=0)[:2]<=hi[:2]+1e-7).all()
  (output/(key+'.source.json')).write_text(json.dumps(data,separators=(',',':'))+'\n')
  write_static_glb(output/(key+'.glb'),data)
  for suffix in ['.source.json','.glb']:
   p=output/(key+suffix);files[p.name]=hashlib.sha256(p.read_bytes()).hexdigest()
  records.append({'key':key,'originalSource':original.relative_to(ROOT).as_posix(),'originalSourceSha256':hashlib.sha256(original.read_bytes()).hexdigest(),'displayScaleRelativeToEquipped':scale,'authorToDisplayLocal':author_to_display.tolist(),'finalDisplayRootMin':final.min(axis=0).tolist(),'finalDisplayRootMax':final.max(axis=0).tolist(),'vertices':len(points),'triangles':len(data['triangles'])})
 report={'schema':'ftkmf.paladin-original-shield-display.v1','generatorSha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'bindingMetadataSha256':hashlib.sha256(BINDING.read_bytes()).hexdigest(),'referenceEnvelope':reference.relative_to(ROOT).as_posix(),'referenceEnvelopeSha256':hashlib.sha256(reference.read_bytes()).hexdigest(),'referenceOriginalSourceSha256':envelope['referenceOriginalSourceSha256'],'referenceRootMin':lo.tolist(),'referenceRootMax':hi.tolist(),'provenance':'Existing original shield geometry transformed only by uniform scale and translation in native display-root space. Native data contributes attachment transforms only. Target is the frozen previously authored novice shield envelope, whose card fit was observed live; no native surface geometry used.','limits':'Offline containment in the verified original novice shield envelope. New shield native card fit remains pending; equipped meshes unchanged.','assets':records,'files':files}
 (output/'manifest.json').write_text(json.dumps(report,indent=2)+'\n')
 print('PASS: six original shield display GLBs fit the original novice shield reference envelope offline')


if __name__=='__main__':
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--output',type=Path,default=OUT);build(parser.parse_args().output)
