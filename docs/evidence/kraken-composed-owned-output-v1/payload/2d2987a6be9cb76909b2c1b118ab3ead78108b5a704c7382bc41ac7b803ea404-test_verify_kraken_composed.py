"""Nonconstant composed-policy corruption tests. Synthetic poses are not Unity evidence."""
import copy,unittest
import numpy as np
from audit_kraken_adapter import MAPPING,models
from test_verify_kraken_endpoint_fixture import fixture as endpoints,make_pose
from test_verify_kraken_modern_mixer import fixed_fixture
from verify_kraken_endpoint_fixture import POLICY,Checks
from verify_kraken_composed_fixture import COMPOSED,PROVENANCE,INTERNAL,validate_composed,compare


def fixture(case='a',start=1,appearance=True):
 r,q,n=endpoints(case,start);f,fq,fn=fixed_fixture(case,start,appearance)
 if not appearance:
  r['scenario']=q['scenario']='damaged';r['identity'].update(trigger='Damaged',targetState='DAMAGED');n[2][20]['clip']='krakenDamage'
  pairs=r['endpointRests']['pairs'];pairs['krakenDamage']=pairs.pop('kraken_appear')
  for frame in r['frames']:
   for side in ('modern','old'):
    for key in ('currentClips','nextClips'):
     for clip in frame[side][key]:
      if clip['name']=='kraken_appear':clip['name']='krakenDamage'
   p=frame['endpointPolicy']
   for sample in p['samples']:
    if sample['clip']=='kraken_appear':sample['clip']='krakenDamage'
   old_rest=models(n[0]);modern_rest=models(n[1]);root=np.array(frame['old']['pose']['prefabRootModels']['Root_M'])
   main=[np.array(frame['modern']['pose']['prefabRootModels'][s])@np.linalg.inv(modern_rest[s])@old_rest[t] for t,s in MAPPING.items()]
   local=[np.linalg.solve(root if i==0 else main[i-1],matrix) for i,matrix in enumerate(main)]
   out={p:np.array(v['matrix']) for p,v in frame['old']['pose']['locals'].items()}
   for path,matrix in zip(MAPPING,local):out[path]=matrix
   p.update(appearanceWeight=0.,branch='whole_modern_controller',mainEndpointModels=[x.tolist() for x in main],appearanceEndpointModels=None,mainEndpointLocals=[x.tolist() for x in local],appearanceEndpointLocals=[],output=make_pose(out,p['output']))
 history=copy.deepcopy(r);history_request=copy.deepcopy(q)
 r.update(endpointPolicy=COMPOSED,provenance=PROVENANCE,compositionInputProvenance=INTERNAL);q['endpointPolicy']=COMPOSED
 r['modernInputMixer']=copy.deepcopy(f['modernInputMixer']);r['cleanup']['modernInputMixerClean']=True
 meta=r['modernInputMixer']['identity'];meta['rootInstanceId']=2000000+start;meta['animatorInstanceId']=2000001+start
 for value in meta['rest']['locals'].values():value['instanceId']+=2000000
 for frame,source_frame in zip(r['frames'],f['frames']):
  m=copy.deepcopy(source_frame['modernInputMixer']);m.update(samplerRootInstanceId=meta['rootInstanceId'],samplerAnimatorInstanceId=meta['animatorInstanceId'])
  for key in ('firstPose','repeatedPose'):
   for path,value in m[key]['locals'].items():value['instanceId']=meta['rest']['locals'][path]['instanceId']
  frame['modernInputMixer']=m;p=frame['endpointPolicy'];appearance_sample=next((s for s in p['samples'] if s['clip']=='kraken_appear'),None)
  if appearance_sample is None:p.update(branch='fixed_modern_sampler',mainInputProvenance='frozen_fixed_four_first_pose')
  else:p['mainInputProvenance']='appearance_only_no_main' if len(p['samples'])==1 else 'independent_full_strength_main_endpoint'
 return r,q,n,history,history_request

class Tests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):cls.r,cls.q,cls.native,cls.h,cls.hq=fixture()
 def bad(self,change,appearance=True):
  r,q,n,_,_=fixture(appearance=appearance);change(r,q);c=Checks()
  try:validate_composed(r,q,n,c,'bad')
  except (ValueError,KeyError):return
  self.assertTrue(c.failures,'Invalid composition passed')
 def test_nonconstant_appearance_pair_and_historical_output(self):
  b,bq,_,bh,bhq=fixture('b',500)
  result=compare(self.r,self.q,b,bq,self.native,self.h,self.hq,bh,bhq)
  self.assertEqual(result['status'],'composed_endpoint_match')
 def test_nonconstant_main_transition_pair_and_historical_output(self):
  a,aq,n,h,hq=fixture(appearance=False);b,bq,_,bh,bhq=fixture('b',500,False)
  self.assertEqual(compare(a,aq,b,bq,n,h,hq,bh,bhq)['status'],'composed_endpoint_match')
 def test_exact_policy_and_internal_provenance(self):
  self.bad(lambda r,q:q.update(endpointPolicy=POLICY))
  self.bad(lambda r,q:r.update(compositionInputProvenance='separately_submitted'))
  self.bad(lambda r,q:q.update(modernInputMixer='fixed-four-raw-v1'))
 def test_mixed_pose_cannot_replace_full_strength_endpoint(self):
  def wrong(r,q):
   frame=r['frames'][20];frame['endpointPolicy']['mainEndpointModels']=[np.eye(4).tolist()]*4
  self.bad(wrong)
  self.bad(lambda r,q:r['frames'][20]['endpointPolicy'].update(mainInputProvenance='frozen_fixed_four_first_pose'))
 def test_double_weight_and_snapshot_clock_corruption(self):
  self.bad(lambda r,q:r['frames'][20]['endpointPolicy'].update(appearanceWeight=r['frames'][20]['endpointPolicy']['appearanceWeight']**2))
  self.bad(lambda r,q:r['frames'][20]['modernInputMixer']['inputs'][0].update(sampleSeconds=.123))
 def test_main_branch_must_use_fixed_source(self):
  self.bad(lambda r,q:r['frames'][40]['endpointPolicy'].update(branch='whole_modern_controller'),False)
  self.bad(lambda r,q:r['frames'][40]['modernInputMixer']['firstPose']['prefabRootModels']['Root_M/base/body'][0].__setitem__(3,.5),False)
 def test_native_root_jaw_and_owner_alias_rejected(self):
  self.bad(lambda r,q:r['frames'][40]['endpointPolicy']['output']['locals']['Root_M/joint1/neck/jaw']['position'].__setitem__(0,.2))
  self.bad(lambda r,q:r['frames'][40]['endpointPolicy']['actualNativeRootModel'][0].__setitem__(3,.5))
  self.bad(lambda r,q:r['modernInputMixer']['identity'].update(rootInstanceId=r['frames'][0]['endpointPolicy']['outputRootInstanceId']))
 def test_historical_clock_shift_not_fitted(self):
  b,bq,_,bh,bhq=fixture('b',500);h=copy.deepcopy(self.h);h['frames'][20]['modern']['current']['normalizedTime']+=.1
  with self.assertRaises(ValueError):compare(self.r,self.q,b,bq,self.native,h,self.hq,bh,bhq)
if __name__=='__main__':unittest.main()
